"""Future local OCR/vision adapter boundary.

The default adapter is deliberately unavailable. EMA AI v1 document ingestion
registers local PDFs and may extract lightweight metadata/text with installed
local PDF libraries, but it does not call external OCR or vision services.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any


class VisionAdapter:
    def is_available(self) -> bool:
        return False

    def analyze_pdf_page(self, path: Path, page_number: int) -> dict[str, Any]:
        return {
            "available": False,
            "path": path.name,
            "page_number": page_number,
            "message": "OCR/vision extraction is future local adapter scope and is not enabled.",
        }
