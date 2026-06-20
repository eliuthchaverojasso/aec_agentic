from pathlib import Path
from uuid import uuid4

from fastapi.testclient import TestClient

from app.config import settings
from app.main import app


def test_project_scoped_processing_routes_exist(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    project_dir = tmp_path / "PROC-TEST"
    (project_dir / "Revit Exports").mkdir(parents=True)
    (project_dir / "landing_manifest.json").write_text('{"files":[]}', encoding="utf-8")

    client = TestClient(app)
    create = client.post("/api/v1/projects", json={"name": "PROC-TEST", "landing_project_folder": "PROC-TEST"})
    assert create.status_code in {200, 409}
    if create.status_code == 200:
        project_id = create.json()["id"]
    else:
        projects = client.get("/api/v1/projects").json()
        project_id = next(p["id"] for p in projects if p["project_title"] == "PROC-TEST")

    status = client.get(f"/api/v1/projects/{project_id}/landing/status")
    assert status.status_code == 200
    scan = client.post(f"/api/v1/projects/{project_id}/landing/scan")
    assert scan.status_code == 200
    rebuild = client.post(f"/api/v1/projects/{project_id}/landing/rebuild-manifest")
    assert rebuild.status_code == 200
    dry = client.post(f"/api/v1/projects/{project_id}/landing/ingest/dry-run")
    assert dry.status_code in {200, 400}


def test_landing_status_folder_not_found_returns_actionable_response(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    (tmp_path / "ACTUAL-FOLDER").mkdir(parents=True)
    (tmp_path / "ACTUAL-FOLDER" / "Revit Exports").mkdir(parents=True)

    client = TestClient(app)
    create = client.post("/api/v1/projects", json={"name": "DIFFERENT-NAME", "landing_project_folder": "DIFFERENT-NAME"})
    assert create.status_code in {200, 409}
    if create.status_code == 200:
        project_id = create.json()["id"]
    else:
        projects = client.get("/api/v1/projects").json()
        project_id = next(p["id"] for p in projects if p["project_title"] == "DIFFERENT-NAME")

    status = client.get(f"/api/v1/projects/{project_id}/landing/status")
    assert status.status_code == 200
    payload = status.json()
    assert payload["folder_found"] is False
    assert payload["requested_folder"] == "DIFFERENT-NAME"
    assert payload["available_folders"] == ["ACTUAL-FOLDER"]
    assert payload["suggested_folder"] is None
    assert "Bind project" in payload["next_actions"][0] or "Bind" in payload["next_actions"][0]


def test_landing_status_folder_not_found_with_suggestion(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    for folder in ["ALPHA HS", "BETA MS", "GAMMA ES"]:
        (tmp_path / folder).mkdir(parents=True)
        (tmp_path / folder / "Revit Exports").mkdir(parents=True)

    client = TestClient(app)
    create = client.post("/api/v1/projects", json={"name": "BETA MIDDLE SCHOOL 8", "landing_project_folder": "BETA MIDDLE SCHOOL 8"})
    assert create.status_code in {200, 409}
    if create.status_code == 200:
        project_id = create.json()["id"]
    else:
        projects = client.get("/api/v1/projects").json()
        project_id = next(p["id"] for p in projects if p["project_title"] == "BETA MIDDLE SCHOOL 8")

    status = client.get(f"/api/v1/projects/{project_id}/landing/status")
    assert status.status_code == 200
    payload = status.json()
    assert payload["folder_found"] is False
    assert payload["requested_folder"] == "BETA MIDDLE SCHOOL 8"
    assert payload["suggested_folder"] == "BETA MS"


def test_landing_status_exact_folder_found_unchanged(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    project_dir = tmp_path / "EXACT-FOLDER"
    (project_dir / "Revit Exports").mkdir(parents=True)
    (project_dir / "landing_manifest.json").write_text('{"files":[]}', encoding="utf-8")

    client = TestClient(app)
    create = client.post("/api/v1/projects", json={"name": "EXACT-MATCH", "landing_project_folder": "EXACT-FOLDER"})
    assert create.status_code in {200, 409}
    if create.status_code == 200:
        project_id = create.json()["id"]
    else:
        projects = client.get("/api/v1/projects").json()
        project_id = next(p["id"] for p in projects if p["project_title"] == "EXACT-MATCH")

    status = client.get(f"/api/v1/projects/{project_id}/landing/status")
    assert status.status_code == 200
    payload = status.json()
    assert payload["folder_found"] is True
    assert payload["project_landing_path"] is not None
    assert "Revit Exports" in payload.get("folder_status", {})


def test_landing_status_folder_found_after_explicit_binding(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    unique_suffix = uuid4().hex[:8]
    real_folder = f"REAL-LANDING-FOLDER-{unique_suffix}"
    display_name = f"DISPLAY-NAME-{unique_suffix}"
    (tmp_path / real_folder).mkdir(parents=True)
    (tmp_path / real_folder / "Revit Exports").mkdir(parents=True)
    (tmp_path / real_folder / "landing_manifest.json").write_text('{"files":[]}', encoding="utf-8")

    client = TestClient(app)
    create = client.post("/api/v1/projects", json={"name": display_name, "landing_project_folder": display_name})
    assert create.status_code in {200, 409}
    if create.status_code == 200:
        project_id = create.json()["id"]
    else:
        projects = client.get("/api/v1/projects").json()
        project_id = next(p["id"] for p in projects if p["project_title"] == display_name)

    status_before = client.get(f"/api/v1/projects/{project_id}/landing/status")
    assert status_before.json()["folder_found"] is False

    configure = client.post(
        f"/api/v1/projects/{project_id}/landing/configure",
        json={
            "landing_root": str(tmp_path),
            "project_folder_name": real_folder,
            "create_folders": False,
        },
    )
    assert configure.status_code == 200

    status_after = client.get(f"/api/v1/projects/{project_id}/landing/status")
    assert status_after.status_code == 200
    payload = status_after.json()
    assert payload["folder_found"] is True
    assert payload["project_folder_name"] == real_folder
