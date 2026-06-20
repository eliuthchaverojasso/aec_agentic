"""API tests for advisory SEION-KGE suggestions."""

from __future__ import annotations

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


@compiles(JSONB, "sqlite")
def _compile_jsonb_for_sqlite(type_, compiler, **kw):
    return "JSON"


@pytest.fixture
def api_db():
    engine = create_engine(
        "sqlite+pysqlite:///:memory:",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    Base.metadata.create_all(engine)
    TestingSessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False)

    with TestingSessionLocal() as session:
        organization = Organization(id=1, name="Synthetic Org")
        client = Client(id=1, organization_id=1, code="OWNER", display_name="Synthetic Owner")
        project = Project(id=1, organization_id=1, client_id=1, project_title="Synthetic API Project")
        requirement = Requirement(
            id=1,
            client_id=1,
            discipline="Electrical",
            category="Power",
            requirement_text="Provide electrical fixture evidence.",
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
        prediction = SeionPrediction(
            id=1,
            project_id=1,
            head_uid="requirement:1",
            relation="suggests_evidence",
            tail_uid="element:1",
            score=0.91,
            rank=1,
            model_version="seion-test",
            metadata_json={"reason": "synthetic"},
        )
        session.add_all([organization, client, project, requirement, compliance, prediction])
        session.commit()

    def override_db():
        with TestingSessionLocal() as session:
            yield session

    app.dependency_overrides[get_db] = override_db
    try:
        yield TestingSessionLocal
    finally:
        app.dependency_overrides.clear()
        Base.metadata.drop_all(engine)


def test_list_suggestions_by_project(api_db):
    response = TestClient(app).get("/api/v1/projects/1/seion/suggestions?status=suggested")

    assert response.status_code == 200
    rows = response.json()
    assert len(rows) == 1
    assert rows[0]["relation"] == "suggests_evidence"
    assert rows[0]["advisory"] is True


def test_accept_suggestion_does_not_alter_readiness_score(api_db):
    with api_db() as session:
        project = session.get(Project, 1)
        before = build_project_readiness(session, project)

    response = TestClient(app).post(
        "/api/v1/seion/suggestions/1/accept",
        json={"reviewer_note": "Looks relevant.", "accepted_by": "reviewer@example.com"},
    )

    assert response.status_code == 200
    assert response.json()["status"] == "accepted"
    with api_db() as session:
        project = session.get(Project, 1)
        after = build_project_readiness(session, project)
    assert after.overall_readiness == before.overall_readiness


def test_reject_suggestion(api_db):
    response = TestClient(app).post(
        "/api/v1/seion/suggestions/1/reject",
        json={"reviewer_note": "Not applicable to this project."},
    )

    assert response.status_code == 200
    assert response.json()["status"] == "rejected"
    assert response.json()["reviewer_note"] == "Not applicable to this project."


def test_invalid_prediction_returns_404(api_db):
    response = TestClient(app).post("/api/v1/seion/suggestions/999/accept", json={})

    assert response.status_code == 404
    assert response.json() == {"detail": "SEION advisory suggestion not found"}
