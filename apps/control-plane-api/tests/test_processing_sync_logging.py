from pathlib import Path

from fastapi.testclient import TestClient

from app.config import settings
from app.main import app


def test_scan_creates_operation_log(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    project_dir = tmp_path / "LOG-PROJECT" / "Revit Exports"
    project_dir.mkdir(parents=True)
    (tmp_path / "LOG-PROJECT" / "landing_manifest.json").write_text('{"files":[]}', encoding="utf-8")

    client = TestClient(app)
    create = client.post("/api/v1/projects", json={"name": "LOG-PROJECT", "landing_project_folder": "LOG-PROJECT"})
    assert create.status_code in {200, 409}
    if create.status_code == 200:
        project_id = create.json()["id"]
    else:
        projects = client.get("/api/v1/projects").json()
        project_id = next(p["id"] for p in projects if p["project_title"] == "LOG-PROJECT")

    scan = client.post(f"/api/v1/projects/{project_id}/landing/scan")
    assert scan.status_code == 200

    logs = client.get(f"/api/v1/debug/logs?project_id={project_id}&operation_type=scan_landing")
    assert logs.status_code == 200
    assert logs.json()["count"] >= 1
