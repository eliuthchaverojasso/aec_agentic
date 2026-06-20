import json
from pathlib import Path

from app.config import settings
from app.ingestion.landing_scan_service import rebuild_project_manifest, scan_landing


def _write_fixture_tree(root: Path) -> None:
    project = root / "TEST PROJECT"
    (project / "Drawings").mkdir(parents=True)
    (project / "Specifications").mkdir()
    (project / "Owner Requirements").mkdir()
    (project / "Revit Exports").mkdir()
    (project / "processed").mkdir()
    (project / "Drawings" / "E-001 Electrical Site Plan.pdf").write_bytes(b"%PDF-1.4\n%%EOF")
    (project / "Specifications" / "26 05 00 Electrical.pdf").write_bytes(b"%PDF-1.4\n%%EOF")
    (project / "Owner Requirements" / "Owner Requirements.xlsx").write_bytes(b"synthetic xlsx bytes")
    (project / "Revit Exports" / "ema_extract_all.meta.json").write_text("{}", encoding="utf-8")
    (project / "Revit Exports" / "auxiliary.json").write_text('{"hello": "world"}', encoding="utf-8")
    (project / "processed" / "ignored.pdf").write_bytes(b"%PDF-1.4\n%%EOF")


def test_scan_project_folder_finds_documents_and_ignores_generated(monkeypatch, tmp_path: Path):
    _write_fixture_tree(tmp_path)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)

    report = scan_landing(project_folder="TEST PROJECT", dry_run=True, include_pdf_metadata=False)

    paths = {document.path for document in report.documents}
    assert report.status == "success"
    assert "TEST PROJECT/Drawings/E-001 Electrical Site Plan.pdf" in paths
    assert "TEST PROJECT/Specifications/26 05 00 Electrical.pdf" in paths
    assert "TEST PROJECT/Revit Exports/ema_extract_all.meta.json" not in paths
    assert all("processed" not in path for path in paths)


def test_dry_run_manifest_update_does_not_write(monkeypatch, tmp_path: Path):
    _write_fixture_tree(tmp_path)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)

    report = scan_landing(project_folder="TEST PROJECT", update_manifest=True, dry_run=True)

    assert report.manifest_updated is False
    assert not (tmp_path / "TEST PROJECT" / "landing_manifest.json").exists()


def test_rebuild_manifest_writes_relative_paths(monkeypatch, tmp_path: Path):
    _write_fixture_tree(tmp_path)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)

    report = rebuild_project_manifest(project_folder="TEST PROJECT", dry_run=False, include_pdf_metadata=False)

    manifest_path = tmp_path / "TEST PROJECT" / "landing_manifest.json"
    payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    assert report.manifest_updated is True
    assert payload["files"]
    assert all(not Path(item["path"]).is_absolute() for item in payload["files"])


def test_scan_rejects_path_traversal(monkeypatch, tmp_path: Path):
    monkeypatch.setattr(settings, "landing_dir", tmp_path)

    report = scan_landing(project_folder="../outside")

    assert report.status == "failed"
    assert report.errors
