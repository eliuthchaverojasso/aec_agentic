"""Requirement-level readiness gap rules.

These rules intentionally work with the current normalized tables only. Fields
that do not exist yet in the schema, such as milestone and expected evidence
type, are treated as explicit mapping gaps instead of hidden demo assumptions.
"""

from __future__ import annotations

from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any, Iterable, Mapping, Sequence

from app.models import Export, Issue, Requirement, RequirementCompliance


EVIDENCE_STALE_DAYS = 14
UNEVALUATED_WARNING_RATIO = 0.50
MAX_REQUIREMENT_FINDINGS_PER_RULE = 25

MILESTONE_TOKENS = (
    "DD50",
    "DD 50",
    "DD75",
    "DD 75",
    "DD95",
    "DD 95",
    "CD50",
    "CD 50",
    "CD75",
    "CD 75",
    "CD95",
    "CD 95",
    "CD100",
    "CD 100",
    "PERMIT",
    "SUBMITTAL",
)
EXPECTED_EVIDENCE_TYPES = {"model", "sheet", "spec", "manual", "hybrid"}
UNMAPPED_DISCIPLINES = {"", "unknown", "unmapped", "n/a", "na", "none", "general"}


@dataclass(frozen=True)
class ReadinessFinding:
    rule_code: str
    severity: str
    status: str
    message: str
    requirement_id: int | None = None
    discipline: str | None = None
    milestone: str | None = None
    evidence_type: str | None = None
    readiness_impact: float = 0.0
    action_type: str | None = None
    evidence: dict[str, Any] | None = None

    def to_gap_dict(self) -> dict[str, Any]:
        return {
            "rule_code": self.rule_code,
            "severity": self.severity,
            "status": self.status,
            "message": self.message,
            "requirement_id": self.requirement_id,
            "discipline": self.discipline,
            "milestone": self.milestone,
            "evidence_type": self.evidence_type,
            "readiness_impact": self.readiness_impact,
            "action_type": self.action_type,
            "evidence": self.evidence,
        }


def evaluate_readiness_rules(
    requirements: Sequence[Requirement],
    compliance_by_requirement: Mapping[int, RequirementCompliance],
    issues: Sequence[Issue],
    latest_export: Export | None,
    now: datetime | None = None,
) -> list[ReadinessFinding]:
    now = _as_utc(now or datetime.now(timezone.utc))
    findings: list[ReadinessFinding] = []

    findings.extend(_requirement_data_quality_rules(requirements))
    findings.extend(_requirement_mapping_rules(requirements, compliance_by_requirement))
    findings.extend(
        _evidence_coverage_rules(
            requirements=requirements,
            compliance_by_requirement=compliance_by_requirement,
            latest_export=latest_export,
            now=now,
        )
    )
    findings.extend(_milestone_rules(requirements, compliance_by_requirement, issues))
    findings.extend(
        _readiness_risk_rules(
            requirements=requirements,
            compliance_by_requirement=compliance_by_requirement,
            issues=issues,
            latest_export=latest_export,
            now=now,
        )
    )

    return findings


def summarize_gaps(findings: Iterable[ReadinessFinding]) -> dict[str, int]:
    counts = Counter(finding.severity for finding in findings)
    return {severity: int(counts.get(severity, 0)) for severity in ("critical", "high", "medium", "low")}


def top_findings(findings: Sequence[ReadinessFinding], limit: int = 12) -> list[ReadinessFinding]:
    severity_rank = {"critical": 0, "high": 1, "medium": 2, "low": 3}
    return sorted(
        findings,
        key=lambda finding: (
            severity_rank.get(finding.severity, 9),
            finding.readiness_impact,
            finding.rule_code,
            finding.requirement_id or 0,
        ),
    )[:limit]


def recommended_actions(
    findings: Sequence[ReadinessFinding],
    limit: int = 8,
) -> list[dict[str, Any]]:
    actions: list[dict[str, Any]] = []
    seen: set[tuple[str, int | None, str | None]] = set()
    for finding in top_findings(findings, limit=limit * 3):
        if finding.action_type is None:
            continue
        key = (finding.action_type, finding.requirement_id, finding.discipline)
        if key in seen:
            continue
        seen.add(key)
        actions.append(
            {
                "action_type": finding.action_type,
                "label": _action_label(finding.action_type),
                "detail": finding.message,
                "severity": finding.severity,
                "rule_code": finding.rule_code,
                "requirement_id": finding.requirement_id,
                "discipline": finding.discipline,
            }
        )
        if len(actions) >= limit:
            break
    return actions


def _requirement_data_quality_rules(requirements: Sequence[Requirement]) -> list[ReadinessFinding]:
    findings: list[ReadinessFinding] = []
    for requirement in requirements:
        discipline = _clean(requirement.discipline)
        if discipline.lower() in UNMAPPED_DISCIPLINES:
            findings.append(
                ReadinessFinding(
                    rule_code="REQ001",
                    severity="medium",
                    status="missing",
                    message="Requirement is missing a usable discipline mapping.",
                    requirement_id=requirement.id,
                    discipline=requirement.discipline,
                    readiness_impact=-1.0,
                    action_type="assign_discipline",
                )
            )

        if requirement.source_file_id is None and not _clean(requirement.resource):
            findings.append(
                ReadinessFinding(
                    rule_code="REQ002",
                    severity="medium",
                    status="missing",
                    message="Requirement is missing a source file or owner resource reference.",
                    requirement_id=requirement.id,
                    discipline=requirement.discipline,
                    readiness_impact=-1.0,
                    action_type="attach_source",
                )
            )

    return findings


def _requirement_mapping_rules(
    requirements: Sequence[Requirement],
    compliance_by_requirement: Mapping[int, RequirementCompliance],
) -> list[ReadinessFinding]:
    findings: list[ReadinessFinding] = []

    for requirement in requirements:
        compliance = compliance_by_requirement.get(requirement.id)
        evidence = _evidence_dict(compliance)
        milestone = _extract_milestone(requirement, evidence)
        evidence_type = _extract_evidence_type(requirement, evidence)
        discipline = _clean(requirement.discipline)

        if milestone is None:
            findings.append(
                ReadinessFinding(
                    rule_code="MAP001",
                    severity="medium",
                    status="unmapped",
                    message="Requirement is not mapped to a DD/CD milestone.",
                    requirement_id=requirement.id,
                    discipline=requirement.discipline,
                    readiness_impact=-1.5,
                    action_type="map_milestone",
                    evidence={"source": "schema_gap", "missing_field": "milestone"},
                )
            )

        if discipline.lower() in UNMAPPED_DISCIPLINES:
            findings.append(
                ReadinessFinding(
                    rule_code="MAP002",
                    severity="high",
                    status="unmapped",
                    message="Requirement cannot contribute to trade readiness until a trade is assigned.",
                    requirement_id=requirement.id,
                    discipline=requirement.discipline,
                    milestone=milestone,
                    readiness_impact=-2.0,
                    action_type="map_trade",
                )
            )

        if evidence_type is None:
            findings.append(
                ReadinessFinding(
                    rule_code="MAP003",
                    severity="medium",
                    status="unmapped",
                    message="Requirement is not mapped to an expected evidence type.",
                    requirement_id=requirement.id,
                    discipline=requirement.discipline,
                    milestone=milestone,
                    readiness_impact=-1.5,
                    action_type="map_evidence_type",
                    evidence={"source": "schema_gap", "expected_values": sorted(EXPECTED_EVIDENCE_TYPES)},
                )
            )

    return findings


def _evidence_coverage_rules(
    requirements: Sequence[Requirement],
    compliance_by_requirement: Mapping[int, RequirementCompliance],
    latest_export: Export | None,
    now: datetime,
) -> list[ReadinessFinding]:
    findings: list[ReadinessFinding] = []
    latest_sync_at = _as_utc(latest_export.completed_at) if latest_export else None

    for requirement in requirements:
        compliance = compliance_by_requirement.get(requirement.id)
        evidence = _evidence_dict(compliance)
        evidence_type = _extract_evidence_type(requirement, evidence)
        status = compliance.status if compliance else "not_evaluated"

        if status in {"not_evaluated", "non_compliant"} or not evidence:
            findings.append(
                ReadinessFinding(
                    rule_code="EVD001",
                    severity="high",
                    status="missing",
                    message="Requirement has no linked model, sheet, spec, or manual evidence.",
                    requirement_id=requirement.id,
                    discipline=requirement.discipline,
                    milestone=_extract_milestone(requirement, evidence),
                    evidence_type=evidence_type,
                    readiness_impact=-4.0,
                    action_type="link_evidence",
                    evidence=evidence or {"source": "requirement_compliance", "status": status},
                )
            )
            continue

        evaluated_at = _as_utc(compliance.evaluated_at) if compliance else None
        if evaluated_at and latest_sync_at and evaluated_at < latest_sync_at:
            findings.append(
                ReadinessFinding(
                    rule_code="EVD002",
                    severity="medium",
                    status="stale",
                    message="Requirement evidence predates the latest model export and should be reviewed.",
                    requirement_id=requirement.id,
                    discipline=requirement.discipline,
                    milestone=_extract_milestone(requirement, evidence),
                    evidence_type=evidence_type,
                    readiness_impact=-2.0,
                    action_type="review_evidence",
                    evidence={"evaluated_at": evaluated_at.isoformat(), "latest_sync_at": latest_sync_at.isoformat()},
                )
            )
        elif evaluated_at and (now - evaluated_at).days > EVIDENCE_STALE_DAYS:
            findings.append(
                ReadinessFinding(
                    rule_code="EVD002",
                    severity="medium",
                    status="stale",
                    message=f"Requirement evidence has not been refreshed in {EVIDENCE_STALE_DAYS}+ days.",
                    requirement_id=requirement.id,
                    discipline=requirement.discipline,
                    milestone=_extract_milestone(requirement, evidence),
                    evidence_type=evidence_type,
                    readiness_impact=-2.0,
                    action_type="review_evidence",
                    evidence={"evaluated_at": evaluated_at.isoformat()},
                )
            )

        if status == "needs_review" or evidence.get("needs_review") is True:
            findings.append(
                ReadinessFinding(
                    rule_code="EVD006",
                    severity="medium",
                    status="needs_review",
                    message="Requirement evidence exists but needs reviewer confirmation.",
                    requirement_id=requirement.id,
                    discipline=requirement.discipline,
                    milestone=_extract_milestone(requirement, evidence),
                    evidence_type=evidence_type,
                    readiness_impact=-2.0,
                    action_type="review_requirement",
                    evidence=evidence,
                )
            )

    return findings


def _milestone_rules(
    requirements: Sequence[Requirement],
    compliance_by_requirement: Mapping[int, RequirementCompliance],
    issues: Sequence[Issue],
) -> list[ReadinessFinding]:
    findings: list[ReadinessFinding] = []
    total = len(requirements)
    unevaluated = [
        requirement
        for requirement in requirements
        if compliance_by_requirement.get(requirement.id) is None
        or compliance_by_requirement[requirement.id].status == "not_evaluated"
    ]

    if total and unevaluated:
        findings.append(
            ReadinessFinding(
                rule_code="MS004",
                severity="high" if len(unevaluated) / total >= UNEVALUATED_WARNING_RATIO else "medium",
                status="not_evaluated",
                message=f"{len(unevaluated)} of {total} requirements have not been evaluated for the current deliverable.",
                readiness_impact=-5.0,
                action_type="evaluate_requirements",
                evidence={"total_requirements": total, "unevaluated_requirements": len(unevaluated)},
            )
        )

    issue_counts_by_discipline = _high_issue_counts_by_discipline(issues)
    missing_by_discipline = Counter(
        _clean(requirement.discipline) or "Unmapped"
        for requirement in unevaluated
    )
    for discipline, missing_count in missing_by_discipline.items():
        if issue_counts_by_discipline.get(discipline.lower(), 0) <= 0:
            continue
        findings.append(
            ReadinessFinding(
                rule_code="MS005",
                severity="high",
                status="blocked",
                message=(
                    f"{discipline} has {missing_count} unevaluated requirements and "
                    f"{issue_counts_by_discipline[discipline.lower()]} high/critical model issues."
                ),
                discipline=discipline,
                readiness_impact=-6.0,
                action_type="review_trade_gaps",
                evidence={
                    "missing_requirements": missing_count,
                    "high_or_critical_issues": issue_counts_by_discipline[discipline.lower()],
                },
            )
        )

    return findings


def _readiness_risk_rules(
    requirements: Sequence[Requirement],
    compliance_by_requirement: Mapping[int, RequirementCompliance],
    issues: Sequence[Issue],
    latest_export: Export | None,
    now: datetime,
) -> list[ReadinessFinding]:
    findings: list[ReadinessFinding] = []
    latest_sync_at = _as_utc(latest_export.completed_at) if latest_export else None

    if latest_sync_at is None:
        findings.append(
            ReadinessFinding(
                rule_code="RDY003",
                severity="high",
                status="stale",
                message="No completed sync exists for this project.",
                readiness_impact=-8.0,
                action_type="run_sync",
            )
        )
    else:
        sync_age_days = (now - latest_sync_at).total_seconds() / 86400
        if sync_age_days > 3:
            findings.append(
                ReadinessFinding(
                    rule_code="RDY003",
                    severity="medium" if sync_age_days <= 7 else "high",
                    status="stale",
                    message=f"Latest sync is {sync_age_days:.1f} days old.",
                    readiness_impact=-4.0,
                    action_type="run_sync",
                    evidence={"latest_sync_at": latest_sync_at.isoformat()},
                )
            )

    total = len(requirements)
    unevaluated_count = sum(
        1
        for requirement in requirements
        if compliance_by_requirement.get(requirement.id) is None
        or compliance_by_requirement[requirement.id].status == "not_evaluated"
    )
    if total and unevaluated_count / total >= UNEVALUATED_WARNING_RATIO:
        findings.append(
            ReadinessFinding(
                rule_code="RDY008",
                severity="high",
                status="not_evaluated",
                message=(
                    f"{unevaluated_count} of {total} requirements are unevaluated; "
                    "readiness is not yet evidence-backed."
                ),
                readiness_impact=-7.0,
                action_type="evaluate_requirements",
                evidence={"total_requirements": total, "unevaluated_requirements": unevaluated_count},
            )
        )

    high_or_critical = [issue for issue in issues if issue.severity in {"high", "critical"}]
    if high_or_critical and not requirements:
        findings.append(
            ReadinessFinding(
                rule_code="MS005",
                severity="high",
                status="blocked",
                message=f"{len(high_or_critical)} high/critical model issues exist without active owner requirements.",
                readiness_impact=-6.0,
                action_type="review_issue_backlog",
            )
        )

    return findings


def _evidence_dict(compliance: RequirementCompliance | None) -> dict[str, Any]:
    if compliance is None or not isinstance(compliance.evidence, dict):
        return {}
    return compliance.evidence


def _extract_milestone(requirement: Requirement, evidence: Mapping[str, Any]) -> str | None:
    for key in ("milestone", "stage", "deliverable", "phase"):
        value = evidence.get(key)
        if isinstance(value, str) and value.strip():
            return value.strip()

    text = " ".join(
        value
        for value in (requirement.category, requirement.requirement_text, requirement.owner_status)
        if value
    ).upper()
    for token in MILESTONE_TOKENS:
        if token in text:
            return token.replace(" ", "")
    return None


def _extract_evidence_type(requirement: Requirement, evidence: Mapping[str, Any]) -> str | None:
    for key in ("evidence_type", "expected_evidence_type", "source_type"):
        value = evidence.get(key)
        if isinstance(value, str) and value.strip().lower() in EXPECTED_EVIDENCE_TYPES:
            return value.strip().lower()

    text = " ".join(
        value
        for value in (requirement.category, requirement.resource, requirement.links, requirement.requirement_text)
        if value
    ).lower()
    for evidence_type in EXPECTED_EVIDENCE_TYPES:
        if evidence_type in text:
            return evidence_type
    if "drawing" in text:
        return "sheet"
    if "specification" in text:
        return "spec"
    return None


def _high_issue_counts_by_discipline(issues: Sequence[Issue]) -> dict[str, int]:
    counts: Counter[str] = Counter()
    for issue in issues:
        if issue.severity not in {"high", "critical"}:
            continue
        text = " ".join(
            value
            for value in (
                issue.rule_code,
                issue.issue_type,
                issue.message,
                str(issue.traceability or ""),
            )
            if value
        ).lower()
        matched = False
        for discipline in ("mechanical", "electrical", "plumbing", "technology", "lighting"):
            if discipline in text:
                counts[discipline] += 1
                matched = True
        if not matched:
            counts["model"] += 1
    return dict(counts)


def _action_label(action_type: str) -> str:
    labels = {
        "assign_discipline": "Assign discipline",
        "attach_source": "Attach source",
        "map_milestone": "Map milestone",
        "map_trade": "Map trade",
        "map_evidence_type": "Map evidence type",
        "link_evidence": "Link evidence",
        "review_evidence": "Review evidence",
        "review_requirement": "Review requirement",
        "evaluate_requirements": "Evaluate requirements",
        "review_trade_gaps": "Review trade gaps",
        "run_sync": "Run sync",
        "review_issue_backlog": "Review issue backlog",
    }
    return labels.get(action_type, action_type.replace("_", " ").title())


def _clean(value: str | None) -> str:
    return (value or "").strip()


def _as_utc(value: datetime | None) -> datetime | None:
    if value is None:
        return None
    if value.tzinfo is None:
        return value.replace(tzinfo=timezone.utc)
    return value.astimezone(timezone.utc)
