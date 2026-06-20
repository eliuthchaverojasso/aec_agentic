"""Tests for the Requirement Audit & Evaluation Bundle API and ingest service."""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from app.api import requirement_audits as audits_api
from app.database import get_db
from app.main import app
from app.models import (
    Project,
    RequirementAuditRecord,
    RequirementAuditRun,
    RequirementCoherenceFinding,
)
from app.services.requirement_audit_ingest import (
    BundleValidationError,
    IngestResult,
    ingest_evaluation_bundle,
)

FIXTURE = Path(__file__).parent / "fixtures" / "sample_evaluation_bundle.json"


def _load_bundle() -> dict:
    return json.loads(FIXTURE.read_text(encoding="utf-8"))


# --------------------------------------------------------------------------- #
# Fakes
# --------------------------------------------------------------------------- #


class FakeScalars:
    def __init__(self, rows):
        self._rows = rows

    def all(self):
        return list(self._rows)


class FakeDb:
    """Minimal Session stand-in for endpoint tests (no real database)."""

    def __init__(self, *, objects=None, scalars_rows=None, scalar_value=None):
        self.objects = objects or {}
        self.scalars_rows = scalars_rows if scalars_rows is not None else []
        self.scalar_value = scalar_value
        self.added = []
        self.committed = False

    def get(self, model, object_id):
        return self.objects.get((model, object_id))

    def add(self, obj):
        self.added.append(obj)

    def flush(self):
        pass

    def commit(self):
        self.committed = True

    def refresh(self, obj):
        if getattr(obj, "id", None) is None:
            obj.id = 1
        if getattr(obj, "created_at", None) is None:
            obj.created_at = datetime.now(timezone.utc)

    def scalar(self, _stmt):
        return self.scalar_value

    def scalars(self, _stmt):
        return FakeScalars(self.scalars_rows)


class CapturingSession:
    """Records add()ed ORM objects for ingest-service unit tests."""

    def __init__(self, existing_run=None, requirement=None):
        self.added = []
        self.committed = False
        self._existing_run = existing_run
        self._requirement = requirement
        self._scalar_calls = 0

    def add(self, obj):
        self.added.append(obj)

    def flush(self):
        for obj in self.added:
            if isinstance(obj, RequirementAuditRun) and obj.id is None:
                obj.id = 42

    def commit(self):
        self.committed = True

    def refresh(self, obj):
        pass

    def scalar(self, _stmt):
        # First scalar() = existing-run lookup; subsequent = requirement link lookups.
        self._scalar_calls += 1
        if self._scalar_calls == 1:
            return self._existing_run
        return self._requirement

    def added_of(self, model):
        return [obj for obj in self.added if isinstance(obj, model)]


def _override_db(fake):
    def _get_db():
        yield fake

    app.dependency_overrides[get_db] = _get_db


def _project(project_id: int = 8, client_id: int | None = None) -> Project:
    return Project(id=project_id, organization_id=1, client_id=client_id, project_title="Sample")


def _make_run(**overrides) -> RequirementAuditRun:
    defaults = dict(
        id=1,
        project_id=8,
        run_uid="abc123",
        run_status="completed",
        as_of=datetime(2026, 6, 15, 12, 0, tzinfo=timezone.utc),
        schema_version="1.0",
        requirements_total=9,
        status_counts={"met": 1, "not_met": 2, "needs_human_review": 6},
        coherence_grade="Conflicts Found",
        coherence_findings_total=4,
        ingested_at=datetime(2026, 6, 15, 12, 5, tzinfo=timezone.utc),
    )
    defaults.update(overrides)
    return RequirementAuditRun(**defaults)


# --------------------------------------------------------------------------- #
# Ingest service (unit, no DB)
# --------------------------------------------------------------------------- #


def test_ingest_service_persists_run_records_and_findings():
    bundle = _load_bundle()
    session = CapturingSession()

    result = ingest_evaluation_bundle(
        session,
        _project(client_id=None),
        manifest=bundle["manifest"],
        audit_records=bundle["auditRecords"],
        coherence=bundle["coherence"],
    )

    assert result.reused_existing is False
    assert result.records_ingested == 9
    assert result.coherence_findings_ingested == 4
    assert result.requirements_linked == 0  # no client -> opportunistic link not attempted
    assert session.committed is True

    run = session.added_of(RequirementAuditRun)[0]
    assert run.run_uid == bundle["manifest"]["evaluationRunId"]
    assert run.coherence_grade == "Conflicts Found"
    assert run.status_counts["met"] == 1

    records = session.added_of(RequirementAuditRecord)
    assert len(records) == 9
    # Decision is projected, not recomputed: the bundle's statuses are preserved verbatim.
    statuses = {r.requirement_uid: r.decision_status for r in records}
    assert statuses["ROW-101"] == "Compliant"
    assert statuses["ROW-102"] == "NonCompliant"

    findings = session.added_of(RequirementCoherenceFinding)
    assert len(findings) == 4
    assert {f.finding_type for f in findings} == {
        "ExactDuplicate",
        "NumericConflict",
        "QuantityConflict",
        "ManufacturerConflict",
    }


def test_ingest_service_rejects_unsupported_schema_version():
    bundle = _load_bundle()
    bundle["manifest"]["schemaVersion"] = "9.9"
    with pytest.raises(BundleValidationError):
        ingest_evaluation_bundle(
            CapturingSession(),
            _project(),
            manifest=bundle["manifest"],
            audit_records=bundle["auditRecords"],
            coherence=bundle["coherence"],
        )


def test_ingest_service_requires_run_uid():
    bundle = _load_bundle()
    bundle["manifest"].pop("evaluationRunId", None)
    with pytest.raises(BundleValidationError):
        ingest_evaluation_bundle(
            CapturingSession(),
            _project(),
            manifest=bundle["manifest"],
            audit_records=bundle["auditRecords"],
            coherence=bundle["coherence"],
        )


def test_ingest_service_is_idempotent_when_run_already_exists():
    bundle = _load_bundle()
    existing = _make_run(run_uid=bundle["manifest"]["evaluationRunId"])
    existing.records = []
    existing.coherence_findings = []
    session = CapturingSession(existing_run=existing)

    result = ingest_evaluation_bundle(
        session,
        _project(),
        manifest=bundle["manifest"],
        audit_records=bundle["auditRecords"],
        coherence=bundle["coherence"],
    )

    assert result.reused_existing is True
    assert session.added == []  # nothing re-created


# --------------------------------------------------------------------------- #
# API endpoints
# --------------------------------------------------------------------------- #


def test_post_requirement_audit_ingests_bundle(monkeypatch):
    bundle = _load_bundle()
    fake = FakeDb(objects={(Project, 8): _project()})
    _override_db(fake)

    fake_result = IngestResult(
        run=_make_run(),
        records_ingested=9,
        coherence_findings_ingested=4,
        requirements_linked=0,
        reused_existing=False,
    )
    monkeypatch.setattr(audits_api, "ingest_evaluation_bundle", lambda *a, **k: fake_result)

    try:
        response = TestClient(app).post("/api/v1/projects/8/requirement-audits", json=bundle)
    finally:
        app.dependency_overrides.clear()

    assert response.status_code == 201
    body = response.json()
    assert body["records_ingested"] == 9
    assert body["coherence_findings_ingested"] == 4
    assert body["run"]["coherence_grade"] == "Conflicts Found"
    assert body["run"]["run_uid"] == "abc123"


def test_post_requirement_audit_unknown_project_returns_404():
    fake = FakeDb(objects={})
    _override_db(fake)
    try:
        response = TestClient(app).post("/api/v1/projects/999/requirement-audits", json=_load_bundle())
    finally:
        app.dependency_overrides.clear()
    assert response.status_code == 404


def test_post_requirement_audit_invalid_bundle_returns_422(monkeypatch):
    fake = FakeDb(objects={(Project, 8): _project()})
    _override_db(fake)

    def _raise(*_a, **_k):
        raise BundleValidationError("Unsupported bundle schema_version '9.9'.")

    monkeypatch.setattr(audits_api, "ingest_evaluation_bundle", _raise)
    try:
        response = TestClient(app).post("/api/v1/projects/8/requirement-audits", json=_load_bundle())
    finally:
        app.dependency_overrides.clear()
    assert response.status_code == 422
    assert "schema_version" in response.json()["detail"]


def test_list_requirement_audit_runs():
    fake = FakeDb(objects={(Project, 8): _project()}, scalars_rows=[_make_run()])
    _override_db(fake)
    try:
        response = TestClient(app).get("/api/v1/projects/8/requirement-audits")
    finally:
        app.dependency_overrides.clear()
    assert response.status_code == 200
    runs = response.json()
    assert len(runs) == 1
    assert runs[0]["coherence_findings_total"] == 4


def test_list_coherence_findings_for_run():
    finding = RequirementCoherenceFinding(
        id=1,
        run_id=1,
        finding_uid="numericconflict:electrical#102|electrical#103",
        finding_type="NumericConflict",
        severity="High",
        requirement_type="parameter_performance",
        status="open",
        rationale="Conflicting voltage for panel: 208V vs 240V.",
        primary_requirement={"requirementId": "ROW-102"},
        related_requirement={"requirementId": "ROW-103"},
        normalized_values={"value_a": "208", "value_b": "240"},
        created_at=datetime(2026, 6, 15, 12, 5, tzinfo=timezone.utc),
    )
    fake = FakeDb(objects={(RequirementAuditRun, 1): _make_run()}, scalars_rows=[finding])
    _override_db(fake)
    try:
        response = TestClient(app).get("/api/v1/projects/8/requirement-audits/1/coherence")
    finally:
        app.dependency_overrides.clear()
    assert response.status_code == 200
    findings = response.json()
    assert findings[0]["finding_type"] == "NumericConflict"
    assert findings[0]["severity"] == "High"


def test_create_review_decision_is_append_only_and_captures_previous_status():
    run = _make_run()
    record = RequirementAuditRecord(
        id=5,
        run_id=1,
        requirement_uid="ROW-102",
        decision_status="NonCompliant",
        lifecycle_status="CoherenceChecked",
        applies=True,
        direct_evidence_count=0,
        supporting_evidence_count=0,
    )
    fake = FakeDb(objects={(RequirementAuditRun, 1): run, (RequirementAuditRecord, 5): record})
    _override_db(fake)

    payload = {"action": "override", "reason": "Owner accepted alternate panel rating.", "reviewer_name": "E. Chavero", "resulting_status": "Compliant"}
    try:
        response = TestClient(app).post(
            "/api/v1/projects/8/requirement-audits/1/records/5/review", json=payload
        )
    finally:
        app.dependency_overrides.clear()

    assert response.status_code == 201
    body = response.json()
    assert body["action"] == "override"
    assert body["previous_status"] == "NonCompliant"  # captured from the immutable record
    assert body["resulting_status"] == "Compliant"
    # The engine decision on the record itself is never mutated.
    assert record.decision_status == "NonCompliant"
