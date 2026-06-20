"""SEION-KGE advisory graph export tests."""

from __future__ import annotations

import json
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
    SeionPrediction,
)
from app.readiness.service import build_project_readiness
from app.seion.exporter import export_seion_graph


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


def _seed_project(db_session: Session) -> Project:
    now = datetime.now(timezone.utc)
    organization = Organization(id=1, name="Synthetic Org")
    client = Client(id=1, organization_id=1, code="OWNER", display_name="Synthetic Owner")
    project = Project(id=1, organization_id=1, client_id=1, project_title="Synthetic SEION Project")
    model = Model(id=1, project_id=1, discipline="Electrical", revit_file_name="synthetic.rvt")
    export = Export(
        id=1,
        project_id=1,
        model_id=1,
        export_type="revit",
        status="completed",
        file_name="synthetic_export.json",
        completed_at=now - timedelta(hours=1),
    )
    element = Element(
        id=1,
        unique_id="element-uid-1",
        element_id=1001,
        model_id=1,
        export_id=1,
        category="Electrical Fixtures",
        name="Fixture A",
    )
    requirement = Requirement(
        id=1,
        client_id=1,
        discipline="Electrical",
        category="Power",
        requirement_text="Provide electrical fixture evidence.",
        content_hash="req-1",
        is_active=True,
        is_actionable=True,
    )
    compliance = RequirementCompliance(
        id=1,
        requirement_id=1,
        project_id=1,
        model_id=1,
        status="compliant",
        evaluated_at=now,
    )
    issue = Issue(
        id=1,
        organization_id=1,
        project_id=1,
        model_id=1,
        export_id=1,
        element_db_id=1,
        rule_code="R001",
        issue_type="missing_parameter",
        severity="high",
        status="open",
        source="automated",
        message="Synthetic issue.",
    )
    db_session.add_all([organization, client, project, model, export, element, requirement, compliance, issue])
    db_session.commit()
    return project


def _jsonl(path):
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]


def test_exporter_writes_entities_and_triples_jsonl(db_session, tmp_path):
    _seed_project(db_session)

    result = export_seion_graph(db_session, tmp_path)

    assert result.entity_count > 0
    assert result.triple_count > 0
    assert result.entities_path.exists()
    assert result.triples_path.exists()

    entities = _jsonl(result.entities_path)
    triples = _jsonl(result.triples_path)
    assert {"uid": "project:1", "type": "project", "label": "Synthetic SEION Project", "properties": {"has_client": True}} in entities
    assert {"head": "project:1", "relation": "belongs_to_client", "tail": "client:1"} in triples
    assert {"head": "export:1", "relation": "contains_element", "tail": "element:1"} in triples
    assert {"head": "requirement_compliance:1", "relation": "evaluates", "tail": "requirement:1"} in triples


def test_exporter_handles_missing_optional_data(db_session, tmp_path):
    organization = Organization(id=1, name="Synthetic Org")
    project = Project(id=1, organization_id=1, project_title="No Optional Rows")
    db_session.add_all([organization, project])
    db_session.commit()

    result = export_seion_graph(db_session, tmp_path)

    assert result.entity_count == 1
    assert result.triple_count == 0
    assert result.warnings == []
    assert result.entities_path.exists()
    assert result.triples_path.exists()


def test_suggested_prediction_does_not_change_official_readiness(db_session):
    project = _seed_project(db_session)
    before = build_project_readiness(db_session, project)
    prediction = SeionPrediction(
        id=1,
        project_id=project.id,
        head_uid="requirement:1",
        relation="suggests_evidence",
        tail_uid="element:1",
        score=0.91,
        rank=1,
        model_version="seion-test",
    )
    db_session.add(prediction)
    db_session.commit()

    after = build_project_readiness(db_session, project)

    assert after.overall_readiness == before.overall_readiness
    assert after.requirement_coverage.score == before.requirement_coverage.score

