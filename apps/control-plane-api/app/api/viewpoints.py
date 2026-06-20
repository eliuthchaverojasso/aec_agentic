from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from app.database import get_db
from app.ingestion.document_service import get_document, list_project_documents
from app.models import Project
from app.schemas import LandingDocumentOut

router = APIRouter(tags=["viewpoints"])


@router.get(
    "/api/v1/projects/{project_id}/viewpoints",
    response_model=list[LandingDocumentOut],
    summary="List indexed viewpoint metadata documents",
)
def list_project_viewpoints(project_id: int, db: Session = Depends(get_db)) -> list[LandingDocumentOut]:
    _require_project(db, project_id)
    return list_project_documents(db, project_id, file_type="viewpoint_json", limit=500)


@router.get(
    "/api/v1/projects/{project_id}/viewpoints/{viewpoint_id}",
    response_model=LandingDocumentOut,
    summary="Get viewpoint metadata document",
)
def get_project_viewpoint(project_id: int, viewpoint_id: int, db: Session = Depends(get_db)) -> LandingDocumentOut:
    _require_project(db, project_id)
    row = get_document(db, viewpoint_id)
    if row is None or row.project_id != project_id or row.file_type != "viewpoint_json":
        raise HTTPException(status_code=404, detail="Viewpoint not found")
    return row


def _require_project(db: Session, project_id: int) -> None:
    if db.get(Project, project_id) is None:
        raise HTTPException(status_code=404, detail="Project not found")

