from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import StrEnum


class Sensitivity(StrEnum):
    PUBLIC = "public"
    INTERNAL = "internal"
    CONFIDENTIAL = "confidential"
    SECRET = "secret"


@dataclass(frozen=True, slots=True)
class ArtifactMetadata:
    artifact_id: str
    source: str
    mime_type: str
    content_hash: str
    project_id: str | None = None
    sensitivity: Sensitivity = Sensitivity.INTERNAL
    parser_version: str = "0.1.0"
    provenance: str = ""
    created_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))

    def is_indexable(self) -> bool:
        return self.sensitivity not in {Sensitivity.SECRET}

