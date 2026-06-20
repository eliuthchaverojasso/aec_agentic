"""Streaming parser for Revit JSON exports.

The files can be very large (~90MB, 2900+ elements) so we use ijson to stream
the top-level array one element at a time rather than loading the whole file
into memory.
"""

from __future__ import annotations

import re
from pathlib import Path
from typing import Any, Iterator

import ijson


# Expected top-level keys per element (confirmed by sample data analysis).
REQUIRED_ELEMENT_KEYS = {"UniqueId", "ElementId", "Category"}

# ProjectTitle format observed in EMA sample:
#   "1 082 0079 003 ROCHELL ES REPLAC MEPC R22"
#   <prefix> <3-part job number> <project name ...> <revit version suffix>
_PROJECT_TITLE_RE = re.compile(
    r"^(?P<prefix>\d+)\s+"
    r"(?P<job_number>\d{3}\s+\d{4}\s+\d{3})\s+"
    r"(?P<middle>.+?)"
    r"(?:\s+(?P<revit_version>R\d{2,4}))?$"
)


def parse_project_title(title: str) -> dict[str, str | None]:
    """Decompose EMA ProjectTitle into structured fields.

    Falls back to the raw title if the expected pattern does not match.
    """
    if not title:
        return {
            "project_title": title,
            "prefix": None,
            "job_number": None,
            "project_name": None,
            "revit_version": None,
        }

    cleaned = " ".join(title.strip().split())
    match = _PROJECT_TITLE_RE.match(cleaned)
    if not match:
        return {
            "project_title": title,
            "prefix": None,
            "job_number": None,
            "project_name": cleaned,
            "revit_version": None,
        }

    return {
        "project_title": title,
        "prefix": match.group("prefix"),
        "job_number": match.group("job_number"),
        "project_name": match.group("middle").strip(),
        "revit_version": match.group("revit_version"),
    }


def stream_elements(json_path: Path) -> Iterator[dict[str, Any]]:
    """Yield element dicts one at a time from the JSON file.

    Uses ijson.items with prefix "item" to iterate the top-level array.
    """
    with open(json_path, "rb") as f:
        for element in ijson.items(f, "item"):
            yield element


def peek_metadata(json_path: Path, max_elements: int = 20) -> dict[str, Any]:
    """Read the first few elements to discover project title + category counts.

    Useful during the first pipeline step (received/validation) before committing
    to a full ingestion.
    """
    project_titles: set[str] = set()
    categories: dict[str, int] = {}
    levels: set[str] = set()
    element_id_samples: list[int] = []

    seen = 0
    for element in stream_elements(json_path):
        if not REQUIRED_ELEMENT_KEYS.issubset(element):
            raise ValueError(
                f"Element missing required keys. Got {list(element.keys())}; "
                f"expected at least {REQUIRED_ELEMENT_KEYS}."
            )

        title = element.get("ProjectTitle")
        if title:
            project_titles.add(title)

        cat = element.get("Category")
        if cat:
            categories[cat] = categories.get(cat, 0) + 1

        lv = element.get("Level")
        if lv:
            levels.add(lv)

        eid = element.get("ElementId")
        if isinstance(eid, int) and len(element_id_samples) < 5:
            element_id_samples.append(eid)

        seen += 1
        if seen >= max_elements:
            break

    return {
        "sampled_elements": seen,
        "project_titles": sorted(project_titles),
        "categories_in_sample": categories,
        "levels_in_sample": sorted(levels),
        "element_id_samples": element_id_samples,
    }


def infer_discipline(categories: list[str]) -> str:
    """Map a set of element categories to a discipline label."""
    cat_set = {c.lower() for c in categories if c}
    has_electrical = any("electrical" in c or "lighting" in c for c in cat_set)
    has_mechanical = any("mechanical" in c for c in cat_set)
    has_plumbing = any("plumbing" in c or "pipe" in c for c in cat_set)

    disciplines = []
    if has_electrical:
        disciplines.append("electrical")
    if has_mechanical:
        disciplines.append("mechanical")
    if has_plumbing:
        disciplines.append("plumbing")

    if not disciplines:
        return "unknown"
    if len(disciplines) == 1:
        return disciplines[0]
    return "multi"
