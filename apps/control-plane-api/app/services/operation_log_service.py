from __future__ import annotations

import os
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from sqlalchemy import select
from sqlalchemy.orm import Session

from app.config import settings
from app.models import PipelineOperationLog

_SENSITIVE_KEYS = {"password", "token", "secret", "api_key", "authorization", "connection_string", "database_url"}


def _now() -> datetime:
    return datetime.now(timezone.utc)


def _new_id() -> str:
    return uuid.uuid4().hex


def redact_payload(payload: Any) -> Any:
    if isinstance(payload, dict):
        redacted: dict[str, Any] = {}
        for key, value in payload.items():
            if any(token in key.lower() for token in _SENSITIVE_KEYS):
                redacted[key] = "[REDACTED]"
            else:
                redacted[key] = redact_payload(value)
        return redacted
    if isinstance(payload, list):
        return [redact_payload(item) for item in payload]
    if isinstance(payload, str) and len(payload) > 5000:
        return payload[:5000] + "...[TRUNCATED]"
    return payload


def start_operation(
    db: Session,
    *,
    operation_type: str,
    operation_label: str | None = None,
    project_id: int | None = None,
    project_name: str | None = None,
    endpoint: str | None = None,
    method: str | None = None,
    source: str = "backend",
    run_id: str | None = None,
    request_id: str | None = None,
    request_summary: dict[str, Any] | None = None,
    metadata: dict[str, Any] | None = None,
) -> PipelineOperationLog:
    entry = PipelineOperationLog(
        run_id=run_id or _new_id(),
        request_id=request_id or _new_id(),
        project_id=project_id,
        project_name=project_name,
        operation_type=operation_type,
        operation_label=operation_label,
        endpoint=endpoint,
        method=method,
        status="started",
        severity="info",
        source=source,
        request_summary_json=redact_payload(request_summary or {}),
        environment_json=build_environment_snapshot(),
        metadata_json=redact_payload(metadata or {}),
    )
    db.add(entry)
    db.commit()
    db.refresh(entry)
    return entry


def finish_operation_success(
    db: Session,
    entry: PipelineOperationLog,
    *,
    status: str = "success",
    severity: str = "info",
    counts: dict[str, Any] | None = None,
    response_summary: dict[str, Any] | None = None,
    warnings: list[str] | None = None,
) -> PipelineOperationLog:
    entry.finished_at = _now()
    entry.duration_ms = int((entry.finished_at - entry.started_at).total_seconds() * 1000) if entry.started_at else None
    entry.status = status
    entry.severity = severity
    entry.counts_json = redact_payload(counts or {})
    entry.response_summary_json = redact_payload(response_summary or {})
    entry.warnings_json = redact_payload(warnings or [])
    db.add(entry)
    db.commit()
    db.refresh(entry)
    return entry


def finish_operation_failure(
    db: Session,
    entry: PipelineOperationLog,
    *,
    errors: list[str],
    warnings: list[str] | None = None,
    status: str = "failed",
    severity: str = "error",
    response_summary: dict[str, Any] | None = None,
) -> PipelineOperationLog:
    entry.finished_at = _now()
    entry.duration_ms = int((entry.finished_at - entry.started_at).total_seconds() * 1000) if entry.started_at else None
    entry.status = status
    entry.severity = severity
    entry.errors_json = redact_payload(errors)
    entry.warnings_json = redact_payload(warnings or [])
    entry.response_summary_json = redact_payload(response_summary or {})
    db.add(entry)
    db.commit()
    db.refresh(entry)
    return entry


def list_operation_logs(db: Session, *, limit: int = 100, offset: int = 0, **filters: Any) -> list[PipelineOperationLog]:
    stmt = select(PipelineOperationLog)
    if filters.get("project_id") is not None:
        stmt = stmt.where(PipelineOperationLog.project_id == filters["project_id"])
    if filters.get("operation_type"):
        stmt = stmt.where(PipelineOperationLog.operation_type == filters["operation_type"])
    if filters.get("severity"):
        stmt = stmt.where(PipelineOperationLog.severity == filters["severity"])
    if filters.get("status"):
        stmt = stmt.where(PipelineOperationLog.status == filters["status"])
    if filters.get("run_id"):
        stmt = stmt.where(PipelineOperationLog.run_id == filters["run_id"])
    if filters.get("request_id"):
        stmt = stmt.where(PipelineOperationLog.request_id == filters["request_id"])
    if filters.get("since"):
        stmt = stmt.where(PipelineOperationLog.started_at >= filters["since"])
    if filters.get("until"):
        stmt = stmt.where(PipelineOperationLog.started_at <= filters["until"])
    stmt = stmt.order_by(PipelineOperationLog.started_at.desc()).offset(offset).limit(min(limit, 500))
    return db.execute(stmt).scalars().all()


def get_operation_log(db: Session, log_id: int) -> PipelineOperationLog | None:
    return db.get(PipelineOperationLog, log_id)


def summarize_operation_logs(db: Session) -> dict[str, Any]:
    rows = list_operation_logs(db, limit=200)
    return {
        "total": len(rows),
        "errors": sum(1 for row in rows if row.severity in {"error", "critical"} or row.status == "failed"),
        "warnings": sum(1 for row in rows if row.severity == "warning" or (row.warnings_json or [])),
        "last_operation": _log_to_dict(rows[0]) if rows else None,
        "last_failed_operation": next((_log_to_dict(row) for row in rows if row.status == "failed"), None),
    }


def build_environment_snapshot() -> dict[str, Any]:
    cwd = str(Path.cwd())
    landing_dir = str(settings.landing_dir)
    db_url = settings.database_url
    db_url_redacted = db_url.split("@")[-1] if "@" in db_url else "[REDACTED]"
    warnings: list[str] = []
    file_path_mode = "unknown"
    if landing_dir.startswith("/"):
        file_path_mode = "container_path"
    elif ":" in landing_dir:
        file_path_mode = "windows_path"
    if file_path_mode == "container_path":
        warnings.append("Backend uses container-style landing_dir; Windows host paths may be unreachable.")
    return {
        "app_environment": getattr(settings, "environment", "local"),
        "landing_dir": landing_dir,
        "cwd": cwd,
        "database_url_redacted": db_url_redacted,
        "file_path_mode": file_path_mode,
        "container_hint": os.path.exists("/.dockerenv"),
        "warnings": warnings,
    }


def create_debug_bundle(db: Session, *, project_id: int | None = None) -> dict[str, Any]:
    logs = list_operation_logs(db, project_id=project_id, limit=100)
    return {
        "generated_at": _now().isoformat(),
        "environment": build_environment_snapshot(),
        "project_id": project_id,
        "summary": summarize_operation_logs(db),
        "logs": [_log_to_dict(log) for log in logs],
        "warnings": ["Debug bundle excludes secrets and raw project document contents."],
    }


def _log_to_dict(log: PipelineOperationLog) -> dict[str, Any]:
    return {
        "id": log.id,
        "run_id": log.run_id,
        "request_id": log.request_id,
        "project_id": log.project_id,
        "project_name": log.project_name,
        "operation_type": log.operation_type,
        "operation_label": log.operation_label,
        "source": log.source,
        "endpoint": log.endpoint,
        "method": log.method,
        "status": log.status,
        "severity": log.severity,
        "started_at": log.started_at.isoformat() if log.started_at else None,
        "finished_at": log.finished_at.isoformat() if log.finished_at else None,
        "duration_ms": log.duration_ms,
        "counts_json": log.counts_json or {},
        "request_summary_json": log.request_summary_json or {},
        "response_summary_json": log.response_summary_json or {},
        "warnings_json": log.warnings_json or [],
        "errors_json": log.errors_json or [],
        "environment_json": log.environment_json or {},
        "metadata_json": log.metadata_json or {},
    }
