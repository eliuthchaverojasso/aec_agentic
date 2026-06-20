"""File classification helpers for landing ingestion."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import re
from typing import Literal

from app.ingestion.parser import REQUIRED_ELEMENT_KEYS, stream_elements


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

KNOWN_FILE_TYPES = {
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
}

DOCUMENT_FILE_TYPES = {"drawing_pdf", "specification_pdf", "pdf_document", "dwfx_export", "viewpoint_json", "timeline_excel"}

DISCIPLINE_ALIASES = {
    "E": "ELECTRICAL",
    "EL": "ELECTRICAL",
    "ELEC": "ELECTRICAL",
    "ELECTRICAL": "ELECTRICAL",
    "M": "MECHANICAL",
    "MECH": "MECHANICAL",
    "MECHANICAL": "MECHANICAL",
    "MH": "MECHANICAL",
    "P": "PLUMBING",
    "PL": "PLUMBING",
    "PLUMBING": "PLUMBING",
    "T": "TECHNOLOGY",
    "ET": "TECHNOLOGY",
    "TECH": "TECHNOLOGY",
    "TECHNOLOGY": "TECHNOLOGY",
    "TELECOM": "TECHNOLOGY",
    "L": "LIGHTING",
    "LTG": "LIGHTING",
    "LIGHTING": "LIGHTING",
    "A": "ARCHITECTURAL",
    "ARCH": "ARCHITECTURAL",
    "ARCHITECTURAL": "ARCHITECTURAL",
    "FP": "FIRE_PROTECTION",
    "FA": "FIRE_ALARM",
    "FIRE": "FIRE_PROTECTION",
}

_SHEET_RE = re.compile(
    r"(?<![A-Z0-9])(?P<prefix>FP|FA|EL|EP|ET|MH|PL|[A-Z])[-\s]?\.?(?P<num>\d{1,4}(?:\.\d{1,2})?)(?![A-Z0-9])",
    re.IGNORECASE,
)
_SPEC_SPACED_RE = re.compile(r"(?<!\d)(?P<a>\d{2})[\s._-]+(?P<b>\d{2})[\s._-]+(?P<c>\d{2})(?!\d)")
_SPEC_COMPACT_RE = re.compile(r"(?<!\d)(?P<a>\d{2})(?P<b>\d{2})(?P<c>\d{2})(?!\d)")


@dataclass(frozen=True)
class FileClassification:
    type: LandingFileType
    confidence: str
    reason: str


def classify_landing_file(path: Path, declared_type: str | None = None) -> FileClassification:
    normalized = normalize_document_type(declared_type)
    if normalized != "unknown":
        compatible, reason = _declared_type_compatible(path, normalized)
        if not compatible:
            return FileClassification(
                type="unknown",
                confidence="manifest_conflict",
                reason=reason,
            )
        return FileClassification(
            type=normalized,  # type: ignore[arg-type]
            confidence="manifest",
            reason="Manifest declared file type",
        )

    suffix = path.suffix.lower()
    name = path.name.lower()

    if is_excel(path):
        if _path_contains(path, "timeline"):
            return FileClassification(
                type="timeline_excel",
                confidence="folder_filename",
                reason="Excel file under Timeline folder",
            )
        return FileClassification(
            type="owner_requirements",
            confidence="folder_filename" if _path_contains(path, "owner requirements") or "requirement" in name else "extension",
            reason="Excel file is treated as an owner requirement catalog",
        )

    if suffix == ".dwfx":
        return FileClassification(
            type="dwfx_export",
            confidence="extension",
            reason="DWFx export candidate",
        )

    if is_pdf(path):
        if _path_contains(path, "drawings") or infer_sheet_number(path.name):
            return FileClassification(
                type="drawing_pdf",
                confidence="folder_filename",
                reason="PDF is in Drawings or resembles a sheet number",
            )
        if _path_contains(path, "specifications") or infer_spec_section(path.name):
            return FileClassification(
                type="specification_pdf",
                confidence="folder_filename",
                reason="PDF is in Specifications or resembles a spec section",
            )
        return FileClassification(
            type="pdf_document",
            confidence="extension",
            reason="PDF document without a drawing/specification signal",
        )

    if suffix != ".json":
        return FileClassification(
            type="unknown",
            confidence="extension",
            reason=f"Unsupported extension {suffix or '<none>'}",
        )

    if _path_contains(path, "viewpoints") or "viewpoint" in name:
        return FileClassification(
            type="viewpoint_json",
            confidence="folder_filename",
            reason="JSON under Viewpoints folder or filename contains viewpoint",
        )

    if "binding" in name or "manifest" in name:
        return FileClassification(
            type="binding",
            confidence="filename",
            reason="Filename indicates binding/config metadata",
        )

    if _looks_like_revit_export(path):
        return FileClassification(
            type="revit_export",
            confidence="schema_probe",
            reason="First JSON array item contains Revit element keys",
        )

    return FileClassification(
        type="project_extract",
        confidence="json_default",
        reason="JSON does not match Revit export element schema",
    )


def _looks_like_revit_export(path: Path) -> bool:
    try:
        first = next(stream_elements(path), None)
    except Exception:
        return False
    return isinstance(first, dict) and REQUIRED_ELEMENT_KEYS.issubset(first)


def normalize_document_type(value: str | None) -> LandingFileType:
    normalized = (value or "unknown").strip().lower().replace("-", "_").replace(" ", "_")
    aliases = {
        "drawing": "drawing_pdf",
        "drawings": "drawing_pdf",
        "sheet": "drawing_pdf",
        "sheet_pdf": "drawing_pdf",
        "spec": "specification_pdf",
        "specification": "specification_pdf",
        "specifications": "specification_pdf",
        "requirements": "owner_requirements",
        "owner_requirement": "owner_requirements",
        "revit": "revit_export",
        "export": "revit_export",
        "dwfx": "dwfx_export",
        "3d_export": "dwfx_export",
        "viewpoint": "viewpoint_json",
        "timeline": "timeline_excel",
    }
    normalized = aliases.get(normalized, normalized)
    if normalized in KNOWN_FILE_TYPES:
        return normalized  # type: ignore[return-value]
    return "unknown"


def is_pdf(path: Path) -> bool:
    return path.suffix.lower() == ".pdf"


def is_excel(path: Path) -> bool:
    return path.suffix.lower() in {".xlsx", ".xlsm"}


def is_revit_export_json(path: Path) -> bool:
    return path.suffix.lower() == ".json" and _looks_like_revit_export(path)


def infer_sheet_number(filename: str) -> str | None:
    stem = Path(filename).stem.upper()
    match = _SHEET_RE.search(stem)
    if not match:
        return None
    prefix = match.group("prefix").upper().replace(" ", "")
    number = match.group("num").upper()
    if len(prefix) == 1 or prefix in {"FP", "FA"}:
        return f"{prefix}-{number}" if "-" not in f"{prefix}{number}" else f"{prefix}{number}"
    return f"{prefix}-{number}"


def infer_spec_section(filename: str) -> str | None:
    stem = Path(filename).stem
    match = _SPEC_SPACED_RE.search(stem) or _SPEC_COMPACT_RE.search(stem)
    if not match:
        division_match = re.search(r"\bDIV(?:ISION)?[-\s]*(?P<div>\d{2})\b", stem, re.IGNORECASE)
        if division_match:
            return f"{division_match.group('div')} 00 00"
        return None
    return f"{match.group('a')} {match.group('b')} {match.group('c')}"


def infer_discipline_from_path_or_name(path: Path | str) -> str | None:
    text = str(path).upper()
    parts = re.split(r"[^A-Z0-9]+", text)
    for part in parts:
        if part in DISCIPLINE_ALIASES:
            return DISCIPLINE_ALIASES[part]
    spec_section = infer_spec_section(Path(text).name)
    if spec_section:
        division = spec_section[:2]
        return {
            "21": "FIRE_PROTECTION",
            "22": "PLUMBING",
            "23": "MECHANICAL",
            "26": "ELECTRICAL",
            "27": "TECHNOLOGY",
            "28": "TECHNOLOGY",
        }.get(division)
    sheet = infer_sheet_number(Path(text).name)
    if sheet:
        return DISCIPLINE_ALIASES.get(sheet.split("-", 1)[0])
    return None


def _path_contains(path: Path, needle: str) -> bool:
    return needle.lower() in str(path).replace("\\", "/").lower()


def _declared_type_compatible(path: Path, file_type: LandingFileType) -> tuple[bool, str]:
    suffix = path.suffix.lower()
    if file_type in {"drawing_pdf", "specification_pdf", "pdf_document"}:
        return (suffix == ".pdf", f"Manifest declared {file_type}, but extension is {suffix or '<none>'}")
    if file_type == "dwfx_export":
        return (suffix == ".dwfx", f"Manifest declared {file_type}, but extension is {suffix or '<none>'}")
    if file_type == "viewpoint_json":
        return (suffix == ".json", f"Manifest declared {file_type}, but extension is {suffix or '<none>'}")
    if file_type == "timeline_excel":
        return (suffix in {".xlsx", ".xlsm"}, f"Manifest declared {file_type}, but extension is {suffix or '<none>'}")
    if file_type == "owner_requirements":
        return (suffix in {".xlsx", ".xlsm"}, f"Manifest declared owner_requirements, but extension is {suffix or '<none>'}")
    if file_type in {"revit_export", "project_extract", "binding"}:
        return (suffix == ".json", f"Manifest declared {file_type}, but extension is {suffix or '<none>'}")
    return True, "Compatible"
