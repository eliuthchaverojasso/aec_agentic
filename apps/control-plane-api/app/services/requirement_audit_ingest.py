"""Ingest a deterministic Evaluation Bundle into the system of record.

The C# engine is the single authority on requirement status. This service
*records* the bundle it produced — the per-requirement audit dossiers and the
coherence findings — into PostgreSQL. It never recomputes a decision.

Ingest is idempotent per ``(project_id, run_uid)``: re-posting the same bundle
returns the existing run instead of duplicating it.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any

from sqlalchemy import select
from sqlalchemy.orm import Session

from app.models import (
    Project,
    Requirement,
    RequirementAuditRecord,
    RequirementAuditRun,
    RequirementCoherenceFinding,
)

SUPPORTED_SCHEMA_VERSIONS = {"1.0"}


class BundleValidationError(ValueError):
    """Raised when a posted bundle is structurally invalid or unsupported."""


@dataclass
class IngestResult:
    run: RequirementAuditRun
    records_ingested: int
    coherence_findings_ingested: int
    requirements_linked: int
    reused_existing: bool


def _pick(data: dict[str, Any] | None, *keys: str, default: Any = None) -> Any:
    """Read the first present key (supports camelCase + snake_case spellings)."""
    if not isinstance(data, dict):
        return default
    for key in keys:
        if key in data and data[key] is not None:
            return data[key]
    return default


def _parse_timestamp(value: Any) -> datetime:
    if isinstance(value, datetime):
        return value
    if isinstance(value, str) and value.strip():
        text = value.strip().replace("Z", "+00:00")
        # Trim over-long fractional seconds (C# writes 7 digits; Python wants <= 6).
        if "." in text:
            head, _, tail = text.partition(".")
            digits = ""
            rest = ""
            for index, char in enumerate(tail):
                if char.isdigit():
                    digits += char
                else:
                    rest = tail[index:]
                    break
            text = f"{head}.{digits[:6]}{rest}"
        try:
            parsed = datetime.fromisoformat(text)
            if parsed.tzinfo is None:
                parsed = parsed.replace(tzinfo=timezone.utc)
            return parsed
        except ValueError:
            pass
    return datetime.now(timezone.utc)


def ingest_evaluation_bundle(
    db: Session,
    project: Project,
    *,
    manifest: dict[str, Any],
    audit_records: list[dict[str, Any]],
    coherence: dict[str, Any],
    export_id: int | None = None,
    source_file_id: int | None = None,
) -> IngestResult:
    if not isinstance(manifest, dict):
        raise BundleValidationError("Bundle manifest is missing or not an object.")

    schema_version = str(_pick(manifest, "schemaVersion", "schema_version", default="")).strip()
    if schema_version not in SUPPORTED_SCHEMA_VERSIONS:
        raise BundleValidationError(
            f"Unsupported bundle schema_version '{schema_version}'. "
            f"Supported: {', '.join(sorted(SUPPORTED_SCHEMA_VERSIONS))}."
        )

    run_uid = str(_pick(manifest, "evaluationRunId", "evaluation_run_id", "run_uid", default="")).strip()
    if not run_uid:
        raise BundleValidationError("Bundle manifest is missing evaluationRunId.")

    # Idempotency: a run already ingested for this project + run_uid is returned as-is.
    existing = db.scalar(
        select(RequirementAuditRun).where(
            RequirementAuditRun.project_id == project.id,
            RequirementAuditRun.run_uid == run_uid,
        )
    )
    if existing is not None:
        return IngestResult(
            run=existing,
            records_ingested=len(existing.records),
            coherence_findings_ingested=len(existing.coherence_findings),
            requirements_linked=sum(1 for r in existing.records if r.requirement_id is not None),
            reused_existing=True,
        )

    coherence = coherence or {}
    audit_records = audit_records or []

    run = RequirementAuditRun(
        project_id=project.id,
        export_id=export_id,
        source_file_id=source_file_id,
        run_uid=run_uid,
        run_status="completed",
        as_of=_parse_timestamp(_pick(manifest, "asOfUtc", "as_of_utc", "as_of")),
        schema_version=schema_version,
        engine_version=_pick(manifest, "engineVersion", "engine_version"),
        ruleset_version=_pick(manifest, "rulesetVersion", "ruleset_version"),
        taxonomy_version=_pick(manifest, "taxonomyVersion", "taxonomy_version"),
        score_policy_version=_pick(manifest, "scorePolicyVersion", "score_policy_version"),
        input_hash=_pick(manifest, "inputHash", "input_hash"),
        output_hash=_pick(manifest, "outputHash", "output_hash"),
        project_name=_pick(manifest, "projectName", "project_name"),
        model_name=_pick(manifest, "modelName", "model_name"),
        requirements_file=_pick(manifest, "requirementsFile", "requirements_file"),
        requirements_total=int(_pick(manifest, "requirementsTotal", "requirements_total", default=len(audit_records)) or 0),
        status_counts=_pick(manifest, "statusCounts", "status_counts", default={}) or {},
        coherence_grade=_pick(coherence, "coherenceGrade", "coherence_grade"),
        coherence_findings_total=int(
            _pick(manifest, "coherenceFindingsTotal", "coherence_findings_total", default=0) or 0
        ),
    )
    db.add(run)
    db.flush()  # assign run.id

    requirements_linked = _ingest_records(db, project, run, audit_records)
    findings_ingested = _ingest_findings(db, run, coherence.get("findings") or [])

    db.commit()
    db.refresh(run)

    return IngestResult(
        run=run,
        records_ingested=len(audit_records),
        coherence_findings_ingested=findings_ingested,
        requirements_linked=requirements_linked,
        reused_existing=False,
    )


def _ingest_records(
    db: Session, project: Project, run: RequirementAuditRun, records: list[dict[str, Any]]
) -> int:
    requirements_linked = 0
    for raw in records:
        if not isinstance(raw, dict):
            continue
        source = _pick(raw, "source", default={}) or {}
        content_hash = _pick(source, "requirementContentHash", "requirement_content_hash")
        requirement_id = _link_requirement(db, project, content_hash)
        if requirement_id is not None:
            requirements_linked += 1

        db.add(
            RequirementAuditRecord(
                run_id=run.id,
                requirement_id=requirement_id,
                requirement_uid=_pick(raw, "requirementId", "requirement_id", "requirement_uid"),
                requirement_content_hash=content_hash,
                decision_status=str(_pick(raw, "decisionStatus", "decision_status", default="Indeterminate")),
                lifecycle_status=str(_pick(raw, "lifecycleStatus", "lifecycle_status", default="CoherenceChecked")),
                requirement_type=_pick(raw, "requirementType", "requirement_type"),
                validation_type=_pick(raw, "validationType", "validation_type"),
                applies=bool(_pick(raw, "applies", default=True)),
                rule_applied=_pick(raw, "ruleApplied", "rule_applied"),
                decision_reason=_pick(raw, "decisionReason", "decision_reason"),
                confidence=_pick(raw, "confidence"),
                direct_evidence_count=int(_pick(raw, "directEvidenceCount", "direct_evidence_count", default=0) or 0),
                supporting_evidence_count=int(
                    _pick(raw, "supportingEvidenceCount", "supporting_evidence_count", default=0) or 0
                ),
                source_provenance=source,
                semantic_ir=_pick(raw, "semanticIr", "semantic_ir", default={}) or {},
                evidence_policy=_pick(raw, "evidencePolicy", "evidence_policy", default={}) or {},
                candidate_funnel=_pick(raw, "candidateFunnel", "candidate_funnel", default={}) or {},
                coherence_finding_ids=_pick(raw, "coherenceFindingIds", "coherence_finding_ids", default=[]) or [],
                next_best_action=_pick(raw, "nextBestAction", "next_best_action"),
                record_hash=_pick(raw, "recordHash", "record_hash"),
            )
        )
    return requirements_linked


def _ingest_findings(db: Session, run: RequirementAuditRun, findings: list[dict[str, Any]]) -> int:
    count = 0
    for raw in findings:
        if not isinstance(raw, dict):
            continue
        db.add(
            RequirementCoherenceFinding(
                run_id=run.id,
                finding_uid=str(_pick(raw, "id", "finding_uid", default=f"finding-{count}")),
                finding_type=str(_pick(raw, "findingType", "finding_type", default="Unknown")),
                severity=str(_pick(raw, "severity", default="Info")),
                requirement_type=_pick(raw, "requirementType", "requirement_type"),
                status=str(_pick(raw, "status", default="open")),
                rationale=_pick(raw, "rationale"),
                primary_requirement=_pick(raw, "primary", "primary_requirement", default={}) or {},
                related_requirement=_pick(raw, "related", "related_requirement"),
                normalized_values=_pick(raw, "normalizedValues", "normalized_values", default={}) or {},
            )
        )
        count += 1
    return count


def _link_requirement(db: Session, project: Project, content_hash: str | None) -> int | None:
    """Best-effort link to an existing requirement by content hash within the project's client.

    The C# content hash and the Python loader hash are normalized differently, so
    a match is opportunistic; when it does not match, the record still keeps the
    requirement_uid + content_hash and requirement_id stays null. The link never
    affects the recorded decision.
    """
    if not content_hash or project.client_id is None:
        return None
    requirement = db.scalar(
        select(Requirement).where(
            Requirement.client_id == project.client_id,
            Requirement.content_hash == content_hash,
        )
    )
    return requirement.id if requirement is not None else None
