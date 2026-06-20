"""Landing manifest loading and safe path resolution."""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Literal


LandingFileType = Literal[
    "revit_export",
    "owner_requirements",
    "drawing_pdf",
    "specification_pdf",
    "pdf_document",
    "dwfx_export",
    "viewpoint_json",
    "timeline_excel",
    "project_extract",
    "binding",
    "unknown",
]


@dataclass(frozen=True)
class LandingFileEntry:
    path: str
    type: LandingFileType = "unknown"
    client_code: str | None = None
    project_title: str | None = None
    discipline: str | None = None
    document_category: str | None = None
    sheet_number: str | None = None
    sheet_title: str | None = None
    spec_section: str | None = None
    spec_title: str | None = None
    source_system: str | None = None
    checksum_sha256: str | None = None
    file_size_bytes: int | None = None
    page_count: int | None = None
    sidecar_path: str | None = None
    batch_id: str | None = None
    export_mode: str | None = None
    required: bool = False
    metadata: dict[str, Any] = field(default_factory=dict)


@dataclass(frozen=True)
class LandingManifest:
    manifest_path: Path
    project_binding: dict[str, Any]
    files: list[LandingFileEntry]


def resolve_landing_path(landing_root: Path, relative_path: str) -> Path:
    root = landing_root.resolve()
    resolved = (root / relative_path).resolve()
    try:
        resolved.relative_to(root)
    except ValueError as exc:
        raise ValueError(f"Path must be inside landing directory: {relative_path}") from exc
    return resolved


def load_landing_manifest(landing_root: Path, manifest_path: str) -> LandingManifest:
    path = resolve_landing_path(landing_root, manifest_path)
    if not path.exists():
        raise FileNotFoundError(f"Landing manifest not found: {manifest_path}")
    if path.suffix.lower() != ".json":
        raise ValueError("Landing manifest must be a .json file")

    with path.open("r", encoding="utf-8") as handle:
        payload = json.load(handle)

    if not isinstance(payload, dict):
        raise ValueError("Landing manifest must contain a JSON object")

    raw_files = payload.get("files", [])
    if not isinstance(raw_files, list):
        raise ValueError("Landing manifest field 'files' must be a list")

    entries: list[LandingFileEntry] = []
    entries.extend(_entries_from_files(raw_files, defaults={}))

    raw_batches = payload.get("batches", [])
    if raw_batches is None:
        raw_batches = []
    if not isinstance(raw_batches, list):
        raise ValueError("Landing manifest field 'batches' must be a list when provided")

    for raw_batch in raw_batches:
        if not isinstance(raw_batch, dict):
            raise ValueError("Each manifest batch must be an object")
        batch_files = raw_batch.get("files", [])
        if not isinstance(batch_files, list):
            raise ValueError("Each manifest batch needs a files list")
        defaults = {
            "batch_id": _clean(raw_batch.get("batch_id")),
            "project_title": _clean(raw_batch.get("project_title")),
            "client_code": _clean(raw_batch.get("client_code")),
            "export_mode": _clean(raw_batch.get("export_mode")),
        }
        entries.extend(_entries_from_files(batch_files, defaults=defaults))

    project_binding = payload.get("project_binding") or {}
    if not isinstance(project_binding, dict):
        raise ValueError("Landing manifest field 'project_binding' must be an object")

    return LandingManifest(
        manifest_path=path,
        project_binding=project_binding,
        files=entries,
    )


def _clean(value: Any) -> str | None:
    if value is None:
        return None
    text = str(value).strip()
    return text or None


def _clean_int(value: Any) -> int | None:
    if value is None or value == "":
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _entries_from_files(
    raw_files: list[Any],
    *,
    defaults: dict[str, Any],
) -> list[LandingFileEntry]:
    entries: list[LandingFileEntry] = []
    for raw in raw_files:
        if not isinstance(raw, dict):
            raise ValueError("Each manifest file entry must be an object")
        file_path = str(raw.get("path") or "").strip()
        if not file_path:
            raise ValueError("Each manifest file entry needs a path")

        file_type = str(raw.get("type") or "unknown").strip().lower()
        if file_type not in {
            "revit_export",
            "owner_requirements",
            "drawing_pdf",
            "specification_pdf",
            "pdf_document",
            "dwfx_export",
            "viewpoint_json",
            "timeline_excel",
            "project_extract",
            "binding",
            "unknown",
        }:
            file_type = "unknown"

        known_keys = {
            "path",
            "type",
            "client_code",
            "project_title",
            "discipline",
            "document_category",
            "sheet_number",
            "sheet_title",
            "spec_section",
            "spec_title",
            "source_system",
            "checksum_sha256",
            "file_size_bytes",
            "page_count",
            "sidecar_path",
            "batch_id",
            "export_mode",
            "required",
        }
        metadata = {key: value for key, value in raw.items() if key not in known_keys}
        entries.append(
            LandingFileEntry(
                path=file_path,
                type=file_type,  # type: ignore[arg-type]
                client_code=_clean(raw.get("client_code")) or defaults.get("client_code"),
                project_title=_clean(raw.get("project_title")) or defaults.get("project_title"),
                discipline=_clean(raw.get("discipline")),
                document_category=_clean(raw.get("document_category")),
                sheet_number=_clean(raw.get("sheet_number")),
                sheet_title=_clean(raw.get("sheet_title")),
                spec_section=_clean(raw.get("spec_section")),
                spec_title=_clean(raw.get("spec_title")),
                source_system=_clean(raw.get("source_system")),
                checksum_sha256=_clean(raw.get("checksum_sha256")),
                file_size_bytes=_clean_int(raw.get("file_size_bytes")),
                page_count=_clean_int(raw.get("page_count")),
                sidecar_path=_clean(raw.get("sidecar_path")),
                batch_id=_clean(raw.get("batch_id")) or defaults.get("batch_id"),
                export_mode=_clean(raw.get("export_mode")) or defaults.get("export_mode"),
                required=bool(raw.get("required", False)),
                metadata=metadata,
            )
        )
    return entries
