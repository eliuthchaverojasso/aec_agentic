from __future__ import annotations

from dataclasses import dataclass, field
from enum import StrEnum
from uuid import uuid4


class MissionStatus(StrEnum):
    CREATED = "created"
    RUNNING = "running"
    PAUSED = "paused"
    WAITING_APPROVAL = "waiting_approval"
    COMPLETED = "completed"
    FAILED = "failed"
    ESCALATED = "escalated"


@dataclass(slots=True)
class MissionBudget:
    max_iterations: int = 40
    max_runtime_hours: float = 8
    max_failures_per_action: int = 3

    def __post_init__(self) -> None:
        if self.max_iterations <= 0:
            raise ValueError("max_iterations must be positive")
        if self.max_runtime_hours <= 0:
            raise ValueError("max_runtime_hours must be positive")
        if self.max_failures_per_action <= 0:
            raise ValueError("max_failures_per_action must be positive")


@dataclass(slots=True)
class MissionState:
    objective: str
    mission_id: str = field(default_factory=lambda: f"mission-{uuid4()}")
    status: MissionStatus = MissionStatus.CREATED
    current_phase: str = "intake"
    active_plan_version: int = 1
    iteration: int = 0
    budget: MissionBudget = field(default_factory=MissionBudget)
    current_task: str | None = None
    relevant_artifacts: tuple[str, ...] = ()
    retrieved_memories: tuple[str, ...] = ()
    pending_approvals: tuple[str, ...] = ()

    def start(self) -> None:
        if self.status != MissionStatus.CREATED:
            raise ValueError(f"cannot start mission from {self.status}")
        self.status = MissionStatus.RUNNING

    def advance_iteration(self) -> None:
        if self.iteration >= self.budget.max_iterations:
            self.status = MissionStatus.ESCALATED
            raise RuntimeError("mission iteration budget exhausted")
        self.iteration += 1

    def require_approval(self, approval_id: str) -> None:
        self.pending_approvals = (*self.pending_approvals, approval_id)
        self.status = MissionStatus.WAITING_APPROVAL

    def complete(self) -> None:
        if self.pending_approvals:
            raise ValueError("cannot complete mission with pending approvals")
        self.status = MissionStatus.COMPLETED

