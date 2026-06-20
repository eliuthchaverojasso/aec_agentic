from __future__ import annotations

from decimal import Decimal

import pytest

from control_plane.kernel.ids import EntityRef, new_id
from control_plane.kernel.money import Money
from control_plane.kernel.quantities import Quantity
from control_plane.modules.obligations import Obligation, ObligationStatus
from control_plane.modules.value import ValueRecognition, ValueStatus
from control_plane.modules.work import WorkPackage, WorkPackageStatus


def ref(entity_type: str) -> EntityRef:
    return EntityRef(entity_type=entity_type, entity_id=new_id())


def test_obligation_accept_and_allocate() -> None:
    obligation = Obligation(
        statement="Provide lighting fixture circuit assignments.",
        issuer=ref("owner"),
        obligated_party=ref("designer"),
        source_reference=ref("requirement"),
    )

    obligation.accept()
    obligation.allocate()

    assert obligation.status == ObligationStatus.ALLOCATED


def test_work_package_requires_actor_evidence_and_clear_constraints() -> None:
    package = WorkPackage(
        title="Circuit Level 02 lighting fixtures",
        obligation_refs=(ref("obligation"),),
        required_evidence=("rule_evaluation",),
    )

    with pytest.raises(ValueError):
        package.release()

    package.assigned_actor = ref("human")
    package.release()

    assert package.status == WorkPackageStatus.RELEASED


def test_value_recognition_requires_approval_reference() -> None:
    recognition = ValueRecognition(
        milestone_ref=ref("milestone"),
        work_refs=(ref("work_package"),),
        approval_refs=(),
        quantity=Quantity(Decimal("10"), "percent"),
        earned_amount=Money(Decimal("1000.00")),
        valuation_method="milestone_percent",
    )

    with pytest.raises(ValueError):
        recognition.mark_earned()

    recognition.approval_refs = (ref("approval"),)
    recognition.mark_earned()

    assert recognition.status == ValueStatus.EARNED

