"""QA/QC rule engine.

Each rule is a pure function: takes an element dict (as parsed from JSON) and
returns zero or more issue payloads. The loader attaches project/model/export
identifiers at insert time.

Rules for Monday MVP (aligned with real Rochell ES data findings):
    R001 Element Without Level
    R002 Unconnected Fixture
    R003 Fixture Missing Circuit
    R004 Panel Without Source
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Callable


FIXTURE_CATEGORIES = {"Electrical Fixtures", "Lighting Fixtures"}
PANEL_CATEGORIES = {"Electrical Equipment"}


@dataclass
class RuleFinding:
    rule_code: str
    severity: str
    issue_type: str
    message: str
    observed_values: dict[str, Any] = field(default_factory=dict)


def _param_value(params: dict[str, Any] | None, name: str) -> str | None:
    """Return the ValueString of a parameter, or None if missing/empty."""
    if not params:
        return None
    entry = params.get(name)
    if not isinstance(entry, dict):
        return None
    if not entry.get("HasValue", False):
        return None
    val = entry.get("ValueString")
    if val is None:
        return None
    val_str = str(val).strip()
    return val_str if val_str else None


# ---------------------------------------------------------------------------
# Rule implementations
# ---------------------------------------------------------------------------


def rule_r001_missing_level(element: dict[str, Any]) -> list[RuleFinding]:
    """R001 -- Element Without Level."""
    level = element.get("Level")
    level_str = str(level).strip() if level is not None else ""
    if level_str:
        return []

    return [
        RuleFinding(
            rule_code="R001",
            severity="low",
            issue_type="field_missing",
            message=(
                f"Element '{element.get('Name', '?')}' "
                f"({element.get('Category', '?')}) has no Level assigned."
            ),
            observed_values={
                "Level": level_str,
                "Category": element.get("Category"),
                "Family": element.get("Family"),
            },
        )
    ]


def rule_r002_unconnected_fixture(element: dict[str, Any]) -> list[RuleFinding]:
    """R002 -- Fixture without Panel assignment."""
    category = element.get("Category")
    if category not in FIXTURE_CATEGORIES:
        return []

    params = element.get("InstanceParameters") or {}
    panel = _param_value(params, "Panel")
    if panel:
        return []

    return [
        RuleFinding(
            rule_code="R002",
            severity="high",
            issue_type="connection_missing",
            message=(
                f"{category} '{element.get('Name', '?')}' is not connected to any Panel."
            ),
            observed_values={
                "Category": category,
                "Family": element.get("Family"),
                "Panel": panel,
            },
        )
    ]


def rule_r003_fixture_missing_circuit(element: dict[str, Any]) -> list[RuleFinding]:
    """R003 -- Fixture assigned to a Panel but without a Circuit Number."""
    category = element.get("Category")
    if category not in FIXTURE_CATEGORIES:
        return []

    params = element.get("InstanceParameters") or {}
    panel = _param_value(params, "Panel")
    if not panel:
        # R002 will cover this; don't double-flag.
        return []

    circuit = _param_value(params, "Circuit Number")
    if circuit:
        return []

    return [
        RuleFinding(
            rule_code="R003",
            severity="medium",
            issue_type="parameter_missing",
            message=(
                f"{category} '{element.get('Name', '?')}' is on panel {panel} "
                f"but has no Circuit Number."
            ),
            observed_values={
                "Category": category,
                "Panel": panel,
                "Circuit Number": circuit,
            },
        )
    ]


def rule_r004_panel_without_source(element: dict[str, Any]) -> list[RuleFinding]:
    """R004 -- Electrical Equipment (panel) without Supply From."""
    category = element.get("Category")
    if category not in PANEL_CATEGORIES:
        return []

    params = element.get("InstanceParameters") or {}
    supply_from = _param_value(params, "Supply From")
    if supply_from:
        return []

    return [
        RuleFinding(
            rule_code="R004",
            severity="high",
            issue_type="connection_missing",
            message=(
                f"Panel '{element.get('Name', '?')}' has no Supply From value. "
                f"It may be a main distribution panel (verify manually) or orphaned."
            ),
            observed_values={
                "Category": category,
                "Family": element.get("Family"),
                "Type": element.get("Type"),
                "Supply From": supply_from,
            },
        )
    ]


# ---------------------------------------------------------------------------
# Registry
# ---------------------------------------------------------------------------


RuleFn = Callable[[dict[str, Any]], list[RuleFinding]]

ALL_RULES: list[RuleFn] = [
    rule_r001_missing_level,
    rule_r002_unconnected_fixture,
    rule_r003_fixture_missing_circuit,
    rule_r004_panel_without_source,
]


def run_all_rules(element: dict[str, Any]) -> list[RuleFinding]:
    """Run every active rule against a single element."""
    findings: list[RuleFinding] = []
    for rule in ALL_RULES:
        findings.extend(rule(element))
    return findings
