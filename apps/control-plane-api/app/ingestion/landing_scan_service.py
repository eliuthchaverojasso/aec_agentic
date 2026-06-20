"""Safe landing-folder scanning and manifest rebuild support."""

from __future__ import annotations

import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from app.config import settings
from app.ingestion.file_classifier import (
    DOCUMENT_FILE_TYPES,
    classify_landing_file,
    infer_discipline_from_path_or_name,
    infer_sheet_number,
    infer_spec_section,
)
from app.ingestion.manifest_loader import resolve_landing_path
from app.ingestion.pdf_document_loader import extract_pdf_metadata
from app.schemas import LandingScanDocument, LandingScanReport

STANDARD_FOLDERS = {"Drawings", "Owner Requirements", "Specifications", "Revit Exports", "Extracts"}
IGNORED_DIRS = {"processed", "rejected", "archive", "thumbnails", "ocr", "extracted", "__pycache__"}
IGNORED_PREFIXES = (".", "~$")


def scan_landing(
    *,
    project_folder: str | None = None,
    update_manifest: bool = False,
    include_pdf_metadata: bool = True,
    dry_run: bool = True,
    preserve_existing: bool = True,
) -> LandingScanReport:
    landing_root = settings.landing_dir.resolve()
    warnings: list[str] = []
    errors: list[str] = []

    try:
        scan_roots = _scan_roots(landing_root, project_folder)
    except (ValueError, FileNotFoundError) as exc:
        return LandingScanReport(
            status="failed",
            project_folder=project_folder,
            warnings=[],
            errors=[str(exc)],
        )

    documents: list[LandingScanDocument] = []
    for root in scan_roots:
        documents.extend(_scan_project_root(landing_root, root, include_pdf_metadata=include_pdf_metadata, warnings=warnings))

    manifest_updated = False
    manifest_path = None
    if update_manifest:
        if project_folder is None:
            errors.append("Manifest update requires a single project_folder.")
        elif not dry_run:
            manifest_path = _write_project_manifest(
                landing_root=landing_root,
                project_root=scan_roots[0],
                documents=documents,
                preserve_existing=preserve_existing,
                warnings=warnings,
            )
            manifest_updated = True
        else:
            manifest_path = str((scan_roots[0] / "landing_manifest.json").relative_to(landing_root))
            warnings.append("Dry run requested; landing_manifest.json was not updated.")

    status = "success" if not errors else "failed"
    if warnings and status == "success":
        status = "success_with_warnings"
    return LandingScanReport(
        status=status,
        project_folder=project_folder,
        files_found=len(documents),
        manifest_updated=manifest_updated,
        manifest_path=manifest_path,
        documents=documents,
        warnings=warnings,
        errors=errors,
    )


def rebuild_project_manifest(
    *,
    project_folder: str,
    preserve_existing: bool = True,
    include_pdf_metadata: bool = True,
    dry_run: bool = False,
) -> LandingScanReport:
    return scan_landing(
        project_folder=project_folder,
        update_manifest=True,
        include_pdf_metadata=include_pdf_metadata,
        dry_run=dry_run,
        preserve_existing=preserve_existing,
    )


def _scan_roots(landing_root: Path, project_folder: str | None) -> list[Path]:
    if project_folder:
        if Path(project_folder).is_absolute():
            raise ValueError("project_folder must be relative to the landing root")
        root = resolve_landing_path(landing_root, project_folder)
        if not root.exists() or not root.is_dir():
            raise FileNotFoundError(f"Landing project folder not found: {project_folder}")
        return [root]
    if not landing_root.exists():
        raise FileNotFoundError(f"Landing root not found: {landing_root}")
    return [
        child
        for child in sorted(landing_root.iterdir())
        if child.is_dir() and not _ignored(child) and any((child / folder).exists() for folder in STANDARD_FOLDERS)
    ]


def _scan_project_root(
    landing_root: Path,
    project_root: Path,
    *,
    include_pdf_metadata: bool,
    warnings: list[str],
) -> list[LandingScanDocument]:
    rows: list[LandingScanDocument] = []
    for path in sorted(project_root.rglob("*")):
        if not path.is_file() or _ignored(path) or any(part.lower() in IGNORED_DIRS for part in path.relative_to(project_root).parts[:-1]):
            continue
        if path.name.lower().endswith(".meta.json"):
            continue
        classification = classify_landing_file(path)
        if classification.type == "unknown":
            continue
        relative_path = str(path.relative_to(landing_root)).replace("\\", "/")
        file_size = path.stat().st_size
        checksum = _sha256_file(path)
        page_count = None
        metadata: dict[str, Any] = {
            "folder": path.parent.name,
            "classification_confidence": classification.confidence,
            "classification_reason": classification.reason,
        }
        if include_pdf_metadata and classification.type in DOCUMENT_FILE_TYPES:
            pdf_metadata = extract_pdf_metadata(path)
            page_count = pdf_metadata.page_count
            if pdf_metadata.title:
                metadata["pdf_title"] = pdf_metadata.title
            if pdf_metadata.warnings:
                metadata["pdf_warnings"] = pdf_metadata.warnings
                warnings.extend(f"{relative_path}: {warning}" for warning in pdf_metadata.warnings)

        spec_section = infer_spec_section(path.name) if classification.type == "specification_pdf" else None
        sheet_number = infer_sheet_number(path.name) if classification.type == "drawing_pdf" else None
        rows.append(
            LandingScanDocument(
                path=relative_path,
                type=classification.type,
                document_category=_category_for_type(classification.type),
                discipline=infer_discipline_from_path_or_name(path),
                sheet_number=sheet_number,
                sheet_title=_title_from_name(path, sheet_number) if classification.type == "drawing_pdf" else None,
                spec_section=spec_section,
                spec_title=_title_from_name(path, spec_section) if classification.type == "specification_pdf" else None,
                checksum_sha256=checksum,
                file_size_bytes=file_size,
                page_count=page_count,
                indexed_at=datetime.now(timezone.utc),
                metadata=metadata,
            )
        )
    return rows


def _write_project_manifest(
    *,
    landing_root: Path,
    project_root: Path,
    documents: list[LandingScanDocument],
    preserve_existing: bool,
    warnings: list[str],
) -> str:
    manifest_path = project_root / "landing_manifest.json"
    payload: dict[str, Any] = {"project_binding": {"project_title": project_root.name}, "batches": [], "files": []}
    existing_by_path: dict[str, dict[str, Any]] = {}
    if preserve_existing and manifest_path.exists():
        try:
            payload = json.loads(manifest_path.read_text(encoding="utf-8"))
            if not isinstance(payload, dict):
                raise ValueError("manifest root is not an object")
            for entry in payload.get("files", []) or []:
                if isinstance(entry, dict) and entry.get("path"):
                    existing_by_path[str(entry["path"])] = entry
        except Exception as exc:  # noqa: BLE001
            warnings.append(f"Existing manifest could not be preserved and will be replaced: {exc}")
            payload = {"project_binding": {"project_title": project_root.name}, "batches": [], "files": []}

    files = dict(existing_by_path)
    for document in documents:
        row = document.model_dump(mode="json", exclude_none=True)
        row["source_system"] = "landing"
        row["required"] = document.type in {"revit_export", "owner_requirements"}
        files[document.path] = {**files.get(document.path, {}), **row}
    payload["files"] = sorted(files.values(), key=lambda item: str(item.get("path", "")))
    manifest_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    return str(manifest_path.relative_to(landing_root)).replace("\\", "/")


def _category_for_type(file_type: str) -> str | None:
    return {
        "drawing_pdf": "drawing",
        "specification_pdf": "specification",
        "owner_requirements": "owner_requirements",
        "revit_export": "revit_export",
        "project_extract": "project_extract",
        "pdf_document": "document",
    }.get(file_type)


def _ignored(path: Path) -> bool:
    return path.name.startswith(IGNORED_PREFIXES) or path.name.lower() in IGNORED_DIRS


def _sha256_file(path: Path, chunk: int = 1024 * 1024) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            data = handle.read(chunk)
            if not data:
                break
            digest.update(data)
    return digest.hexdigest()


def _title_from_name(path: Path, leading_token: str | None) -> str | None:
    title = path.stem
    if leading_token:
        title = title.replace(leading_token, "", 1)
    title = " ".join(title.replace("_", " ").replace("-", " ").split())
    return title or None
