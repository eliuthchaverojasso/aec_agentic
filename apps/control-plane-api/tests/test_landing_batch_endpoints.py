import json
from pathlib import Path

from fastapi.testclient import TestClient
from openpyxl import Workbook

from app.config import settings
from app.main import app


REQ_HEADERS = [
    "STATUS",
    "DISCIPLINE",
    "REQUIREMENT",
    "LINKS",
    "CATEGORY LIST",
    "DATE UPDATED",
    "MODIFIED BY",
    "RESOURCE",
    "Item Type",
    "Path",
]


def _write_requirements_xlsx(path: Path) -> None:
    """Create a minimal valid Owner Requirements XLSX."""
    wb = Workbook()
    ws = wb.active
    ws.append(REQ_HEADERS)
    ws.append(["DONE", "ELECTRICAL", "All panels labeled per NEC", None, "Safety", None, None, "Owner matrix", None, None])
    ws.append(["DONE", "MECHANICAL", "HVAC load calc provided", None, "Comfort", None, None, "Owner matrix", None, None])
    wb.save(path)
    wb.close()


def _seed_project_with_owner_reqs(root: Path, name: str) -> None:
    """Seed a project with a valid owner requirements XLSX and manifest entry (no client_code)."""
    project = root / name
    (project / "Drawings").mkdir(parents=True, exist_ok=True)
    (project / "Owner Requirements").mkdir(parents=True, exist_ok=True)
    (project / "Revit Exports").mkdir(parents=True, exist_ok=True)
    (project / "Specifications").mkdir(parents=True, exist_ok=True)
    (project / "Drawings" / "S001.pdf").write_bytes(b"%PDF-1.4\n%%EOF")
    (project / "Specifications" / "spec.pdf").write_bytes(b"%PDF-1.4\n%%EOF")
    (project / "Revit Exports" / "model.json").write_text('{"elements":[]}', encoding="utf-8")
    (project / "Revit Exports" / "model.meta.json").write_text('{}', encoding="utf-8")
    _write_requirements_xlsx(project / "Owner Requirements" / "req.xlsx")
    (project / "landing_manifest.json").write_text(
        json.dumps({
            "project_binding": {"project_title": name},
            "files": [
                {"path": f"{name}/Drawings/S001.pdf", "type": "drawing_pdf"},
                {"path": f"{name}/Specifications/spec.pdf", "type": "specification_pdf"},
                {"path": f"{name}/Owner Requirements/req.xlsx", "type": "owner_requirements"},
            ],
        }),
        encoding="utf-8",
    )


def _seed_project(root: Path, name: str, with_manifest: bool = True, with_revit: bool = True) -> None:
    project = root / name
    (project / "Revit Exports").mkdir(parents=True, exist_ok=True)
    (project / "Drawings").mkdir(parents=True, exist_ok=True)
    (project / "Owner Requirements").mkdir(parents=True, exist_ok=True)
    (project / "Specifications").mkdir(parents=True, exist_ok=True)
    if with_revit:
        (project / "Revit Exports" / "model.json").write_text('{"elements":[]}', encoding="utf-8")
        (project / "Revit Exports" / "model.meta.json").write_text('{}', encoding="utf-8")
    (project / "Drawings" / "E001.pdf").write_bytes(b"%PDF-1.4\n%%EOF")
    (project / "Owner Requirements" / "req.xlsx").write_bytes(b"xlsx")
    (project / "Specifications" / "spec.pdf").write_bytes(b"%PDF-1.4\n%%EOF")
    if with_manifest:
        (project / "landing_manifest.json").write_text(
            json.dumps({
                "project_binding": {"project_title": name},
                "files": [
                    {"path": f"{name}/Drawings/E001.pdf", "type": "drawing_pdf"},
                    {"path": f"{name}/Specifications/spec.pdf", "type": "specification_pdf"},
                ],
            }),
            encoding="utf-8",
        )


def _seed_manifest(root: Path, name: str) -> None:
    project = root / name
    project.mkdir(parents=True, exist_ok=True)
    (project / "landing_manifest.json").write_text(
        json.dumps({
            "project_binding": {"project_title": name},
            "files": [
                {"path": f"{name}/drawing.pdf", "type": "drawing_pdf"},
            ],
        }),
        encoding="utf-8",
    )
    (project / "drawing.pdf").write_bytes(b"%PDF-1.4\n%%EOF")


def test_landing_projects_includes_status_and_counts(monkeypatch, tmp_path: Path):
    """Verify the full LandingProjectDiscoveryResponse shape used by ExecutiveOverviewPage."""
    _seed_project(tmp_path, "ALPHA", with_manifest=True, with_revit=True)
    _seed_project(tmp_path, "BETA", with_manifest=False, with_revit=False)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    response = client.get("/api/v1/landing/projects")
    assert response.status_code == 200
    payload = response.json()

    assert payload["project_count"] == 2
    assert "project_count" in payload
    assert "totals" in payload
    assert "projects" in payload
    assert isinstance(payload["projects"], list)
    assert len(payload["projects"]) == 2

    alpha = next(p for p in payload["projects"] if p["project_folder"] == "ALPHA")
    assert alpha["status"] in ("ready", "needs_client_binding")
    assert alpha["counts"]["revit_exports"] == 1
    assert alpha["counts"]["revit_meta"] == 1
    assert alpha["counts"]["drawings"] == 1
    assert alpha["counts"]["owner_requirements"] == 1
    assert alpha["counts"]["specifications"] == 1

    beta = next(p for p in payload["projects"] if p["project_folder"] == "BETA")
    assert beta["status"] in ("needs_manifest", "empty")
    assert beta["project_id"] is None
    assert beta["client_id"] is None
    assert beta["client_suggestion"] is not None


def test_rebuild_all_manifests_real_mode(monkeypatch, tmp_path: Path):
    """Verify rebuild-all-manifests with dry_run=False writes actual manifest files."""
    _seed_project(tmp_path, "PROJ A", with_manifest=False, with_revit=True)
    _seed_project(tmp_path, "PROJ B", with_manifest=False, with_revit=False)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    response = client.post("/api/v1/landing/rebuild-all-manifests", json={"dry_run": False})
    assert response.status_code == 200
    payload = response.json()

    assert payload["dry_run"] is False
    assert payload["project_count"] == 2
    assert (tmp_path / "PROJ A" / "landing_manifest.json").exists()
    assert (tmp_path / "PROJ B" / "landing_manifest.json").exists()

    manifest_a = json.loads((tmp_path / "PROJ A" / "landing_manifest.json").read_text(encoding="utf-8"))
    assert len(manifest_a["files"]) > 0
    manifest_b = json.loads((tmp_path / "PROJ B" / "landing_manifest.json").read_text(encoding="utf-8"))
    assert isinstance(manifest_b["files"], list)
    assert len(manifest_b["files"]) >= 0



def test_ingest_all_mixed_results(monkeypatch, tmp_path: Path):
    """Verify ingest-all dry-run handles mixed success, partial, and failed projects."""
    _seed_project(tmp_path, "GOOD", with_manifest=True, with_revit=True)
    _seed_project(tmp_path, "NO_MANIFEST", with_manifest=False, with_revit=True)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    # require_client_for_owner_requirements=False because no DB binding exists for these projects
    response = client.post("/api/v1/landing/ingest-all", json={"dry_run": True, "require_client_for_owner_requirements": False})
    assert response.status_code == 200
    payload = response.json()

    assert payload["project_count"] == 2
    assert payload["processed"] == 2
    assert payload["success"] == 1
    assert payload["partial"] == 1
    assert payload["failed"] == 0

    good = next(p for p in payload["projects"] if p["project_folder"] == "GOOD")
    assert good["status"] == "success"

    partial = next(p for p in payload["projects"] if p["project_folder"] == "NO_MANIFEST")
    assert partial["status"] == "partial"
    assert any("Missing landing_manifest.json" in e for e in partial["errors"])


def test_bind_landing_project(monkeypatch, tmp_path: Path):
    """Verify POST /api/v1/landing/projects/{folder}/bind returns correct response shape."""
    _seed_project(tmp_path, "BIND-ME", with_manifest=True)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    response = client.post(
        "/api/v1/landing/projects/BIND-ME/bind",
        json={"create_project": True},
    )
    assert response.status_code == 200
    payload = response.json()

    assert payload["project_folder"] == "BIND-ME"
    assert payload["project_id"] > 0
    assert payload["project_name"] == "BIND-ME"
    assert payload["client_id"] is not None or payload["client_id"] is None
    assert "client_name" in payload
    assert "client_code" in payload
    assert payload["status"] in ("ready", "needs_manifest", "in_progress")
    assert isinstance(payload["warnings"], list)
    assert isinstance(payload["errors"], list)
    assert isinstance(payload["next_actions"], list)


def test_bind_landing_project_with_custom_client(monkeypatch, tmp_path: Path):
    """Verify bind with custom client code/name binds correctly."""
    _seed_project(tmp_path, "CUSTOM-CLIENT", with_manifest=True)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    response = client.post(
        "/api/v1/landing/projects/CUSTOM-CLIENT/bind",
        json={
            "create_project": True,
            "client_code": "C001",
            "client_name": "Custom Client Inc",
        },
    )
    assert response.status_code == 200
    payload = response.json()

    assert payload["client_code"] == "C001"
    assert payload["client_name"] == "Custom Client Inc"


def test_bind_landing_project_create_on_missing(monkeypatch, tmp_path: Path):
    """Verify bind with create_project=True creates a project for a nonexistent folder."""
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    response = client.post(
        "/api/v1/landing/projects/NEW-PROJECT/bind",
        json={"create_project": True, "client_code": "C002", "client_name": "New Client"},
    )
    assert response.status_code == 200
    payload = response.json()
    assert payload["project_folder"] == "NEW-PROJECT"
    assert payload["project_id"] > 0
    assert payload["client_code"] == "C002"


def test_ingest_all_respects_require_client_for_owner_req(monkeypatch, tmp_path: Path):
    """Verify require_client_for_owner_requirements flag causes partial for unbound projects."""
    _seed_project(tmp_path, "UNBOUND", with_manifest=True, with_revit=True)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    response = client.post(
        "/api/v1/landing/ingest-all",
        json={"dry_run": True, "require_client_for_owner_requirements": True},
    )
    assert response.status_code == 200
    payload = response.json()

    unbound = next(p for p in payload["projects"] if p["project_folder"] == "UNBOUND")
    assert unbound["status"] == "partial"


def test_ingest_all_selective_projects(monkeypatch, tmp_path: Path):
    """Verify ingest-all with project_folders filter only processes specified projects."""
    _seed_project(tmp_path, "PROJ A", with_manifest=True)
    _seed_project(tmp_path, "PROJ B", with_manifest=True)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    response = client.post(
        "/api/v1/landing/ingest-all",
        json={"dry_run": True, "project_folders": ["PROJ A"]},
    )
    assert response.status_code == 200
    payload = response.json()

    assert payload["project_count"] == 1
    assert payload["projects"][0]["project_folder"] == "PROJ A"


def test_ingest_all_owner_req_fallback_via_bind(monkeypatch, tmp_path: Path):
    """Bind project then ingest-all — fallback finds client via project.client_id."""
    _seed_project_with_owner_reqs(tmp_path, "FALLBACK-PROJ")
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    bind_resp = client.post(
        "/api/v1/landing/projects/FALLBACK-PROJ/bind",
        json={"create_project": True, "client_code": "FBC001", "client_name": "Fallback Client"},
    )
    assert bind_resp.status_code == 200
    bind_data = bind_resp.json()
    assert bind_data["project_id"] > 0
    assert bind_data["client_code"] == "FBC001"

    ingest_resp = client.post(
        "/api/v1/landing/ingest-all",
        json={"dry_run": True, "require_client_for_owner_requirements": True},
    )
    assert ingest_resp.status_code == 200
    payload = ingest_resp.json()

    fallback = next(p for p in payload["projects"] if p["project_folder"] == "FALLBACK-PROJ")
    assert fallback["status"] == "success", f"Expected success, got {fallback['status']}: {fallback['errors']}"


def test_ingest_all_owner_req_dry_run_then_real(monkeypatch, tmp_path: Path):
    """Dry-run then real ingest of owner requirements should both succeed with fallback."""
    _seed_project_with_owner_reqs(tmp_path, "DRY-REAL")
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    bind_resp = client.post(
        "/api/v1/landing/projects/DRY-REAL/bind",
        json={"create_project": True, "client_code": "DRY001", "client_name": "Dry Run Client"},
    )
    assert bind_resp.status_code == 200

    dry_resp = client.post(
        "/api/v1/landing/ingest-all",
        json={"dry_run": True, "require_client_for_owner_requirements": True},
    )
    assert dry_resp.status_code == 200
    dry_data = dry_resp.json()
    dry_proj = next(p for p in dry_data["projects"] if p["project_folder"] == "DRY-REAL")
    assert dry_proj["status"] == "success", f"Dry run failed: {dry_proj}"

    real_resp = client.post(
        "/api/v1/landing/ingest-all",
        json={"dry_run": False, "require_client_for_owner_requirements": True},
    )
    assert real_resp.status_code == 200
    real_data = real_resp.json()
    real_proj = next(p for p in real_data["projects"] if p["project_folder"] == "DRY-REAL")
    assert real_proj["status"] == "success", f"Real ingest failed: {real_proj}"
    assert real_data["success"] >= 1


def test_ingest_all_owner_req_idempotent_repeat(monkeypatch, tmp_path: Path):
    """Repeated real ingest should not duplicate owner requirements."""
    _seed_project_with_owner_reqs(tmp_path, "IDEM-PROJ")
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    bind_resp = client.post(
        "/api/v1/landing/projects/IDEM-PROJ/bind",
        json={"create_project": True, "client_code": "IDEM01", "client_name": "Idempotent Client"},
    )
    assert bind_resp.status_code == 200
    project_id = bind_resp.json()["project_id"]

    resp1 = client.post(
        "/api/v1/landing/ingest-all",
        json={"dry_run": False, "require_client_for_owner_requirements": True},
    )
    assert resp1.status_code == 200
    p1 = next(p for p in resp1.json()["projects"] if p["project_folder"] == "IDEM-PROJ")
    assert p1["status"] == "success", f"First ingest failed: {p1}"

    resp2 = client.post(
        "/api/v1/landing/ingest-all",
        json={"dry_run": False, "require_client_for_owner_requirements": True},
    )
    assert resp2.status_code == 200
    p2 = next(p for p in resp2.json()["projects"] if p["project_folder"] == "IDEM-PROJ")
    assert p2["status"] == "success", f"Second ingest failed: {p2}"

    reqs_resp = client.get(f"/api/v1/projects/{project_id}/requirements")
    assert reqs_resp.status_code == 200
    reqs_data = reqs_resp.json()
    assert reqs_data["counts"]["total"] == 2, f"Expected 2, got {reqs_data['counts']['total']}"


def test_ingest_all_owner_req_rejected_no_bind(monkeypatch, tmp_path: Path):
    """require_client_for_owner_requirements=True with no bind should mark partial."""
    _seed_project_with_owner_reqs(tmp_path, "NO-BIND")
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    response = client.post(
        "/api/v1/landing/ingest-all",
        json={"dry_run": True, "require_client_for_owner_requirements": True},
    )
    assert response.status_code == 200
    payload = response.json()

    nobind = next(p for p in payload["projects"] if p["project_folder"] == "NO-BIND")
    assert nobind["status"] == "partial"


def test_ingest_all_owner_req_no_entry_client_code_but_project_bound(monkeypatch, tmp_path: Path):
    """No entry client_code but project has client_id via bind — proceeds."""
    _seed_project_with_owner_reqs(tmp_path, "BOUND-FALLBACK")
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    bind_resp = client.post(
        "/api/v1/landing/projects/BOUND-FALLBACK/bind",
        json={"create_project": True, "client_code": "BF001", "client_name": "Bound Fallback Client"},
    )
    assert bind_resp.status_code == 200

    ingest_resp = client.post(
        "/api/v1/landing/ingest-all",
        json={"dry_run": True, "require_client_for_owner_requirements": True},
    )
    assert ingest_resp.status_code == 200
    payload = ingest_resp.json()

    bound = next(p for p in payload["projects"] if p["project_folder"] == "BOUND-FALLBACK")
    assert bound["status"] == "success", f"Expected success, got {bound['status']}: {bound['errors']}"


def test_ingest_all_owner_req_blocked_no_binding(monkeypatch, tmp_path: Path):
    """No binding at all — blocked with actionable message."""
    _seed_project_with_owner_reqs(tmp_path, "BLOCKED-NO-BIND")
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    response = client.post(
        "/api/v1/landing/ingest-all",
        json={"dry_run": True, "require_client_for_owner_requirements": True},
    )
    assert response.status_code == 200
    payload = response.json()

    blocked = next(p for p in payload["projects"] if p["project_folder"] == "BLOCKED-NO-BIND")
    assert blocked["status"] == "partial"
    assert any("client_code" in e for e in blocked["errors"]), f"Expected actionable error, got {blocked['errors']}"


def test_ingest_all_owner_req_mixed_continues_after_blocked(monkeypatch, tmp_path: Path):
    """One project blocked, another proceeds — mixed results."""
    _seed_project_with_owner_reqs(tmp_path, "BLOCKED-PROJ")
    _seed_project_with_owner_reqs(tmp_path, "GOOD-PROJ")
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    bind_resp = client.post(
        "/api/v1/landing/projects/GOOD-PROJ/bind",
        json={"create_project": True, "client_code": "GP001", "client_name": "Good Project Client"},
    )
    assert bind_resp.status_code == 200

    ingest_resp = client.post(
        "/api/v1/landing/ingest-all",
        json={"dry_run": True, "require_client_for_owner_requirements": True},
    )
    assert ingest_resp.status_code == 200
    payload = ingest_resp.json()

    assert payload["project_count"] == 2
    assert payload["partial"] >= 1
    assert payload["success"] >= 1

    blocked = next(p for p in payload["projects"] if p["project_folder"] == "BLOCKED-PROJ")
    assert blocked["status"] == "partial"
    good = next(p for p in payload["projects"] if p["project_folder"] == "GOOD-PROJ")
    assert good["status"] == "success", f"Expected success for GOOD-PROJ, got {good['status']}: {good['errors']}"


def test_ingest_all_owner_req_dry_run_fallback_warning(monkeypatch, tmp_path: Path):
    """Dry-run ingest shows fallback warning in per-project warnings."""
    _seed_project_with_owner_reqs(tmp_path, "DRY-WARN")
    monkeypatch.setattr(settings, "landing_dir", tmp_path)
    client = TestClient(app)

    bind_resp = client.post(
        "/api/v1/landing/projects/DRY-WARN/bind",
        json={"create_project": True, "client_code": "DW001", "client_name": "Dry Warn Client"},
    )
    assert bind_resp.status_code == 200

    ingest_resp = client.post(
        "/api/v1/landing/ingest-all",
        json={"dry_run": True, "require_client_for_owner_requirements": True},
    )
    assert ingest_resp.status_code == 200
    payload = ingest_resp.json()

    dry = next(p for p in payload["projects"] if p["project_folder"] == "DRY-WARN")
    assert dry["status"] == "success"
    has_fallback_warning = any("client binding" in w.lower() for w in dry["warnings"])
    assert has_fallback_warning, f"Expected fallback warning, got warnings: {dry['warnings']}"
