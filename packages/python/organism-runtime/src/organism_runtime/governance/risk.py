from __future__ import annotations

from dataclasses import dataclass
from enum import IntEnum


class RiskLevel(IntEnum):
    READ = 0
    CREATE = 1
    MODIFY = 2
    EXECUTE = 3
    DESTRUCTIVE_OR_EXTERNAL = 4


@dataclass(frozen=True, slots=True)
class PermissionDecision:
    allowed: bool
    requires_human_approval: bool
    reason: str


def evaluate_risk(level: RiskLevel, mission_scoped: bool) -> PermissionDecision:
    if level <= RiskLevel.CREATE:
        return PermissionDecision(True, False, "low-risk action")
    if level == RiskLevel.MODIFY:
        return PermissionDecision(mission_scoped, not mission_scoped, "file modification requires mission scope")
    if level == RiskLevel.EXECUTE:
        return PermissionDecision(mission_scoped, False, "command execution requires mission scope and logging")
    return PermissionDecision(False, True, "destructive or external action requires explicit approval")

