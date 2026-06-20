"""Owner Requirements loader.

Ingests a SharePoint-style xlsx export of a district's requirement catalog into
the `client` / `requirement_source_file` / `requirement` tables.

Idempotent on `(client_id, file_hash)` and on `(client_id, content_hash)`:
- If the same file is re-uploaded -> returns the existing source-file record.
- If the same requirement text reappears -> bumps `last_seen_at` + metadata.
- If a requirement previously seen is NOT present in the new file -> soft-delete
  (`is_active = false`) so history is preserved.

The ingestion is deliberately pure-Python (openpyxl + stdlib) and does not depend
on pandas to keep the container image small.
"""
from __future__ import annotations

import hashlib
import logging
import re
from collections import Counter
from datetime import date, datetime, timezone
from pathlib import Path
from typing import Any

from openpyxl import load_workbook
from sqlalchemy import select, update
from sqlalchemy.orm import Session

from app.models import Client, Requirement, RequirementSourceFile

logger = logging.getLogger(__name__)


# ----------------------------------------------------------------------------
# Constants / normalization
# ----------------------------------------------------------------------------

CANONICAL_HEADERS = {
    "STATUS": "status",
    "DISCIPLINE": "discipline",
    "REQUIREMENT": "requirement",
    "LINKS": "links",
    "CATEGORY LIST": "category",
    "DATE UPDATED": "date_updated",
    "MODIFIED BY": "modified_by",
    "RESOURCE": "resource",
    "ITEM TYPE": "item_type",
    "PATH": "path",
}

VALID_DISCIPLINES = {
    "ELECTRICAL",
    "LIGHTING",
    "PLUMBING",
    "MECHANICAL",
    "TECHNOLOGY",
}

# Pattern for placeholder/sentinel rows (emoji or literal text). Owner lists use
# a "DRAG & DROP requirement HERE when DONE" template row at the top of each
# discipline section.
SENTINEL_PATTERNS = (
    re.compile(r"drag\s*&\s*drop\s+requirement\s+here", re.IGNORECASE),
    re.compile(r"\U0001f60e"),  # :sunglasses: emoji used in the template
)

# Allow dates from 1990-01-01 to "today + 5 years". Owner files have had bad
# values like 3016-10-31 (clearly a typo); we keep the raw text out of the DB.
MIN_DATE = datetime(1990, 1, 1, tzinfo=timezone.utc)


# ----------------------------------------------------------------------------
# Helpers
# ----------------------------------------------------------------------------


def _sha256_file(path: Path, chunk: int = 1024 * 1024) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        while True:
            buf = f.read(chunk)
            if not buf:
                break
            h.update(buf)
    return h.hexdigest()


def _sha256_text(*parts: str) -> str:
    return hashlib.sha256("|".join(parts).encode("utf-8")).hexdigest()


def _normalize_text(value: Any) -> str:
    if value is None:
        return ""
    s = str(value).replace("\r", " ").replace("\n", " | ")
    s = re.sub(r"\s+", " ", s).strip()
    return s


def _is_sentinel(text: str) -> bool:
    return any(p.search(text) for p in SENTINEL_PATTERNS)


def _is_reference_only_requirement(req_text: str, links: str | None, resource: str | None) -> bool:
    """
    Determine if a requirement is reference-only (not actionable).
    
    A reference-only requirement has text that indicates it points to external information
    but contains no actionable content itself.
    
    Returns True if:
    - Requirement text contains strong reference phrases like "refer to links column", 
      "refer to hyperlink", "hyperlink to", "refer to links"
    - And links or resource is present
    
    Returns False for normal requirements, even if they have resource metadata.
    """
    if not req_text:
        return False
    
    # Normalize text for comparison
    normalized_text = req_text.lower().strip()
    
    # Reference phrases that indicate the requirement points to external links
    reference_phrases = [
        "refer to links column",
        "refer to hyperlink",
        "hyperlink to",
        "refer to links"
    ]
    
    # Check if any reference phrase is in the requirement text
    has_reference_phrase = any(phrase in normalized_text for phrase in reference_phrases)
    
    # Must have either links or resource present to be considered a reference row
    has_links_or_resource = bool(_normalize_text(links)) or bool(_normalize_text(resource))
    
    return has_reference_phrase and has_links_or_resource


def _normalize_discipline(value: Any) -> str:
    s = _normalize_text(value).upper()
    if not s:
        return "OTHER"
    if s in VALID_DISCIPLINES:
        return s
    # Common variants
    if s.startswith("ELEC"):
        return "ELECTRICAL"
    if s.startswith("LIGHT"):
        return "LIGHTING"
    if s.startswith("PLUMB"):
        return "PLUMBING"
    if s.startswith("MECH"):
        return "MECHANICAL"
    if s.startswith("TECH"):
        return "TECHNOLOGY"
    return "OTHER"


def _normalize_status(value: Any) -> str | None:
    s = _normalize_text(value).upper()
    if not s:
        return None
    if "DONE" in s:
        return "DONE"
    if "NOT STARTED" in s or s == "NOT_STARTED":
        return "NOT_STARTED"
    return s


def _coerce_date(value: Any) -> datetime | None:
    if value is None or value == "":
        return None
    if isinstance(value, datetime):
        dt = value
    elif isinstance(value, date):
        dt = datetime(value.year, value.month, value.day)
    else:
        try:
            dt = datetime.fromisoformat(str(value))
        except ValueError:
            return None
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    # Reject absurd dates (e.g. 3016) -> store NULL, not raw garbage.
    now = datetime.now(timezone.utc)
    if dt < MIN_DATE or dt.year > now.year + 5:
        return None
    return dt


_FILENAME_DATE_RE = re.compile(
    r"(?P<m>\d{1,2})[._\- ](?P<d>\d{1,2})[._\- ](?P<y>\d{2,4})"
)


def _parse_export_date_from_filename(name: str) -> date | None:
    m = _FILENAME_DATE_RE.search(name)
    if not m:
        return None
    try:
        y = int(m.group("y"))
        if y < 100:
            y += 2000
        return date(y, int(m.group("m")), int(m.group("d")))
    except ValueError:
        return None


def _header_map(ws) -> dict[str, int]:
    mapping: dict[str, int] = {}
    for col in range(1, ws.max_column + 1):
        raw = ws.cell(1, col).value
        if raw is None:
            continue
        key = str(raw).strip().upper()
        if key in CANONICAL_HEADERS:
            mapping[CANONICAL_HEADERS[key]] = col
    return mapping


# ----------------------------------------------------------------------------
# Public API
# ----------------------------------------------------------------------------


def get_client_by_code(db: Session, code: str) -> Client | None:
    return db.execute(select(Client).where(Client.code == code)).scalar_one_or_none()


def ingest_requirements_file(
    db: Session,
    *,
    client: Client,
    xlsx_path: Path,
    original_filename: str | None = None,
) -> dict[str, Any]:
    """Ingest a single xlsx file for the given client.

    Returns a dict with counts + the source_file record (already flushed).
    """
    original_filename = original_filename or xlsx_path.name
    file_hash = _sha256_file(xlsx_path)

    # Idempotency: same file bytes for the same client -> return existing record.
    existing = db.execute(
        select(RequirementSourceFile).where(
            RequirementSourceFile.client_id == client.id,
            RequirementSourceFile.file_hash == file_hash,
        )
    ).scalar_one_or_none()

    reused_existing_file = False
    if existing is not None:
        logger.info(
            "Requirements file already ingested (client=%s hash=%s), skipping re-parse",
            client.code,
            file_hash[:12],
        )
        reused_existing_file = True
        source_file = existing
    else:
        source_file = RequirementSourceFile(
            client_id=client.id,
            original_filename=original_filename,
            file_hash=file_hash,
            export_date=_parse_export_date_from_filename(original_filename),
        )
        db.add(source_file)
        db.flush()

    wb = load_workbook(xlsx_path, data_only=True, read_only=True)
    ws = wb[wb.sheetnames[0]]
    hmap = _header_map(ws)

    if "requirement" not in hmap or "discipline" not in hmap:
        raise ValueError(
            f"Missing required columns in {original_filename}. "
            f"Detected headers: {list(hmap.keys())}"
        )

    row_count_raw = 0
    row_count_skipped = 0
    row_count_new = 0
    row_count_updated = 0
    per_discipline: Counter[str] = Counter()
    seen_hashes: set[str] = set()

    def _cell(row: tuple, key: str) -> Any:
        col = hmap.get(key)
        if col is None:
            return None
        # openpyxl read_only rows are 1-indexed tuples of Cell, but ws.iter_rows
        # with values_only=True returns tuples of values (0-indexed). We use the
        # latter so `col - 1` gives the index.
        idx = col - 1
        return row[idx] if 0 <= idx < len(row) else None

    for row in ws.iter_rows(min_row=2, values_only=True):
        row_count_raw += 1
        req_text_raw = _cell(row, "requirement")
        req_text = _normalize_text(req_text_raw)

        if not req_text:
            row_count_skipped += 1
            continue
        if _is_sentinel(req_text):
            row_count_skipped += 1
            continue

        discipline = _normalize_discipline(_cell(row, "discipline"))
        content_hash = _sha256_text(discipline, req_text.lower())
        if content_hash in seen_hashes:
            # Intra-file duplicate: keep first occurrence only.
            row_count_skipped += 1
            continue
        seen_hashes.add(content_hash)

        category = _normalize_text(_cell(row, "category")) or None
        owner_status = _normalize_status(_cell(row, "status"))
        resource = _normalize_text(_cell(row, "resource")) or None
        links = _normalize_text(_cell(row, "links")) or None
        modified_by = _normalize_text(_cell(row, "modified_by")) or None
        date_updated = _coerce_date(_cell(row, "date_updated"))
        sharepoint_path = _normalize_text(_cell(row, "path")) or None

        existing_req = db.execute(
            select(Requirement).where(
                Requirement.client_id == client.id,
                Requirement.content_hash == content_hash,
            )
        ).scalar_one_or_none()

        if existing_req is None:
            is_actionable = not _is_reference_only_requirement(req_text, links, resource)
            db.add(
                Requirement(
                    client_id=client.id,
                    source_file_id=source_file.id,
                    discipline=discipline,
                    category=category,
                    requirement_text=req_text,
                    content_hash=content_hash,
                    owner_status=owner_status,
                    resource=resource,
                    links=links,
                    modified_by=modified_by,
                    date_updated=date_updated,
                    sharepoint_path=sharepoint_path,
                    is_actionable=is_actionable,
                    is_active=True,
                )
            )
            row_count_new += 1
        else:
            # Recalculate is_actionable for existing requirements during re-ingest
            is_actionable = not _is_reference_only_requirement(req_text, links, resource)
            existing_req.source_file_id = source_file.id
            existing_req.category = category
            existing_req.owner_status = owner_status
            existing_req.resource = resource
            existing_req.links = links
            existing_req.modified_by = modified_by
            existing_req.date_updated = date_updated
            existing_req.sharepoint_path = sharepoint_path
            existing_req.is_active = True
            existing_req.is_actionable = is_actionable
            existing_req.last_seen_at = datetime.now(timezone.utc)
            row_count_updated += 1

        per_discipline[discipline] += 1

    # Soft-delete requirements that were NOT seen in this file for this client.
    deactivated_stmt = (
        update(Requirement)
        .where(
            Requirement.client_id == client.id,
            Requirement.is_active.is_(True),
            Requirement.content_hash.notin_(seen_hashes) if seen_hashes else Requirement.content_hash.is_(None),
        )
        .values(is_active=False)
    )
    result = db.execute(deactivated_stmt)
    row_count_deactivated = int(result.rowcount or 0)

    row_count_loaded = row_count_new + row_count_updated

    source_file.row_count_raw = row_count_raw
    source_file.row_count_loaded = row_count_loaded
    source_file.row_count_skipped = row_count_skipped

    db.flush()

    wb.close()

    return {
        "client_id": client.id,
        "source_file_id": source_file.id,
        "original_filename": original_filename,
        "file_hash": file_hash,
        "row_count_raw": row_count_raw,
        "row_count_loaded": row_count_loaded,
        "row_count_skipped": row_count_skipped,
        "row_count_new": row_count_new,
        "row_count_updated": row_count_updated,
        "row_count_deactivated": row_count_deactivated,
        "export_date": source_file.export_date,
        "reused_existing_file": reused_existing_file,
        "per_discipline": dict(per_discipline),
    }
