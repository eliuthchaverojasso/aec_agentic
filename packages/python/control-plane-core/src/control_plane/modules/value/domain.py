from __future__ import annotations

from dataclasses import dataclass, field
from enum import StrEnum
from uuid import UUID

from control_plane.kernel.ids import EntityRef, new_id
from control_plane.kernel.money import Money
from control_plane.kernel.quantities import Quantity


class ValueStatus(StrEnum):
    UNBUDGETED = "unbudgeted"
    BUDGETED = "budgeted"
    COMMITTED = "committed"
    PLANNED = "planned"
    EARNED = "earned"
    CERTIFIED = "certified"
    BILLED = "billed"
    APPROVED_FOR_PAYMENT = "approved_for_payment"
    PAID = "paid"
    COLLECTED = "collected"


@dataclass(slots=True)
class ValueRecognition:
    milestone_ref: EntityRef
    work_refs: tuple[EntityRef, ...]
    approval_refs: tuple[EntityRef, ...]
    quantity: Quantity
    earned_amount: Money
    valuation_method: str
    id: UUID = field(default_factory=new_id)
    status: ValueStatus = ValueStatus.PLANNED

    def mark_earned(self) -> None:
        if not self.approval_refs:
            raise ValueError("earned value requires at least one approval reference")
        self.status = ValueStatus.EARNED

