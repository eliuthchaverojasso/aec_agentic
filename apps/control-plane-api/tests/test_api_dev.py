"""API tests for local dev status and smoke checks."""

from fastapi.testclient import TestClient

from app.main import app


def test_dev_status_endpoint_available():
    client = TestClient(app)
    response = client.get("/api/v1/dev/status")
    assert response.status_code == 200
    payload = response.json()
    assert "status" in payload
    assert "backend_health" in payload
    assert "database_health" in payload
    assert "counts" in payload
    assert "projects" in payload["counts"]
    assert "critical_issues" in payload["counts"]
    assert "actions" in payload["counts"]
    assert "snapshots" in payload["counts"]
    assert "endpoint_availability" in payload
    assert "/api/v1/projects/{project_id}/requirements" in payload["endpoint_availability"]


def test_dev_smoke_test_endpoint_available():
    client = TestClient(app)
    response = client.post("/api/v1/dev/smoke-test")
    assert response.status_code == 200
    payload = response.json()
    assert payload["status"] in {"ok", "degraded"}
    assert isinstance(payload.get("checks"), list)
    endpoints = {check["endpoint"] for check in payload["checks"]}
    assert "/health" in endpoints
    assert "/api/v1/projects/{project_id}/requirements" in endpoints
