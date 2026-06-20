from pathlib import Path

from fastapi.testclient import TestClient

from app.config import settings
from app.main import app


def test_legacy_landing_endpoints_still_available(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    project_dir = tmp_path / "LEGACY-LANDING"
    project_dir.mkdir(parents=True)
    (project_dir / "landing_manifest.json").write_text('{"files":[]}', encoding="utf-8")

    client = TestClient(app)
    scan = client.post("/api/v1/landing/scan", json={"project_folder": "LEGACY-LANDING", "dry_run": True})
    assert scan.status_code == 200
    rebuild = client.post("/api/v1/landing/rebuild-manifest", json={"project_folder": "LEGACY-LANDING", "dry_run": True})
    assert rebuild.status_code == 200
    ingest = client.post("/api/v1/landing/ingest", json={"manifest_path": "LEGACY-LANDING/landing_manifest.json", "dry_run": True})
    assert ingest.status_code == 200

