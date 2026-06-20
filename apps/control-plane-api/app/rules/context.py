"""Rule execution context objects."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True)
class RuleContext:
    project_id: int | None = None
    model_id: int | None = None
    export_id: int | None = None
    metadata: dict[str, Any] | None = None
