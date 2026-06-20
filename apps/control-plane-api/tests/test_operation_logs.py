from fastapi.testclient import TestClient

from app.main import app


def test_debug_logs_summary_endpoint():
    client = TestClient(app)
    response = client.get("/api/v1/debug/logs/summary")
    assert response.status_code == 200
    payload = response.json()
    assert "total" in payload
    assert "errors" in payload
    assert "warnings" in payload


def test_frontend_debug_log_creation():
    client = TestClient(app)
    response = client.post(
        "/api/v1/debug/logs/frontend",
        json={
            "action": "test_frontend_action",
            "route": "/debug/logs",
            "severity": "info",
            "status": "success",
            "message": "test event",
        },
    )
    assert response.status_code == 200
    payload = response.json()
    assert payload["ok"] is True
    assert payload["log_id"] > 0
