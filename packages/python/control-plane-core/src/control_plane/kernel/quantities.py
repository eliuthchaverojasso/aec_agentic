from __future__ import annotations

from dataclasses import dataclass
from decimal import Decimal


@dataclass(frozen=True, slots=True)
class Quantity:
    value: Decimal
    unit: str

    def __post_init__(self) -> None:
        if not self.unit:
            raise ValueError("unit is required")

