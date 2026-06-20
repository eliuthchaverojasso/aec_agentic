from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class SupervisionPacket:
    mission_id: str
    objective: str
    status_summary: str
    evidence: tuple[str, ...]
    risks: tuple[str, ...]
    pending_decisions: tuple[str, ...]
    questions: tuple[str, ...]

    def to_markdown(self) -> str:
        def bullet(items: tuple[str, ...]) -> str:
            return "\n".join(f"- {item}" for item in items) if items else "- None"

        return "\n".join(
            [
                f"# Supervision Packet: {self.mission_id}",
                "",
                "## Objective",
                self.objective,
                "",
                "## Status",
                self.status_summary,
                "",
                "## Evidence",
                bullet(self.evidence),
                "",
                "## Risks",
                bullet(self.risks),
                "",
                "## Pending Decisions",
                bullet(self.pending_decisions),
                "",
                "## Questions",
                bullet(self.questions),
                "",
            ]
        )

