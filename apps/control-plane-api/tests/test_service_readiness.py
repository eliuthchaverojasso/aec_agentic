"""Service-level baseline tests for project readiness."""

from datetime import datetime, timedelta, timezone

import pytest
from sqlalchemy import create_engine
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.ext.compiler import compiles
from sqlalchemy.orm import Session

from app.models import (
    Base,
    Client,
    Element,
    Export,
    Issue,
    Model,
    Organization,
    Project,
    Requirement,
    RequirementCompliance,
    RequirementEvidence,
)
from app.readiness.service import build_project_readiness


@compiles(JSONB, "sqlite")
def _compile_jsonb_for_sqlite(type_, compiler, **kw):
    return "JSON"


@pytest.fixture
def db_session():
    engine = create_engine("sqlite+pysqlite:///:memory:")
    Base.metadata.create_all(engine)

    with Session(engine) as session:
        yield session

    Base.metadata.drop_all(engine)


def test_build_project_readiness_with_client_export_requirement_and_high_issue(db_session):
    now = datetime.now(timezone.utc)
    completed_at = now - timedelta(hours=2)

    organization = Organization(id=1, name="Test Org")
    client = Client(
        id=1,
        organization_id=organization.id,
        code="OWNER",
        display_name="Owner Agency",
    )
    project = Project(
        id=1,
        organization_id=organization.id,
        client_id=client.id,
        project_title="Readiness Baseline",
    )
    model = Model(id=1, project_id=project.id, discipline="Electrical")
    export = Export(
        id=1,
        project_id=project.id,
        model_id=model.id,
        export_type="revit",
        status="completed",
        completed_at=completed_at,
    )
    element = Element(
        id=1,
        unique_id="element-1",
        element_id=1001,
        model_id=model.id,
        export_id=export.id,
        category="Electrical Fixtures",
    )
    requirement = Requirement(
        id=1,
        client_id=client.id,
        discipline="Electrical",
        category="DD50 model",
        requirement_text="Electrical model evidence is required at DD50.",
        content_hash="requirement-1",
        resource="Owner requirement matrix",
        is_active=True,
    )
    compliance = RequirementCompliance(
        id=1,
        requirement_id=requirement.id,
        project_id=project.id,
        model_id=model.id,
        status="compliant",
        evidence={"milestone": "DD50", "evidence_type": "model"},
        evaluated_at=now,
    )
    issue = Issue(
        id=1,
        organization_id=organization.id,
        project_id=project.id,
        model_id=model.id,
        export_id=export.id,
        element_db_id=element.id,
        rule_code="ELECTRICAL_CLEARANCE",
        issue_type="electrical_clearance",
        severity="high",
        status="open",
        source="automated",
        message="Electrical clearance needs review.",
    )

    db_session.add_all(
        [
            organization,
            client,
            project,
            model,
            export,
            element,
            requirement,
            compliance,
            issue,
        ]
    )
    db_session.commit()

    result = build_project_readiness(db_session, project)

    assert result.project_id == project.id
    assert result.project_title == "Readiness Baseline"
    assert result.client_id == client.id
    assert result.client_name == "Owner Agency"
    assert result.latest_export_id == export.id
    assert result.latest_sync_at == completed_at.replace(tzinfo=None)
    assert result.requirement_coverage.score == 100.0
    assert result.requirement_coverage.detail == "1 of 1 applicable owner requirements covered"
    assert result.qaqc_health.score == 98.0
    assert result.qaqc_health.detail == "1 open issues across 1 model elements"
    assert result.sync_freshness.score == 100.0
    assert result.sync_freshness.detail == "Latest completed sync found"
    assert result.overall_readiness == 99.4
    assert result.label == "Ready"
    assert result.open_issues == {"high": 1, "critical": 0, "medium": 0, "low": 0}
    assert result.trade_readiness[0].discipline == "Electrical"
    assert result.trade_readiness[0].requirements_total == 1
    assert result.trade_readiness[0].requirements_evaluated == 1
    assert result.trade_readiness[0].high_issues == 1
    assert result.trade_readiness[0].readiness == 98.0


def test_build_project_readiness_without_client_or_completed_export(db_session):
    organization = Organization(id=1, name="Test Org")
    project = Project(
        id=1,
        organization_id=organization.id,
        client_id=None,
        project_title="Unassigned Project",
        client_name="Legacy Client Name",
    )

    db_session.add_all([organization, project])
    db_session.commit()

    result = build_project_readiness(db_session, project)

    assert result.project_id == project.id
    assert result.project_title == "Unassigned Project"
    assert result.client_id is None
    assert result.client_name == "Legacy Client Name"
    assert result.latest_export_id is None
    assert result.latest_sync_at is None
    assert result.requirement_coverage.score == 0.0
    assert result.requirement_coverage.detail == "Project is not associated with a client"
    # Empty model (0 elements) carries no QA/QC evidence, so QA health is 0.0, not a
    # free 100.0. A project with no client, no export, no sync, and no elements
    # therefore has 0.0 overall readiness (product truth: missing data != perfect score).
    assert result.qaqc_health.score == 0.0
    assert result.qaqc_health.detail == "0 open issues across 0 model elements"
    assert result.sync_freshness.score == 0.0
    assert result.sync_freshness.detail == "No completed sync found"
    assert result.overall_readiness == 0.0
    assert result.label == "Critical"
    assert result.open_issues == {"critical": 0, "high": 0, "medium": 0, "low": 0}
    assert result.trade_readiness == []
    assert result.gap_summary == {"critical": 0, "high": 1, "medium": 0, "low": 0}


def test_build_project_readiness_excludes_non_actionable_requirements(db_session):
    """Test that non-actionable requirements are excluded from readiness calculations."""
    now = datetime.now(timezone.utc)
    completed_at = now - timedelta(hours=2)

    organization = Organization(id=1, name="Test Org")
    client = Client(
        id=1,
        organization_id=organization.id,
        code="OWNER",
        display_name="Owner Agency",
    )
    project = Project(
        id=1,
        organization_id=organization.id,
        client_id=client.id,
        project_title="Readiness Baseline",
    )
    model = Model(id=1, project_id=project.id, discipline="Electrical")
    export = Export(
        id=1,
        project_id=project.id,
        model_id=model.id,
        export_type="revit",
        status="completed",
        completed_at=completed_at,
    )
    element = Element(
        id=1,
        unique_id="element-1",
        element_id=1001,
        model_id=model.id,
        export_id=export.id,
        category="Electrical Fixtures",
    )

    # Actionable requirement (should be counted)
    actionable_req = Requirement(
        id=1,
        client_id=client.id,
        discipline="Electrical",
        category="DD50 model",
        requirement_text="Electrical model evidence is required at DD50.",
        content_hash="actionable-1",
        resource="Owner requirement matrix",
        is_active=True,
        is_actionable=True,
    )
    
    # Non-actionable requirement (should NOT be counted)
    non_actionable_req = Requirement(
        id=2,
        client_id=client.id,
        discipline="Mechanical",
        category="Manual reference",
        requirement_text="Refer to links column for specs.",
        content_hash="non-actionable-1",
        resource="External link",
        is_active=True,
        is_actionable=False,
    )

    compliance = RequirementCompliance(
        id=1,
        requirement_id=actionable_req.id,
        project_id=project.id,
        model_id=model.id,
        status="compliant",
        evidence={"milestone": "DD50", "evidence_type": "model"},
        evaluated_at=now,
    )
    
    issue = Issue(
        id=1,
        organization_id=organization.id,
        project_id=project.id,
        model_id=model.id,
        export_id=export.id,
        element_db_id=element.id,
        rule_code="ELECTRICAL_CLEARANCE",
        issue_type="electrical_clearance",
        severity="high",
        status="open",
        source="automated",
        message="Electrical clearance needs review.",
    )

    db_session.add_all(
        [
            organization,
            client,
            project,
            model,
            export,
            element,
            actionable_req,
            non_actionable_req,
            compliance,
            issue,
        ]
    )
    db_session.commit()

    result = build_project_readiness(db_session, project)

    # Coverage should be 100% (1/1 actionable requirements covered)
    assert result.requirement_coverage.score == 100.0
    assert result.requirement_coverage.detail == "1 of 1 applicable owner requirements covered"
    
    # Should only list the actionable discipline
    assert len(result.trade_readiness) == 1
    assert result.trade_readiness[0].discipline == "Electrical"
    assert result.trade_readiness[0].requirements_total == 1
    assert result.trade_readiness[0].requirements_evaluated == 1
    assert result.trade_readiness[0].missing_requirements == 0
    assert result.trade_readiness[0].needs_review == 0


def _seed_status_semantics_project(db_session, statuses):
    now = datetime.now(timezone.utc)
    organization = Organization(id=1, name="Test Org")
    client = Client(
        id=1,
        organization_id=organization.id,
        code="OWNER",
        display_name="Owner Agency",
    )
    project = Project(
        id=1,
        organization_id=organization.id,
        client_id=client.id,
        project_title="Readiness Status Semantics",
    )
    model = Model(id=1, project_id=project.id, discipline="Electrical")
    export = Export(
        id=1,
        project_id=project.id,
        model_id=model.id,
        export_type="revit",
        status="completed",
        completed_at=now - timedelta(hours=2),
    )

    db_session.add_all([organization, client, project, model, export])
    for index, status in enumerate(statuses, start=1):
        requirement = Requirement(
            id=index,
            client_id=client.id,
            discipline="Electrical",
            category="DD50 model",
            requirement_text=f"Electrical owner requirement {index}.",
            content_hash=f"status-requirement-{index}",
            resource="Owner requirement matrix",
            is_active=True,
            is_actionable=True,
        )
        db_session.add(requirement)
        if status is not None:
            db_session.add(
                RequirementCompliance(
                    id=index,
                    requirement_id=requirement.id,
                    project_id=project.id,
                    model_id=model.id,
                    status=status,
                    evidence={"milestone": "DD50", "evidence_type": "model"},
                    evaluated_at=now,
                )
            )

    db_session.commit()
    return project


def test_non_compliant_is_evaluated_but_not_covered(db_session):
    project = _seed_status_semantics_project(
        db_session,
        statuses=["compliant", "non_compliant"],
    )

    result = build_project_readiness(db_session, project)

    assert result.requirement_coverage.score == 50.0
    assert result.requirement_coverage.detail == "1 of 2 applicable owner requirements covered"
    assert result.trade_readiness[0].requirements_total == 2
    assert result.trade_readiness[0].requirements_evaluated == 2
    assert result.trade_readiness[0].missing_requirements == 0
    assert result.trade_readiness[0].needs_review == 0
    assert result.trade_readiness[0].readiness == 50.0


def test_not_applicable_is_excluded_from_applicable_denominator(db_session):
    project = _seed_status_semantics_project(
        db_session,
        statuses=["compliant", "not_applicable"],
    )

    result = build_project_readiness(db_session, project)

    assert result.requirement_coverage.score == 100.0
    assert result.requirement_coverage.detail == "1 of 1 applicable owner requirements covered"
    assert result.trade_readiness[0].requirements_total == 1
    assert result.trade_readiness[0].requirements_evaluated == 1
    assert result.trade_readiness[0].missing_requirements == 0
    assert result.trade_readiness[0].needs_review == 0
    assert result.trade_readiness[0].readiness == 100.0


def test_needs_review_is_evaluated_visible_and_not_covered(db_session):
    project = _seed_status_semantics_project(
        db_session,
        statuses=["compliant", "needs_review"],
    )

    result = build_project_readiness(db_session, project)

    assert result.requirement_coverage.score == 50.0
    assert result.requirement_coverage.detail == "1 of 2 applicable owner requirements covered"
    assert result.trade_readiness[0].requirements_total == 2
    assert result.trade_readiness[0].requirements_evaluated == 2
    assert result.trade_readiness[0].missing_requirements == 0
    assert result.trade_readiness[0].needs_review == 1
    assert result.trade_readiness[0].readiness == 50.0


def test_missing_compliance_row_counts_as_missing_requirement(db_session):
    project = _seed_status_semantics_project(
        db_session,
        statuses=["compliant", None],
    )

    result = build_project_readiness(db_session, project)

    assert result.requirement_coverage.score == 50.0
    assert result.requirement_coverage.detail == "1 of 2 applicable owner requirements covered"
    assert result.trade_readiness[0].requirements_total == 2
    assert result.trade_readiness[0].requirements_evaluated == 1
    assert result.trade_readiness[0].missing_requirements == 1
    assert result.trade_readiness[0].needs_review == 0
    assert result.trade_readiness[0].readiness == 50.0


def test_accepted_evidence_counts_as_covered(db_session):
    project = _seed_evidence_semantics_project(
        db_session,
        review_status="accepted",
        evidence_status="covered",
    )

    result = build_project_readiness(db_session, project)

    assert result.requirement_coverage.score == 100.0
    assert result.requirement_coverage.detail == "1 of 1 applicable owner requirements covered"
    assert result.trade_readiness[0].requirements_total == 1
    assert result.trade_readiness[0].requirements_evaluated == 1
    assert result.trade_readiness[0].missing_requirements == 0
    assert result.trade_readiness[0].needs_review == 0
    assert result.trade_readiness[0].readiness == 100.0


def test_candidate_evidence_does_not_count_as_covered(db_session):
    project = _seed_evidence_semantics_project(
        db_session,
        review_status="candidate",
        evidence_status="needs_review",
    )

    result = build_project_readiness(db_session, project)

    assert result.requirement_coverage.score == 0.0
    assert result.requirement_coverage.detail == "0 of 1 applicable owner requirements covered"
    assert result.trade_readiness[0].requirements_total == 1
    assert result.trade_readiness[0].requirements_evaluated == 1
    assert result.trade_readiness[0].missing_requirements == 0
    assert result.trade_readiness[0].needs_review == 1
    assert result.trade_readiness[0].readiness == 0.0


def test_rejected_evidence_does_not_count_as_covered(db_session):
    project = _seed_evidence_semantics_project(
        db_session,
        review_status="rejected",
        evidence_status="missing",
    )

    result = build_project_readiness(db_session, project)

    assert result.requirement_coverage.score == 0.0
    assert result.requirement_coverage.detail == "0 of 1 applicable owner requirements covered"
    assert result.trade_readiness[0].requirements_total == 1
    assert result.trade_readiness[0].requirements_evaluated == 0
    assert result.trade_readiness[0].missing_requirements == 1
    assert result.trade_readiness[0].needs_review == 0
    assert result.trade_readiness[0].readiness == 0.0


def _seed_evidence_semantics_project(db_session, review_status: str, evidence_status: str):
    now = datetime.now(timezone.utc)
    organization = Organization(id=10, name="Evidence Org")
    client = Client(
        id=10,
        organization_id=organization.id,
        code="OWNER",
        display_name="Owner Agency",
    )
    project = Project(
        id=10,
        organization_id=organization.id,
        client_id=client.id,
        project_title="Evidence Coverage Project",
    )
    requirement = Requirement(
        id=10,
        client_id=client.id,
        discipline="Electrical",
        category="DD50 model",
        requirement_text="Electrical model evidence is required at DD50.",
        content_hash="evidence-1",
        resource="Owner requirement matrix",
        is_active=True,
        is_actionable=True,
    )
    evidence = RequirementEvidence(
        id=10,
        project_id=project.id,
        requirement_id=requirement.id,
        evidence_type="manual",
        evidence_status=evidence_status,
        source_ref=f"manual:req:{requirement.id}",
        confidence=0.9,
        metadata_json={
            "review_status": review_status,
            "source_label": f"REQ-{requirement.id}",
            "review_note": "seeded test evidence",
            "reviewed_by": "test_user",
            "reviewed_at": now.isoformat(),
        },
        created_at=now,
        updated_at=now,
    )

    db_session.add_all([organization, client, project, requirement, evidence])
    db_session.commit()
    return project
