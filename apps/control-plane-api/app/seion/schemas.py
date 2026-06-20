"""Internal schemas for SEION-KGE graph export."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class SeionEntity:
    uid: str
    type: str
    label: str | None = None
    properties: dict[str, Any] = field(default_factory=dict)


@dataclass(frozen=True)
class SeionTriple:
    head: str
    relation: str
    tail: str
    properties: dict[str, Any] = field(default_factory=dict)


@dataclass(frozen=True)
class SeionGraphExportResult:
    entity_count: int
    triple_count: int
    entities_path: Path
    triples_path: Path
    warnings: list[str] = field(default_factory=list)

