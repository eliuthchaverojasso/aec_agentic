from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone
from uuid import uuid4


@dataclass(frozen=True, slots=True)
class TaskLease:
    task_id: str
    worker_id: str
    lease_id: str = field(default_factory=lambda: f"lease-{uuid4()}")
    acquired_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))
    ttl_seconds: int = 900

    @property
    def expires_at(self) -> datetime:
        return self.acquired_at + timedelta(seconds=self.ttl_seconds)

    def is_expired(self, now: datetime | None = None) -> bool:
        current = now or datetime.now(timezone.utc)
        return current >= self.expires_at

