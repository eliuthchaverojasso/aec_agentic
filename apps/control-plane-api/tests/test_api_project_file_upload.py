"""Tests for POST /api/v1/projects/{project_id}/files/upload."""

from io import BytesIO
from pathlib import Path

from fastapi.testclient import TestClient

from app.config import settings
from app.main import app


def _make_project(client: TestClient, name: str) -> int:
    resp = client.post("/api/v1/projects", json={"name": name, "landing_project_folder": name})
    assert resp.status_code in {200, 409}
    if resp.status_code == 200:
        return resp.json()["id"]
    projects = client.get("/api/v1/projects").json()
    return next(p["id"] for p in projects if p["project_title"] == name)


def test_upload_project_not_found(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    c = TestClient(app)
    resp = c.post(
        "/api/v1/projects/99999/files/upload",
        data={"category": "drawings"},
        files=[("files", ("plan.pdf", BytesIO(b"%PDF"), "application/pdf"))],
    )
    assert resp.status_code == 404


def test_upload_invalid_category(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    c = TestClient(app)
    project_id = _make_project(c, "UPLOAD-INVALID-CAT")
    resp = c.post(
        f"/api/v1/projects/{project_id}/files/upload",
        data={"category": "not_a_real_category"},
        files=[("files", ("a.pdf", BytesIO(b"%PDF"), "application/pdf"))],
    )
    assert resp.status_code == 400
    assert "Invalid category" in resp.json()["detail"]


def test_upload_path_traversal_blocked(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    c = TestClient(app)
    project_id = _make_project(c, "UPLOAD-TRAVERSAL")
    resp = c.post(
        f"/api/v1/projects/{project_id}/files/upload",
        data={"category": "drawings"},
        files=[("files", ("../../evil.pdf", BytesIO(b"%PDF"), "application/pdf"))],
    )
    # Either 200 (sanitized and saved safely) or 400 — must NOT escape landing_dir
    if resp.status_code == 200:
        saved_name = resp.json()["uploaded"][0]["saved_name"]
        assert ".." not in saved_name
        saved_path = tmp_path / "UPLOAD-TRAVERSAL" / "Drawings" / saved_name
        assert saved_path.exists()
    else:
        assert resp.status_code == 400


def test_upload_one_drawing(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    c = TestClient(app)
    project_id = _make_project(c, "UPLOAD-DRAWING")
    content = b"%PDF-1.4\nSample drawing\n%%EOF"
    resp = c.post(
        f"/api/v1/projects/{project_id}/files/upload",
        data={"category": "drawings"},
        files=[("files", ("E-001 Site Plan.pdf", BytesIO(content), "application/pdf"))],
    )
    assert resp.status_code == 200
    body = resp.json()
    assert body["ok"] is True
    assert body["category"] == "drawings"
    assert body["subfolder"] == "Drawings"
    assert len(body["uploaded"]) == 1
    saved = body["uploaded"][0]
    assert saved["size_bytes"] == len(content)
    dest = tmp_path / "UPLOAD-DRAWING" / "Drawings" / saved["saved_name"]
    assert dest.exists()
    assert dest.read_bytes() == content


def test_upload_multiple_files(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    c = TestClient(app)
    project_id = _make_project(c, "UPLOAD-MULTI")
    resp = c.post(
        f"/api/v1/projects/{project_id}/files/upload",
        data={"category": "specifications"},
        files=[
            ("files", ("26 05 00.pdf", BytesIO(b"%PDF-1"), "application/pdf")),
            ("files", ("26 09 00.pdf", BytesIO(b"%PDF-2"), "application/pdf")),
            ("files", ("26 24 00.pdf", BytesIO(b"%PDF-3"), "application/pdf")),
        ],
    )
    assert resp.status_code == 200
    body = resp.json()
    assert len(body["uploaded"]) == 3
    spec_dir = tmp_path / "UPLOAD-MULTI" / "Specifications"
    assert spec_dir.is_dir()
    assert len(list(spec_dir.iterdir())) == 3


def test_upload_rebuild_manifest_called(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    c = TestClient(app)
    project_id = _make_project(c, "UPLOAD-MANIFEST")
    c.post(
        f"/api/v1/projects/{project_id}/files/upload",
        data={"category": "drawings"},
        files=[("files", ("sheet.pdf", BytesIO(b"%PDF"), "application/pdf"))],
    )
    manifest = tmp_path / "UPLOAD-MANIFEST" / "landing_manifest.json"
    assert manifest.exists(), "landing_manifest.json should be written after upload"


def test_upload_no_overwrite_collision(tmp_path: Path, monkeypatch):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    c = TestClient(app)
    project_id = _make_project(c, "UPLOAD-COLLISION")

    # Upload the same filename twice
    for i in range(2):
        c.post(
            f"/api/v1/projects/{project_id}/files/upload",
            data={"category": "drawings"},
            files=[("files", ("dup.pdf", BytesIO(f"content{i}".encode()), "application/pdf"))],
        )

    draws_dir = tmp_path / "UPLOAD-COLLISION" / "Drawings"
    saved_names = {p.name for p in draws_dir.iterdir()}
    assert "dup.pdf" in saved_names
    assert "dup_1.pdf" in saved_names
    # Both files present with distinct content
    assert (draws_dir / "dup.pdf").read_bytes() != (draws_dir / "dup_1.pdf").read_bytes()
