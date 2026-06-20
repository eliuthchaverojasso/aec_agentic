from fastapi.testclient import TestClient

from app.main import app


def test_debug_environment_endpoint():
    client = TestClient(app)
    response = client.get("/api/v1/debug/environment")
    assert response.status_code == 200
    payload = response.json()
    assert "landing_dir" in payload
    assert "database_url_redacted" in payload


def test_debug_pipeline_state_endpoint():
    client = TestClient(app)
    response = client.get("/api/v1/debug/pipeline-state")
    assert response.status_code == 200
    payload = response.json()
    assert "summary" in payload
