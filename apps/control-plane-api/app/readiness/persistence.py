"""Persistence helpers for readiness snapshots, actions, and MVP evidence.

Schema is owned by Alembic (apps/control-plane-api/alembic); these helpers assume
the readiness/evidence tables already exist and never issue DDL.
"""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from sqlalchemy.orm import Session

from app.models import (
    Project,
    ReadinessAction,
    ReadinessSnapshot,
    Requirement,
    RequirementEvidence,
    TradeReadinessSnapshot,
)
from app.schemas import (
    ProjectReadinessOut,
    ReadinessActionOut,
    ReadinessSnapshotOut,
    TradeReadinessSnapshotOut,
)


def persist_readiness_snapshot(
    db: Session,
    project: Project,
    readiness: ProjectReadinessOut,
) -> ReadinessSnapshotOut:
    _sync_missing_evidence_rows(db, project, readiness)

    snapshot = ReadinessSnapshot(
        project_id=project.id,
        export_id=readiness.latest_export_id,
        overall_score=readiness.overall_readiness,
        label=readiness.label,
        requirement_coverage_score=readiness.requirement_coverage.score,
        qaqc_health_score=readiness.qaqc_health.score,
        sync_freshness_score=readiness.sync_freshness.score,
        gap_summary=readiness.gap_summary,
    )
    db.add(snapshot)
    db.flush()

    trade_rows: list[TradeReadinessSnapshot] = []
    for trade in readiness.trade_readiness:
        row = TradeReadinessSnapshot(
            snapshot_id=snapshot.id,
            discipline=trade.discipline,
            score=trade.readiness,
            requirements_total=trade.requirements_total,
            requirements_covered=trade.requirements_evaluated,
            missing_requirements=trade.missing_requirements,
            needs_review=trade.needs_review,
            open_issues=trade.critical_issues + trade.high_issues,
            critical_gaps=trade.critical_issues,
        )
        db.add(row)
        trade_rows.append(row)

    created_actions: list[ReadinessAction] = []
    for action in readiness.recommended_actions:
        if _existing_open_action(db, project.id, action):
            continue
        row = ReadinessAction(
            project_id=project.id,
            requirement_id=action.requirement_id,
            rule_code=action.rule_code,
            action_type=action.action_type,
            title=action.label,
            description=action.detail,
            priority=_priority_from_severity(action.severity),
        )
        db.add(row)
        created_actions.append(row)

    db.commit()
    db.refresh(snapshot)
    for row in trade_rows:
        db.refresh(row)
    for row in created_actions:
        db.refresh(row)

    return _snapshot_to_schema(snapshot, trade_rows)


def list_readiness_snapshots(db: Session, project_id: int) -> list[ReadinessSnapshotOut]:
    snapshots = (
        db.query(ReadinessSnapshot)
        .filter(ReadinessSnapshot.project_id == project_id)
        .order_by(ReadinessSnapshot.created_at.desc(), ReadinessSnapshot.id.desc())
        .limit(25)
        .all()
    )
    if not snapshots:
        return []

    snapshot_ids = [row.id for row in snapshots]
    trades = (
        db.query(TradeReadinessSnapshot)
        .filter(TradeReadinessSnapshot.snapshot_id.in_(snapshot_ids))
        .order_by(TradeReadinessSnapshot.discipline)
        .all()
    )
    trades_by_snapshot: dict[int, list[TradeReadinessSnapshot]] = {}
    for row in trades:
        trades_by_snapshot.setdefault(row.snapshot_id, []).append(row)
    return [_snapshot_to_schema(row, trades_by_snapshot.get(row.id, [])) for row in snapshots]


def list_readiness_actions(db: Session, project_id: int) -> list[ReadinessActionOut]:
    rows = (
        db.query(ReadinessAction)
        .filter(ReadinessAction.project_id == project_id)
        .order_by(ReadinessAction.created_at.desc(), ReadinessAction.id.desc())
        .all()
    )
    return [ReadinessActionOut.model_validate(row) for row in rows]


def update_readiness_action(
    db: Session,
    action_id: int,
    values: dict[str, Any],
) -> ReadinessActionOut | None:
    row = db.get(ReadinessAction, action_id)
    if row is None:
        return None
    for key in ("status", "priority", "owner", "description"):
        if key in values and values[key] is not None:
            setattr(row, key, values[key])
    row.updated_at = datetime.now(timezone.utc)
    db.commit()
    db.refresh(row)
    return ReadinessActionOut.model_validate(row)


def _sync_missing_evidence_rows(
    db: Session,
    project: Project,
    readiness: ProjectReadinessOut,
) -> None:
    if project.client_id is None:
        return
    requirement_ids = {
        gap.requirement_id
        for gap in readiness.top_gaps
        if gap.rule_code == "EVD001" and gap.requirement_id is not None
    }
    if not requirement_ids:
        return

    existing = {
        row.requirement_id
        for row in db.query(RequirementEvidence.requirement_id)
        .filter(
            RequirementEvidence.project_id == project.id,
            RequirementEvidence.requirement_id.in_(requirement_ids),
            RequirementEvidence.source_ref == "readiness_rule:EVD001",
        )
        .all()
    }
    valid_requirements = {
        row.id
        for row in db.query(Requirement.id)
        .filter(
            Requirement.client_id == project.client_id,
            Requirement.id.in_(requirement_ids),
        )
        .all()
    }
    for requirement_id in sorted(valid_requirements - existing):
        db.add(
            RequirementEvidence(
                project_id=project.id,
                requirement_id=requirement_id,
                evidence_type="manual",
                evidence_status="missing",
                source_ref="readiness_rule:EVD001",
                confidence=0,
                metadata_json={"source": "readiness", "demo_fallback": False},
            )
        )


def _existing_open_action(db: Session, project_id: int, action: Any) -> bool:
    query = db.query(ReadinessAction.id).filter(
        ReadinessAction.project_id == project_id,
        ReadinessAction.action_type == action.action_type,
        ReadinessAction.rule_code == action.rule_code,
        ReadinessAction.status.in_(("open", "in_review")),
    )
    if action.requirement_id is None:
        query = query.filter(ReadinessAction.requirement_id.is_(None))
    else:
        query = query.filter(ReadinessAction.requirement_id == action.requirement_id)
    return query.first() is not None


def _priority_from_severity(severity: str) -> str:
    severity = severity.lower()
    if severity in {"critical", "high", "medium", "low"}:
        return severity
    return "medium"


def _snapshot_to_schema(
    snapshot: ReadinessSnapshot,
    trades: list[TradeReadinessSnapshot],
) -> ReadinessSnapshotOut:
    return ReadinessSnapshotOut(
        id=snapshot.id,
        project_id=snapshot.project_id,
        export_id=snapshot.export_id,
        overall_score=float(snapshot.overall_score),
        label=snapshot.label,
        requirement_coverage_score=float(snapshot.requirement_coverage_score),
        qaqc_health_score=float(snapshot.qaqc_health_score),
        sync_freshness_score=float(snapshot.sync_freshness_score),
        gap_summary=snapshot.gap_summary,
        created_at=snapshot.created_at,
        trade_readiness=[
            TradeReadinessSnapshotOut(
                id=row.id,
                snapshot_id=row.snapshot_id,
                discipline=row.discipline,
                score=float(row.score),
                requirements_total=row.requirements_total,
                requirements_covered=row.requirements_covered,
                missing_requirements=row.missing_requirements,
                needs_review=row.needs_review,
                open_issues=row.open_issues,
                critical_gaps=row.critical_gaps,
            )
            for row in trades
        ],
    )
