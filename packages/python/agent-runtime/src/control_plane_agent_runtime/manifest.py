from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class AgentManifest:
    agent_id: str
    version: str
    capability: str
    required_approval: str
    maximum_actions: int

