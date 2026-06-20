"""Service helpers for SEION-KGE advisory predictions."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from sqlalchemy import inspect, text
from sqlalchemy.orm import Session

from app.models import Base, Project, SeionPrediction
from app.schemas import SeionPredictionCreate, SeionPredictionOut

VALID_STATUSES = {"suggested", "accepted", "rejected", "stale", "superseded"}


def ensure_seion_tables(db: Session) -> None:
    """Create the additive advisory prediction table when local DBs predate it."""

    if db.bind is not None and db.bind.dialect.name != "postgresql":
        if "seion_prediction" not in inspect(db.bind).get_table_names():
            Base.metadata.create_all(db.bind, tables=[SeionPrediction.__table__])
        return

    existing = db.execute(text("SELECT to_regclass('public.seion_prediction') IS NOT NULL")).scalar()
    if existing:
        return

    db.execute(text("SELECT pg_advisory_xact_lock(7420190520)"))
    existing = db.execute(text("SELECT to_regclass('public.seion_prediction') IS NOT NULL")).scalar()
    if existing:
        return

    statements = [
        """
        CREATE TABLE IF NOT EXISTS seion_prediction (
            id BIGSERIAL PRIMARY KEY,
            project_id INT REFERENCES project(id) ON DELETE CASCADE,
            head_uid TEXT NOT NULL,
            relation TEXT NOT NULL,
            tail_uid TEXT NOT NULL,
            score DOUBLE PRECISION NOT NULL,
            rank INT,
            model_version TEXT NOT NULL,
            status VARCHAR(30) NOT NULL DEFAULT 'suggested',
            source TEXT NOT NULL DEFAULT 'seion_kge',
            reviewer_note TEXT,
            accepted_by TEXT,
            accepted_at TIMESTAMPTZ,
            metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMPTZ,
            CONSTRAINT chk_seion_prediction_status CHECK (
                status IN ('suggested','accepted','rejected','stale','superseded')
            )
        )
        """,
        """
        CREATE INDEX IF NOT EXISTS idx_seion_prediction_project_status
        ON seion_prediction(project_id, status)
        """,
        """
        CREATE INDEX IF NOT EXISTS idx_seion_prediction_relation
        ON seion_prediction(relation)
        """,
    ]
    for statement in statements:
        db.execute(text(statement))
    db.commit()


def create_seion_prediction(db: Session, payload: SeionPredictionCreate) -> SeionPredictionOut:
    ensure_seion_tables(db)
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
    ensure_seion_tables(db)
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
    ensure_seion_tables(db)
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
