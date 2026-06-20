"""Controlled landing document delete and dedupe endpoints."""

from __future__ import annotations

import hashlib
import re
from pathlib import Path
from typing import Any

from fastapi import APIRouter, Body, Depends, HTTPException, Query
from sqlalchemy import delete, select
from sqlalchemy.orm import Session

from app.config import settings
from app.database import get_db
from app.ingestion.landing_scan_service import rebuild_project_manifest
from app.ingestion.manifest_loader import resolve_landing_path
from app.models import DocumentTextSnippet, DrawingSheet, LandingDocument, Project

router = APIRouter(tags=["landing-admin"])


@router.delete(
    "/api/v1/projects/{project_id}/documents/{document_id}",
    response_model=dict,
    summary="Delete an indexed project document and optionally its physical landing file",
)
def delete_project_document(
    project_id: int,
    document_id: int,
    delete_file: bool = Query(False),
    rebuild_manifest: bool = Query(True),
    db: Session = Depends(get_db),
) -> dict[str, Any]:
    project = _require_project(db, project_id)
    document = db.get(LandingDocument, document_id)

    if document is None or document.project_id != project_id:
        raise HTTPException(status_code=404, detail="Document not found for project")

    relative_path = document.relative_path
    project_folder = document.project_folder or project.project_name or project.project_title
    deleted_file = False

    if delete_file:
        file_path = _safe_resolve_landing_file(relative_path)
        if file_path.exists():
            file_path.unlink()
            deleted_file = True

    _delete_landing_document_rows(db, [document.id])
    db.delete(document)
    db.commit()

    manifest = None
    if rebuild_manifest and project_folder:
        manifest = _rebuild_manifest(project_folder)

    return {
        "ok": True,
        "operation": "delete-project-document",
        "project_id": project_id,
        "document_id": document_id,
        "relative_path": relative_path,
        "deleted_file": deleted_file,
        "manifest_updated": bool(manifest),
        "manifest": manifest,
    }


@router.delete(
    "/api/v1/projects/{project_id}/landing/file",
    response_model=dict,
    summary="Delete a physical landing file by relative path and optionally remove indexed records",
)
def delete_project_landing_file(
    project_id: int,
    relative_path: str = Query(...),
    delete_index: bool = Query(True),
    rebuild_manifest: bool = Query(True),
    db: Session = Depends(get_db),
) -> dict[str, Any]:
    project = _require_project(db, project_id)
    project_folder = project.project_name or project.project_title

    normalized_path = _normalize_relative_path(relative_path)
    _require_path_under_project_folder(normalized_path, project_folder)

    file_path = _safe_resolve_landing_file(normalized_path)
    deleted_file = False

    if file_path.exists():
        file_path.unlink()
        deleted_file = True

    deleted_index_rows = 0

    if delete_index:
        docs = db.execute(
            select(LandingDocument).where(
                LandingDocument.project_id == project_id,
                LandingDocument.relative_path == normalized_path,
            )
        ).scalars().all()

        deleted_index_rows = len(docs)
        _delete_landing_document_rows(db, [doc.id for doc in docs])

        for doc in docs:
            db.delete(doc)

        db.commit()

    manifest = None
    if rebuild_manifest:
        manifest = _rebuild_manifest(project_folder)

    return {
        "ok": True,
        "operation": "delete-project-landing-file",
        "project_id": project_id,
        "relative_path": normalized_path,
        "deleted_file": deleted_file,
        "deleted_index_rows": deleted_index_rows,
        "manifest_updated": bool(manifest),
        "manifest": manifest,
    }


@router.post(
    "/api/v1/projects/{project_id}/landing/dedupe",
    response_model=dict,
    summary="Deduplicate indexed and physical landing files by checksum",
)
def dedupe_project_landing(
    project_id: int,
    payload: dict[str, Any] = Body(default_factory=dict),
    db: Session = Depends(get_db),
) -> dict[str, Any]:
    project = _require_project(db, project_id)

    category = str(payload.get("category") or "owner_requirements")
    dry_run = bool(payload.get("dry_run", False))
    delete_files = bool(payload.get("delete_files", True))
    rebuild_manifest = bool(payload.get("rebuild_manifest", True))
    prefer_clean_filename = bool(payload.get("prefer_clean_filename", True))

    project_folder = project.project_name or project.project_title

    kept: list[dict[str, Any]] = []
    deleted_documents: list[dict[str, Any]] = []
    deleted_files: list[dict[str, Any]] = []

    docs = db.execute(
        select(LandingDocument).where(
            LandingDocument.project_id == project_id,
            LandingDocument.document_category == category,
            LandingDocument.checksum_sha256.is_not(None),
        )
    ).scalars().all()

    by_hash: dict[str, list[LandingDocument]] = {}

    for doc in docs:
        if doc.checksum_sha256:
            by_hash.setdefault(doc.checksum_sha256, []).append(doc)

    for checksum, group in by_hash.items():
        if len(group) < 2:
            continue

        ordered = sorted(
            group,
            key=lambda d: (
                _filename_dirty_score(d.file_name, prefer_clean_filename),
                d.indexed_at,
                d.id,
            ),
        )

        keep_doc = ordered[0]
        kept.append(
            {
                "kind": "indexed_document",
                "id": keep_doc.id,
                "relative_path": keep_doc.relative_path,
                "checksum_sha256": checksum,
            }
        )

        for duplicate in ordered[1:]:
            deleted_documents.append(
                {
                    "id": duplicate.id,
                    "relative_path": duplicate.relative_path,
                    "checksum_sha256": checksum,
                    "dry_run": dry_run,
                }
            )

            if not dry_run:
                if delete_files:
                    file_path = _safe_resolve_landing_file(duplicate.relative_path)
                    if file_path.exists():
                        file_path.unlink()
                        deleted_files.append(
                            {
                                "relative_path": duplicate.relative_path,
                                "reason": "duplicate-indexed-document",
                            }
                        )

                _delete_landing_document_rows(db, [duplicate.id])
                db.delete(duplicate)

    if not dry_run:
        db.commit()

    category_folder = _category_to_folder_name(category)
    physical_root = settings.landing_dir.resolve() / project_folder / category_folder

    if physical_root.exists() and physical_root.is_dir():
        physical_groups: dict[str, list[Path]] = {}

        for file_path in physical_root.rglob("*"):
            if not file_path.is_file():
                continue
            checksum = _sha256_file(file_path)
            physical_groups.setdefault(checksum, []).append(file_path)

        for checksum, group in physical_groups.items():
            if len(group) < 2:
                continue

            ordered_paths = sorted(
                group,
                key=lambda p: (
                    _filename_dirty_score(p.name, prefer_clean_filename),
                    p.stat().st_mtime,
                    p.name,
                ),
            )

            keep_path = ordered_paths[0]
            kept.append(
                {
                    "kind": "physical_file",
                    "relative_path": _relative_to_landing(keep_path),
                    "checksum_sha256": checksum,
                }
            )

            for duplicate_path in ordered_paths[1:]:
                rel = _relative_to_landing(duplicate_path)
                deleted_files.append(
                    {
                        "relative_path": rel,
                        "checksum_sha256": checksum,
                        "dry_run": dry_run,
                        "reason": "duplicate-physical-file",
                    }
                )

                if not dry_run and delete_files and duplicate_path.exists():
                    duplicate_path.unlink()

                    docs_to_delete = db.execute(
                        select(LandingDocument).where(
                            LandingDocument.project_id == project_id,
                            LandingDocument.relative_path == rel,
                        )
                    ).scalars().all()

                    _delete_landing_document_rows(db, [doc.id for doc in docs_to_delete])

                    for doc in docs_to_delete:
                        db.delete(doc)

                    db.commit()

    manifest = None
    if rebuild_manifest and not dry_run:
        manifest = _rebuild_manifest(project_folder)

    return {
        "ok": True,
        "operation": "dedupe-project-landing",
        "project_id": project_id,
        "project_folder": project_folder,
        "category": category,
        "dry_run": dry_run,
        "delete_files": delete_files,
        "kept": kept,
        "deleted_documents": deleted_documents,
        "deleted_files": deleted_files,
        "counts": {
            "kept": len(kept),
            "deleted_documents": len(deleted_documents),
            "deleted_files": len(deleted_files),
        },
        "manifest_updated": bool(manifest),
        "manifest": manifest,
    }


def _require_project(db: Session, project_id: int) -> Project:
    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found")
    return project


def _normalize_relative_path(relative_path: str) -> str:
    normalized = relative_path.replace("\\", "/").strip().lstrip("/")
    if not normalized or normalized.startswith("../") or "/../" in normalized:
        raise HTTPException(status_code=400, detail="Invalid relative_path")
    return normalized


def _require_path_under_project_folder(relative_path: str, project_folder: str) -> None:
    folder = project_folder.replace("\\", "/").strip().strip("/")
    if not folder:
        raise HTTPException(status_code=400, detail="Project folder is not configured")
    if relative_path != folder and not relative_path.startswith(f"{folder}/"):
        raise HTTPException(status_code=400, detail="relative_path is outside project landing folder")


def _safe_resolve_landing_file(relative_path: str) -> Path:
    normalized = _normalize_relative_path(relative_path)
    try:
        return resolve_landing_path(settings.landing_dir, normalized)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


def _delete_landing_document_rows(db: Session, document_ids: list[int]) -> None:
    if not document_ids:
        return

    db.execute(delete(DocumentTextSnippet).where(DocumentTextSnippet.document_id.in_(document_ids)))
    db.execute(delete(DrawingSheet).where(DrawingSheet.document_id.in_(document_ids)))


def _rebuild_manifest(project_folder: str) -> dict[str, Any]:
    report = rebuild_project_manifest(
        project_folder=project_folder,
        preserve_existing=True,
        include_pdf_metadata=True,
        dry_run=False,
    )

    if hasattr(report, "model_dump"):
        return report.model_dump()

    return dict(report)


def _category_to_folder_name(category: str) -> str:
    mapping = {
        "owner_requirements": "Owner Requirements",
        "drawing": "Drawings",
        "drawings": "Drawings",
        "specification": "Specifications",
        "specifications": "Specifications",
        "revit_exports": "Revit Exports",
        "supporting": "Supporting",
    }
    return mapping.get(category, category)


def _filename_dirty_score(file_name: str, prefer_clean_filename: bool) -> int:
    if not prefer_clean_filename:
        return 0

    if re.search(r"_\d{8}T\d{6}_[A-Za-z0-9]+", file_name):
        return 10

    if re.search(r"\(\d+\)", file_name):
        return 5

    return 0


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()

    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)

    return digest.hexdigest()


def _relative_to_landing(path: Path) -> str:
    return path.resolve().relative_to(settings.landing_dir.resolve()).as_posix()
