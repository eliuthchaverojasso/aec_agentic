from pathlib import Path

from fastapi.testclient import TestClient

from app.config import settings
from app.main import app


def test_register_project_files_rejects_absolute_path(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    project_dir = tmp_path / "FILE-TEST"
    (project_dir / "Specifications").mkdir(parents=True)
    (project_dir / "Specifications" / "26 05 19.pdf").write_bytes(b"%PDF-1.4\n%%EOF")

    client = TestClient(app)
    create = client.post("/api/v1/projects", json={"name": "FILE-TEST", "landing_project_folder": "FILE-TEST"})
    assert create.status_code in {200, 409}
    if create.status_code == 200:
        project_id = create.json()["id"]
    else:
        projects = client.get("/api/v1/projects").json()
        project_id = next(p["id"] for p in projects if p["project_title"] == "FILE-TEST")

    bad = client.post(
        f"/api/v1/projects/{project_id}/files/register",
        json={"files": [{"relative_path": str((project_dir / 'Specifications' / '26 05 19.pdf').resolve())}]},
    )
    assert bad.status_code == 400

