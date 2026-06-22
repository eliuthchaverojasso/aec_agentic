"""Requirement Audit & Evaluation Bundle API.

Ingests the deterministic Evaluation Bundle produced by the C# engine and serves
the per-requirement audit dossiers, the coherence findings, and an append-only
human review trail. The backend records the engine's decisions; it never
recomputes a status.
"""

from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import select
from sqlalchemy.orm import Session, selectinload

from app.api.auth import get_current_user
from app.database import get_db
from app.models import (
    AppUser,
    Project,
    RequirementAuditRecord,
    RequirementAuditRun,
    RequirementCoherenceFinding,
    RequirementReviewDecision,
)
from app.schemas import (
    EvaluationBundleIngestIn,
    RequirementAuditIngestResult,
    RequirementAuditRecordOut,
    RequirementAuditRunOut,
    RequirementCoherenceFindingOut,
    RequirementReviewDecisionCreate,
    RequirementReviewDecisionOut,
)
from app.services.requirement_audit_ingest import (
    BundleValidationError,
    ingest_evaluation_bundle,
)

router = APIRouter(prefix="/api/v1/projects", tags=["requirement-audits"])


def _require_project(db: Session, project_id: int) -> Project:
    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found")
    return project


def _require_run(db: Session, project_id: int, run_id: int) -> RequirementAuditRun:
    run = db.get(RequirementAuditRun, run_id)
    if run is None or run.project_id != project_id:
        raise HTTPException(status_code=404, detail="Requirement audit run not found")
    return run


@router.post(
    "/{project_id}/requirement-audits",
    response_model=RequirementAuditIngestResult,
    status_code=201,
    summary="Ingest an Evaluation Bundle produced by the C# engine",
)
def create_requirement_audit_run(
    project_id: int,
    payload: EvaluationBundleIngestIn,
    db: Session = Depends(get_db),
) -> RequirementAuditIngestResult:
    project = _require_project(db, project_id)
    try:
        result = ingest_evaluation_bundle(
            db,
            project,
            manifest=payload.manifest,
            audit_records=payload.audit_records,
            coherence=payload.coherence,
            export_id=payload.export_id,
            source_file_id=payload.source_file_id,
        )
    except BundleValidationError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc

    return RequirementAuditIngestResult(
        run=RequirementAuditRunOut.model_validate(result.run),
        records_ingested=result.records_ingested,
        coherence_findings_ingested=result.coherence_findings_ingested,
        requirements_linked=result.requirements_linked,
        reused_existing=result.reused_existing,
    )


@router.get(
    "/{project_id}/requirement-audits",
    response_model=list[RequirementAuditRunOut],
    summary="List requirement audit runs for a project (most recent first)",
)
def list_requirement_audit_runs(
    project_id: int,
    db: Session = Depends(get_db),
) -> list[RequirementAuditRunOut]:
    _require_project(db, project_id)
    runs = db.scalars(
        select(RequirementAuditRun)
        .where(RequirementAuditRun.project_id == project_id)
        .order_by(RequirementAuditRun.ingested_at.desc(), RequirementAuditRun.id.desc())
    ).all()
    return [RequirementAuditRunOut.model_validate(run) for run in runs]


@router.get(
    "/{project_id}/requirement-audits/{run_id}",
    response_model=RequirementAuditRunOut,
    summary="Get a single requirement audit run",
)
def get_requirement_audit_run(
    project_id: int,
    run_id: int,
    db: Session = Depends(get_db),
) -> RequirementAuditRunOut:
    run = _require_run(db, project_id, run_id)
    return RequirementAuditRunOut.model_validate(run)


@router.get(
    "/{project_id}/requirement-audits/{run_id}/records",
    response_model=list[RequirementAuditRecordOut],
    summary="List per-requirement audit records for a run",
)
def list_requirement_audit_records(
    project_id: int,
    run_id: int,
    db: Session = Depends(get_db),
) -> list[RequirementAuditRecordOut]:
    _require_run(db, project_id, run_id)
    records = db.scalars(
        select(RequirementAuditRecord)
        .where(RequirementAuditRecord.run_id == run_id)
        .options(selectinload(RequirementAuditRecord.review_decisions))
        .order_by(RequirementAuditRecord.id.asc())
    ).all()
    return [RequirementAuditRecordOut.model_validate(record) for record in records]


@router.get(
    "/{project_id}/requirement-audits/{run_id}/coherence",
    response_model=list[RequirementCoherenceFindingOut],
    summary="List coherence findings (duplicates/conflicts) for a run",
)
def list_requirement_coherence_findings(
    project_id: int,
    run_id: int,
    db: Session = Depends(get_db),
) -> list[RequirementCoherenceFindingOut]:
    _require_run(db, project_id, run_id)
    findings = db.scalars(
        select(RequirementCoherenceFinding)
        .where(RequirementCoherenceFinding.run_id == run_id)
        .order_by(RequirementCoherenceFinding.id.asc())
    ).all()
    return [RequirementCoherenceFindingOut.model_validate(finding) for finding in findings]


@router.post(
    "/{project_id}/requirement-audits/{run_id}/records/{record_id}/review",
    response_model=RequirementReviewDecisionOut,
    status_code=201,
    summary="Append an immutable human review decision to an audit record",
)
def create_requirement_review_decision(
    project_id: int,
    run_id: int,
    record_id: int,
    payload: RequirementReviewDecisionCreate,
    current_user: AppUser = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> RequirementReviewDecisionOut:
    _require_run(db, project_id, run_id)
    record = db.get(RequirementAuditRecord, record_id)
    if record is None or record.run_id != run_id:
        raise HTTPException(status_code=404, detail="Requirement audit record not found")

    # Reviewer identity is authority context, never caller-supplied.
    # Strip any client-provided values and inject from the authenticated user.
    decision = RequirementReviewDecision(
        audit_record_id=record.id,
        reviewer_user_id=current_user.id,
        reviewer_name=current_user.name or current_user.email or f"user:{current_user.id}",
        action=payload.action,
        previous_status=record.decision_status,
        resulting_status=payload.resulting_status,
        reason=payload.reason,
    )
    db.add(decision)
    db.commit()
    db.refresh(decision)
    return RequirementReviewDecisionOut.model_validate(decision)
