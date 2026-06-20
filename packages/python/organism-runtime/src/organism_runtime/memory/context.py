from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class ContextPackage:
    doctrine: tuple[str, ...]
    policies: tuple[str, ...]
    objective: str
    mission_state: str
    active_plan: str
    files: tuple[str, ...]
    memories: tuple[str, ...]
    recent_errors: tuple[str, ...] = ()

    def sections(self) -> tuple[str, ...]:
        return (
            *self.doctrine,
            *self.policies,
            self.objective,
            self.mission_state,
            self.active_plan,
            *self.files,
            *self.memories,
            *self.recent_errors,
        )

