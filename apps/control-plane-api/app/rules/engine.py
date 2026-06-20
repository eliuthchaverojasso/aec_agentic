"""Modular rule engine foundation."""

from __future__ import annotations

import time
from dataclasses import dataclass, field

from app.rules.base import RuleFinding
from app.rules.context import RuleContext
from app.rules.registry import RuleRegistry


@dataclass(frozen=True)
class RuleExecutionResult:
    rule_code: str
    status: str
    findings: list[RuleFinding] = field(default_factory=list)
    duration_ms: int = 0
    error_message: str | None = None


class RuleEngine:
    def __init__(self, registry: RuleRegistry) -> None:
        self.registry = registry

    def evaluate_element(
        self,
        element: dict,
        context: RuleContext | None = None,
    ) -> list[RuleExecutionResult]:
        _ = context
        results: list[RuleExecutionResult] = []
        for rule in self.registry.all():
            started = time.monotonic()
            try:
                findings = rule.evaluate_element(element)
                results.append(
                    RuleExecutionResult(
                        rule_code=rule.rule_code,
                        status="completed",
                        findings=findings,
                        duration_ms=int((time.monotonic() - started) * 1000),
                    )
                )
            except Exception as exc:  # noqa: BLE001
                results.append(
                    RuleExecutionResult(
                        rule_code=rule.rule_code,
                        status="failed",
                        duration_ms=int((time.monotonic() - started) * 1000),
                        error_message=str(exc),
                    )
                )
        return results
