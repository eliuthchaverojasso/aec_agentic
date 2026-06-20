from __future__ import annotations

import json
from pathlib import Path

from control_plane_policy import evaluate_required_fields


ROOT = Path(__file__).resolve().parents[2]


def test_fixture_missing_circuit_conformance() -> None:
    rule = json.loads((ROOT / "standard/policies/rules/fixture-missing-circuit.rule.json").read_text())
    fixture = json.loads((ROOT / "standard/conformance/rules/fixtures/fixture-missing-circuit.input.json").read_text())
    expected = json.loads(
        (ROOT / "standard/conformance/rules/expected-results/fixture-missing-circuit.expected.json").read_text()
    )

    result = evaluate_required_fields(
        rule_id=rule["rule_id"],
        payload=fixture["payload"],
        required_fields=tuple(rule["required_fields"]),
    )

    assert result.rule_id == expected["rule_id"]
    assert result.passed is expected["passed"]
    assert list(result.missing_fields) == expected["missing_fields"]

