from __future__ import annotations

from dataclasses import dataclass, field
from enum import StrEnum
from uuid import UUID

from control_plane.kernel.ids import EntityRef, new_id


class WorkPackageStatus(StrEnum):
    DRAFT = "draft"
    PLANNED = "planned"
    CONSTRAINT_BLOCKED = "constraint_blocked"
    READY = "ready"
    RELEASED = "released"
    IN_EXECUTION = "in_execution"
    EVIDENCE_SUBMITTED = "evidence_submitted"
    REVIEWED = "reviewed"
    ACCEPTED = "accepted"
    CLOSED = "closed"


@dataclass(slots=True)
class WorkPackage:
    title: str
    obligation_refs: tuple[EntityRef, ...]
    location_refs: tuple[EntityRef, ...] = ()
    system_refs: tuple[EntityRef, ...] = ()
    assigned_actor: EntityRef | None = None
    required_evidence: tuple[str, ...] = ()
    constraints: tuple[str, ...] = ()
    id: UUID = field(default_factory=new_id)
    status: WorkPackageStatus = WorkPackageStatus.DRAFT

    @property
    def is_ready(self) -> bool:
        return self.assigned_actor is not None and not self.constraints and bool(self.required_evidence)

    def release(self) -> None:
        if not self.is_ready:
            raise ValueError("work package cannot be released until actor, evidence, and constraints are resolved")
        self.status = WorkPackageStatus.RELEASED

