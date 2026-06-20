import json
from pathlib import Path

import pytest
from sqlalchemy import BigInteger, create_engine
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.ext.compiler import compiles
from sqlalchemy.orm import Session

from app.config import settings
from app.ingestion.landing_service import ingest_landing_manifest
from app.models import Base, Export, LandingDocument, Model, Organization, Project


@compiles(JSONB, "sqlite")
def _compile_jsonb_for_sqlite(type_, compiler, **kw):
    return "JSON"


@compiles(BigInteger, "sqlite")
def _compile_bigint_for_sqlite(type_, compiler, **kw):
    return "INTEGER"


_VALID_REVIT_ELEMENTS: list[dict] = [
    {
        "ProjectTitle": "TEST PROJECT",
        "UniqueId": "aaaa-0000",
        "ElementId": 12345,
        "Category": "Walls",
        "Name": "Basic Wall",
    },
    {
        "UniqueId": "bbbb-0000",
        "ElementId": 67890,
        "Category": "Doors",
        "Name": "Single Flush",
    },
]


@pytest.fixture
def db_session():
    engine = create_engine("sqlite+pysqlite:///:memory:")
    Base.metadata.create_all(engine)
    with Session(engine) as session:
        org = Organization(id=1, name="EMA Engineering")
        project = Project(id=1, organization_id=1, project_title="TEST PROJECT")
        session.add_all([org, project])
        session.commit()
        yield session
    Base.metadata.drop_all(engine)


def test_manifest_with_pdf_documents_indexes_records(monkeypatch, tmp_path: Path, db_session: Session):
    project = tmp_path / "TEST PROJECT"
    (project / "Drawings").mkdir(parents=True)
    (project / "Specifications").mkdir()
    (project / "Drawings" / "E-001 Electrical.pdf").write_bytes(b"%PDF-1.4\n%%EOF")
    (project / "Specifications" / "26 05 00 Electrical.pdf").write_bytes(b"%PDF-1.4\n%%EOF")
    (project / "landing_manifest.json").write_text(
        json.dumps(
            {
                "project_binding": {"project_title": "TEST PROJECT"},
                "files": [
                    {"path": "TEST PROJECT/Drawings/E-001 Electrical.pdf", "type": "drawing_pdf"},
                    {"path": "TEST PROJECT/Specifications/26 05 00 Electrical.pdf", "type": "specification_pdf"},
                ],
            }
        ),
        encoding="utf-8",
    )
    monkeypatch.setattr(settings, "landing_dir", tmp_path)

    report = ingest_landing_manifest(
        db=db_session,
        manifest_path="TEST PROJECT/landing_manifest.json",
        dry_run=False,
        recalculate_readiness=False,
    )

    rows = db_session.query(LandingDocument).order_by(LandingDocument.file_name).all()
    assert report.status in {"completed", "completed_with_warnings"}
    assert {row.file_type for row in rows} == {"drawing_pdf", "specification_pdf"}
    assert rows[0].project_id == 1
    assert all(row.evidence_status == "candidate" for row in rows)


def test_optional_missing_pdf_warns_required_missing_errors(monkeypatch, tmp_path: Path, db_session: Session):
    project = tmp_path / "TEST PROJECT"
    project.mkdir()
    (project / "landing_manifest.json").write_text(
        json.dumps(
            {
                "files": [
                    {"path": "TEST PROJECT/optional.pdf", "type": "drawing_pdf", "required": False},
                    {"path": "TEST PROJECT/required.pdf", "type": "drawing_pdf", "required": True},
                ]
            }
        ),
        encoding="utf-8",
    )
    monkeypatch.setattr(settings, "landing_dir", tmp_path)

    report = ingest_landing_manifest(
        db=db_session,
        manifest_path="TEST PROJECT/landing_manifest.json",
        dry_run=False,
        recalculate_readiness=False,
    )

    assert report.status == "failed"
    assert "TEST PROJECT/optional.pdf" in report.skipped
    assert report.errors


def test_sidecar_meta_json_not_ingested_as_document(monkeypatch, tmp_path: Path, db_session: Session):
    project = tmp_path / "TEST PROJECT"
    (project / "Revit Exports").mkdir(parents=True)
    (project / "Revit Exports" / "export.meta.json").write_text("{}", encoding="utf-8")
    (project / "landing_manifest.json").write_text(
        json.dumps({"files": [{"path": "TEST PROJECT/Revit Exports/export.meta.json", "type": "unknown"}]}),
        encoding="utf-8",
    )
    monkeypatch.setattr(settings, "landing_dir", tmp_path)

    report = ingest_landing_manifest(
        db=db_session,
        manifest_path="TEST PROJECT/landing_manifest.json",
        dry_run=False,
        recalculate_readiness=False,
    )

    assert report.files[0].status == "skipped"
    assert db_session.query(LandingDocument).count() == 0


def _create_revit_export_json(path: Path, revit_version: str | None = None, project_title: str | None = None) -> None:
    """Write a valid revit export JSON with at least one element."""
    title = project_title or "TEST PROJECT"
    if revit_version:
        title = f"1 082 0079 000 {title} {revit_version}"
    elements = list(_VALID_REVIT_ELEMENTS)
    elements[0]["ProjectTitle"] = title
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(elements), encoding="utf-8")


def _create_revit_manifest(tmp_path: Path, project_name: str, project_title: str | None = None) -> Path:
    """Create a manifest with a revit_export entry."""
    title = project_title or "TEST PROJECT"
    manifest = tmp_path / project_name / "landing_manifest.json"
    manifest.write_text(
        json.dumps({
            "project_binding": {"project_title": title},
            "files": [
                {"path": f"{project_name}/Revit Exports/model.json", "type": "revit_export"},
            ],
        }),
        encoding="utf-8",
    )
    return manifest


def test_revit_export_reingest_idempotent(monkeypatch, tmp_path: Path, db_session: Session):
    """Ingesting the same revit export JSON twice must not create duplicate model rows."""
    project_name = "TEST PROJECT"
    export_path = tmp_path / project_name / "Revit Exports" / "model.json"
    _create_revit_export_json(export_path)
    _create_revit_manifest(tmp_path, project_name)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)

    # First ingest
    report1 = ingest_landing_manifest(
        db=db_session,
        manifest_path=f"{project_name}/landing_manifest.json",
        dry_run=False,
        recalculate_readiness=False,
    )
    assert report1.status in {"completed", "completed_with_warnings"}

    model_count_after_first = db_session.query(Model).count()
    assert model_count_after_first == 1, "First ingest must create exactly 1 model"

    # Second ingest — same file, same manifest
    report2 = ingest_landing_manifest(
        db=db_session,
        manifest_path=f"{project_name}/landing_manifest.json",
        dry_run=False,
        recalculate_readiness=False,
    )
    assert report2.status in {"completed", "completed_with_warnings"}

    model_count_after_second = db_session.query(Model).count()
    assert model_count_after_second == 1, "Second ingest must NOT create a duplicate model"

    # Two export rows should exist (one per ingest)
    export_count = db_session.query(Export).count()
    assert export_count == 2, "Each ingest creates a new Export row"


def test_revit_export_reingest_updates_revit_version(monkeypatch, tmp_path: Path, db_session: Session):
    """Re-ingesting must update model revit_version and last_sync_at."""
    project_name = "TEST PROJECT"
    export_path = tmp_path / project_name / "Revit Exports" / "model.json"
    _create_revit_export_json(export_path)
    _create_revit_manifest(tmp_path, project_name)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)

    # First ingest
    r1 = ingest_landing_manifest(
        db=db_session,
        manifest_path=f"{project_name}/landing_manifest.json",
        dry_run=False,
        recalculate_readiness=False,
    )

    # Find the model by known unique constraint fields
    project = db_session.query(Project).filter_by(project_title="TEST PROJECT").first()
    assert project is not None, "Fixture project must exist"
    models = db_session.query(Model).filter_by(project_id=project.id).all()
    assert len(models) >= 1, f"Expected at least 1 model for project {project.id}, got {len(models)}"
    model = models[0]
    assert model.last_sync_at is not None, "last_sync_at must be set after first ingest"
    before = model.last_sync_at

    # Second ingest — same file, same project
    r2 = ingest_landing_manifest(
        db=db_session,
        manifest_path=f"{project_name}/landing_manifest.json",
        dry_run=False,
        recalculate_readiness=False,
    )

    db_session.refresh(model)
    assert model.last_sync_at > before, "last_sync_at must advance on re-ingest"
    assert db_session.query(Model).filter_by(project_id=project.id, discipline="all").count() == 1


def test_revit_export_different_project_no_cross_contamination(monkeypatch, tmp_path: Path, db_session: Session):
    """Same model file name under different projects must not share model rows."""
    proj_a = Project(id=99, organization_id=1, project_title="PROJECT_A")
    proj_b = Project(id=100, organization_id=1, project_title="PROJECT_B")
    db_session.add_all([proj_a, proj_b])
    db_session.commit()

    _create_revit_export_json(
        tmp_path / "PROJ_A" / "Revit Exports" / "model.json",
        project_title="PROJECT_A",
    )
    _create_revit_manifest(tmp_path, "PROJ_A", project_title="PROJECT_A")

    _create_revit_export_json(
        tmp_path / "PROJ_B" / "Revit Exports" / "model.json",
        project_title="PROJECT_B",
    )
    _create_revit_manifest(tmp_path, "PROJ_B", project_title="PROJECT_B")

    monkeypatch.setattr(settings, "landing_dir", tmp_path)

    # Ingest Project A, then Project B
    ingest_landing_manifest(
        db=db_session,
        manifest_path="PROJ_A/landing_manifest.json",
        dry_run=False,
        recalculate_readiness=False,
    )
    ingest_landing_manifest(
        db=db_session,
        manifest_path="PROJ_B/landing_manifest.json",
        dry_run=False,
        recalculate_readiness=False,
    )

    models = db_session.query(Model).order_by(Model.project_id).all()
    assert len(models) == 2, f"Expected 2 model rows, got {len(models)}"
    assert models[0].project_id == proj_a.id
    assert models[1].project_id == proj_b.id
    assert models[0].revit_file_name == "model.json"
    assert models[1].revit_file_name == "model.json"
    assert models[0].discipline == "all"
    assert models[1].discipline == "all"

    # Re-ingest Project A — model count must stay at 2, no merge
    ingest_landing_manifest(
        db=db_session,
        manifest_path="PROJ_A/landing_manifest.json",
        dry_run=False,
        recalculate_readiness=False,
    )
    assert db_session.query(Model).count() == 2, "Re-ingest must not create extra model rows"

    model_b = db_session.query(Model).filter_by(project_id=proj_b.id).first()
    assert model_b is not None, "Project B model must survive re-ingest of Project A"


def test_revit_export_dry_run_does_not_write(monkeypatch, tmp_path: Path, db_session: Session):
    """Dry-run ingest of revit export must not create model or export rows."""
    project_name = "TEST PROJECT"
    _create_revit_export_json(tmp_path / project_name / "Revit Exports" / "model.json")
    _create_revit_manifest(tmp_path, project_name)
    monkeypatch.setattr(settings, "landing_dir", tmp_path)

    model_count_before = db_session.query(Model).count()
    export_count_before = db_session.query(Export).count()

    report = ingest_landing_manifest(
        db=db_session,
        manifest_path=f"{project_name}/landing_manifest.json",
        dry_run=True,
        recalculate_readiness=False,
    )

    assert report.status in {"completed", "completed_with_warnings"}
    assert db_session.query(Model).count() == model_count_before, "Dry-run must not create model rows"
    assert db_session.query(Export).count() == export_count_before, "Dry-run must not create export rows"
