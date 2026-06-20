from pathlib import Path

from fastapi.testclient import TestClient

from app.config import settings
from app.main import app


def test_project_landing_config_rejects_traversal(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)
    create = client.post("/api/v1/projects", json={"name": "LANDING-CONFIG-PROJECT"})
    assert create.status_code in {200, 409}
    if create.status_code == 200:
        project_id = create.json()["id"]
    else:
        projects = client.get("/api/v1/projects").json()
        project_id = next(p["id"] for p in projects if p["project_title"] == "LANDING-CONFIG-PROJECT")

    bad = client.post(
        f"/api/v1/projects/{project_id}/landing/configure",
        json={
            "landing_root": str(tmp_path),
            "project_folder_name": "../bad",
            "create_folders": True,
        },
    )
    assert bad.status_code == 400

