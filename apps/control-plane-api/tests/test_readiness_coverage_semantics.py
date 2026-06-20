"""Comprehensive tests for readiness/coverage semantics.

Covers all 12 acceptance scenarios:
A - New project, no client
B - Client linked, no requirements
C - Requirements loaded, no evidence
D - Candidate evidence
E - Accepted evidence
F - Rejected evidence
G - Non-actionable requirement
H - Multiple evidence; accepted overrides candidate
I - Manual evidence creation defaults
J - Model evidence resolver creates candidates only
K - ProjectOverview readiness not 100 for no-evidence project
L - _requirement_coverage returns 0 when only non-actionable requirements
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
from app.models import (
    Base,
    Client,
    Element,
    Export,
    Model,
    Organization,
    Project,
    Requirement,
    RequirementCompliance,
    RequirementEvidence,
)
from app.readiness.scoring import qaqc_health_score
from app.readiness.service import _requirement_coverage, build_project_readiness


@compiles(JSONB, "sqlite")
def _compile_jsonb_for_sqlite(type_, compiler, **kw):
    return "JSON"


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------


def _make_engine():
    return create_engine(
        "sqlite+pysqlite:///:memory:",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )


@pytest.fixture
def db():
    engine = _make_engine()
    Base.metadata.create_all(engine)
    with Session(engine) as session:
        yield session
    Base.metadata.drop_all(engine)


@pytest.fixture
def api_client():
    engine = _make_engine()
    Base.metadata.create_all(engine)

    def override_db():
        with Session(engine) as session:
            yield session

    app.dependency_overrides[get_db] = override_db
    try:
        yield TestClient(app), engine
    finally:
        app.dependency_overrides.clear()
        Base.metadata.drop_all(engine)


def _seed_base(session: Session) -> tuple[Organization, Client, Project]:
    org = Organization(id=1, name="Test Org")
    client = Client(id=1, organization_id=1, code="OWNER", display_name="Owner Agency")
    project = Project(id=1, organization_id=1, client_id=1, project_title="Test Project")
    session.add_all([org, client, project])
    session.commit()
    return org, client, project


def _add_requirement(session: Session, req_id: int, is_actionable: bool = True) -> Requirement:
    req = Requirement(
        id=req_id,
        client_id=1,
        discipline="Electrical",
        category="DD50",
        requirement_text=f"Requirement {req_id}",
        content_hash=f"hash-{req_id}",
        is_active=True,
        is_actionable=is_actionable,
    )
    session.add(req)
    session.commit()
    return req


def _add_evidence(
    session: Session,
    ev_id: int,
    req_id: int,
    review_status: str,
) -> RequirementEvidence:
    evidence_status_map = {
        "accepted": "covered",
        "candidate": "needs_review",
        "needs_review": "needs_review",
        "rejected": "missing",
    }
    ev = RequirementEvidence(
        id=ev_id,
        project_id=1,
        requirement_id=req_id,
        evidence_type="manual",
        evidence_status=evidence_status_map.get(review_status, "needs_review"),
        source_ref=f"manual:req:{req_id}:{ev_id}",
        metadata_json={"review_status": review_status},
    )
    session.add(ev)
    session.commit()
    return ev


# ---------------------------------------------------------------------------
# Scenario A — New project, no client
# ---------------------------------------------------------------------------


def test_a_no_client_linked(api_client):
    http, engine = api_client
    with Session(engine) as session:
        org = Organization(id=1, name="Org")
        project = Project(id=1, organization_id=1, project_title="Empty")
        session.add_all([org, project])
        session.commit()

    resp = http.get("/api/v1/projects/1/requirements")
    assert resp.status_code == 200
    payload = resp.json()
    assert payload["state"] == "no_client_linked"
    assert payload["counts"]["covered"] == 0
    assert payload["counts"]["requirement_coverage_percent"] == 0.0
    assert payload["total"] == 0


def test_a_readiness_not_100_no_client(db):
    org = Organization(id=1, name="Org")
    project = Project(id=1, organization_id=1, project_title="Empty")
    db.add_all([org, project])
    db.commit()
    result = build_project_readiness(db, project)
    assert result.overall_readiness != 100
    assert result.requirement_coverage.score == 0.0


# ---------------------------------------------------------------------------
# Scenario B — Client linked, no requirements
# ---------------------------------------------------------------------------


def test_b_client_no_requirements(api_client):
    http, engine = api_client
    with Session(engine) as session:
        org = Organization(id=1, name="Org")
        client = Client(id=1, organization_id=1, code="C1", display_name="Client")
        project = Project(id=1, organization_id=1, client_id=1, project_title="P1")
        session.add_all([org, client, project])
        session.commit()

    resp = http.get("/api/v1/projects/1/requirements")
    assert resp.status_code == 200
    payload = resp.json()
    assert payload["state"] == "client_linked_no_requirements"
    counts = payload["counts"]
    assert counts["total"] == 0
    assert counts["actionable"] == 0
    assert counts["covered"] == 0
    assert counts["requirement_coverage_percent"] == 0.0


def test_b_readiness_requirement_coverage_zero_no_requirements(db):
    org = Organization(id=1, name="Org")
    client = Client(id=1, organization_id=1, code="C1", display_name="Client")
    project = Project(id=1, organization_id=1, client_id=1, project_title="P1")
    db.add_all([org, client, project])
    db.commit()
    result = build_project_readiness(db, project)
    assert result.requirement_coverage.score == 0.0
    assert result.overall_readiness != 100


# ---------------------------------------------------------------------------
# Scenario C — Requirements loaded, no evidence
# ---------------------------------------------------------------------------


def test_c_requirements_no_evidence(api_client):
    http, engine = api_client
    with Session(engine) as session:
        _seed_base(session)
        _add_requirement(session, 1)
        _add_requirement(session, 2)

    resp = http.get("/api/v1/projects/1/requirements")
    assert resp.status_code == 200
    payload = resp.json()
    assert payload["state"] == "requirements_loaded_no_evidence"
    counts = payload["counts"]
    assert counts["total"] == 2
    assert counts["actionable"] == 2
    assert counts["covered"] == 0
    assert counts["missing"] == 2
    assert counts["needs_review"] == 0
    assert counts["no_evidence_count"] == 2
    assert counts["requirement_coverage_percent"] == 0.0


def test_c_readiness_zero_no_evidence(db):
    org = Organization(id=1, name="Org")
    client = Client(id=1, organization_id=1, code="C1", display_name="Client")
    project = Project(id=1, organization_id=1, client_id=1, project_title="P1")
    req = Requirement(
        id=1, client_id=1, discipline="Electrical", category="DD50",
        requirement_text="Req 1", content_hash="h1", is_active=True, is_actionable=True,
    )
    db.add_all([org, client, project, req])
    db.commit()
    score, detail = _requirement_coverage(db, project)
    assert score == 0.0
    assert "0 of 1" in detail


# ---------------------------------------------------------------------------
# Scenario D — Candidate evidence
# ---------------------------------------------------------------------------


def test_d_candidate_evidence(api_client):
    http, engine = api_client
    with Session(engine) as session:
        _seed_base(session)
        _add_requirement(session, 1)
        _add_evidence(session, 1, 1, "candidate")

    resp = http.get("/api/v1/projects/1/requirements")
    assert resp.status_code == 200
    payload = resp.json()
    assert payload["state"] == "evidence_candidates_pending"
    counts = payload["counts"]
    assert counts["candidate_evidence_count"] == 1
    assert counts["covered"] == 0
    assert counts["requirement_coverage_percent"] == 0.0
    item = payload["items"][0]
    assert item["normalized_status"] == "candidate"


def test_d_readiness_zero_candidate_evidence(db):
    org = Organization(id=1, name="Org")
    client = Client(id=1, organization_id=1, code="C1", display_name="Client")
    project = Project(id=1, organization_id=1, client_id=1, project_title="P1")
    req = Requirement(
        id=1, client_id=1, discipline="Electrical", category="DD50",
        requirement_text="Req 1", content_hash="h1", is_active=True, is_actionable=True,
    )
    db.add_all([org, client, project, req])
    db.commit()
    _add_evidence(db, 1, 1, "candidate")
    score, _ = _requirement_coverage(db, project)
    assert score == 0.0


# ---------------------------------------------------------------------------
# Scenario E — Accepted evidence
# ---------------------------------------------------------------------------


def test_e_accepted_evidence(api_client):
    http, engine = api_client
    with Session(engine) as session:
        _seed_base(session)
        _add_requirement(session, 1)
        _add_requirement(session, 2)
        _add_evidence(session, 1, 1, "accepted")

    resp = http.get("/api/v1/projects/1/requirements")
    assert resp.status_code == 200
    payload = resp.json()
    assert payload["state"] == "readiness_available"
    counts = payload["counts"]
    assert counts["accepted_evidence_count"] == 1
    assert counts["covered"] == 1
    assert counts["requirement_coverage_percent"] == 50.0
    items = {i["requirement_id"]: i for i in payload["items"]}
    assert items[1]["normalized_status"] == "accepted"
    assert items[2]["normalized_status"] == "no_evidence"


def test_e_readiness_score_updates_with_accepted_evidence(db):
    org = Organization(id=1, name="Org")
    client = Client(id=1, organization_id=1, code="C1", display_name="Client")
    project = Project(id=1, organization_id=1, client_id=1, project_title="P1")
    req = Requirement(
        id=1, client_id=1, discipline="Electrical", category="DD50",
        requirement_text="Req 1", content_hash="h1", is_active=True, is_actionable=True,
    )
    db.add_all([org, client, project, req])
    db.commit()
    _add_evidence(db, 1, 1, "accepted")
    score, _ = _requirement_coverage(db, project)
    assert score == 100.0


# ---------------------------------------------------------------------------
# Scenario F — Rejected evidence
# ---------------------------------------------------------------------------


def test_f_rejected_evidence(api_client):
    http, engine = api_client
    with Session(engine) as session:
        _seed_base(session)
        _add_requirement(session, 1)
        _add_evidence(session, 1, 1, "rejected")

    resp = http.get("/api/v1/projects/1/requirements")
    assert resp.status_code == 200
    payload = resp.json()
    counts = payload["counts"]
    assert counts["rejected_evidence_count"] == 1
    assert counts["covered"] == 0
    assert counts["requirement_coverage_percent"] == 0.0
    item = payload["items"][0]
    assert item["normalized_status"] == "rejected"
    assert item["evidence_status"] == "missing"


# ---------------------------------------------------------------------------
# Scenario G — Non-actionable requirement
# ---------------------------------------------------------------------------


def test_g_non_actionable_excluded_from_denominator(api_client):
    http, engine = api_client
    with Session(engine) as session:
        _seed_base(session)
        _add_requirement(session, 1, is_actionable=False)

    resp = http.get("/api/v1/projects/1/requirements")
    assert resp.status_code == 200
    payload = resp.json()
    counts = payload["counts"]
    assert counts["non_actionable"] == 1
    assert counts["actionable"] == 0
    assert counts["requirement_coverage_percent"] == 0.0
    item = payload["items"][0]
    assert item["normalized_status"] == "not_applicable"


def test_g_non_actionable_only_returns_zero_not_100(db):
    org = Organization(id=1, name="Org")
    client = Client(id=1, organization_id=1, code="C1", display_name="Client")
    project = Project(id=1, organization_id=1, client_id=1, project_title="P1")
    req = Requirement(
        id=1, client_id=1, discipline="Electrical", category="DD50",
        requirement_text="Non-actionable req", content_hash="h1",
        is_active=True, is_actionable=False,
    )
    db.add_all([org, client, project, req])
    db.commit()
    score, detail = _requirement_coverage(db, project)
    assert score == 0.0, f"Expected 0.0 but got {score}. Detail: {detail}"


# ---------------------------------------------------------------------------
# Scenario H — Multiple evidence; accepted overrides candidate
# ---------------------------------------------------------------------------


def test_h_accepted_overrides_candidate(db):
    org = Organization(id=1, name="Org")
    client = Client(id=1, organization_id=1, code="C1", display_name="Client")
    project = Project(id=1, organization_id=1, client_id=1, project_title="P1")
    req = Requirement(
        id=1, client_id=1, discipline="Electrical", category="DD50",
        requirement_text="Req 1", content_hash="h1", is_active=True, is_actionable=True,
    )
    db.add_all([org, client, project, req])
    db.commit()
    _add_evidence(db, 1, 1, "candidate")
    accepted_ev = RequirementEvidence(
        id=2,
        project_id=1,
        requirement_id=1,
        evidence_type="manual",
        evidence_status="covered",
        source_ref="manual:req:1:accepted",
        metadata_json={"review_status": "accepted"},
    )
    db.add(accepted_ev)
    db.commit()
    score, _ = _requirement_coverage(db, project)
    assert score == 100.0


# ---------------------------------------------------------------------------
# Scenario I — Manual evidence creation defaults to candidate
# ---------------------------------------------------------------------------


def test_i_manual_evidence_defaults_to_candidate(api_client):
    http, engine = api_client
    with Session(engine) as session:
        _seed_base(session)
        _add_requirement(session, 1)

    resp = http.post(
        "/api/v1/projects/1/requirements/1/evidence",
        json={
            "evidence_type": "manual",
            "source_label": "Manual test",
            "review_status": "candidate",
        },
    )
    assert resp.status_code == 200
    payload = resp.json()
    assert payload["review_status"] == "candidate"
    assert payload["evidence_status"] == "needs_review"

    req_resp = http.get("/api/v1/projects/1/requirements")
    counts = req_resp.json()["counts"]
    assert counts["covered"] == 0
    assert counts["candidate_evidence_count"] == 1


# ---------------------------------------------------------------------------
# Scenario J — Model evidence resolver creates candidates only
# ---------------------------------------------------------------------------


def test_j_model_resolver_creates_candidates(api_client):
    http, engine = api_client
    with Session(engine) as session:
        _seed_base(session)
        _add_requirement(session, 1)
        _add_evidence(session, 1, 1, "candidate")

    req_resp = http.get("/api/v1/projects/1/requirements")
    counts = req_resp.json()["counts"]
    assert counts["covered"] == 0
    assert counts["candidate_evidence_count"] >= 1
    assert counts["requirement_coverage_percent"] == 0.0


# ---------------------------------------------------------------------------
# Scenario K — Readiness not 100 for project with requirements but no evidence
# ---------------------------------------------------------------------------


def test_k_readiness_not_100_requirements_no_evidence(db):
    org = Organization(id=1, name="Org")
    client = Client(id=1, organization_id=1, code="C1", display_name="Client")
    project = Project(id=1, organization_id=1, client_id=1, project_title="P1")
    req = Requirement(
        id=1, client_id=1, discipline="Electrical", category="DD50",
        requirement_text="Req 1", content_hash="h1", is_active=True, is_actionable=True,
    )
    db.add_all([org, client, project, req])
    db.commit()
    result = build_project_readiness(db, project)
    assert result.overall_readiness != 100
    assert result.requirement_coverage.score == 0.0


# ---------------------------------------------------------------------------
# Scenario L — _requirement_coverage returns 0 not 100 for non-actionable only
# ---------------------------------------------------------------------------


def test_l_requirement_coverage_zero_for_non_actionable_only(db):
    org = Organization(id=1, name="Org")
    client = Client(id=1, organization_id=1, code="C1", display_name="Client")
    project = Project(id=1, organization_id=1, client_id=1, project_title="P1")
    req1 = Requirement(
        id=1, client_id=1, discipline="Electrical", category="DD50",
        requirement_text="Non-actionable 1", content_hash="h1",
        is_active=True, is_actionable=False,
    )
    req2 = Requirement(
        id=2, client_id=1, discipline="Mechanical", category="DD50",
        requirement_text="Non-actionable 2", content_hash="h2",
        is_active=True, is_actionable=False,
    )
    db.add_all([org, client, project, req1, req2])
    db.commit()
    score, detail = _requirement_coverage(db, project)
    assert score == 0.0, f"Expected 0.0 (not 100.0). Got: {score}. Detail: {detail}"


# ---------------------------------------------------------------------------
# Additional: qaqc_health returns 0 for empty elements
# ---------------------------------------------------------------------------


def test_qaqc_health_returns_zero_for_no_elements():
    assert qaqc_health_score(0, 0, 0, 0, 0) == 0.0


def test_qaqc_health_returns_100_for_elements_with_no_issues():
    assert qaqc_health_score(100, 0, 0, 0, 0) == 100.0


def test_qaqc_health_penalizes_issues():
    score = qaqc_health_score(100, 1, 0, 0, 0)
    assert score < 100.0


# ---------------------------------------------------------------------------
# Evidence status mapping
# ---------------------------------------------------------------------------


def test_evidence_status_from_review_mappings():
    from app.services.evidence_service import evidence_status_from_review
    assert evidence_status_from_review("accepted") == "covered"
    assert evidence_status_from_review("candidate") == "needs_review"
    assert evidence_status_from_review("needs_review") == "needs_review"
    assert evidence_status_from_review("rejected") == "missing"
    assert evidence_status_from_review("none") == "missing"


def test_normalized_status_from_evidence_mappings():
    from app.services.evidence_service import normalized_status_from_evidence
    assert normalized_status_from_evidence(True, "accepted") == "accepted"
    assert normalized_status_from_evidence(True, "candidate") == "candidate"
    assert normalized_status_from_evidence(True, "needs_review") == "needs_review"
    assert normalized_status_from_evidence(True, "rejected") == "rejected"
    assert normalized_status_from_evidence(True, "none") == "no_evidence"
    assert normalized_status_from_evidence(False, "accepted") == "not_applicable"
    assert normalized_status_from_evidence(False, "none") == "not_applicable"
