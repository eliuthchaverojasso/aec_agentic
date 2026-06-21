"""Document metadata endpoints for landing-indexed PDFs and source files.

Access control (Pending Work Register Item 9): every project-scoped endpoint
enforces project access via ``require_project_access`` (404 when the project does
not exist, 403 when the caller is not a member). The two global ``/documents/{id}``
endpoints derive the owning project and apply the same check, returning 404 on
no-access so document ids cannot be enumerated across tenants.
"""

from __future__ import annotations

from pathlib import Path

from fastapi import APIRouter, Depends, HTTPException, Query
from fastapi.responses import FileResponse
from sqlalchemy.orm import Session

from app.api.auth import get_current_user
from app.authz import is_superuser, require_project_access, user_can_access_project
from app.config import settings
from app.database import get_db
from app.ingestion.document_service import (
    get_document,
    get_document_text_preview,
    list_project_documents,
)
from app.ingestion.manifest_loader import resolve_landing_path
from app.models import AppUser, Project
from app.schemas import DocumentPreviewOut, DocumentTextPreviewOut, LandingDocumentOut

router = APIRouter(tags=["documents"])


@router.get(
    "/api/v1/projects/{project_id}/documents",
    response_model=list[LandingDocumentOut],
    summary="List indexed landing documents for a project",
)
def list_documents(
    project_id: int,
    category: str | None = None,
    discipline: str | None = None,
    file_type: str | None = None,
    document_type: str | None = None,
    evidence_status: str | None = None,
    source_path: str | None = None,
    search: str | None = None,
    limit: int = Query(100, ge=1, le=500),
    offset: int = Query(0, ge=0),
    db: Session = Depends(get_db),
    _project: Project = Depends(require_project_access),
) -> list[LandingDocumentOut]:
    return list_project_documents(
        db,
        project_id,
        category=category,
        discipline=discipline,
        file_type=file_type or document_type,
        evidence_status=evidence_status,
        source_path=source_path,
        search=search,
        limit=limit,
        offset=offset,
    )


@router.get(
    "/api/v1/projects/{project_id}/drawings",
    response_model=list[LandingDocumentOut],
    summary="List indexed drawing PDFs for a project",
)
def list_drawings(
    project_id: int,
    db: Session = Depends(get_db),
    _project: Project = Depends(require_project_access),
) -> list[LandingDocumentOut]:
    return list_project_documents(db, project_id, category="drawing", limit=500)


@router.get(
    "/api/v1/projects/{project_id}/specifications",
    response_model=list[LandingDocumentOut],
    summary="List indexed specification PDFs for a project",
)
def list_specifications(
    project_id: int,
    db: Session = Depends(get_db),
    _project: Project = Depends(require_project_access),
) -> list[LandingDocumentOut]:
    return list_project_documents(db, project_id, category="specification", limit=500)


@router.get(
    "/api/v1/documents/{document_id}",
    response_model=LandingDocumentOut,
    summary="Get document metadata",
)
def get_document_metadata(
    document_id: int,
    db: Session = Depends(get_db),
    current_user: AppUser = Depends(get_current_user),
) -> LandingDocumentOut:
    return _require_document_access(db, current_user, document_id)


@router.get(
    "/api/v1/documents/{document_id}/text-preview",
    response_model=DocumentTextPreviewOut,
    summary="Get capped local text preview for an indexed document",
)
def get_text_preview(
    document_id: int,
    db: Session = Depends(get_db),
    current_user: AppUser = Depends(get_current_user),
) -> DocumentTextPreviewOut:
    _require_document_access(db, current_user, document_id)
    return get_document_text_preview(db, document_id)


@router.get(
    "/api/v1/projects/{project_id}/documents/{document_id}",
    response_model=LandingDocumentOut,
    summary="Get project-scoped document metadata",
)
def get_project_document(
    project_id: int,
    document_id: int,
    db: Session = Depends(get_db),
    _project: Project = Depends(require_project_access),
) -> LandingDocumentOut:
    return _require_project_document(db, project_id, document_id)


@router.get(
    "/api/v1/projects/{project_id}/documents/{document_id}/metadata",
    response_model=LandingDocumentOut,
    summary="Get project-scoped document metadata payload",
)
def get_project_document_metadata(
    project_id: int,
    document_id: int,
    db: Session = Depends(get_db),
    _project: Project = Depends(require_project_access),
) -> LandingDocumentOut:
    return _require_project_document(db, project_id, document_id)


@router.get(
    "/api/v1/projects/{project_id}/documents/{document_id}/preview",
    response_model=DocumentPreviewOut,
    summary="Get safe project-scoped preview metadata",
)
def get_project_document_preview(
    project_id: int,
    document_id: int,
    db: Session = Depends(get_db),
    _project: Project = Depends(require_project_access),
) -> DocumentPreviewOut:
    document = _require_project_document(db, project_id, document_id)
    parser_status = str((document.metadata_json or {}).get("parser_status") or "indexed")
    return DocumentPreviewOut(
        document_id=document_id,
        available=True,
        category=document.document_category,
        parser_status=parser_status,
        metadata=document.metadata_json or {},
        message="Preview metadata available.",
    )


@router.get(
    "/api/v1/projects/{project_id}/documents/{document_id}/text",
    response_model=DocumentTextPreviewOut,
    summary="Get project-scoped extracted text preview",
)
def get_project_document_text(
    project_id: int,
    document_id: int,
    db: Session = Depends(get_db),
    _project: Project = Depends(require_project_access),
) -> DocumentTextPreviewOut:
    _require_project_document(db, project_id, document_id)
    return get_document_text_preview(db, document_id)


@router.get(
    "/api/v1/projects/{project_id}/documents/{document_id}/pdf",
    summary="Serve PDF inline when document type is PDF",
)
def get_project_document_pdf(
    project_id: int,
    document_id: int,
    db: Session = Depends(get_db),
    _project: Project = Depends(require_project_access),
) -> FileResponse:
    document = _require_project_document(db, project_id, document_id)
    if document.file_ext.lower() != ".pdf":
        raise HTTPException(status_code=400, detail="Document is not a PDF")
    file_path = _resolve_document_path(document)
    if not file_path.exists():
        raise HTTPException(status_code=404, detail="Indexed file is missing from landing storage")
    return FileResponse(path=file_path, media_type="application/pdf")


@router.get(
    "/api/v1/projects/{project_id}/documents/{document_id}/download",
    summary="Download indexed document when enabled",
)
def download_project_document(
    project_id: int,
    document_id: int,
    db: Session = Depends(get_db),
    _project: Project = Depends(require_project_access),
) -> FileResponse:
    document = _require_project_document(db, project_id, document_id)
    if not settings.enable_document_download:
        raise HTTPException(status_code=403, detail="Raw download disabled in this environment")
    file_path = _resolve_document_path(document)
    if not file_path.exists():
        raise HTTPException(status_code=404, detail="Indexed file is missing from landing storage")
    return FileResponse(path=file_path, media_type="application/octet-stream", filename=document.file_name)


def _require_document_access(
    db: Session, current_user: AppUser, document_id: int
) -> LandingDocumentOut:
    """Load a document by id and enforce access via its owning project.

    Returns 404 both when the document is missing and when the caller may not
    access it, so document ids cannot be enumerated across tenants. Documents
    with no owning project are visible only to superusers.
    """
    document = get_document(db, document_id)
    if document is None:
        raise HTTPException(status_code=404, detail="Document not found")
    if is_superuser(current_user):
        return document
    project = db.get(Project, document.project_id) if document.project_id is not None else None
    if project is None or not user_can_access_project(db, current_user, project):
        raise HTTPException(status_code=404, detail="Document not found")
    return document


def _require_project_document(db: Session, project_id: int, document_id: int) -> LandingDocumentOut:
    document = get_document(db, document_id)
    if document is None:
        raise HTTPException(status_code=404, detail="Document not found")
    if document.project_id != project_id:
        raise HTTPException(status_code=404, detail="Document not found for project")
    return document


def _resolve_document_path(document: LandingDocumentOut) -> Path:
    try:
        return resolve_landing_path(settings.landing_dir, document.relative_path)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
