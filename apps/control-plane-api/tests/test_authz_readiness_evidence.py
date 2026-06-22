"""Access-control tests for readiness and requirement evidence.

These endpoints are the first real Owner Requirements Readiness vertical, so
they must require both authentication and project membership. Evidence review
identity is also authority context: callers may not forge ``reviewed_by``.
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
from app.models import (
    AppUser,
    Base,
    Client,
    Membership,
    Organization,
    Project,
    ReadinessAction,
    Requirement,
)


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
            Client(id=1, organization_id=1, code="OWNER1", display_name="Owner One"),
            Client(id=2, organization_id=2, code="OWNER2", display_name="Owner Two"),
            Project(
                id=1,
                organization_id=1,
                client_id=1,
                project_title="ORG1 PROJECT",
                created_at=_NOW,
                updated_at=_NOW,
            ),
            Project(
                id=2,
                organization_id=2,
                client_id=2,
                project_title="ORG2 PROJECT",
                created_at=_NOW,
                updated_at=_NOW,
            ),
            Requirement(
                id=1,
                client_id=1,
                discipline="Electrical",
                category="DD50 model",
                requirement_text="Electrical model evidence is required at DD50.",
                content_hash="req-org1",
                is_active=True,
                is_actionable=True,
            ),
            Requirement(
                id=2,
                client_id=2,
                discipline="Mechanical",
                category="DD50 model",
                requirement_text="Mechanical model evidence is required at DD50.",
                content_hash="req-org2",
                is_active=True,
                is_actionable=True,
            ),
            ReadinessAction(
                id=1,
                project_id=1,
                rule_code="EVD001",
                action_type="link_evidence",
                title="Link evidence",
                description="Evidence is missing.",
                status="open",
                priority="medium",
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


@pytest.mark.noauth
def test_readiness_and_evidence_reject_missing_token(env):
    assert env.client.get("/api/v1/projects/1/readiness").status_code == 401
    assert env.client.get("/api/v1/projects/1/evidence").status_code == 401
    assert (
        env.client.post(
            "/api/v1/projects/1/requirements/1/evidence",
            json={"review_status": "candidate", "evidence_type": "manual"},
        ).status_code
        == 401
    )


def test_evidence_routes_require_project_membership(env):
    member = env.as_user(11)

    assert member.get("/api/v1/projects/1/evidence").status_code == 200
    assert member.get("/api/v1/projects/2/evidence").status_code == 403
    assert (
        member.post(
            "/api/v1/projects/2/requirements/2/evidence",
            json={"review_status": "candidate", "evidence_type": "manual"},
        ).status_code
        == 403
    )


def test_reviewer_identity_comes_from_authenticated_user(env):
    member = env.as_user(11)

    created = member.post(
        "/api/v1/projects/1/requirements/1/evidence",
        json={
            "review_status": "accepted",
            "evidence_type": "manual",
            "source_label": "REQ-1",
            "review_note": "Accepted from project review.",
            "reviewed_by": "forged@example.com",
            "metadata": {"reviewed_by": "metadata-forged@example.com"},
        },
    )

    assert created.status_code == 200
    payload = created.json()
    assert payload["review_status"] == "accepted"
    assert payload["reviewed_by"] == "org1@example.com"
    assert payload["metadata_json"]["reviewed_by_user_id"] == 11

    updated = member.patch(
        f"/api/v1/projects/1/evidence/{payload['id']}",
        json={
            "review_status": "rejected",
            "review_note": "Rejected on second review.",
            "reviewed_by": "other-forged@example.com",
        },
    )

    assert updated.status_code == 200
    assert updated.json()["review_status"] == "rejected"
    assert updated.json()["reviewed_by"] == "org1@example.com"


def test_readiness_routes_require_project_membership(env):
    member = env.as_user(11)

    assert member.get("/api/v1/projects/2/readiness").status_code == 403
    assert member.get("/api/v1/projects/2/readiness/snapshots").status_code == 403
    assert member.get("/api/v1/projects/2/readiness/actions").status_code == 403
    assert member.post("/api/v1/projects/2/readiness/recalculate").status_code == 403


def test_readiness_action_patch_requires_project_membership(env):
    outsider = env.as_user(12)
    assert (
        outsider.patch(
            "/api/v1/readiness/actions/1",
            json={"status": "done"},
        ).status_code
        == 403
    )

    member = env.as_user(11)
    response = member.patch("/api/v1/readiness/actions/1", json={"status": "done"})
    assert response.status_code == 200
    assert response.json()["status"] == "done"
