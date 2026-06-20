"""Project/extract JSON summarization for landing files.

Extract files are auxiliary product/development context. They should not be
inserted into Revit Element rows unless they match the Revit export schema.
"""

from __future__ import annotations

import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


MAX_EXTRACT_PARSE_BYTES = 25 * 1024 * 1024


def summarize_project_extract(
    *,
    extract_path: Path,
    landing_root: Path,
    dry_run: bool = False,
    client_code: str | None = None,
    project_title: str | None = None,
) -> dict[str, Any]:
    if dry_run:
        return _build_metadata_only_summary(
            extract_path=extract_path,
            landing_root=landing_root,
            client_code=client_code,
            project_title=project_title,
        )

    summary = _build_summary(
        extract_path=extract_path,
        landing_root=landing_root,
        client_code=client_code,
        project_title=project_title,
    )
    if not dry_run:
        processed_dir = landing_root / "processed"
        processed_dir.mkdir(parents=True, exist_ok=True)
        output_path = processed_dir / f"{extract_path.stem}_summary.json"
        with output_path.open("w", encoding="utf-8") as handle:
            json.dump(summary, handle, indent=2, default=str)
        summary["summary_path"] = str(output_path.relative_to(landing_root))
    return summary


def _build_metadata_only_summary(
    *,
    extract_path: Path,
    landing_root: Path,
    client_code: str | None,
    project_title: str | None,
) -> dict[str, Any]:
    return {
        "file_name": extract_path.name,
        "relative_path": str(extract_path.relative_to(landing_root)),
        "file_type": "project_extract",
        "file_size_bytes": extract_path.stat().st_size,
        "file_hash": None,
        "client_code": client_code,
        "project_title": project_title,
        "ingested_at": datetime.now(timezone.utc).isoformat(),
        "parse_error": None,
        "dry_run": True,
        "json_shape": "not_parsed",
        "summary_note": "Dry run only records file metadata; JSON body and SHA256 were not parsed.",
    }


def _build_summary(
    *,
    extract_path: Path,
    landing_root: Path,
    client_code: str | None,
    project_title: str | None,
) -> dict[str, Any]:
    file_size = extract_path.stat().st_size
    if file_size > MAX_EXTRACT_PARSE_BYTES:
        return {
            "file_name": extract_path.name,
            "relative_path": str(extract_path.relative_to(landing_root)),
            "file_type": "project_extract",
            "file_size_bytes": file_size,
            "file_hash": None,
            "client_code": client_code,
            "project_title": project_title,
            "ingested_at": datetime.now(timezone.utc).isoformat(),
            "parse_error": None,
            "json_shape": "not_parsed",
            "summary_note": (
                "Extract exceeded summary parse limit; file was registered as an "
                "auxiliary source without loading JSON into memory."
            ),
            "max_parse_bytes": MAX_EXTRACT_PARSE_BYTES,
        }

    file_hash = _sha256_file(extract_path)
    payload: Any | None = None
    parse_error: str | None = None
    if extract_path.suffix.lower() == ".json":
        try:
            with extract_path.open("r", encoding="utf-8") as handle:
                payload = json.load(handle)
        except Exception as exc:  # noqa: BLE001
            parse_error = str(exc)
    else:
        parse_error = "Project extract loader currently supports JSON summaries only"

    summary: dict[str, Any] = {
        "file_name": extract_path.name,
        "relative_path": str(extract_path.relative_to(landing_root)),
        "file_type": "project_extract",
        "file_size_bytes": file_size,
        "file_hash": file_hash,
        "client_code": client_code,
        "project_title": project_title,
        "ingested_at": datetime.now(timezone.utc).isoformat(),
        "parse_error": parse_error,
    }

    if parse_error is not None:
        return summary

    summary.update(_summarize_payload(payload))
    return summary


def _summarize_payload(payload: Any) -> dict[str, Any]:
    if isinstance(payload, dict):
        return {
            "json_shape": "object",
            "top_level_keys": sorted(str(key) for key in payload.keys())[:100],
            "top_level_key_count": len(payload),
            "sample": _sample_value(payload),
        }
    if isinstance(payload, list):
        item_types = sorted({type(item).__name__ for item in payload[:100]})
        return {
            "json_shape": "array",
            "item_count": len(payload),
            "sample_item_types": item_types,
            "sample": _sample_value(payload[:3]),
        }
    return {
        "json_shape": type(payload).__name__,
        "sample": _sample_value(payload),
    }


def _sample_value(value: Any) -> Any:
    text = json.dumps(value, default=str)
    if len(text) <= 2000:
        return value
    return {"truncated_json_preview": text[:2000]}


def _sha256_file(path: Path, chunk: int = 1024 * 1024) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            data = handle.read(chunk)
            if not data:
                break
            digest.update(data)
    return digest.hexdigest()
