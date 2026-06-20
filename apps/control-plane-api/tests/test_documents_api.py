from datetime import datetime, timezone

import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.ext.compiler import compiles
from sqlalchemy.orm import Session
from sqlalchemy.pool import StaticPool

from app.database import get_db
from app.config import settings
from app.main import app
from app.models import Base, DocumentTextSnippet, LandingDocument, Organization, Project


@compiles(JSONB, "sqlite")
def _compile_jsonb_for_sqlite(type_, compiler, **kw):
    return "JSON"


@pytest.fixture
def client(tmp_path):
    project_root = tmp_path / "TEST PROJECT" / "Drawings"
    project_root.mkdir(parents=True, exist_ok=True)
    (project_root / "E-001 Electrical.pdf").write_bytes(b"%PDF-1.4\n%EOF")

    engine = create_engine(
        "sqlite+pysqlite:///:memory:",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    Base.metadata.create_all(engine)
    with Session(engine) as session:
        session.add(Organization(id=1, name="EMA Engineering"))
        session.add(Project(id=1, organization_id=1, project_title="TEST PROJECT"))
        session.add(
            LandingDocument(
                id=1,
                project_id=1,
                relative_path="TEST PROJECT/Drawings/E-001 Electrical.pdf",
                file_name="E-001 Electrical.pdf",
                file_ext=".pdf",
                file_type="drawing_pdf",
                document_category="drawing",
                discipline="ELECTRICAL",
                sheet_number="E-001",
                page_count=1,
                file_size_bytes=10,
                checksum_sha256="a" * 64,
                source_system="landing",
                ingestion_status="indexed",
                evidence_status="candidate",
                indexed_at=datetime.now(timezone.utc),
                metadata_json={"local_path": "should not appear as raw content"},
            )
        )
        session.add(
            LandingDocument(
                id=2,
                project_id=1,
                relative_path="TEST PROJECT/Drawings/readme.json",
                file_name="readme.json",
                file_ext=".json",
                file_type="project_extract",
                document_category="document",
                discipline="ELECTRICAL",
                page_count=None,
                file_size_bytes=10,
                checksum_sha256="b" * 64,
                source_system="landing",
                ingestion_status="indexed",
                evidence_status="candidate",
                indexed_at=datetime.now(timezone.utc),
                metadata_json={},
            )
        )
        session.add(
            DocumentTextSnippet(
                id=1,
                document_id=1,
                text_preview="Short capped preview",
                extraction_method="pypdf",
            )
        )
        session.commit()

    def override_db():
        with Session(engine) as session:
            yield session

    app.dependency_overrides[get_db] = override_db
    original_landing = settings.landing_dir
    settings.landing_dir = tmp_path
    try:
        yield TestClient(app)
    finally:
        settings.landing_dir = original_landing
        app.dependency_overrides.clear()
        Base.metadata.drop_all(engine)


def test_list_project_documents(client: TestClient):
    response = client.get("/api/v1/projects/1/documents")

    assert response.status_code == 200
    payload = response.json()
    assert any(row["file_type"] == "drawing_pdf" for row in payload)
    assert any(row["relative_path"] == "TEST PROJECT/Drawings/E-001 Electrical.pdf" for row in payload)


def test_filter_drawings(client: TestClient):
    response = client.get("/api/v1/projects/1/drawings")

    assert response.status_code == 200
    assert response.json()[0]["document_category"] == "drawing"


def test_text_preview_endpoint_returns_capped_preview_only(client: TestClient):
    response = client.get("/api/v1/documents/1/text-preview")

    assert response.status_code == 200
    payload = response.json()
    assert payload["available"] is True
    assert payload["text_preview"] == "Short capped preview"
    assert "C:\\" not in payload["text_preview"]


def test_project_scoped_metadata_endpoint(client: TestClient):
    response = client.get("/api/v1/projects/1/documents/1/metadata")
    assert response.status_code == 200
    assert response.json()["id"] == 1


def test_project_pdf_endpoint_rejects_non_pdf(client: TestClient):
    response = client.get("/api/v1/projects/1/documents/2/pdf")
    assert response.status_code == 400
    assert "not a PDF" in response.text


def test_project_pdf_endpoint_serves_pdf(client: TestClient):
    response = client.get("/api/v1/projects/1/documents/1/pdf")
    assert response.status_code == 200
    assert response.headers["content-type"].startswith("application/pdf")
