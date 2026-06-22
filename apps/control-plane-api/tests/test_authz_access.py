"""Tenant/project authorization tests (Pending Work Register Item 8).

SQLite-isolated (no PostgreSQL required): an in-memory DB is seeded with two
organizations, a project in each, and users with different membership, then the
``projects`` endpoints and the ``app.authz`` helpers are exercised directly.

Auto-marked ``integration`` by the repo-root conftest, but self-contained on
SQLite, so it also runs standalone:
    pytest apps/control-plane-api/tests/test_authz_access.py -o "addopts=--strict-markers"
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

from app import authz
from app.api.auth import get_current_user
from app.database import get_db
from app.main import app
from app.models import AppUser, Base, Membership, Organization, Project, ProjectMembership


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
            AppUser(id=10, name="Admin", email="admin@example.com", role="admin"),
            AppUser(id=11, name="Org1 Member", email="org1@example.com", role="engineer"),
            AppUser(id=12, name="Outsider", email="out@example.com", role="engineer"),
            AppUser(id=13, name="Project Guest", email="guest@example.com", role="engineer"),
        ]
    )
    session.flush()
    session.add(Membership(user_id=11, organization_id=1, role="member"))
    session.add(ProjectMembership(user_id=13, project_id=2, role="member"))
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


# --------------------------------------------------------------------------- #
# Endpoint enforcement on the projects router (reference implementation)
# --------------------------------------------------------------------------- #


def test_superuser_lists_all_projects(env):
    resp = env.as_user(10).get("/api/v1/projects")
    assert resp.status_code == 200
    assert {p["id"] for p in resp.json()} == {1, 2}


def test_org_member_lists_only_their_org(env):
    resp = env.as_user(11).get("/api/v1/projects")
    assert resp.status_code == 200
    assert {p["id"] for p in resp.json()} == {1}


def test_outsider_lists_nothing(env):
    resp = env.as_user(12).get("/api/v1/projects")
    assert resp.status_code == 200
    assert resp.json() == []


def test_project_guest_lists_only_granted_project(env):
    resp = env.as_user(13).get("/api/v1/projects")
    assert resp.status_code == 200
    assert {p["id"] for p in resp.json()} == {2}


def test_org_member_can_open_own_project(env):
    resp = env.as_user(11).get("/api/v1/projects/1")
    assert resp.status_code == 200
    assert resp.json()["id"] == 1


def test_org_member_cannot_open_other_org_project(env):
    assert env.as_user(11).get("/api/v1/projects/2").status_code == 403


def test_outsider_cannot_open_any_project(env):
    assert env.as_user(12).get("/api/v1/projects/1").status_code == 403


def test_project_guest_access_is_scoped(env):
    guest = env.as_user(13)
    assert guest.get("/api/v1/projects/2").status_code == 200  # directly granted
    assert guest.get("/api/v1/projects/1").status_code == 403  # not granted


def test_missing_project_is_404_for_superuser(env):
    assert env.as_user(10).get("/api/v1/projects/999").status_code == 404


# --------------------------------------------------------------------------- #
# authz helpers in isolation
# --------------------------------------------------------------------------- #


def test_accessible_project_ids_superuser_is_unrestricted(env):
    assert authz.accessible_project_ids(env.session, env.session.get(AppUser, 10)) is None


def test_accessible_project_ids_for_org_member(env):
    assert authz.accessible_project_ids(env.session, env.session.get(AppUser, 11)) == {1}


def test_accessible_project_ids_for_project_guest(env):
    assert authz.accessible_project_ids(env.session, env.session.get(AppUser, 13)) == {2}


def test_outsider_has_no_accessible_projects(env):
    assert authz.accessible_project_ids(env.session, env.session.get(AppUser, 12)) == set()


def test_user_can_access_project_matrix(env):
    session = env.session
    org_member = session.get(AppUser, 11)
    assert authz.user_can_access_project(session, org_member, session.get(Project, 1)) is True
    assert authz.user_can_access_project(session, org_member, session.get(Project, 2)) is False


# --------------------------------------------------------------------------- #
# Role hierarchy helpers
# --------------------------------------------------------------------------- #


def test_has_minimum_role_exact_match():
    assert authz.has_minimum_role("viewer", "viewer") is True
    assert authz.has_minimum_role("member", "member") is True
    assert authz.has_minimum_role("reviewer", "reviewer") is True
    assert authz.has_minimum_role("manager", "manager") is True
    assert authz.has_minimum_role("admin", "admin") is True
    assert authz.has_minimum_role("owner", "owner") is True


def test_has_minimum_role_higher_ok():
    assert authz.has_minimum_role("admin", "viewer") is True
    assert authz.has_minimum_role("owner", "member") is True
    assert authz.has_minimum_role("manager", "reviewer") is True


def test_has_minimum_role_insufficient():
    assert authz.has_minimum_role("viewer", "member") is False
    assert authz.has_minimum_role("member", "reviewer") is False
    assert authz.has_minimum_role("reviewer", "manager") is False


def test_has_minimum_role_none_or_unknown():
    assert authz.has_minimum_role(None, "viewer") is False
    assert authz.has_minimum_role("unknown", "viewer") is False


def test_has_minimum_role_case_insensitive():
    assert authz.has_minimum_role("Admin", "admin") is True
    assert authz.has_minimum_role("OWNER", "viewer") is True


# --------------------------------------------------------------------------- #
# require_project_role dependency (exercised through the readiness endpoint
# which uses require_project_access — role check is an additional gate)
# --------------------------------------------------------------------------- #


def test_superuser_bypasses_role_check(env):
    """Superusers can access any project regardless of role."""
    resolved = authz.require_project_role("manager")(
        project=env.session.get(Project, 1),
        db=env.session,
        user=env.session.get(AppUser, 10),
    )
    assert resolved.id == 1


def test_org_member_with_sufficient_role(env):
    """Member role meets 'member' requirement."""
    resolved = authz.require_project_role("member")(
        project=env.session.get(Project, 1),
        db=env.session,
        user=env.session.get(AppUser, 11),
    )
    assert resolved.id == 1


def test_org_member_with_insufficient_role_is_rejected(env):
    """Member role does NOT meet 'manager' requirement."""
    from fastapi import HTTPException
    import pytest

    with pytest.raises(HTTPException) as exc:
        authz.require_project_role("manager")(
            project=env.session.get(Project, 1),
            db=env.session,
            user=env.session.get(AppUser, 11),
        )
    assert exc.value.status_code == 403


def test_project_guest_with_sufficient_role(env):
    """Project guest has 'member' role on project 2."""
    resolved = authz.require_project_role("member")(
        project=env.session.get(Project, 2),
        db=env.session,
        user=env.session.get(AppUser, 13),
    )
    assert resolved.id == 2
