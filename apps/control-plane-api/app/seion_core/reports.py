"""SEION report persistence helpers."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any


def write_audit_report(report: dict[str, Any], path: str | Path) -> Path:
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(report, indent=2, sort_keys=True, default=str), encoding="utf-8")
    return target


def load_audit_report(path: str | Path) -> dict[str, Any]:
    return json.loads(Path(path).read_text(encoding="utf-8"))
