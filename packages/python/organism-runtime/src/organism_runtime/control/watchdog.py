from __future__ import annotations

from dataclasses import dataclass
from enum import StrEnum

from organism_runtime.control.mission import MissionState


class WatchdogAction(StrEnum):
    CONTINUE = "continue"
    PAUSE = "pause"
    ESCALATE = "escalate"


@dataclass(frozen=True, slots=True)
class WatchdogDecision:
    action: WatchdogAction
    reason: str


def evaluate_progress(mission: MissionState, repeated_failure_count: int = 0) -> WatchdogDecision:
    if mission.iteration >= mission.budget.max_iterations:
        return WatchdogDecision(WatchdogAction.ESCALATE, "iteration budget exhausted")
    if repeated_failure_count >= mission.budget.max_failures_per_action:
        return WatchdogDecision(WatchdogAction.ESCALATE, "repeated action failures")
    if mission.pending_approvals:
        return WatchdogDecision(WatchdogAction.PAUSE, "waiting for approval")
    return WatchdogDecision(WatchdogAction.CONTINUE, "within mission budget")

