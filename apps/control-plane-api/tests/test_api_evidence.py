from datetime import datetime, timezone

import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.ext.compiler import compiles
from sqlalchemy.orm import Session
from sqlalchemy.pool import StaticPool

from app.database import get_db
from app.main import app
from app.models import Base, Client, Organization, Project, Requirement


@compiles(JSONB, "sqlite")
def _compile_jsonb_for_sqlite(type_, compiler, **kw):
    return "JSON"


@pytest.fixture
def client():
    engine = create_engine(
        "sqlite+pysqlite:///:memory:",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    Base.metadata.create_all(engine)
    with Session(engine) as session:
        org = Organization(id=1, name="EMA Engineering")
        client = Client(id=1, organization_id=1, code="OWNER", display_name="Owner Agency")
        project = Project(id=1, organization_id=1, client_id=1, project_title="TEST PROJECT")
        requirement = Requirement(
            id=1,
            client_id=1,
            discipline="Electrical",
            category="DD50 model",
            requirement_text="Electrical model evidence is required at DD50.",
            content_hash="evidence-test",
            is_active=True,
            is_actionable=True,
        )
        session.add_all([org, client, project, requirement])
        session.commit()

    def override_db():
        with Session(engine) as session:
            yield session

    app.dependency_overrides[get_db] = override_db
    try:
        yield TestClient(app)
    finally:
        app.dependency_overrides.clear()
        Base.metadata.drop_all(engine)


def test_create_and_list_requirement_evidence(client: TestClient):
    response = client.post(
        "/api/v1/projects/1/requirements/1/evidence",
        json={
            "review_status": "candidate",
            "evidence_type": "manual",
            "source_label": "REQ-1",
            "review_note": "Candidate evidence for DD50",
            "reviewed_by": "reviewer@example.com",
        },
    )

    assert response.status_code == 200
    payload = response.json()
    assert payload["review_status"] == "candidate"
    assert payload["evidence_status"] == "needs_review"
    assert payload["source_label"] == "REQ-1"
    assert payload["review_note"] == "Candidate evidence for DD50"

    list_response = client.get("/api/v1/projects/1/evidence")
    assert list_response.status_code == 200
    assert len(list_response.json()) == 1

    requirement_response = client.get("/api/v1/projects/1/requirements/1/evidence")
    assert requirement_response.status_code == 200
    assert requirement_response.json()[0]["review_status"] == "candidate"


def test_update_requirement_evidence_review_status(client: TestClient):
    created = client.post(
        "/api/v1/projects/1/requirements/1/evidence",
        json={
            "review_status": "candidate",
            "evidence_type": "manual",
            "source_label": "REQ-1",
        },
    ).json()

    updated = client.patch(
        f"/api/v1/projects/1/evidence/{created['id']}",
        json={
            "review_status": "accepted",
            "review_note": "Accepted after reviewer review.",
            "reviewed_by": "lead.reviewer@example.com",
        },
    )

    assert updated.status_code == 200
    payload = updated.json()
    assert payload["review_status"] == "accepted"
    assert payload["evidence_status"] == "covered"
    assert payload["review_note"] == "Accepted after reviewer review."
    assert payload["reviewed_by"] == "lead.reviewer@example.com"


def test_project_requirements_surfaces_evidence_review_status(client: TestClient):
    client.post(
        "/api/v1/projects/1/requirements/1/evidence",
        json={
            "review_status": "accepted",
            "evidence_type": "manual",
            "source_label": "REQ-1",
        },
    )

    response = client.get("/api/v1/projects/1/requirements")
    assert response.status_code == 200
    payload = response.json()
    assert payload["items"][0]["evidence_review_status"] == "accepted"
    assert payload["items"][0]["evidence_status"] == "compliant"
    assert payload["counts"]["covered"] == 1
