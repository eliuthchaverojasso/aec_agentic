"""Advisory SEION-KGE endpoints.

SEION predictions are suggestions only. They do not approve readiness, decide
official compliance, close issues, or mutate official evidence/compliance rows.
"""

from __future__ import annotations

from pathlib import Path

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.orm import Session

from app.database import get_db
from app.api.auth import get_current_user
from app.models import AppUser, Project
from app.schemas import (
    SeionGraphExportOut,
    SeionPredictionImportOut,
    SeionPredictionImportRequest,
    SeionPredictionOut,
    SeionPredictionReviewUpdate,
)
from app.seion.exporter import DEFAULT_OUTPUT_DIR, export_seion_graph
from app.seion.importer import import_seion_predictions
from app.seion.service import list_seion_predictions, review_seion_prediction

router = APIRouter(prefix="/api/v1", tags=["seion"])


@router.post(
    "/seion/export-graph",
    response_model=SeionGraphExportOut,
    summary="Export official PostgreSQL facts for advisory SEION-KGE scoring",
)
def export_graph(
    output_dir: str | None = Query(default=None, description="Optional server-local export directory"),
    db: Session = Depends(get_db),
) -> SeionGraphExportOut:
    target_dir = Path(output_dir) if output_dir else DEFAULT_OUTPUT_DIR
    result = export_seion_graph(db, target_dir)
    return SeionGraphExportOut(
        entity_count=result.entity_count,
        triple_count=result.triple_count,
        entities_path=str(result.entities_path),
        triples_path=str(result.triples_path),
        warnings=result.warnings,
    )


@router.post(
    "/seion/import-predictions",
    response_model=SeionPredictionImportOut,
    summary="Import advisory SEION-KGE predictions from the server artifacts directory",
)
def import_predictions(
    payload: SeionPredictionImportRequest,
    db: Session = Depends(get_db),
) -> SeionPredictionImportOut:
    try:
        result = import_seion_predictions(db, payload.path, project_id=payload.project_id)
    except FileNotFoundError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    return SeionPredictionImportOut(
        inserted_count=result.inserted_count,
        skipped_count=result.skipped_count,
        warnings=result.warnings,
    )


@router.get(
    "/projects/{project_id}/seion/suggestions",
    response_model=list[SeionPredictionOut],
    summary="List advisory SEION-KGE suggestions for a project",
)
def get_project_suggestions(
    project_id: int,
    status: str | None = Query(default=None, description="Advisory suggestion status"),
    relation: str | None = Query(default=None, description="Suggested relationship type"),
    limit: int = Query(default=50, ge=1, le=250),
    db: Session = Depends(get_db),
) -> list[SeionPredictionOut]:
    if db.get(Project, project_id) is None:
        raise HTTPException(status_code=404, detail="Project not found")
    return list_seion_predictions(db, project_id, status=status, relation=relation, limit=limit)


@router.post(
    "/seion/suggestions/{prediction_id}/accept",
    response_model=SeionPredictionOut,
    summary="Mark an advisory SEION-KGE suggestion as accepted",
)
def accept_suggestion(
    prediction_id: int,
    payload: SeionPredictionReviewUpdate | None = None,
    current_user: AppUser = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> SeionPredictionOut:
    # Reviewer identity comes from the authenticated user, never the payload.
    reviewer_note = payload.reviewer_note if payload else None
    updated = review_seion_prediction(
        db,
        prediction_id,
        status="accepted",
        reviewer_note=reviewer_note,
        accepted_by=current_user.email or current_user.name or f"user:{current_user.id}",
    )
    if updated is None:
        raise HTTPException(status_code=404, detail="SEION advisory suggestion not found")
    return updated


@router.post(
    "/seion/suggestions/{prediction_id}/reject",
    response_model=SeionPredictionOut,
    summary="Mark an advisory SEION-KGE suggestion as rejected",
)
def reject_suggestion(
    prediction_id: int,
    payload: SeionPredictionReviewUpdate | None = None,
    current_user: AppUser = Depends(get_current_user),
    db: Session = Depends(get_db),
) -> SeionPredictionOut:
    # Reviewer identity comes from the authenticated user, never the payload.
    reviewer_note = payload.reviewer_note if payload else None
    updated = review_seion_prediction(
        db,
        prediction_id,
        status="rejected",
        reviewer_note=reviewer_note,
        accepted_by=current_user.email or current_user.name or f"user:{current_user.id}",
    )
    if updated is None:
        raise HTTPException(status_code=404, detail="SEION advisory suggestion not found")
    return updated
