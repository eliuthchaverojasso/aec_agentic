import json
from pathlib import Path

from fastapi.testclient import TestClient

from app.config import settings
from app.main import app


def test_bootstrap_from_folder(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    project_dir = tmp_path / "NISD-MIDDLE SCHOOL"
    (project_dir / "Revit Exports").mkdir(parents=True)
    (project_dir / "Drawings").mkdir()
    (project_dir / "Specifications").mkdir()
    (project_dir / "Revit Exports" / "sample.json").write_text(json.dumps({"elements": []}), encoding="utf-8")
    (project_dir / "Drawings" / "E001.pdf").write_bytes(b"%PDF-1.4\n%%EOF")

    client = TestClient(app)
    discover = client.post("/api/v1/landing/projects/discover", json={"landing_root": str(tmp_path)})
    assert discover.status_code == 200
    assert any(p["project_folder_name"] == "NISD-MIDDLE SCHOOL" for p in discover.json()["projects"])

    bootstrap = client.post(
        "/api/v1/landing/projects/bootstrap-from-folder",
        json={
            "landing_root": str(tmp_path),
            "project_folder_name": "NISD-MIDDLE SCHOOL",
            "project_display_name": "NISD-MIDDLE SCHOOL",
            "project_code": "NISD-001",
            "client_code": "0024",
            "client_name": "NISD",
            "environment": "Local",
        },
    )
    assert bootstrap.status_code == 200
    payload = bootstrap.json()
    assert payload["project_id"] > 0
    projects = client.get("/api/v1/projects")
    assert projects.status_code == 200
    assert any(p["id"] == payload["project_id"] for p in projects.json())

