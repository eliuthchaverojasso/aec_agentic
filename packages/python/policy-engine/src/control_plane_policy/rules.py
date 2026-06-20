from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True, slots=True)
class RuleEvaluation:
    rule_id: str
    passed: bool
    message: str
    missing_fields: tuple[str, ...] = ()


def evaluate_required_fields(rule_id: str, payload: dict[str, Any], required_fields: tuple[str, ...]) -> RuleEvaluation:
    missing = tuple(field for field in required_fields if payload.get(field) in (None, ""))
    return RuleEvaluation(
        rule_id=rule_id,
        passed=not missing,
        message="passed" if not missing else "missing required fields",
        missing_fields=missing,
    )

