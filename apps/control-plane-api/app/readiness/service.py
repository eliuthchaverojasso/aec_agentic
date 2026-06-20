"""Computed project readiness service."""

from __future__ import annotations

from datetime import datetime, timezone

from sqlalchemy import func, or_, select
from sqlalchemy.orm import Session

from app.models import (
    Client,
    Element,
    Export,
    Issue,
    Project,
    Requirement,
    RequirementCompliance,
)
from app.readiness.scoring import (
    qaqc_health_score,
    readiness_label,
    sync_freshness_score,
    weighted_readiness,
)
from app.services.evidence_service import (
    coverage_status_from_requirement,
    latest_project_evidence_by_requirement,
)
from app.readiness.rules import (
    evaluate_readiness_rules,
    recommended_actions,
    summarize_gaps,
    top_findings,
)
from app.schemas import ProjectReadinessOut, ReadinessComponent, TradeReadinessRow

COVERED_STATUSES = {"compliant"}
EVALUATED_STATUSES = {"compliant", "non_compliant", "needs_review"}
EXCLUDED_STATUSES = {"not_applicable"}


def build_project_readiness(db: Session, project: Project) -> ProjectReadinessOut:
    latest_export = _latest_completed_export(db, project.id)
    latest_sync_at = latest_export.completed_at if latest_export else None
    total_elements = _element_count(db, project.id)
    severity_counts = _open_issue_counts(db, project.id)
    requirements = _active_requirements(db, project)
    compliance_by_requirement = _latest_compliance_by_requirement(db, project.id)
    evidence_by_requirement = latest_project_evidence_by_requirement(db, project.id)
    readiness_compliance_by_requirement = _evidence_augmented_compliance_by_requirement(
        project=project,
        requirements=requirements,
        compliance_by_requirement=compliance_by_requirement,
        evidence_by_requirement=evidence_by_requirement,
    )
    open_issues = _open_issues(db, project.id)
    findings = evaluate_readiness_rules(
        requirements=requirements,
        compliance_by_requirement=readiness_compliance_by_requirement,
        issues=open_issues,
        latest_export=latest_export,
    )

    requirement_coverage_score, requirement_detail = _requirement_coverage(db, project)
    qaqc_score = qaqc_health_score(
        total_elements=total_elements,
        critical_issues=severity_counts.get("critical", 0),
        high_issues=severity_counts.get("high", 0),
        medium_issues=severity_counts.get("medium", 0),
        low_issues=severity_counts.get("low", 0),
    )
    sync_score = sync_freshness_score(latest_sync_at)
    overall_score = weighted_readiness(requirement_coverage_score, qaqc_score, sync_score)

    client = db.get(Client, project.client_id) if project.client_id else None

    return ProjectReadinessOut(
        project_id=project.id,
        project_title=project.project_title,
        client_id=project.client_id,
        client_name=client.display_name if client else project.client_name,
        overall_readiness=overall_score,
        label=readiness_label(overall_score),
        requirement_coverage=ReadinessComponent(
            score=requirement_coverage_score,
            label=readiness_label(requirement_coverage_score),
            detail=requirement_detail,
        ),
        qaqc_health=ReadinessComponent(
            score=qaqc_score,
            label=readiness_label(qaqc_score),
            detail=(
                f"{sum(severity_counts.values())} open issues across "
                f"{total_elements} model elements"
            ),
        ),
        sync_freshness=ReadinessComponent(
            score=sync_score,
            label=readiness_label(sync_score),
            detail="Latest completed sync found" if latest_sync_at else "No completed sync found",
        ),
        open_issues=severity_counts,
        latest_export_id=latest_export.id if latest_export else None,
        latest_sync_at=latest_sync_at,
        trade_readiness=_trade_readiness(db, project, severity_counts),
        gap_summary=summarize_gaps(findings),
        top_gaps=[finding.to_gap_dict() for finding in top_findings(findings)],
        recommended_actions=recommended_actions(findings),
    )


def _latest_completed_export(db: Session, project_id: int) -> Export | None:
    return db.execute(
        select(Export)
        .where(Export.project_id == project_id, Export.status == "completed")
        .order_by(Export.completed_at.desc().nulls_last(), Export.id.desc())
        .limit(1)
    ).scalar_one_or_none()


def _element_count(db: Session, project_id: int) -> int:
    return int(
        db.execute(
            select(func.count(Element.id))
            .join(Export, Export.id == Element.export_id)
            .where(Export.project_id == project_id)
        ).scalar_one()
    )


def _open_issue_counts(db: Session, project_id: int) -> dict[str, int]:
    rows = db.execute(
        select(Issue.severity, func.count(Issue.id))
        .where(Issue.project_id == project_id, Issue.status == "open")
        .group_by(Issue.severity)
    ).all()
    counts = {severity: int(count) for severity, count in rows}
    for severity in ("critical", "high", "medium", "low"):
        counts.setdefault(severity, 0)
    return counts


def _open_issues(db: Session, project_id: int) -> list[Issue]:
    return list(
        db.execute(
            select(Issue).where(Issue.project_id == project_id, Issue.status == "open")
        ).scalars()
    )


def _active_requirements(db: Session, project: Project) -> list[Requirement]:
    if project.client_id is None:
        return []
    return list(
        db.execute(
            select(Requirement)
            .where(
                Requirement.client_id == project.client_id,
                Requirement.is_active.is_(True),
                Requirement.is_actionable.is_(True),
            )
            .order_by(Requirement.discipline, Requirement.id)
        ).scalars()
    )


def _latest_compliance_by_requirement(
    db: Session,
    project_id: int,
) -> dict[int, RequirementCompliance]:
    rows = db.execute(
        select(RequirementCompliance)
        .where(RequirementCompliance.project_id == project_id)
        .order_by(
            RequirementCompliance.requirement_id,
            RequirementCompliance.evaluated_at.desc(),
            RequirementCompliance.id.desc(),
        )
    ).scalars()

    compliance_by_requirement: dict[int, RequirementCompliance] = {}
    for row in rows:
        compliance_by_requirement.setdefault(row.requirement_id, row)
    return compliance_by_requirement


def _requirement_coverage(db: Session, project: Project) -> tuple[float, str]:
    if project.client_id is None:
        return 0.0, "Project is not associated with a client"

    requirements = _active_requirements(db, project)
    if not requirements:
        return 0.0, "No active owner requirements found for this client"

    compliance_by_requirement = _latest_compliance_by_requirement(db, project.id)
    evidence_by_requirement = latest_project_evidence_by_requirement(db, project.id)
    applicable = [
        requirement
        for requirement in requirements
        if _requirement_status(requirement, compliance_by_requirement, evidence_by_requirement) not in EXCLUDED_STATUSES
    ]
    if not applicable:
        return 0.0, "0 of 0 applicable owner requirements covered"

    covered = sum(
        1
        for requirement in applicable
        if _requirement_status(requirement, compliance_by_requirement, evidence_by_requirement) in COVERED_STATUSES
    )
    score = round(covered / len(applicable) * 100.0, 2)
    return score, f"{covered} of {len(applicable)} applicable owner requirements covered"


def _trade_readiness(
    db: Session,
    project: Project,
    severity_counts: dict[str, int],
) -> list[TradeReadinessRow]:
    if project.client_id is None:
        return []

    requirements = _active_requirements(db, project)
    compliance_by_requirement = _latest_compliance_by_requirement(db, project.id)
    evidence_by_requirement = latest_project_evidence_by_requirement(db, project.id)
    stats_by_discipline: dict[str, dict[str, int]] = {}
    for requirement in requirements:
        status = _requirement_status(requirement, compliance_by_requirement, evidence_by_requirement)
        if status in EXCLUDED_STATUSES:
            continue

        stats = stats_by_discipline.setdefault(
            requirement.discipline,
            {"total": 0, "evaluated": 0, "covered": 0, "needs_review": 0},
        )
        stats["total"] += 1
        if status in EVALUATED_STATUSES:
            stats["evaluated"] += 1
        if status in COVERED_STATUSES:
            stats["covered"] += 1
        if status == "needs_review":
            stats["needs_review"] += 1

    rows: list[TradeReadinessRow] = []
    for discipline in sorted(stats_by_discipline):
        stats = stats_by_discipline[discipline]
        total_int = stats["total"]
        evaluated_int = stats["evaluated"]
        covered_int = stats["covered"]
        coverage = round(covered_int / total_int * 100.0, 2) if total_int else 0.0
        discipline_issue_counts = _issue_counts_for_discipline(db, project.id, discipline)
        issue_penalty = (
            discipline_issue_counts.get("critical", 0) * 5.0
            + discipline_issue_counts.get("high", 0) * 2.0
        )
        readiness = max(0.0, round(coverage - issue_penalty, 2))
        rows.append(
            TradeReadinessRow(
                discipline=discipline,
                readiness=readiness,
                label=readiness_label(readiness),
                requirements_total=total_int,
                requirements_evaluated=evaluated_int,
                missing_requirements=max(0, total_int - evaluated_int),
                needs_review=stats["needs_review"],
                critical_issues=discipline_issue_counts.get("critical", 0),
                high_issues=discipline_issue_counts.get("high", 0),
            )
        )

    if not rows and sum(severity_counts.values()) > 0:
        rows.extend(_model_health_trade_fallback(db, project.id))

    return rows


def _model_health_trade_fallback(db: Session, project_id: int) -> list[TradeReadinessRow]:
    issue_rows = db.execute(
        select(Issue, Element)
        .outerjoin(Element, Element.id == Issue.element_db_id)
        .where(Issue.project_id == project_id, Issue.status == "open")
    ).all()
    element_rows = db.execute(
        select(Element.category, func.count(Element.id))
        .join(Export, Export.id == Element.export_id)
        .where(Export.project_id == project_id)
        .group_by(Element.category)
    ).all()
    stats: dict[str, dict[str, int]] = {}
    for category, count in element_rows:
        trade = _infer_trade(category or "")
        stats.setdefault(trade, _empty_trade_stats())["elements"] += int(count)
    for issue, element in issue_rows:
        trade = _infer_trade(f"{element.category if element else ''} {issue.message or ''} {issue.issue_type or ''}")
        row = stats.setdefault(trade, _empty_trade_stats())
        row["open_issues"] += 1
        row[issue.severity] += 1

    rows: list[TradeReadinessRow] = []
    for trade in ("MECHANICAL", "ELECTRICAL", "PLUMBING", "TECHNOLOGY", "LIGHTING", "UNKNOWN"):
        row = stats.get(trade)
        if not row or (row["elements"] == 0 and row["open_issues"] == 0):
            continue
        score = qaqc_health_score(
            total_elements=row["elements"],
            critical_issues=row["critical"],
            high_issues=row["high"],
            medium_issues=row["medium"],
            low_issues=row["low"],
        )
        rows.append(
            TradeReadinessRow(
                discipline=trade,
                readiness=score,
                label=readiness_label(score),
                requirements_total=0,
                requirements_evaluated=0,
                missing_requirements=0,
                needs_review=0,
                critical_issues=row["critical"],
                high_issues=row["high"],
                source="model_health_fallback",
                official=False,
                elements=row["elements"],
                open_issues=row["open_issues"],
                medium_issues=row["medium"],
                low_issues=row["low"],
            )
        )
    return rows


def _empty_trade_stats() -> dict[str, int]:
    return {"elements": 0, "open_issues": 0, "critical": 0, "high": 0, "medium": 0, "low": 0}


def _infer_trade(text: str) -> str:
    normalized = text.upper()
    if any(token in normalized for token in ("MECHANICAL", "MECH", "HVAC", "DUCT", "AIR ")):
        return "MECHANICAL"
    if any(token in normalized for token in ("ELECTRICAL", "ELEC", "PANEL", "OUTLET", "LIGHT", "LIGHTING", "POWER")):
        return "ELECTRICAL"
    if any(token in normalized for token in ("PLUMB", "PIPE", "WASTE", "WATER", "GAS")):
        return "PLUMBING"
    if any(token in normalized for token in ("TECH", "TELECOM", "DATA", "COMM", "SECURITY", "AV ")):
        return "TECHNOLOGY"
    if "LIGHT" in normalized:
        return "LIGHTING"
    return "UNKNOWN"


def _issue_counts_for_discipline(db: Session, project_id: int, discipline: str) -> dict[str, int]:
    token = discipline.lower()
    category_filters = [Element.category.ilike(f"%{token}%")]
    if token == "electrical":
        category_filters.append(Element.category.ilike("%lighting%"))
    if token == "plumbing":
        category_filters.append(Element.category.ilike("%pipe%"))

    rows = db.execute(
        select(Issue.severity, func.count(Issue.id))
        .join(Element, Element.id == Issue.element_db_id)
        .where(
            Issue.project_id == project_id,
            Issue.status == "open",
            or_(*category_filters),
        )
        .group_by(Issue.severity)
    ).all()
    return {severity: int(count) for severity, count in rows}


def _requirement_status(
    requirement: Requirement,
    compliance_by_requirement: dict[int, RequirementCompliance],
    evidence_by_requirement: dict[int, object],
) -> str:
    compliance = compliance_by_requirement.get(requirement.id)
    evidence = evidence_by_requirement.get(requirement.id)
    return coverage_status_from_requirement(requirement, compliance, evidence)


def _evidence_augmented_compliance_by_requirement(
    project: Project,
    requirements: list[Requirement],
    compliance_by_requirement: dict[int, RequirementCompliance],
    evidence_by_requirement: dict[int, object],
) -> dict[int, RequirementCompliance]:
    augmented = dict(compliance_by_requirement)
    now = datetime.now(timezone.utc)

    for requirement in requirements:
        if requirement.id in augmented:
            continue
        evidence = evidence_by_requirement.get(requirement.id)
        if evidence is None:
            continue
        review_status = getattr(evidence, "review_status", "none")
        if review_status == "none":
            continue

        status = "compliant"
        if review_status in {"candidate", "needs_review"}:
            status = "needs_review"
        elif review_status == "rejected":
            status = "non_compliant"

        augmented[requirement.id] = RequirementCompliance(
            requirement_id=requirement.id,
            project_id=project.id,
            model_id=None,
            status=status,
            evidence={
                "source": getattr(evidence, "source_label", None) or getattr(evidence, "source_ref", None),
                "evidence_type": getattr(evidence, "evidence_type", None),
                "evidence_status": getattr(evidence, "evidence_status", None),
                "review_status": review_status,
                "source_ref": getattr(evidence, "source_ref", None),
                "document_id": getattr(evidence, "document_id", None),
                "sheet_id": getattr(evidence, "sheet_id", None),
                "model_element_id": getattr(evidence, "model_element_id", None),
                "sheet_number": getattr(evidence, "sheet_number", None),
                "spec_section": getattr(evidence, "spec_section", None),
                "confidence": float(getattr(evidence, "confidence", 0.0)) if getattr(evidence, "confidence", None) is not None else None,
                "review_note": getattr(evidence, "review_note", None),
                "needs_review": review_status in {"candidate", "needs_review"},
            },
            evaluated_by=getattr(evidence, "reviewed_by", None) or "manual",
            evaluated_at=getattr(evidence, "reviewed_at", None) or getattr(evidence, "updated_at", None) or now,
            notes=getattr(evidence, "review_note", None),
        )

    return augmented
