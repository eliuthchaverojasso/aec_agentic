"""Project requirements API contract tests."""

from datetime import datetime, timezone

from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.ext.compiler import compiles
from sqlalchemy.orm import Session
from sqlalchemy.pool import StaticPool

from app.database import get_db
from app.main import app
from app.models import Base, Client, Organization, Project, Requirement, RequirementCompliance


@compiles(JSONB, "sqlite")
def _compile_jsonb_for_sqlite(type_, compiler, **kw):
    return "JSON"


def test_project_requirements_returns_state_and_counts():
    client = TestClient(app)
    response = client.get("/api/v1/projects/1/requirements")
    assert response.status_code == 200
    payload = response.json()
    assert payload["project_id"] == 1
    assert payload["state"] in {
        "no_client_linked",
        "client_linked_no_requirements",
        "requirements_loaded",
        "filtered_empty",
        "readiness_available",
    }
    assert "counts" in payload
    assert "page" in payload
    assert "page_size" in payload
    assert "total" in payload


def test_project_requirements_supports_pagination_and_filters():
    client = TestClient(app)
    response = client.get(
        "/api/v1/projects/1/requirements",
        params={"page": 1, "page_size": 10, "evidence_status": "missing"},
    )
    assert response.status_code == 200
    payload = response.json()
    assert payload["page"] == 1
    assert payload["page_size"] == 10
    assert isinstance(payload["items"], list)


def test_project_requirement_mapping_patch_persists_milestone_and_flags():
    engine = create_engine(
        "sqlite+pysqlite:///:memory:",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    Base.metadata.create_all(engine)

    with Session(engine) as session:
        organization = Organization(id=1, name="Test Org")
        client = Client(id=1, organization_id=1, code="OWNER", display_name="Owner Agency")
        project = Project(id=1, organization_id=1, client_id=1, project_title="Project One")
        requirement = Requirement(
            id=1,
            client_id=1,
            discipline="Electrical",
            category="General",
            requirement_text="Electrical package item",
            content_hash="hash-1",
            resource="Owner matrix",
            is_active=True,
            is_actionable=True,
        )
        session.add_all([organization, client, project, requirement])
        session.commit()

        def _get_db():
            yield session

        app.dependency_overrides[get_db] = _get_db
        try:
            client_api = TestClient(app)
            response = client_api.patch(
                "/api/v1/projects/1/requirements/1/mapping",
                json={
                    "milestone": "DD 50%",
                    "discipline": "mechanical",
                    "is_actionable": False,
                    "notes": "Mapped from drawer",
                },
            )
            assert response.status_code == 200
            payload = response.json()
            assert payload["status"] == "not_evaluated"
            assert payload["evidence"]["milestone"] == "DD 50%"

            compliance = session.get(RequirementCompliance, payload["id"])
            assert compliance is not None
            assert compliance.evidence["milestone"] == "DD 50%"

            requirements = client_api.get("/api/v1/projects/1/requirements")
            assert requirements.status_code == 200
            items = requirements.json()["items"]
            assert items[0]["milestone"] == "DD 50%"
            assert items[0]["discipline"] == "MECHANICAL"
        finally:
            app.dependency_overrides.clear()

    Base.metadata.drop_all(engine)
