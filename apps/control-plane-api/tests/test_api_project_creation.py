from fastapi.testclient import TestClient

from app.main import app


def test_create_project_with_new_client():
    client = TestClient(app)
    response = client.post(
        "/api/v1/projects",
        json={
            "name": "NISD-MIDDLE SCHOOL",
            "project_code": "NISD-001",
            "client_code": "0024",
            "client_name": "NISD",
            "current_milestone": "DD75",
            "landing_project_folder": "NISD-MIDDLE SCHOOL",
        },
    )
    assert response.status_code in {200, 409}
    if response.status_code == 200:
        payload = response.json()
        assert payload["project_title"] == "NISD-MIDDLE SCHOOL"
        assert payload["project_code"] == "NISD-001"

