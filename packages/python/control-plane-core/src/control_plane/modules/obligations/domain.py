from __future__ import annotations

from dataclasses import dataclass, field
from enum import StrEnum
from uuid import UUID

from control_plane.kernel.ids import EntityRef, new_id


class ObligationStatus(StrEnum):
    DISCOVERED = "discovered"
    CLASSIFIED = "classified"
    ACCEPTED = "accepted"
    ALLOCATED = "allocated"
    IN_PROGRESS = "in_progress"
    CLAIMED_SATISFIED = "claimed_satisfied"
    VERIFIED = "verified"
    APPROVED = "approved"
    SUPERSEDED = "superseded"
    REJECTED = "rejected"


@dataclass(slots=True)
class Obligation:
    statement: str
    issuer: EntityRef
    obligated_party: EntityRef
    source_reference: EntityRef
    acceptance_criteria: tuple[str, ...] = ()
    affected_scope: tuple[EntityRef, ...] = ()
    affected_assets: tuple[EntityRef, ...] = ()
    required_evidence: tuple[str, ...] = ()
    required_reviewer: EntityRef | None = None
    approval_authority: EntityRef | None = None
    id: UUID = field(default_factory=new_id)
    status: ObligationStatus = ObligationStatus.DISCOVERED
    version: int = 1

    def accept(self) -> None:
        if self.status not in {ObligationStatus.DISCOVERED, ObligationStatus.CLASSIFIED}:
            raise ValueError(f"cannot accept obligation from {self.status}")
        self.status = ObligationStatus.ACCEPTED

    def allocate(self) -> None:
        if self.status != ObligationStatus.ACCEPTED:
            raise ValueError("only accepted obligations can be allocated")
        self.status = ObligationStatus.ALLOCATED

