from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import StrEnum
from uuid import UUID

from control_plane.kernel.ids import EntityRef, new_id


class EvidenceStatus(StrEnum):
    CAPTURED = "captured"
    LINKED = "linked"
    VALIDATED = "validated"
    REVIEWED = "reviewed"
    ACCEPTED = "accepted"
    SUPERSEDED = "superseded"
    REJECTED = "rejected"


@dataclass(slots=True)
class EvidenceClaim:
    claim: str
    subject_ref: EntityRef
    artifact_refs: tuple[EntityRef, ...]
    producer: EntityRef
    policy_id: str
    id: UUID = field(default_factory=new_id)
    captured_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))
    status: EvidenceStatus = EvidenceStatus.CAPTURED

    def link(self) -> None:
        if not self.artifact_refs:
            raise ValueError("evidence claim requires at least one artifact")
        self.status = EvidenceStatus.LINKED

