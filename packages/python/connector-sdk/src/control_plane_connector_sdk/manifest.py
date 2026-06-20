from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class ConnectorManifest:
    connector_id: str
    version: str
    runtime: str
    capabilities: tuple[str, ...]

