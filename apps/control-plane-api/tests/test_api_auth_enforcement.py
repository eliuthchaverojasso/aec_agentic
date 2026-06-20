"""Proves the authentication boundary added in PR 2.

Every business router now requires an authenticated principal; only the system
endpoints (``/``, ``/health``) and the auth router (login + gated register) are
public. These run on an in-memory SQLite ``get_db`` override, so no live
PostgreSQL is required even though the repo-root conftest marks them
``integration``.
"""

from __future__ import annotations

import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.ext.compiler import compiles
from sqlalchemy.orm import Session
from sqlalchemy.pool import StaticPool

from app.database import get_db
from app.main import app
from app.models import Base, Organization, Project

PROTECTED_PATH = "/api/v1/projects/1/evidence"


@compiles(JSONB, "sqlite")
def _compile_jsonb_for_sqlite(type_, compiler, **kw):  # pragma: no cover - dialect shim
    return "JSON"


@pytest.fixture
def sqlite_db():
    engine = create_engine(
        "sqlite+pysqlite:///:memory:",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    Base.metadata.create_all(engine)
    with Session(engine) as session:
        session.add(Organization(id=1, name="EMA Engineering"))
        session.add(Project(id=1, organization_id=1, project_title="TEST PROJECT"))
        session.commit()

    def override_db():
        with Session(engine) as session:
            yield session

    app.dependency_overrides[get_db] = override_db
    try:
        yield
    finally:
        app.dependency_overrides.pop(get_db, None)
        Base.metadata.drop_all(engine)


@pytest.mark.noauth
def test_protected_route_rejects_unauthenticated(sqlite_db):
    # noauth -> the real get_current_user runs and rejects the missing token.
    response = TestClient(app).get(PROTECTED_PATH)
    assert response.status_code == 401
    assert response.json()["detail"] == "Not authenticated"


def test_protected_route_allows_authenticated(sqlite_db):
    # conftest injects a synthetic authenticated user for non-noauth tests.
    response = TestClient(app).get(PROTECTED_PATH)
    assert response.status_code == 200
    assert response.json() == []


@pytest.mark.noauth
def test_public_root_needs_no_auth():
    assert TestClient(app).get("/").status_code == 200
