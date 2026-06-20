from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from sqlalchemy import case, func, select
from sqlalchemy.orm import Session

from app.models import Requirement, RequirementEvidence
from app.schemas import RequirementEvidenceCreate, RequirementEvidenceOut, RequirementEvidenceUpdate

SEED_SOURCE_REF = "readiness_rule:EVD001"
REVIEW_STATUSES = {"candidate", "accepted", "rejected", "needs_review", "none"}


def list_project_evidence(
    db: Session,
    project_id: int,
    requirement_id: int | None = None,
) -> list[RequirementEvidenceOut]:
    stmt = (
        select(RequirementEvidence)
        .where(RequirementEvidence.project_id == project_id)
        .where(RequirementEvidence.source_ref != SEED_SOURCE_REF)
    )
    if requirement_id is not None:
        stmt = stmt.where(RequirementEvidence.requirement_id == requirement_id)
    rows = db.execute(
        stmt.order_by(
            RequirementEvidence.requirement_id.asc(),
            RequirementEvidence.updated_at.desc().nullslast(),
            RequirementEvidence.id.desc(),
        )
    ).scalars().all()
    return [RequirementEvidenceOut.model_validate(row) for row in rows]


def latest_project_evidence_by_requirement(
    db: Session,
    project_id: int,
) -> dict[int, RequirementEvidence]:
    priority = case((RequirementEvidence.source_ref == SEED_SOURCE_REF, 0), else_=1)
    rows = db.execute(
        select(RequirementEvidence)
        .where(RequirementEvidence.project_id == project_id)
        .order_by(
            RequirementEvidence.requirement_id.asc(),
            priority.desc(),
            RequirementEvidence.updated_at.desc().nullslast(),
            RequirementEvidence.id.desc(),
        )
    ).scalars()
    latest: dict[int, RequirementEvidence] = {}
    for row in rows:
        latest.setdefault(row.requirement_id, row)
    return latest


def latest_requirement_evidence(
    db: Session,
    project_id: int,
    requirement_id: int,
) -> RequirementEvidence | None:
    return (
        db.execute(
            select(RequirementEvidence)
            .where(
                RequirementEvidence.project_id == project_id,
                RequirementEvidence.requirement_id == requirement_id,
                RequirementEvidence.source_ref != SEED_SOURCE_REF,
            )
            .order_by(
                RequirementEvidence.updated_at.desc().nullslast(),
                RequirementEvidence.id.desc(),
            )
        )
        .scalars()
        .first()
    )


def upsert_requirement_evidence(
    db: Session,
    project_id: int,
    requirement_id: int,
    payload: RequirementEvidenceCreate | RequirementEvidenceUpdate | dict[str, Any],
) -> RequirementEvidence:
    data = payload.model_dump(exclude_unset=True) if hasattr(payload, "model_dump") else dict(payload)
    review_status = _normalize_review_status(data.get("review_status"))
    source_ref = _resolve_source_ref(data, requirement_id)
    metadata = _build_metadata(data, review_status, source_ref)
    evidence_status = _coverage_status_from_review(review_status)
    now = datetime.now(timezone.utc)

    existing = db.execute(
        select(RequirementEvidence).where(
            RequirementEvidence.project_id == project_id,
            RequirementEvidence.requirement_id == requirement_id,
            RequirementEvidence.source_ref == source_ref,
        )
    ).scalar_one_or_none()

    if existing is None:
        evidence_id = _next_requirement_evidence_id(db)
        existing = RequirementEvidence(
            id=evidence_id,
            project_id=project_id,
            requirement_id=requirement_id,
            evidence_type=data.get("evidence_type") or "manual",
            evidence_status=evidence_status,
            source_ref=source_ref,
            element_unique_id=data.get("model_element_id") or data.get("element_unique_id"),
            sheet_number=data.get("sheet_number"),
            spec_section=data.get("spec_section"),
            confidence=data.get("confidence"),
            metadata_json=metadata,
        )
        db.add(existing)
    else:
        existing.evidence_type = data.get("evidence_type") or existing.evidence_type
        existing.evidence_status = evidence_status
        existing.source_ref = source_ref
        existing.element_unique_id = data.get("model_element_id") or data.get("element_unique_id") or existing.element_unique_id
        existing.sheet_number = data.get("sheet_number") or existing.sheet_number
        existing.spec_section = data.get("spec_section") or existing.spec_section
        if data.get("confidence") is not None:
            existing.confidence = data.get("confidence")
        existing.metadata_json = metadata
        existing.updated_at = now

    if review_status == "accepted":
        existing.metadata_json["reviewed_at"] = metadata.get("reviewed_at") or now.isoformat()
    elif review_status in {"rejected", "needs_review"} and metadata.get("reviewed_at") is None:
        existing.metadata_json["reviewed_at"] = now.isoformat()

    db.commit()
    db.refresh(existing)
    return existing


def _next_requirement_evidence_id(db: Session) -> int | None:
    bind = db.get_bind()
    if bind is not None and getattr(bind.dialect, "name", "") == "postgresql":
        return None
    current_max = db.execute(select(func.coalesce(func.max(RequirementEvidence.id), 0))).scalar_one()
    return int(current_max) + 1


def update_requirement_evidence(
    db: Session,
    project_id: int,
    evidence_id: int,
    payload: RequirementEvidenceUpdate,
) -> RequirementEvidence:
    existing = db.execute(
        select(RequirementEvidence).where(
            RequirementEvidence.id == evidence_id,
            RequirementEvidence.project_id == project_id,
        )
    ).scalar_one_or_none()
    if existing is None:
        raise LookupError("Requirement evidence not found")
    data = payload.model_dump(exclude_unset=True)
    merged = {
        **(existing.metadata_json or {}),
        **data,
        "source_ref": data.get("source_ref") or existing.source_ref,
        "source_label": data.get("source_label") or existing.source_label,
        "model_element_id": data.get("model_element_id") or existing.model_element_id,
        "sheet_number": data.get("sheet_number") or existing.sheet_number,
        "spec_section": data.get("spec_section") or existing.spec_section,
    }
    review_status = _normalize_review_status(merged.get("review_status") or existing.review_status)
    source_ref = _resolve_source_ref(merged, existing.requirement_id)
    metadata = _build_metadata(merged, review_status, source_ref)
    existing.evidence_status = _coverage_status_from_review(review_status)
    existing.source_ref = source_ref
    existing.evidence_type = data.get("evidence_type") or existing.evidence_type
    existing.element_unique_id = data.get("model_element_id") or data.get("element_unique_id") or existing.element_unique_id
    existing.sheet_number = data.get("sheet_number") or existing.sheet_number
    existing.spec_section = data.get("spec_section") or existing.spec_section
    if data.get("confidence") is not None:
        existing.confidence = data.get("confidence")
    existing.metadata_json = metadata
    existing.updated_at = datetime.now(timezone.utc)
    db.commit()
    db.refresh(existing)
    return existing


def normalized_status_from_evidence(is_actionable: bool, review_status: str) -> str:
    """Map (is_actionable, review_status) → normalized frontend status."""
    if not is_actionable:
        return "not_applicable"
    return {
        "accepted": "accepted",
        "rejected": "rejected",
        "candidate": "candidate",
        "needs_review": "needs_review",
    }.get(review_status, "no_evidence")


def evidence_status_from_review(review_status: str) -> str:
    """Map an evidence object's review_status → evidence coverage status
    (covered / needs_review / missing). This is the EVIDENCE-object vocabulary."""
    return {
        "accepted": "covered",
        "candidate": "needs_review",
        "needs_review": "needs_review",
        "rejected": "missing",
    }.get(review_status, "missing")


def requirement_evidence_status_from_review(review_status: str) -> str:
    """Map a requirement's evidence review_status → requirement-level status
    (compliant / needs_review / missing). This is the REQUIREMENT vocabulary used
    in the requirements listing: accepted evidence makes the requirement compliant,
    while the coverage COUNT still treats it as covered. Distinct from
    evidence_status_from_review, which describes the evidence object itself."""
    return {
        "accepted": "compliant",
        "candidate": "needs_review",
        "needs_review": "needs_review",
        "rejected": "missing",
    }.get(review_status, "missing")


def evidence_review_status(row: RequirementEvidence | None) -> str:
    if row is None:
        return "none"
    review_status = (row.metadata_json or {}).get("review_status")
    if isinstance(review_status, str) and review_status in REVIEW_STATUSES:
        return review_status
    if row.evidence_status == "covered":
        return "accepted"
    if row.evidence_status == "needs_review":
        return "needs_review"
    if row.evidence_status == "blocked":
        return "needs_review"
    return "none"


def coverage_status_from_requirement(
    requirement: Requirement,
    compliance_row: Any | None,
    evidence_row: RequirementEvidence | None,
) -> str:
    if not requirement.is_actionable:
        return "not_applicable"
    if compliance_row is not None:
        return compliance_row.status
    if evidence_row is None:
        return "missing"
    review_status = evidence_review_status(evidence_row)
    if review_status == "accepted":
        return "compliant"
    if review_status in {"candidate", "needs_review"}:
        return "needs_review"
    if review_status == "rejected":
        return "missing"
    if evidence_row.evidence_status == "covered":
        return "compliant"
    if evidence_row.evidence_status == "needs_review":
        return "needs_review"
    if evidence_row.evidence_status == "not_applicable":
        return "not_applicable"
    return "missing"


def _normalize_review_status(raw: Any) -> str:
    if isinstance(raw, str):
        normalized = raw.strip().lower().replace(" ", "_")
        if normalized in REVIEW_STATUSES:
            return normalized
    return "candidate"


def _resolve_source_ref(data: dict[str, Any], requirement_id: int) -> str:
    source_ref = data.get("source_ref")
    if isinstance(source_ref, str) and source_ref.strip():
        return source_ref.strip()
    source_label = data.get("source_label")
    if isinstance(source_label, str) and source_label.strip():
        return f"manual:req:{requirement_id}:{_slug(source_label)}"
    document_id = data.get("document_id")
    if document_id is not None:
        return f"document:{document_id}"
    sheet_id = data.get("sheet_id")
    if sheet_id is not None:
        return f"sheet:{sheet_id}"
    model_element_id = data.get("model_element_id")
    if isinstance(model_element_id, str) and model_element_id.strip():
        return f"model:{_slug(model_element_id)}"
    return f"manual:req:{requirement_id}"


def _build_metadata(data: dict[str, Any], review_status: str, source_ref: str) -> dict[str, Any]:
    metadata = dict(data.get("metadata") or {})
    metadata["review_status"] = review_status
    metadata["source_ref"] = source_ref

    for key in (
        "source_label",
        "review_note",
        "reviewed_by",
        "reviewed_by_user_id",
        "document_id",
        "sheet_id",
        "model_element_id",
        "sheet_number",
        "spec_section",
    ):
        value = data.get(key)
        if value is not None:
            metadata[key] = value

    if data.get("confidence") is not None:
        metadata["confidence"] = data.get("confidence")
    if review_status == "accepted" and "reviewed_at" not in metadata:
        metadata["reviewed_at"] = datetime.now(timezone.utc).isoformat()
    elif review_status in {"rejected", "needs_review"} and "reviewed_at" not in metadata:
        metadata["reviewed_at"] = datetime.now(timezone.utc).isoformat()
    return metadata


def _coverage_status_from_review(review_status: str) -> str:
    if review_status == "accepted":
        return "covered"
    if review_status in {"candidate", "needs_review"}:
        return "needs_review"
    if review_status == "rejected":
        return "missing"
    return "missing"


def _slug(value: str) -> str:
    return "".join(char.lower() if char.isalnum() else "-" for char in value).strip("-")[:80] or "value"
