from pathlib import Path

from fastapi.testclient import TestClient

from app.config import settings
from app.main import app


def _seed_project(root: Path, name: str, with_manifest: bool = True) -> None:
    project = root / name
    (project / "Revit Exports").mkdir(parents=True, exist_ok=True)
    (project / "Drawings").mkdir(parents=True, exist_ok=True)
    (project / "Owner Requirements").mkdir(parents=True, exist_ok=True)
    (project / "Specifications").mkdir(parents=True, exist_ok=True)
    (project / "Revit Exports" / "model.json").write_text('{"ok":true}', encoding="utf-8")
    (project / "Revit Exports" / "model.meta.json").write_text("{}", encoding="utf-8")
    (project / "Drawings" / "A101.pdf").write_bytes(b"%PDF-1.4\n%%EOF")
    (project / "Owner Requirements" / "ROCKWALL ISD 03.12.2024.xlsx").write_bytes(b"xlsx")
    (project / "Specifications" / "Division-26-Electrical.pdf").write_bytes(b"%PDF-1.4\n%%EOF")
    if with_manifest:
        (project / "landing_manifest.json").write_text(
            '{"project_binding":{"project_title":"%s"},"files":[]}' % name,
            encoding="utf-8",
        )


def test_landing_projects_discovery(monkeypatch, tmp_path: Path):
    _seed_project(tmp_path, "ROCHELL ES", with_manifest=True)
    _seed_project(tmp_path, "DENTON HS", with_manifest=False)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    response = client.get("/api/v1/landing/projects")
    assert response.status_code == 200
    payload = response.json()
    assert payload["project_count"] == 2
    folders = {item["project_folder"] for item in payload["projects"]}
    assert "ROCHELL ES" in folders
    assert "DENTON HS" in folders
    rochell = next(item for item in payload["projects"] if item["project_folder"] == "ROCHELL ES")
    assert rochell["counts"]["revit_meta"] == 1
    assert rochell["counts"]["owner_requirements"] == 1


def test_rebuild_all_manifests_dry_run(monkeypatch, tmp_path: Path):
    _seed_project(tmp_path, "NORTHWEST MS 8", with_manifest=False)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    response = client.post("/api/v1/landing/rebuild-all-manifests", json={"dry_run": True})
    assert response.status_code == 200
    payload = response.json()
    assert payload["dry_run"] is True
    assert payload["project_count"] == 1
    assert payload["projects"][0]["project_folder"] == "NORTHWEST MS 8"


def test_ingest_all_dry_run_handles_missing_manifest(monkeypatch, tmp_path: Path):
    _seed_project(tmp_path, "COMP HIGH SCHOOL NO 5", with_manifest=False)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    response = client.post("/api/v1/landing/ingest-all", json={"dry_run": True})
    assert response.status_code == 200
    payload = response.json()
    assert payload["processed"] == 1
    assert payload["partial"] == 1
    assert payload["projects"][0]["status"] == "partial"
    assert "Missing landing_manifest.json" in payload["projects"][0]["errors"][0]
