"""Electrical rule-pack extension point for pilot hardening."""

from __future__ import annotations

from app.rules.core_rules import CORE_RULES


ELECTRICAL_RULES = [rule for rule in CORE_RULES if rule.discipline == "electrical"]
