"""Small, deterministic readiness scoring helpers."""

from __future__ import annotations

from datetime import datetime, timezone


def clamp_score(value: float) -> float:
    return round(max(0.0, min(100.0, value)), 2)


def readiness_label(score: float) -> str:
    if score >= 90:
        return "Ready"
    if score >= 75:
        return "On Track"
    if score >= 60:
        return "At Risk"
    if score >= 40:
        return "Behind"
    return "Critical"


def qaqc_health_score(
    total_elements: int,
    critical_issues: int,
    high_issues: int,
    medium_issues: int,
    low_issues: int,
) -> float:
    # Product truth: missing data is not a perfect score. An empty model (no
    # elements) carries no QA/QC evidence, so it must not score 100. This mirrors
    # sync_freshness_score(None) -> 0.0 and prevents an empty project from being
    # handed free readiness points via the weighted blend.
    if total_elements <= 0:
        return 0.0

    penalty = (
        critical_issues * 5.0
        + high_issues * 2.0
        + medium_issues * 0.75
        + low_issues * 0.25
    )
    normalized = penalty / max(total_elements / 100.0, 1.0)
    return clamp_score(100.0 - normalized)


def sync_freshness_score(latest_sync_at: datetime | None) -> float:
    if latest_sync_at is None:
        return 0.0

    now = datetime.now(timezone.utc)
    if latest_sync_at.tzinfo is None:
        latest_sync_at = latest_sync_at.replace(tzinfo=timezone.utc)
    age_days = (now - latest_sync_at).total_seconds() / 86400

    if age_days <= 1:
        return 100.0
    if age_days <= 3:
        return 85.0
    if age_days <= 7:
        return 70.0
    if age_days <= 14:
        return 50.0
    return 25.0


def weighted_readiness(
    requirement_coverage: float,
    qaqc_health: float,
    sync_freshness: float,
) -> float:
    return clamp_score(
        requirement_coverage * 0.50
        + qaqc_health * 0.30
        + sync_freshness * 0.20
    )
