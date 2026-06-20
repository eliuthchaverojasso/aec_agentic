from pathlib import Path

from app.ingestion.file_classifier import (
    classify_landing_file,
    infer_discipline_from_path_or_name,
    infer_sheet_number,
    infer_spec_section,
)
from app.ingestion.manifest_loader import resolve_landing_path


def test_classifies_owner_requirements_excel_under_owner_requirements():
    result = classify_landing_file(Path("ROCHELL ES/Owner Requirements/Owner Requirements.xlsx"))

    assert result.type == "owner_requirements"


def test_classifies_drawing_pdf_under_drawings_and_infers_sheet():
    path = Path("ROCHELL ES/Drawings/E-001 Electrical Site Plan.pdf")
    result = classify_landing_file(path)

    assert result.type == "drawing_pdf"
    assert infer_sheet_number(path.name) == "E-001"
    assert infer_discipline_from_path_or_name(path) == "ELECTRICAL"


def test_classifies_spec_pdf_under_specifications_and_infers_section():
    path = Path("ROCHELL ES/Specifications/26 05 00 Electrical General Requirements.pdf")
    result = classify_landing_file(path)

    assert result.type == "specification_pdf"
    assert infer_spec_section(path.name) == "26 05 00"
    assert infer_discipline_from_path_or_name(path) == "ELECTRICAL"


def test_manifest_type_is_trusted_when_extension_is_compatible():
    result = classify_landing_file(Path("docs/package.pdf"), declared_type="drawing_pdf")

    assert result.type == "drawing_pdf"
    assert result.confidence == "manifest"


def test_manifest_type_conflict_returns_unknown():
    result = classify_landing_file(Path("docs/package.txt"), declared_type="drawing_pdf")

    assert result.type == "unknown"
    assert result.confidence == "manifest_conflict"


def test_classifies_dwfx_export_by_extension():
    result = classify_landing_file(Path("ROCHELL ES/3D Exports/Level01.dwfx"))
    assert result.type == "dwfx_export"


def test_classifies_viewpoint_json_from_folder():
    result = classify_landing_file(Path("ROCHELL ES/Viewpoints/issue_42_viewpoint.json"))
    assert result.type == "viewpoint_json"


def test_classifies_timeline_excel_from_folder():
    result = classify_landing_file(Path("ROCHELL ES/Timeline/DD-CD Milestones.xlsx"))
    assert result.type == "timeline_excel"


def test_path_traversal_rejected():
    try:
        resolve_landing_path(Path("landing"), "../secret.pdf")
    except ValueError as exc:
        assert "inside landing" in str(exc)
    else:
        raise AssertionError("Expected path traversal rejection")
