"""Local-only PDF metadata helpers for landing documents.

This module never calls external OCR, vision, or AI services. PDF parsing is
best-effort and optional so a missing/corrupt parser cannot block manifest
ingestion of otherwise valid landing data.
"""

from __future__ import annotations

import importlib.util
from dataclasses import dataclass, field
from pathlib import Path
import re
from typing import Any

from app.ingestion.file_classifier import (
    classify_landing_file,
    infer_discipline_from_path_or_name,
    infer_sheet_number,
    infer_spec_section,
)

MAX_TEXT_PREVIEW_CHARS = 4000


@dataclass
class PdfMetadata:
    page_count: int | None = None
    title: str | None = None
    text_preview: str | None = None
    extraction_method: str | None = None
    warnings: list[str] = field(default_factory=list)


def extract_pdf_metadata(path: Path, *, include_text_preview: bool = False) -> PdfMetadata:
    if path.suffix.lower() != ".pdf":
        return PdfMetadata(warnings=["PDF metadata extraction skipped because file is not a PDF."])

    reader_result = _read_with_pypdf(path, include_text_preview=include_text_preview)
    if reader_result is not None:
        return reader_result

    return PdfMetadata(warnings=["PDF parsing dependency unavailable; registered file metadata only."])


def get_page_count(path: Path) -> tuple[int | None, list[str]]:
    metadata = extract_pdf_metadata(path, include_text_preview=False)
    return metadata.page_count, metadata.warnings


def infer_pdf_kind(path: Path) -> str:
    return classify_landing_file(path).type


def maybe_extract_text_preview(
    path: Path,
    *,
    max_pages: int = 2,
    max_chars: int = MAX_TEXT_PREVIEW_CHARS,
) -> dict[str, Any]:
    metadata = _read_with_pypdf(
        path,
        include_text_preview=True,
        max_pages=max_pages,
        max_chars=max_chars,
    )
    if metadata is None:
        return {
            "available": False,
            "text_preview": None,
            "extraction_method": None,
            "warnings": ["PDF text extraction dependency unavailable."],
        }
    return {
        "available": bool(metadata.text_preview),
        "text_preview": metadata.text_preview,
        "extraction_method": metadata.extraction_method,
        "warnings": metadata.warnings,
    }


def maybe_render_thumbnail(path: Path, *, page: int = 1) -> dict[str, Any]:
    if importlib.util.find_spec("fitz") is None:
        return {
            "available": False,
            "path": None,
            "warnings": ["PyMuPDF/fitz unavailable; thumbnail rendering skipped."],
        }
    return {
        "available": False,
        "path": None,
        "warnings": ["Thumbnail rendering is intentionally deferred pending a reviewed ignored output path."],
    }


def build_pdf_document_record(path: Path, relative_path: str, *, include_text_preview: bool = False) -> dict[str, Any]:
    classification = classify_landing_file(path)
    pdf_metadata = extract_pdf_metadata(path, include_text_preview=include_text_preview)
    file_type = classification.type
    document_category = {
        "drawing_pdf": "drawing",
        "specification_pdf": "specification",
        "pdf_document": "document",
    }.get(file_type)
    sheet_number = infer_sheet_number(path.name) if file_type == "drawing_pdf" else None
    spec_section = infer_spec_section(path.name) if file_type == "specification_pdf" else None
    return {
        "relative_path": relative_path,
        "file_name": path.name,
        "file_ext": path.suffix.lower(),
        "file_type": file_type,
        "document_category": document_category,
        "discipline": infer_discipline_from_path_or_name(path),
        "sheet_number": sheet_number,
        "sheet_title": _title_from_filename(path, sheet_number) if file_type == "drawing_pdf" else None,
        "spec_section": spec_section,
        "spec_title": _title_from_filename(path, spec_section) if file_type == "specification_pdf" else None,
        "page_count": pdf_metadata.page_count,
        "metadata": {
            "pdf_title": pdf_metadata.title,
            "classification_confidence": classification.confidence,
            "classification_reason": classification.reason,
            "pdf_warnings": pdf_metadata.warnings,
        },
        "text_preview": pdf_metadata.text_preview,
        "text_extraction_method": pdf_metadata.extraction_method,
        "warnings": pdf_metadata.warnings,
    }


def _read_with_pypdf(
    path: Path,
    *,
    include_text_preview: bool,
    max_pages: int = 2,
    max_chars: int = MAX_TEXT_PREVIEW_CHARS,
) -> PdfMetadata | None:
    module_name = "pypdf" if importlib.util.find_spec("pypdf") else "PyPDF2" if importlib.util.find_spec("PyPDF2") else None
    if module_name is None:
        return None
    try:
        module = __import__(module_name)
        reader = module.PdfReader(str(path))
        page_count = len(reader.pages)
        raw_title = None
        if getattr(reader, "metadata", None):
            raw_title = reader.metadata.get("/Title") or reader.metadata.get("title")
        text_preview = None
        if include_text_preview:
            chunks: list[str] = []
            for page in reader.pages[:max_pages]:
                try:
                    text = page.extract_text() or ""
                except Exception as exc:  # noqa: BLE001
                    chunks.append(f"[page text unavailable: {exc}]")
                    continue
                if text:
                    chunks.append(text.strip())
                if sum(len(chunk) for chunk in chunks) >= max_chars:
                    break
            text_preview = "\n\n".join(chunks).strip()[:max_chars] or None
        return PdfMetadata(
            page_count=page_count,
            title=str(raw_title).strip() if raw_title else None,
            text_preview=text_preview,
            extraction_method=module_name if text_preview else None,
        )
    except Exception as exc:  # noqa: BLE001
        return PdfMetadata(warnings=[f"PDF parser could not read file metadata: {exc}"])


def _title_from_filename(path: Path, leading_token: str | None) -> str | None:
    title = path.stem
    if leading_token:
        normalized = r"[\s._-]*".join(re.escape(part) for part in re.split(r"[\s._-]+", leading_token) if part)
        title = re.sub(rf"^\s*{normalized}\s*[-_\s]*", "", title, flags=re.IGNORECASE)
    title = title.replace("_", " ").replace("-", " ")
    title = " ".join(title.split())
    return title or None
