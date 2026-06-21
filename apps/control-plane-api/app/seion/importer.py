"""Safe importer for advisory SEION prediction JSONL files."""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from sqlalchemy.orm import Session

from app.models import Project, SeionPrediction
from app.seion.exporter import DEFAULT_OUTPUT_DIR


@dataclass(frozen=True)
class SeionPredictionImportResult:
    inserted_count: int = 0
    skipped_count: int = 0
    warnings: list[str] = field(default_factory=list)


def resolve_allowed_prediction_path(path: str | Path, allowed_base: str | Path = DEFAULT_OUTPUT_DIR) -> Path:
    base = Path(allowed_base).resolve()
    target = Path(path)
    if not target.is_absolute():
        target = base / target
    resolved = target.resolve()
    if base != resolved and base not in resolved.parents:
        raise ValueError("Prediction import path must be inside the allowed SEION artifacts directory")
    if resolved.suffix.lower() != ".jsonl":
        raise ValueError("Prediction import path must be a .jsonl file")
    return resolved


def import_seion_predictions(
    db: Session,
    predictions_path: str | Path,
    project_id: int | None = None,
    allowed_base: str | Path = DEFAULT_OUTPUT_DIR,
) -> SeionPredictionImportResult:
    """Import advisory predictions without mutating official readiness records."""

    path = resolve_allowed_prediction_path(predictions_path, allowed_base)
    if not path.exists():
        raise FileNotFoundError(f"Prediction file not found: {path}")
    if project_id is not None and db.get(Project, project_id) is None:
        raise ValueError("Project not found")

    inserted = 0
    skipped = 0
    warnings: list[str] = []
    with path.open("r", encoding="utf-8") as handle:
        for line_no, line in enumerate(handle, start=1):
            stripped = line.strip()
            if not stripped:
                continue
            try:
                row = json.loads(stripped)
            except json.JSONDecodeError as exc:
                skipped += 1
                warnings.append(f"line {line_no}: invalid JSON ({exc.msg})")
                continue
            normalized = _normalize_prediction_row(row)
            missing = [key for key in ("head_uid", "relation", "tail_uid", "score", "model_version") if normalized.get(key) in (None, "")]
            if missing:
                skipped += 1
                warnings.append(f"line {line_no}: missing required fields {', '.join(missing)}")
                continue
            exists = (
                db.query(SeionPrediction)
                .filter(
                    SeionPrediction.project_id == project_id,
                    SeionPrediction.head_uid == normalized["head_uid"],
                    SeionPrediction.relation == normalized["relation"],
                    SeionPrediction.tail_uid == normalized["tail_uid"],
                    SeionPrediction.model_version == normalized["model_version"],
                )
                .first()
            )
            if exists is not None:
                skipped += 1
                continue
            metadata = normalized.get("metadata") or {}
            if not isinstance(metadata, dict):
                metadata = {"raw_metadata": metadata}
            metadata = {"advisory": True, **metadata}
            db.add(
                SeionPrediction(
                    id=_next_sqlite_id(db),
                    project_id=project_id,
                    head_uid=str(normalized["head_uid"]),
                    relation=str(normalized["relation"]),
                    tail_uid=str(normalized["tail_uid"]),
                    score=float(normalized["score"]),
                    rank=int(normalized["rank"]) if normalized.get("rank") is not None else None,
                    model_version=str(normalized["model_version"]),
                    status="suggested",
                    source=str(normalized.get("source") or "seion_kge"),
                    metadata_json=metadata,
                )
            )
            inserted += 1
    db.commit()
    return SeionPredictionImportResult(inserted_count=inserted, skipped_count=skipped, warnings=warnings)


def _normalize_prediction_row(row: dict[str, Any]) -> dict[str, Any]:
    return {
        "head_uid": row.get("head_uid") or row.get("head") or row.get("h"),
        "relation": row.get("relation") or row.get("r"),
        "tail_uid": row.get("tail_uid") or row.get("tail") or row.get("t"),
        "score": row.get("score"),
        "rank": row.get("rank"),
        "model_version": row.get("model_version"),
        "source": row.get("source", "seion_kge"),
        "metadata": row.get("metadata", {}),
    }


def _next_sqlite_id(db: Session) -> int | None:
    if db.bind is None or db.bind.dialect.name != "sqlite":
        return None
    current = db.query(SeionPrediction.id).order_by(SeionPrediction.id.desc()).first()
    return int(current[0]) + 1 if current else 1
