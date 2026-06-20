from __future__ import annotations

from dataclasses import dataclass
from enum import StrEnum


class AgentRole(StrEnum):
    PLANNER = "planner"
    RESEARCHER = "researcher"
    EXECUTOR = "executor"
    CRITIC = "critic"
    MEMORY_CURATOR = "memory_curator"


@dataclass(frozen=True, slots=True)
class AgentContract:
    role: AgentRole
    can_modify_files: bool
    can_execute_commands: bool
    requires_provenance: bool = True


DEFAULT_AGENT_CONTRACTS = {
    AgentRole.PLANNER: AgentContract(AgentRole.PLANNER, False, False),
    AgentRole.RESEARCHER: AgentContract(AgentRole.RESEARCHER, False, False),
    AgentRole.EXECUTOR: AgentContract(AgentRole.EXECUTOR, True, True),
    AgentRole.CRITIC: AgentContract(AgentRole.CRITIC, False, True),
    AgentRole.MEMORY_CURATOR: AgentContract(AgentRole.MEMORY_CURATOR, True, False),
}

