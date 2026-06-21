"""Persistence helpers for landing document metadata."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from sqlalchemy import or_
from sqlalchemy.orm import Session

from app.models import DocumentTextSnippet, DrawingSheet, LandingDocument, Project
from app.schemas import DocumentTextPreviewOut, LandingDocumentOut


def register_landing_document(
    db: Session,
    *,
    values: dict[str, Any],
    dry_run: bool = False,
) -> dict[str, Any]:
    if dry_run:
        return {"document_id": None, "created": False, "updated": False}

    checksum = values.get("checksum_sha256")
    relative_path = values["relative_path"]
    existing = (
        db.query(LandingDocument)
        .filter(
            LandingDocument.relative_path == relative_path,
            LandingDocument.checksum_sha256 == checksum,
        )
        .one_or_none()
    )
    now = datetime.now(timezone.utc)
    if existing is None:
        document = LandingDocument(**_document_kwargs(values), indexed_at=now)
        db.add(document)
        db.flush()
        created = True
    else:
        document = existing
        for key, value in _document_kwargs(values).items():
            setattr(document, key, value)
        document.indexed_at = now
        created = False

    if document.file_type == "drawing_pdf" and document.sheet_number:
        _upsert_drawing_sheet(db, document, values)
    text_preview = values.get("text_preview")
    if text_preview:
        _upsert_text_preview(db, document, text_preview, values.get("text_extraction_method") or "local_pdf")

    db.commit()
    db.refresh(document)
    return {"document_id": document.id, "created": created, "updated": not created}


def list_project_documents(
    db: Session,
    project_id: int,
    *,
    category: str | None = None,
    discipline: str | None = None,
    file_type: str | None = None,
    evidence_status: str | None = None,
    source_path: str | None = None,
    search: str | None = None,
    limit: int = 100,
    offset: int = 0,
) -> list[LandingDocumentOut]:
    query = db.query(LandingDocument).filter(LandingDocument.project_id == project_id)
    if category:
        query = query.filter(LandingDocument.document_category == category)
    if discipline:
        query = query.filter(LandingDocument.discipline == discipline)
    if file_type:
        query = query.filter(LandingDocument.file_type == file_type)
    if evidence_status:
        query = query.filter(LandingDocument.evidence_status == evidence_status)
    if source_path:
        query = query.filter(LandingDocument.relative_path.ilike(f"%{source_path}%"))
    if search:
        token = f"%{search}%"
        query = query.filter(
            or_(
                LandingDocument.file_name.ilike(token),
                LandingDocument.sheet_number.ilike(token),
                LandingDocument.sheet_title.ilike(token),
                LandingDocument.spec_section.ilike(token),
                LandingDocument.spec_title.ilike(token),
            )
        )
    rows = (
        query.order_by(
            LandingDocument.document_category,
            LandingDocument.discipline,
            LandingDocument.sheet_number,
            LandingDocument.spec_section,
            LandingDocument.file_name,
        )
        .offset(offset)
        .limit(min(limit, 500))
        .all()
    )
    return [LandingDocumentOut.model_validate(row) for row in rows]


def get_document(db: Session, document_id: int) -> LandingDocumentOut | None:
    row = db.get(LandingDocument, document_id)
    return LandingDocumentOut.model_validate(row) if row else None


def get_document_text_preview(db: Session, document_id: int) -> DocumentTextPreviewOut:
    row = (
        db.query(DocumentTextSnippet)
        .filter(DocumentTextSnippet.document_id == document_id)
        .order_by(DocumentTextSnippet.page_number.nulls_first(), DocumentTextSnippet.id)
        .first()
    )
    if row is None:
        return DocumentTextPreviewOut(
            document_id=document_id,
            available=False,
            message="No local text preview is stored for this document.",
        )
    return DocumentTextPreviewOut(
        document_id=document_id,
        page_number=row.page_number,
        text_preview=row.text_preview,
        extraction_method=row.extraction_method,
        available=True,
    )


def find_project_for_document(db: Session, *, project_title: str | None, project_folder: str | None) -> Project | None:
    candidates = [project_title, project_folder]
    for candidate in candidates:
        if not candidate:
            continue
        exact = db.query(Project).filter(Project.project_title == candidate).first()
        if exact:
            return exact
        token = f"%{candidate}%"
        fuzzy = (
            db.query(Project)
            .filter(or_(Project.project_title.ilike(token), Project.project_name.ilike(token)))
            .order_by(Project.id.desc())
            .first()
        )
        if fuzzy:
            return fuzzy
    return None


def _document_kwargs(values: dict[str, Any]) -> dict[str, Any]:
    allowed = {
        "project_id",
        "client_id",
        "project_folder",
        "relative_path",
        "file_name",
        "file_ext",
        "file_type",
        "document_category",
        "discipline",
        "sheet_number",
        "sheet_title",
        "spec_section",
        "spec_title",
        "page_count",
        "file_size_bytes",
        "checksum_sha256",
        "manifest_path",
        "source_system",
        "ingestion_status",
        "processed_at",
        "evidence_status",
        "metadata_json",
    }
    return {key: value for key, value in values.items() if key in allowed}


def _upsert_drawing_sheet(db: Session, document: LandingDocument, values: dict[str, Any]) -> None:
    existing = (
        db.query(DrawingSheet)
        .filter(DrawingSheet.document_id == document.id, DrawingSheet.sheet_number == document.sheet_number)
        .one_or_none()
    )
    payload = {
        "document_id": document.id,
        "project_id": document.project_id,
        "sheet_number": document.sheet_number,
        "sheet_title": document.sheet_title,
        "discipline": document.discipline,
        "page_number": 1,
        "metadata_json": values.get("metadata_json"),
    }
    if existing is None:
        db.add(DrawingSheet(**payload))
    else:
        for key, value in payload.items():
            setattr(existing, key, value)


def _upsert_text_preview(db: Session, document: LandingDocument, text_preview: str, method: str) -> None:
    capped = text_preview[:4000]
    existing = (
        db.query(DocumentTextSnippet)
        .filter(DocumentTextSnippet.document_id == document.id, DocumentTextSnippet.page_number.is_(None))
        .one_or_none()
    )
    if existing is None:
        db.add(
            DocumentTextSnippet(
                document_id=document.id,
                page_number=None,
                text_preview=capped,
                extraction_method=method,
            )
        )
    else:
        existing.text_preview = capped
        existing.extraction_method = method
        existing.created_at = datetime.now(timezone.utc)
