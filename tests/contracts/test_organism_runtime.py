from __future__ import annotations

from datetime import datetime, timedelta, timezone

import pytest

from organism_runtime.control.lease import TaskLease
from organism_runtime.control.mission import MissionBudget, MissionState, MissionStatus
from organism_runtime.control.watchdog import WatchdogAction, evaluate_progress
from organism_runtime.gateway.routing import DEFAULT_OLLAMA_ROUTES, ModelCapability, ModelGateway
from organism_runtime.governance.risk import RiskLevel, evaluate_risk
from organism_runtime.memory.artifact import ArtifactMetadata, Sensitivity
from organism_runtime.supervision.gpt_packet import SupervisionPacket


def test_mission_escalates_when_iteration_budget_is_exhausted() -> None:
    mission = MissionState(objective="Implement module", budget=MissionBudget(max_iterations=1))
    mission.start()
    mission.advance_iteration()

    with pytest.raises(RuntimeError):
        mission.advance_iteration()

    assert mission.status == MissionStatus.ESCALATED


def test_watchdog_pauses_for_pending_approval() -> None:
    mission = MissionState(objective="Deploy release")
    mission.require_approval("approval-1")

    decision = evaluate_progress(mission)

    assert decision.action == WatchdogAction.PAUSE


def test_task_lease_expiration() -> None:
    lease = TaskLease(
        task_id="task-1",
        worker_id="worker-1",
        acquired_at=datetime(2026, 1, 1, tzinfo=timezone.utc),
        ttl_seconds=60,
    )

    assert lease.is_expired(datetime(2026, 1, 1, 0, 2, tzinfo=timezone.utc))
    assert not lease.is_expired(datetime(2026, 1, 1, 0, 0, 30, tzinfo=timezone.utc))
    assert lease.expires_at == datetime(2026, 1, 1, 0, 1, tzinfo=timezone.utc)


def test_model_gateway_resolves_installed_ollama_routes() -> None:
    gateway = ModelGateway(DEFAULT_OLLAMA_ROUTES)

    assert gateway.resolve(ModelCapability.CODE_PRODUCTION).model == "qwen3.6:27b"
    assert gateway.resolve("critical_review").model == "gemma4:26b"
    assert gateway.resolve("document_extraction").model == "granite4.1:30b"
    assert gateway.resolve("embeddings").model == "bge-m3:latest"


def test_governance_requires_approval_for_level_four() -> None:
    decision = evaluate_risk(RiskLevel.DESTRUCTIVE_OR_EXTERNAL, mission_scoped=True)

    assert not decision.allowed
    assert decision.requires_human_approval


def test_secret_artifacts_are_not_indexable() -> None:
    artifact = ArtifactMetadata(
        artifact_id="sha256:abc",
        source="local_repo",
        mime_type="text/plain",
        content_hash="abc",
        sensitivity=Sensitivity.SECRET,
    )

    assert not artifact.is_indexable()


def test_supervision_packet_markdown_contains_questions() -> None:
    packet = SupervisionPacket(
        mission_id="mission-1",
        objective="Finish scaffold",
        status_summary="Runtime contracts complete.",
        evidence=("tests passed",),
        risks=("database not started",),
        pending_decisions=("choose first LangGraph adapter",),
        questions=("Proceed with DB adapter?",),
    )

    text = packet.to_markdown()

    assert "# Supervision Packet: mission-1" in text
    assert "- Proceed with DB adapter?" in text

