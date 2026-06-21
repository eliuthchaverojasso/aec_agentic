"""Service helpers for SEION-KGE advisory predictions."""

from __future__ import annotations

from datetime import datetime, timezone

from sqlalchemy.orm import Session

from app.models import Project, SeionPrediction
from app.schemas import SeionPredictionCreate, SeionPredictionOut

VALID_STATUSES = {"suggested", "accepted", "rejected", "stale", "superseded"}


def create_seion_prediction(db: Session, payload: SeionPredictionCreate) -> SeionPredictionOut:
    if payload.project_id is not None and db.get(Project, payload.project_id) is None:
        raise ValueError("Project not found")
    row = SeionPrediction(
        project_id=payload.project_id,
        head_uid=payload.head_uid,
        relation=payload.relation,
        tail_uid=payload.tail_uid,
        score=payload.score,
        rank=payload.rank,
        model_version=payload.model_version,
        source=payload.source,
        metadata_json=payload.metadata,
    )
    db.add(row)
    db.commit()
    db.refresh(row)
    return SeionPredictionOut.model_validate(row)


def list_seion_predictions(
    db: Session,
    project_id: int,
    status: str | None = None,
    relation: str | None = None,
    limit: int = 50,
) -> list[SeionPredictionOut]:
    query = db.query(SeionPrediction).filter(SeionPrediction.project_id == project_id)
    if status:
        query = query.filter(SeionPrediction.status == status)
    if relation:
        query = query.filter(SeionPrediction.relation == relation)
    rows = query.order_by(SeionPrediction.rank.asc().nulls_last(), SeionPrediction.score.desc()).limit(limit).all()
    return [SeionPredictionOut.model_validate(row) for row in rows]


def review_seion_prediction(
    db: Session,
    prediction_id: int,
    status: str,
    reviewer_note: str | None = None,
    accepted_by: str | None = None,
) -> SeionPredictionOut | None:
    if status not in {"accepted", "rejected"}:
        raise ValueError("Only accepted/rejected review updates are supported")
    row = db.get(SeionPrediction, prediction_id)
    if row is None:
        return None
    row.status = status
    row.reviewer_note = reviewer_note
    row.updated_at = datetime.now(timezone.utc)
    if status == "accepted":
        row.accepted_by = accepted_by
        row.accepted_at = datetime.now(timezone.utc)
    db.commit()
    db.refresh(row)
    return SeionPredictionOut.model_validate(row)
