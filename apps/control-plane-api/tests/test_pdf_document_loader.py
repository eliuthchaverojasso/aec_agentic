from pathlib import Path

from app.ingestion.pdf_document_loader import (
    build_pdf_document_record,
    extract_pdf_metadata,
    maybe_extract_text_preview,
)


MINIMAL_PDF = b"""%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>
endobj
4 0 obj
<< /Length 44 >>
stream
BT /F1 12 Tf 20 100 Td (EMA test sheet) Tj ET
endstream
endobj
xref
0 5
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000204 00000 n
trailer
<< /Root 1 0 R /Size 5 >>
startxref
298
%%EOF
"""


def test_pdf_metadata_handles_minimal_pdf_without_external_calls(tmp_path: Path):
    pdf = tmp_path / "E-101 Test Sheet.pdf"
    pdf.write_bytes(MINIMAL_PDF)

    metadata = extract_pdf_metadata(pdf)

    assert metadata.page_count in {None, 1}
    assert isinstance(metadata.warnings, list)


def test_build_pdf_document_record_infers_drawing_fields(tmp_path: Path):
    pdf = tmp_path / "E-101 Test Sheet.pdf"
    pdf.write_bytes(MINIMAL_PDF)

    record = build_pdf_document_record(pdf, "TEST/Drawings/E-101 Test Sheet.pdf")

    assert record["file_type"] == "drawing_pdf"
    assert record["document_category"] == "drawing"
    assert record["sheet_number"] == "E-101"


def test_corrupt_pdf_is_registered_with_warning(tmp_path: Path):
    pdf = tmp_path / "26 05 00 Electrical.pdf"
    pdf.write_bytes(b"not a pdf")

    record = build_pdf_document_record(pdf, "TEST/Specifications/26 05 00 Electrical.pdf")

    assert record["file_type"] == "specification_pdf"
    assert record["spec_section"] == "26 05 00"
    assert record["warnings"]


def test_text_preview_is_capped_or_unavailable(tmp_path: Path):
    pdf = tmp_path / "E-101 Test Sheet.pdf"
    pdf.write_bytes(MINIMAL_PDF)

    preview = maybe_extract_text_preview(pdf, max_chars=20)

    assert "text_preview" in preview
    if preview["text_preview"]:
        assert len(preview["text_preview"]) <= 20
