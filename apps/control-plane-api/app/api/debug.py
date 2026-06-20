from __future__ import annotations

from datetime import datetime
from typing import Any

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.orm import Session

from app.database import get_db
from app.services.operation_log_service import (
    build_environment_snapshot,
    create_debug_bundle,
    get_operation_log,
    list_operation_logs,
    start_operation,
    finish_operation_success,
    summarize_operation_logs,
)

router = APIRouter(prefix="/api/v1/debug", tags=["debug"])


@router.get("/logs")
def get_debug_logs(
    project_id: int | None = None,
    operation_type: str | None = None,
    severity: str | None = None,
    status: str | None = None,
    run_id: str | None = None,
    request_id: str | None = None,
    since: datetime | None = None,
    until: datetime | None = None,
    limit: int = Query(default=100, ge=1, le=500),
    offset: int = Query(default=0, ge=0),
    db: Session = Depends(get_db),
) -> dict[str, Any]:
    rows = list_operation_logs(
        db,
        project_id=project_id,
        operation_type=operation_type,
        severity=severity,
        status=status,
        run_id=run_id,
        request_id=request_id,
        since=since,
        until=until,
        limit=limit,
        offset=offset,
    )
    return {"items": [_serialize(row) for row in rows], "count": len(rows), "limit": limit, "offset": offset}


@router.get("/logs/summary")
def get_debug_logs_summary(db: Session = Depends(get_db)) -> dict[str, Any]:
    return summarize_operation_logs(db)


@router.get("/logs/{log_id}")
def get_debug_log(log_id: int, db: Session = Depends(get_db)) -> dict[str, Any]:
    row = get_operation_log(db, log_id)
    if row is None:
        raise HTTPException(status_code=404, detail="Operation log not found")
    return _serialize(row)


@router.post("/logs/frontend")
def create_frontend_debug_log(payload: dict[str, Any], db: Session = Depends(get_db)) -> dict[str, Any]:
    entry = start_operation(
        db,
        operation_type="frontend_action",
        operation_label=payload.get("action") or "frontend_action",
        project_id=payload.get("project_id"),
        project_name=payload.get("project_name"),
        endpoint=payload.get("endpoint"),
        method=payload.get("method"),
        source="frontend",
        request_id=payload.get("request_id"),
        run_id=payload.get("run_id"),
        request_summary=payload,
    )
    finish_operation_success(
        db,
        entry,
        status=payload.get("status") or "success",
        severity=payload.get("severity") or "info",
        response_summary={"message": payload.get("message"), "route": payload.get("route")},
        warnings=payload.get("warnings") or [],
    )
    return {"ok": True, "log_id": entry.id, "request_id": entry.request_id, "run_id": entry.run_id}


@router.get("/environment")
def get_debug_environment() -> dict[str, Any]:
    return build_environment_snapshot()


@router.get("/pipeline-state")
def get_debug_pipeline_state(project_id: int | None = None, db: Session = Depends(get_db)) -> dict[str, Any]:
    recent = list_operation_logs(db, project_id=project_id, limit=20)
    latest_scan = next((row for row in recent if row.operation_type == "scan_landing"), None)
    latest_ingest = next((row for row in recent if row.operation_type in {"run_ingest", "dry_run_ingest"}), None)
    return {
        "project_id": project_id,
        "summary": summarize_operation_logs(db),
        "latest_scan": _serialize(latest_scan) if latest_scan else None,
        "latest_ingest": _serialize(latest_ingest) if latest_ingest else None,
    }


@router.get("/projects/{project_id}/timeline")
def get_project_timeline(project_id: int, db: Session = Depends(get_db)) -> dict[str, Any]:
    rows = list_operation_logs(db, project_id=project_id, limit=200)
    return {"project_id": project_id, "items": [_serialize(row) for row in rows]}


@router.post("/bundle")
def post_debug_bundle(payload: dict[str, Any] | None = None, db: Session = Depends(get_db)) -> dict[str, Any]:
    project_id = (payload or {}).get("project_id")
    return create_debug_bundle(db, project_id=project_id)
def _serialize(row: Any) -> dict[str, Any]:
    return {
        "id": row.id,
        "run_id": row.run_id,
        "request_id": row.request_id,
        "project_id": row.project_id,
        "project_name": row.project_name,
        "operation_type": row.operation_type,
        "operation_label": row.operation_label,
        "source": row.source,
        "endpoint": row.endpoint,
        "method": row.method,
        "status": row.status,
        "severity": row.severity,
        "started_at": row.started_at.isoformat() if row.started_at else None,
        "finished_at": row.finished_at.isoformat() if row.finished_at else None,
        "duration_ms": row.duration_ms,
        "counts_json": row.counts_json or {},
        "request_summary_json": row.request_summary_json or {},
        "response_summary_json": row.response_summary_json or {},
        "warnings_json": row.warnings_json or [],
        "errors_json": row.errors_json or [],
        "environment_json": row.environment_json or {},
        "metadata_json": row.metadata_json or {},
    }
