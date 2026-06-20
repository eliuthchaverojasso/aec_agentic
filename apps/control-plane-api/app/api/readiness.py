"""Project readiness API."""

from __future__ import annotations

from datetime import datetime, timezone

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from app.database import get_db
from app.models import Project
from app.readiness.persistence import (
    list_readiness_actions,
    list_readiness_snapshots,
    persist_readiness_snapshot,
    update_readiness_action,
)
from app.readiness.service import build_project_readiness
from app.schemas import (
    ProjectReadinessOut,
    ReadinessActionOut,
    ReadinessActionUpdate,
    ReadinessSnapshotOut,
)

router = APIRouter(prefix="/api/v1/projects", tags=["readiness"])
actions_router = APIRouter(prefix="/api/v1/readiness", tags=["readiness"])


@router.get(
    "/{project_id}/readiness",
    response_model=ProjectReadinessOut,
    summary="Computed delivery readiness for a project",
)
def get_project_readiness(project_id: int, db: Session = Depends(get_db)) -> ProjectReadinessOut:
    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found")
    return build_project_readiness(db, project)


@router.post(
    "/{project_id}/readiness/recalculate",
    response_model=ReadinessSnapshotOut,
    summary="Recalculate readiness and persist a traceable snapshot",
)
def recalculate_project_readiness(
    project_id: int,
    db: Session = Depends(get_db),
) -> ReadinessSnapshotOut:
    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found")
    readiness = build_project_readiness(db, project)
    return persist_readiness_snapshot(db, project, readiness)


@router.get(
    "/{project_id}/readiness/snapshots",
    response_model=list[ReadinessSnapshotOut],
    summary="List persisted readiness snapshots for a project",
)
def get_project_readiness_snapshots(
    project_id: int,
    db: Session = Depends(get_db),
) -> list[ReadinessSnapshotOut]:
    if db.get(Project, project_id) is None:
        raise HTTPException(status_code=404, detail="Project not found")
    return list_readiness_snapshots(db, project_id)


@router.get(
    "/{project_id}/readiness/actions",
    response_model=list[ReadinessActionOut],
    summary="List readiness actions for a project",
)
def get_project_readiness_actions(
    project_id: int,
    db: Session = Depends(get_db),
) -> list[ReadinessActionOut]:
    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found")
    persisted = list_readiness_actions(db, project_id)
    if persisted:
        return persisted
    readiness = build_project_readiness(db, project)
    now = datetime.now(timezone.utc)
    return [
        ReadinessActionOut(
            id=-(index + 1),
            project_id=project_id,
            requirement_id=action.requirement_id,
            issue_id=None,
            rule_code=action.rule_code,
            action_type=action.action_type,
            title=action.label,
            description=action.detail,
            status="open",
            priority=_priority_from_severity(action.severity),
            owner=None,
            created_at=now,
            updated_at=now,
            persisted=False,
            source="readiness_engine",
        )
        for index, action in enumerate(readiness.recommended_actions)
    ]


@router.post(
    "/{project_id}/readiness/snapshots",
    response_model=ReadinessSnapshotOut,
    summary="Create a deterministic readiness snapshot for a project",
)
def create_project_readiness_snapshot(
    project_id: int,
    db: Session = Depends(get_db),
) -> ReadinessSnapshotOut:
    return recalculate_project_readiness(project_id, db)


@router.post(
    "/{project_id}/readiness/actions",
    response_model=list[ReadinessActionOut],
    summary="Persist generated readiness recommendations as actions",
)
def create_project_readiness_actions(
    project_id: int,
    db: Session = Depends(get_db),
) -> list[ReadinessActionOut]:
    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found")
    readiness = build_project_readiness(db, project)
    persist_readiness_snapshot(db, project, readiness)
    return list_readiness_actions(db, project_id)


@actions_router.patch(
    "/actions/{action_id}",
    response_model=ReadinessActionOut,
    summary="Update readiness action status, owner, or priority",
)
def patch_readiness_action(
    action_id: int,
    payload: ReadinessActionUpdate,
    db: Session = Depends(get_db),
) -> ReadinessActionOut:
    updated = update_readiness_action(db, action_id, payload.model_dump(exclude_unset=True))
    if updated is None:
        raise HTTPException(status_code=404, detail="Readiness action not found")
    return updated


def _priority_from_severity(severity: str) -> str:
    severity = severity.lower()
    if severity in {"critical", "high", "medium", "low"}:
        return severity
    return "medium"
