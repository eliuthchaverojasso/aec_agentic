"""Shared abstractions for modular EMA AI rules."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Protocol


@dataclass(frozen=True)
class RuleFinding:
    rule_code: str
    severity: str
    issue_type: str
    message: str
    element_unique_id: str | None = None
    observed_values: dict[str, Any] = field(default_factory=dict)


class BaseRule(Protocol):
    rule_code: str
    name: str
    discipline: str
    severity: str

    def evaluate_element(self, element: dict[str, Any]) -> list[RuleFinding]:
        """Evaluate one normalized element record."""
