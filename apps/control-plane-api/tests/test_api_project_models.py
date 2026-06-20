from fastapi.testclient import TestClient

from app.main import app


def test_create_project_model_endpoint():
    client = TestClient(app)
    create = client.post(
        "/api/v1/projects",
        json={
            "name": "MODEL-TEST-PROJECT",
            "client_code": "MODEL-CLIENT",
            "client_name": "Model Client",
        },
    )
    if create.status_code == 409:
        projects = client.get("/api/v1/projects").json()
        project_id = next(p["id"] for p in projects if p["project_title"] == "MODEL-TEST-PROJECT")
    else:
        assert create.status_code == 200
        project_id = create.json()["id"]

    response = client.post(
        f"/api/v1/projects/{project_id}/models",
        json={
            "model_name": "MEP-NISD-MIDDLE SCHOOL 8",
            "model_type": "Revit",
            "discipline": "MEP",
            "source_system": "Revit",
        },
    )
    assert response.status_code == 200
    payload = response.json()
    assert payload["project_id"] == project_id
    assert payload["model_name"] == "MEP-NISD-MIDDLE SCHOOL 8"

