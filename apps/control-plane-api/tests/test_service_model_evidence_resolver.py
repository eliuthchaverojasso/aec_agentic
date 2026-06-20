"""Service-level tests for model_evidence_resolver.

All 14 acceptance-criteria cases covered.
Uses SQLite in-memory (no real landing files or real DB required).
"""

from __future__ import annotations

from datetime import datetime, timezone

import pytest
from sqlalchemy import create_engine, select
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.ext.compiler import compiles
from sqlalchemy.orm import Session

from app.models import (
    Base,
    Client,
    Element,
    Export,
    Model,
    Organization,
    Project,
    Requirement,
    RequirementEvidence,
)
from app.services.model_evidence_resolver import (
    classify_requirement_intent,
    find_requirement_model_candidates,
    resolve_project_model_evidence,
    score_element_against_requirement,
)


# ---------------------------------------------------------------------------
# SQLite compatibility shim for JSONB
# ---------------------------------------------------------------------------

@compiles(JSONB, "sqlite")
def _compile_jsonb_for_sqlite(type_, compiler, **kw):
    return "JSON"


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------

@pytest.fixture
def db_session():
    engine = create_engine("sqlite+pysqlite:///:memory:")
    Base.metadata.create_all(engine)
    with Session(engine) as session:
        yield session
    Base.metadata.drop_all(engine)


def _base_records(db: Session, client_id: int = 1, project_id: int = 1) -> tuple[Organization, Client, Project, Model, Export]:
    """Create the minimal org/client/project/model/export structure."""
    now = datetime.now(timezone.utc)
    org = Organization(id=1, name="Test Org")
    client = Client(id=client_id, organization_id=1, code="TST", display_name="Test Client")
    project = Project(id=project_id, organization_id=1, client_id=client_id, project_title="Test Project")
    model = Model(id=1, project_id=project_id, discipline="General")
    export = Export(
        id=1,
        project_id=project_id,
        model_id=1,
        export_type="revit",
        status="completed",
        completed_at=now,
    )
    db.add_all([org, client, project, model, export])
    db.commit()
    return org, client, project, model, export


def _make_element(
    db: Session,
    uid: str,
    category: str = "Electrical Fixtures",
    name: str | None = None,
    family: str | None = None,
    elem_type: str | None = None,
    level: str | None = None,
    instance_params: dict | None = None,
    type_params: dict | None = None,
    element_id: int = 1001,
    export_id: int = 1,
    model_id: int = 1,
    element_db_id: int | None = None,
) -> Element:
    # Compute a stable db id for SQLite (no auto-increment serial)
    if element_db_id is None:
        existing = db.execute(select(Element)).scalars().all()
        element_db_id = len(existing) + 1

    elem = Element(
        id=element_db_id,
        unique_id=uid,
        element_id=element_id,
        model_id=model_id,
        export_id=export_id,
        category=category,
        name=name,
        family=family,
        type=elem_type,
        level=level,
        instance_parameters=instance_params or {},
        type_parameters=type_params or {},
    )
    db.add(elem)
    db.commit()
    return elem


def _make_requirement(
    db: Session,
    req_id: int,
    client_id: int = 1,
    discipline: str = "Electrical",
    category: str | None = None,
    text: str = "Provide model element",
    is_actionable: bool = True,
    is_active: bool = True,
) -> Requirement:
    from hashlib import sha256
    content_hash = sha256(f"{discipline}{text.lower()}".encode()).hexdigest()
    req = Requirement(
        id=req_id,
        client_id=client_id,
        discipline=discipline,
        category=category,
        requirement_text=text,
        content_hash=content_hash,
        is_actionable=is_actionable,
        is_active=is_active,
    )
    db.add(req)
    db.commit()
    return req


# ---------------------------------------------------------------------------
# Tests 1-6: Intent matching (each discipline finds the right element category)
# ---------------------------------------------------------------------------

def test_electrical_panel_requirement_finds_electrical_equipment(db_session):
    """Test 1: Electrical panel req finds Electrical Equipment with Supply From param."""
    _base_records(db_session)
    elem = _make_element(
        db_session,
        uid="panel-uid-1",
        category="Electrical Equipment",
        name="MDP Panel",
        family="Distribution Panel",
        elem_type="480V MDP",
        level="Level 1",
        instance_params={"Supply From": "Utility Transformer 1"},
    )
    req = _make_requirement(
        db_session, req_id=1, discipline="Electrical",
        text="Main distribution panel must show Supply From source"
    )
    elements = [elem]
    candidates = find_requirement_model_candidates(req, elements)

    assert len(candidates) >= 1
    top = candidates[0]
    assert top["element"].unique_id == "panel-uid-1"
    assert top["score"] >= 0.65
    assert top["breakdown"]["category_match"] is True
    assert "Supply From" in top["matched_params"]


def test_electrical_fixture_requirement_finds_electrical_fixtures(db_session):
    """Test 2: Electrical fixture req finds Electrical Fixtures with Panel and Circuit Number."""
    _base_records(db_session)
    elem = _make_element(
        db_session,
        uid="receptacle-uid-1",
        category="Electrical Fixtures",
        name="Duplex Receptacle",
        family="Receptacle",
        elem_type="20A Outlet",
        level="Level 2",
        instance_params={"Panel": "DP-A", "Circuit Number": "5"},
    )
    req = _make_requirement(
        db_session, req_id=1, discipline="Electrical",
        text="All power receptacles and outlets must have Panel and Circuit Number"
    )
    elements = [elem]
    candidates = find_requirement_model_candidates(req, elements)

    assert len(candidates) >= 1
    top = candidates[0]
    assert top["element"].unique_id == "receptacle-uid-1"
    assert top["score"] >= 0.65
    assert "Panel" in top["matched_params"]
    assert "Circuit Number" in top["matched_params"]


def test_lighting_requirement_finds_lighting_fixtures(db_session):
    """Test 3: Lighting requirement finds Lighting Fixtures."""
    _base_records(db_session)
    elem = _make_element(
        db_session,
        uid="light-uid-1",
        category="Lighting Fixtures",
        name="LED Recessed Light",
        family="Recessed Luminaire",
        elem_type="2x4 LED",
        level="Level 3",
        instance_params={"Panel": "LP-1", "Circuit Number": "12"},
    )
    req = _make_requirement(
        db_session, req_id=1, discipline="Electrical",
        text="Interior lighting fixtures must be coordinated with reflected ceiling plan"
    )
    elements = [elem]
    candidates = find_requirement_model_candidates(req, elements)

    assert len(candidates) >= 1
    top = candidates[0]
    assert top["element"].unique_id == "light-uid-1"
    assert top["score"] >= 0.40
    assert top["intent"]["intent_type"] == "lighting"


def test_mechanical_requirement_finds_mechanical_equipment(db_session):
    """Test 4: Mechanical requirement finds Mechanical Equipment."""
    _base_records(db_session)
    elem = _make_element(
        db_session,
        uid="ahu-uid-1",
        category="Mechanical Equipment",
        name="AHU-1",
        family="Air Handling Unit",
        elem_type="40000 CFM AHU",
        level="Roof",
        instance_params={"Mark": "AHU-1", "Type Mark": "AHU-40000"},
    )
    req = _make_requirement(
        db_session, req_id=1, discipline="Mechanical",
        text="All mechanical equipment AHU units must have Mark and Type Mark"
    )
    elements = [elem]
    candidates = find_requirement_model_candidates(req, elements)

    assert len(candidates) >= 1
    top = candidates[0]
    assert top["element"].unique_id == "ahu-uid-1"
    assert top["score"] >= 0.65
    assert top["intent"]["intent_type"] == "mechanical"
    assert "Mark" in top["matched_params"]


def test_plumbing_requirement_finds_plumbing_fixtures(db_session):
    """Test 5: Plumbing requirement finds Plumbing Fixtures."""
    _base_records(db_session)
    elem = _make_element(
        db_session,
        uid="pump-uid-1",
        category="Plumbing Fixtures",
        name="Water Closet",
        family="Toilet",
        elem_type="Flush Valve WC",
        level="Level 1",
        instance_params={"Mark": "P-1", "Type Mark": "WC-FV"},
    )
    req = _make_requirement(
        db_session, req_id=1, discipline="Plumbing",
        text="Plumbing fixtures must have Mark and Type Mark assigned"
    )
    elements = [elem]
    candidates = find_requirement_model_candidates(req, elements)

    assert len(candidates) >= 1
    top = candidates[0]
    assert top["element"].unique_id == "pump-uid-1"
    assert top["intent"]["intent_type"] == "plumbing"


def test_technology_requirement_finds_communication_devices(db_session):
    """Test 6: Technology requirement finds Data/Communication Devices."""
    _base_records(db_session)
    elem = _make_element(
        db_session,
        uid="data-uid-1",
        category="Data Devices",
        name="Data Outlet",
        family="Low Voltage Device",
        elem_type="CAT6 Port",
        level="Level 2",
        instance_params={"Mark": "D-101", "System Name": "DATA-A"},
    )
    req = _make_requirement(
        db_session, req_id=1, discipline="Technology",
        text="All telecom and data communications devices must show System Name"
    )
    elements = [elem]
    candidates = find_requirement_model_candidates(req, elements)

    assert len(candidates) >= 1
    top = candidates[0]
    assert top["element"].unique_id == "data-uid-1"
    assert top["intent"]["intent_type"] == "technology"
    assert "System Name" in top["matched_params"]


# ---------------------------------------------------------------------------
# Tests 7-8: Evidence status semantics
# ---------------------------------------------------------------------------

def test_requirement_without_match_produces_no_candidate(db_session):
    """Test 7: Requirement with no compatible element → no candidates."""
    _base_records(db_session)
    # A civil/structural element with no electrical traits
    elem = _make_element(
        db_session,
        uid="struct-uid-1",
        category="Structural Columns",
        name="W12x26 Column",
        family="Steel Column",
        elem_type="W12x26",
    )
    req = _make_requirement(
        db_session, req_id=1, discipline="Electrical",
        text="Provide panel with Supply From documented"
    )
    candidates = find_requirement_model_candidates(req, [elem])
    assert candidates == []


def test_candidate_evidence_has_needs_review_status(db_session):
    """Test 8: Candidate evidence has evidence_status=needs_review, review_status=candidate."""
    _base_records(db_session)
    _make_element(
        db_session,
        uid="panel-uid-8",
        category="Electrical Equipment",
        name="Panel Board",
        instance_params={"Supply From": "Main Switchboard"},
        level="Level 1",
    )
    _make_requirement(
        db_session, req_id=1, discipline="Electrical",
        text="Distribution panel required with Supply From source"
    )

    summary = resolve_project_model_evidence(db_session, project_id=1)

    evidence_rows = db_session.execute(select(RequirementEvidence)).scalars().all()
    assert len(evidence_rows) >= 1
    ev = evidence_rows[0]
    assert ev.evidence_status == "needs_review"
    assert (ev.metadata_json or {}).get("review_status") == "candidate"
    assert ev.evidence_type == "model"
    assert summary["candidate_evidence_created"] >= 1


# ---------------------------------------------------------------------------
# Tests 9-14: Idempotency, missing exports, clients, inactive reqs, etc.
# ---------------------------------------------------------------------------

def test_running_resolver_twice_does_not_duplicate_evidence(db_session):
    """Test 9: Running resolver twice updates existing row, no duplicates."""
    _base_records(db_session)
    _make_element(
        db_session,
        uid="panel-uid-9",
        category="Electrical Equipment",
        name="MDP",
        instance_params={"Supply From": "Utility"},
        level="Level B",
    )
    _make_requirement(
        db_session, req_id=1, discipline="Electrical",
        text="Distribution panel must model Supply From"
    )

    # First run
    s1 = resolve_project_model_evidence(db_session, project_id=1)
    count_after_first = db_session.execute(
        select(RequirementEvidence)
        .where(RequirementEvidence.project_id == 1)
    ).scalars().all()

    # Second run
    s2 = resolve_project_model_evidence(db_session, project_id=1)
    count_after_second = db_session.execute(
        select(RequirementEvidence)
        .where(RequirementEvidence.project_id == 1)
    ).scalars().all()

    # Same number of rows after both runs
    assert len(count_after_first) == len(count_after_second)
    # Second run should register updates, not creates
    assert s2["candidate_evidence_created"] == 0
    assert s2["candidate_evidence_updated"] >= 1


def test_project_without_latest_export_returns_clear_summary(db_session):
    """Test 10: Project without completed export → clear state=no_completed_export."""
    now = datetime.now(timezone.utc)
    org = Organization(id=1, name="Test Org")
    client = Client(id=1, organization_id=1, code="C1", display_name="Client One")
    project = Project(id=1, organization_id=1, client_id=1, project_title="No Export Project")
    model = Model(id=1, project_id=1, discipline="Electrical")
    # Export with status="failed" (not "completed")
    export = Export(
        id=1, project_id=1, model_id=1,
        export_type="revit", status="failed", completed_at=now,
    )
    db_session.add_all([org, client, project, model, export])
    db_session.commit()

    result = resolve_project_model_evidence(db_session, project_id=1)

    assert result["state"] == "no_completed_export"
    assert result["latest_export_id"] is None
    assert result["requirements_checked"] == 0


def test_project_without_client_id_returns_clear_summary(db_session):
    """Test 11: Project without client_id → state=no_client_linked."""
    org = Organization(id=1, name="Test Org")
    # No client_id on project
    project = Project(id=1, organization_id=1, client_id=None, project_title="Unbound Project")
    db_session.add_all([org, project])
    db_session.commit()

    result = resolve_project_model_evidence(db_session, project_id=1)

    assert result["state"] == "no_client_linked"
    assert result["requirements_checked"] == 0


def test_nonactionable_requirement_is_ignored(db_session):
    """Test 12: is_actionable=False requirements are skipped."""
    _base_records(db_session)
    _make_element(
        db_session,
        uid="elem-na",
        category="Electrical Equipment",
        name="Panel",
        instance_params={"Supply From": "X"},
        level="L1",
    )
    _make_requirement(
        db_session, req_id=1, discipline="Electrical",
        text="Distribution panel with Supply From",
        is_actionable=False,
    )

    result = resolve_project_model_evidence(db_session, project_id=1)

    assert result["requirements_checked"] == 0
    assert result["candidate_evidence_created"] == 0


def test_inactive_requirement_is_ignored(db_session):
    """Test 13: is_active=False requirements are skipped."""
    _base_records(db_session)
    _make_element(
        db_session,
        uid="elem-inactive",
        category="Electrical Equipment",
        name="Panel",
        instance_params={"Supply From": "X"},
        level="L1",
    )
    _make_requirement(
        db_session, req_id=1, discipline="Electrical",
        text="Distribution panel with Supply From",
        is_active=False,
    )

    result = resolve_project_model_evidence(db_session, project_id=1)

    assert result["requirements_checked"] == 0
    assert result["candidate_evidence_created"] == 0


def test_resolver_uses_latest_completed_export_only(db_session):
    """Test 14: Resolver picks the most recent completed export."""
    now = datetime.now(timezone.utc)
    from datetime import timedelta

    org = Organization(id=1, name="Test Org")
    client = Client(id=1, organization_id=1, code="TST", display_name="Test Client")
    project = Project(id=1, organization_id=1, client_id=1, project_title="Dual Export Project")
    model = Model(id=1, project_id=1, discipline="Electrical")

    # Older completed export (id=1) – has elements
    old_export = Export(
        id=1, project_id=1, model_id=1, export_type="revit",
        status="completed", completed_at=now - timedelta(days=3),
    )
    # Newer completed export (id=2) – no elements
    new_export = Export(
        id=2, project_id=1, model_id=1, export_type="revit",
        status="completed", completed_at=now - timedelta(days=1),
    )
    # Element belongs to the OLD export
    old_elem = Element(
        id=1, unique_id="old-elem-uid", element_id=9001,
        model_id=1, export_id=1,
        category="Electrical Equipment",
        name="Old Panel",
        instance_parameters={"Supply From": "Grid"},
        type_parameters={},
    )
    req = Requirement(
        id=1, client_id=1, discipline="Electrical",
        requirement_text="Panel Supply From required",
        content_hash="abc123",
        is_actionable=True, is_active=True,
    )

    db_session.add_all([org, client, project, model, old_export, new_export, old_elem, req])
    db_session.commit()

    result = resolve_project_model_evidence(db_session, project_id=1)

    # Should have used export id=2 (the newest) which has no elements → 0 candidates
    assert result["latest_export_id"] == 2
    assert result["candidate_evidence_created"] == 0
    assert len(result["warnings"]) >= 1  # "no elements" warning
