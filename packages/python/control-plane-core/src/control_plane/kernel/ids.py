from __future__ import annotations

from dataclasses import dataclass
from uuid import UUID, uuid4


def new_id() -> UUID:
    return uuid4()


@dataclass(frozen=True, slots=True)
class EntityRef:
    entity_type: str
    entity_id: UUID

