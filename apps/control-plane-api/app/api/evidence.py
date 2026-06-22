"""Evidence acceptance workflow API."""

from __future__ import annotations

from typing import Any

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.orm import Session

from app.api.auth import get_current_user
from app.authz import require_project_access
from app.database import get_db
from app.models import AppUser, Project, Requirement
from app.schemas import (
    RequirementEvidenceCreate,
    RequirementEvidenceOut,
    RequirementEvidenceUpdate,
)
from app.services.evidence_service import (
    list_project_evidence,
    latest_requirement_evidence,
    upsert_requirement_evidence,
    update_requirement_evidence,
)
from app.services.model_evidence_resolver import resolve_project_model_evidence

router = APIRouter(prefix="/api/v1/projects", tags=["evidence"])


@router.get(
    "/{project_id}/evidence",
    response_model=list[RequirementEvidenceOut],
    summary="List evidence links for a project",
)
def get_project_evidence(
    project_id: int,
    requirement_id: int | None = Query(default=None),
    project: Project = Depends(require_project_access),
    db: Session = Depends(get_db),
) -> list[RequirementEvidenceOut]:
    _ = project
    return list_project_evidence(db, project_id, requirement_id=requirement_id)


@router.get(
    "/{project_id}/requirements/{requirement_id}/evidence",
    response_model=list[RequirementEvidenceOut],
    summary="List evidence links for a specific requirement",
)
def get_requirement_evidence(
    project_id: int,
    requirement_id: int,
    project: Project = Depends(require_project_access),
    db: Session = Depends(get_db),
) -> list[RequirementEvidenceOut]:
    _require_project_requirement(project, db, requirement_id)
    return list_project_evidence(db, project_id, requirement_id=requirement_id)


@router.post(
    "/{project_id}/requirements/{requirement_id}/evidence",
    response_model=RequirementEvidenceOut,
    summary="Create or upsert an evidence link for a requirement",
)
def create_requirement_evidence(
    project_id: int,
    requirement_id: int,
    payload: RequirementEvidenceCreate,
    project: Project = Depends(require_project_access),
    current_user: AppUser = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> RequirementEvidenceOut:
    _require_project_requirement(project, db, requirement_id)
    evidence = upsert_requirement_evidence(
        db,
        project_id,
        requirement_id,
        _payload_with_reviewer(payload, current_user),
    )
    return RequirementEvidenceOut.model_validate(evidence)


@router.patch(
    "/{project_id}/evidence/{evidence_id}",
    response_model=RequirementEvidenceOut,
    summary="Update review state for an evidence link",
)
def patch_requirement_evidence(
    project_id: int,
    evidence_id: int,
    payload: RequirementEvidenceUpdate,
    project: Project = Depends(require_project_access),
    current_user: AppUser = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> RequirementEvidenceOut:
    _ = project
    try:
        evidence = update_requirement_evidence(
            db,
            project_id,
            evidence_id,
            _payload_with_reviewer(payload, current_user),
        )
    except LookupError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    return RequirementEvidenceOut.model_validate(evidence)


@router.get(
    "/{project_id}/requirements/{requirement_id}/evidence/latest",
    response_model=RequirementEvidenceOut | None,
    summary="Get the latest evidence link for a requirement",
)
def get_latest_requirement_evidence(
    project_id: int,
    requirement_id: int,
    project: Project = Depends(require_project_access),
    db: Session = Depends(get_db),
) -> RequirementEvidenceOut | None:
    _require_project_requirement(project, db, requirement_id)
    evidence = latest_requirement_evidence(db, project_id, requirement_id)
    return RequirementEvidenceOut.model_validate(evidence) if evidence else None


@router.post(
    "/{project_id}/evidence/resolve-model",
    summary="Scan Revit export and create model evidence candidates for all requirements",
)
def resolve_model_evidence(
    project_id: int,
    project: Project = Depends(require_project_access),
    db: Session = Depends(get_db),
) -> dict:
    """Deterministic resolver: scores Element rows against active Requirements and
    creates RequirementEvidence candidates (needs_review / candidate).
    Candidates do NOT count as covered until a reviewer accepts them.
    """
    _ = project
    result = resolve_project_model_evidence(db, project_id)
    state = result.get("state", "ok")
    if state == "project_not_found":
        raise HTTPException(status_code=404, detail=result.get("error"))
    return result


def _require_project_requirement(project: Project, db: Session, requirement_id: int) -> Requirement:
    requirement = db.get(Requirement, requirement_id)
    if requirement is None:
        raise HTTPException(status_code=404, detail="Requirement not found")
    if project.client_id is not None and requirement.client_id != project.client_id:
        raise HTTPException(status_code=400, detail="Requirement does not belong to this project client")
    return requirement


def _payload_with_reviewer(
    payload: RequirementEvidenceCreate | RequirementEvidenceUpdate,
    current_user: AppUser,
) -> dict[str, Any]:
    data = payload.model_dump(exclude_unset=True)
    # Reviewer identity is authority context, never caller-supplied evidence.
    data.pop("reviewed_by", None)
    data.pop("reviewed_by_user_id", None)
    data["reviewed_by"] = current_user.email or current_user.name or f"user:{current_user.id}"
    data["reviewed_by_user_id"] = current_user.id
    return data
