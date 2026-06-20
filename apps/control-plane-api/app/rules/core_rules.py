"""Core model QA/QC rules wrapped in the modular registry shape."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Callable

from app.ingestion import rules as legacy_rules
from app.rules.base import RuleFinding


@dataclass(frozen=True)
class LegacyElementRule:
    rule_code: str
    name: str
    discipline: str
    severity: str
    _fn: Callable[[dict[str, Any]], list[legacy_rules.RuleFinding]]

    def evaluate_element(self, element: dict[str, Any]) -> list[RuleFinding]:
        return [
            RuleFinding(
                rule_code=finding.rule_code,
                severity=finding.severity,
                issue_type=finding.issue_type,
                message=finding.message,
                element_unique_id=element.get("UniqueId"),
                observed_values=finding.observed_values,
            )
            for finding in self._fn(element)
        ]


CORE_RULES = [
    LegacyElementRule(
        "R001",
        "Element Without Level",
        "all",
        "low",
        legacy_rules.rule_r001_missing_level,
    ),
    LegacyElementRule(
        "R002",
        "Unconnected Fixture",
        "electrical",
        "high",
        legacy_rules.rule_r002_unconnected_fixture,
    ),
    LegacyElementRule(
        "R003",
        "Fixture Missing Circuit",
        "electrical",
        "medium",
        legacy_rules.rule_r003_fixture_missing_circuit,
    ),
    LegacyElementRule(
        "R004",
        "Panel Without Source",
        "electrical",
        "high",
        legacy_rules.rule_r004_panel_without_source,
    ),
]
