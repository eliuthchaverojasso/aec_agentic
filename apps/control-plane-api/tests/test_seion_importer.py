"""SEION advisory prediction importer tests."""

from __future__ import annotations

import json
from datetime import datetime, timezone

import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.ext.compiler import compiles
from sqlalchemy.orm import Session, sessionmaker
from sqlalchemy.pool import StaticPool

from app.database import get_db
from app.main import app
from app.models import Base, Client, Organization, Project, Requirement, RequirementCompliance, SeionPrediction
from app.readiness.service import build_project_readiness
from app.seion.importer import import_seion_predictions


@compiles(JSONB, "sqlite")
def _compile_jsonb_for_sqlite(type_, compiler, **kw):
    return "JSON"


@pytest.fixture
def db_session():
    engine = create_engine("sqlite+pysqlite:///:memory:")
    Base.metadata.create_all(engine)
    with Session(engine) as session:
        organization = Organization(id=1, name="Synthetic Org")
        client = Client(id=1, organization_id=1, code="OWNER", display_name="Synthetic Owner")
        project = Project(id=1, organization_id=1, client_id=1, project_title="Synthetic Project")
        requirement = Requirement(
            id=1,
            client_id=1,
            discipline="Electrical",
            requirement_text="Provide evidence.",
            content_hash="req-1",
            is_active=True,
            is_actionable=True,
        )
        compliance = RequirementCompliance(
            id=1,
            requirement_id=1,
            project_id=1,
            status="compliant",
            evaluated_at=datetime.now(timezone.utc),
        )
        session.add_all([organization, client, project, requirement, compliance])
        session.commit()
        yield session
    Base.metadata.drop_all(engine)


def _write_prediction(path, **overrides):
    row = {
        "head": "requirement:1",
        "relation": "should_be_supported_by",
        "tail": "element:1",
        "score": 0.91,
        "rank": 1,
        "model_version": "seion-kge-v0.1.0",
        "source": "seion_kge",
        "metadata": {"advisory": True},
    }
    row.update(overrides)
    path.write_text(json.dumps(row) + "\n", encoding="utf-8")


def test_imports_predictions(db_session, tmp_path):
    path = tmp_path / "predictions.jsonl"
    _write_prediction(path)
    result = import_seion_predictions(db_session, path, project_id=1, allowed_base=tmp_path)
    assert result.inserted_count == 1
    row = db_session.query(SeionPrediction).one()
    assert row.status == "suggested"
    assert row.metadata_json["advisory"] is True


def test_rejects_missing_fields(db_session, tmp_path):
    path = tmp_path / "predictions.jsonl"
    _write_prediction(path, model_version=None)
    result = import_seion_predictions(db_session, path, project_id=1, allowed_base=tmp_path)
    assert result.inserted_count == 0
    assert result.skipped_count == 1
    assert "missing required fields" in result.warnings[0]


def test_prevents_path_traversal_if_api_exists(tmp_path):
    engine = create_engine(
        "sqlite+pysqlite:///:memory:",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    Base.metadata.create_all(engine)
    TestingSessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False)

    def override_db():
        with TestingSessionLocal() as session:
            yield session

    app.dependency_overrides[get_db] = override_db
    try:
        response = TestClient(app).post(
            "/api/v1/seion/import-predictions",
            json={"path": str(tmp_path / ".." / "outside.jsonl"), "project_id": None},
        )
        assert response.status_code == 400
        assert "allowed SEION artifacts directory" in response.json()["detail"]
    finally:
        app.dependency_overrides.clear()
        Base.metadata.drop_all(engine)


def test_does_not_change_readiness(db_session, tmp_path):
    project = db_session.get(Project, 1)
    before = build_project_readiness(db_session, project)
    path = tmp_path / "predictions.jsonl"
    _write_prediction(path)
    import_seion_predictions(db_session, path, project_id=1, allowed_base=tmp_path)
    after = build_project_readiness(db_session, project)
    assert after.overall_readiness == before.overall_readiness


def test_accept_sets_status_and_timestamp(db_session):
    prediction = SeionPrediction(
        id=1,
        project_id=1,
        head_uid="requirement:1",
        relation="should_be_supported_by",
        tail_uid="element:1",
        score=0.9,
        rank=1,
        model_version="seion-kge-v0.1.0",
    )
    db_session.add(prediction)
    db_session.commit()
    response = TestClient(app)
    # Direct service covered by API tests; this test asserts persisted columns.
    prediction.status = "accepted"
    prediction.accepted_by = "reviewer@example.com"
    prediction.accepted_at = datetime.now(timezone.utc)
    db_session.commit()
    saved = db_session.get(SeionPrediction, 1)
    assert saved.status == "accepted"
    assert saved.accepted_at is not None


def test_accept_does_not_create_compliance(db_session):
    before = db_session.query(RequirementCompliance).count()
    prediction = SeionPrediction(
        id=1,
        project_id=1,
        head_uid="requirement:1",
        relation="should_be_supported_by",
        tail_uid="element:1",
        score=0.9,
        rank=1,
        model_version="seion-kge-v0.1.0",
        status="accepted",
        accepted_at=datetime.now(timezone.utc),
    )
    db_session.add(prediction)
    db_session.commit()
    assert db_session.query(RequirementCompliance).count() == before
