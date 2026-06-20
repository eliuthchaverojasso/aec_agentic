"""Small rule registry foundation for pilot rule packs."""

from __future__ import annotations

from app.rules.base import BaseRule


class RuleRegistry:
    def __init__(self) -> None:
        self._rules: dict[str, BaseRule] = {}

    def register(self, rule: BaseRule) -> None:
        self._rules[rule.rule_code] = rule

    def get(self, rule_code: str) -> BaseRule | None:
        return self._rules.get(rule_code)

    def all(self) -> list[BaseRule]:
        return list(self._rules.values())
