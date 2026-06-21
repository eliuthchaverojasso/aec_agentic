"""Document access-control tests (Pending Work Register Item 9). SQLite-isolated.

Proves that the global ``/documents/{id}`` endpoints derive the owning project
and enforce access (404 on no-access, so ids can't be enumerated across tenants),
and that project-scoped document endpoints require project membership (403).

    pytest apps/control-plane-api/tests/test_authz_documents.py -o "addopts=--strict-markers"
"""

from __future__ import annotations

from datetime import datetime, timezone

import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.ext.compiler import compiles
from sqlalchemy.orm import Session
from sqlalchemy.pool import StaticPool

from app.api.auth import get_current_user
from app.database import get_db
from app.main import app
from app.models import AppUser, Base, LandingDocument, Membership, Organization, Project


@compiles(JSONB, "sqlite")
def _compile_jsonb_for_sqlite(type_, compiler, **kw):
    return "JSON"


_NOW = datetime(2026, 6, 21, tzinfo=timezone.utc)


class _Env:
    def __init__(self, session: Session) -> None:
        self.session = session
        self.client = TestClient(app)

    def as_user(self, user_id: int) -> TestClient:
        user = self.session.get(AppUser, user_id)
        app.dependency_overrides[get_current_user] = lambda: user
        return self.client


@pytest.fixture
def env():
    engine = create_engine(
        "sqlite+pysqlite:///:memory:",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    Base.metadata.create_all(engine)
    session = Session(engine)

    session.add_all([Organization(id=1, name="Org One"), Organization(id=2, name="Org Two")])
    session.flush()
    session.add_all(
        [
            Project(id=1, organization_id=1, project_title="ORG1 PROJECT", created_at=_NOW, updated_at=_NOW),
            Project(id=2, organization_id=2, project_title="ORG2 PROJECT", created_at=_NOW, updated_at=_NOW),
        ]
    )
    session.add_all(
        [
            LandingDocument(
                id=1, project_id=1, relative_path="ORG1/Drawings/E-001.pdf",
                file_name="E-001.pdf", file_ext=".pdf", file_type="drawing_pdf",
            ),
            LandingDocument(
                id=2, project_id=2, relative_path="ORG2/Drawings/E-002.pdf",
                file_name="E-002.pdf", file_ext=".pdf", file_type="drawing_pdf",
            ),
        ]
    )
    session.add_all(
        [
            AppUser(id=10, name="Admin", email="admin@example.com", role="admin"),
            AppUser(id=11, name="Org1 Member", email="org1@example.com", role="engineer"),
            AppUser(id=12, name="Outsider", email="out@example.com", role="engineer"),
        ]
    )
    session.flush()
    session.add(Membership(user_id=11, organization_id=1, role="member"))
    session.commit()

    def _get_db():
        yield session

    app.dependency_overrides[get_db] = _get_db
    try:
        yield _Env(session)
    finally:
        app.dependency_overrides.pop(get_db, None)
        app.dependency_overrides.pop(get_current_user, None)
        session.close()
        engine.dispose()


# ---- global /documents/{id} endpoints (the Item 9 vulnerability) ----


def test_superuser_reads_any_document_by_id(env):
    client = env.as_user(10)
    assert client.get("/api/v1/documents/1").status_code == 200
    assert client.get("/api/v1/documents/2").status_code == 200


def test_member_reads_own_org_document_by_id(env):
    assert env.as_user(11).get("/api/v1/documents/1").status_code == 200


def test_member_cannot_read_other_org_document_by_id(env):
    # 404 (not 403) so the document's existence is not revealed cross-tenant.
    assert env.as_user(11).get("/api/v1/documents/2").status_code == 404


def test_outsider_cannot_read_any_document_by_id(env):
    assert env.as_user(12).get("/api/v1/documents/1").status_code == 404


def test_text_preview_enforces_access(env):
    member = env.as_user(11)
    assert member.get("/api/v1/documents/2/text-preview").status_code == 404
    assert member.get("/api/v1/documents/1/text-preview").status_code == 200


def test_missing_document_is_404_for_superuser(env):
    assert env.as_user(10).get("/api/v1/documents/999").status_code == 404


# ---- project-scoped document endpoints ----


def test_project_document_list_requires_membership(env):
    member = env.as_user(11)
    assert member.get("/api/v1/projects/1/documents").status_code == 200
    assert member.get("/api/v1/projects/2/documents").status_code == 403


def test_project_scoped_document_requires_membership(env):
    member = env.as_user(11)
    assert member.get("/api/v1/projects/1/documents/1").status_code == 200
    assert member.get("/api/v1/projects/2/documents/2").status_code == 403
