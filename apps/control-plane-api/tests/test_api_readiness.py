"""API baseline tests for project readiness."""

from datetime import datetime, timezone
from unittest.mock import patch

from fastapi.testclient import TestClient

from app.database import get_db
from app.main import app
from app.models import Project
from app.schemas import ProjectReadinessOut, ReadinessComponent


class FakeDb:
    def __init__(self, projects: dict[int, Project]):
        self.projects = projects

    def get(self, model, object_id):
        if model is Project:
            return self.projects.get(object_id)
        return None


def _override_db(fake_db):
    def _get_db():
        yield fake_db

    app.dependency_overrides[get_db] = _get_db


def test_get_project_readiness_returns_payload_for_existing_project():
    latest_sync_at = datetime(2026, 5, 21, 12, 0, tzinfo=timezone.utc)
    project = Project(
        id=1,
        organization_id=1,
        client_id=10,
        project_title="Readiness API Baseline",
        client_name="Owner Agency",
    )
    fake_db = FakeDb(projects={project.id: project})
    readiness = ProjectReadinessOut(
        project_id=project.id,
        project_title=project.project_title,
        client_id=project.client_id,
        client_name=project.client_name,
        overall_readiness=88.5,
        label="On Track",
        requirement_coverage=ReadinessComponent(
            score=90.0,
            label="Ready",
            detail="9 of 10 active owner requirements evaluated",
        ),
        qaqc_health=ReadinessComponent(
            score=95.0,
            label="Ready",
            detail="1 open issues across 100 model elements",
        ),
        sync_freshness=ReadinessComponent(
            score=70.0,
            label="At Risk",
            detail="Latest completed sync found",
        ),
        open_issues={"critical": 0, "high": 1, "medium": 0, "low": 0},
        latest_export_id=123,
        latest_sync_at=latest_sync_at,
        trade_readiness=[],
        gap_summary={"critical": 0, "high": 1, "medium": 0, "low": 0},
        top_gaps=[],
        recommended_actions=[],
    )

    _override_db(fake_db)
    try:
        with patch("app.api.readiness.build_project_readiness", return_value=readiness) as build_readiness:
            response = TestClient(app).get("/api/v1/projects/1/readiness")
    finally:
        app.dependency_overrides.clear()

    assert response.status_code == 200
    assert response.json() == {
        "project_id": 1,
        "project_title": "Readiness API Baseline",
        "client_id": 10,
        "client_name": "Owner Agency",
        "overall_readiness": 88.5,
        "label": "On Track",
        "requirement_coverage": {
            "score": 90.0,
            "label": "Ready",
            "detail": "9 of 10 active owner requirements evaluated",
        },
        "qaqc_health": {
            "score": 95.0,
            "label": "Ready",
            "detail": "1 open issues across 100 model elements",
        },
        "sync_freshness": {
            "score": 70.0,
            "label": "At Risk",
            "detail": "Latest completed sync found",
        },
        "open_issues": {"critical": 0, "high": 1, "medium": 0, "low": 0},
        "latest_export_id": 123,
        "latest_sync_at": "2026-05-21T12:00:00Z",
        "trade_readiness": [],
        "gap_summary": {"critical": 0, "high": 1, "medium": 0, "low": 0},
        "top_gaps": [],
        "recommended_actions": [],
    }
    build_readiness.assert_called_once_with(fake_db, project)


def test_get_project_readiness_returns_404_for_missing_project():
    fake_db = FakeDb(projects={})

    _override_db(fake_db)
    try:
        with patch("app.api.readiness.build_project_readiness") as build_readiness:
            response = TestClient(app).get("/api/v1/projects/999/readiness")
    finally:
        app.dependency_overrides.clear()

    assert response.status_code == 404
    assert response.json() == {"detail": "Project not found"}
    build_readiness.assert_not_called()
