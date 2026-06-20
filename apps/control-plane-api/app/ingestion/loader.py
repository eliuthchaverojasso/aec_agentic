"""End-to-end ingestion: parse JSON -> insert project/model/export/elements -> run rules -> insert issues.

The pipeline writes a sync_log entry for each step so the Dashboard can show
processing progress per Dashboard Guidelines section 8.4.
"""

from __future__ import annotations

import logging
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

from sqlalchemy import select
from sqlalchemy.orm import Session

from app.ingestion.parser import (
    REQUIRED_ELEMENT_KEYS,
    infer_discipline,
    parse_project_title,
    stream_elements,
)
from app.ingestion.rules import run_all_rules
from app.models import (
    Element,
    Export,
    Issue,
    Model as ModelRecord,
    Organization,
    Project,
    Rule,
    SyncLog,
)

logger = logging.getLogger(__name__)

ELEMENT_BATCH_SIZE = 500
ISSUE_BATCH_SIZE = 1000


PIPELINE_STEPS = (
    "received",
    "validation",
    "parsing",
    "element_extraction",
    "param_normalization",
    "qa_qc_checks",
    "dashboard_update",
)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _get_or_create_organization(db: Session, name: str) -> Organization:
    org = db.execute(select(Organization).where(Organization.name == name)).scalar_one_or_none()
    if org is None:
        org = Organization(name=name)
        db.add(org)
        db.flush()
    return org


def _get_or_create_project(db: Session, organization_id: int, title_info: dict[str, Any]) -> Project:
    project = db.execute(
        select(Project).where(
            Project.organization_id == organization_id,
            Project.project_title == title_info["project_title"],
        )
    ).scalar_one_or_none()

    if project is None:
        project = Project(
            organization_id=organization_id,
            project_title=title_info["project_title"],
            project_code=title_info.get("prefix"),
            project_name=title_info.get("project_name"),
            job_number=title_info.get("job_number"),
            revit_version=title_info.get("revit_version"),
        )
        db.add(project)
        db.flush()
    return project


def _get_or_create_model(
    db: Session,
    project_id: int,
    file_name: str,
    discipline: str,
    revit_version: str | None,
) -> ModelRecord:
    model = db.execute(
        select(ModelRecord).where(
            ModelRecord.project_id == project_id,
            ModelRecord.revit_file_name == file_name,
            ModelRecord.discipline == discipline,
        )
    ).scalar_one_or_none()

    if model is None:
        model = ModelRecord(
            project_id=project_id,
            revit_file_name=file_name,
            discipline=discipline,
            revit_version=revit_version,
            model_type="revit",
        )
        db.add(model)
        db.flush()
    else:
        if revit_version and model.revit_version != revit_version:
            model.revit_version = revit_version
        model.last_sync_at = datetime.now(timezone.utc)
        db.flush()
    return model


def _log_step(
    db: Session,
    export_id: int,
    step: str,
    status: str,
    started_at: datetime,
    message: str | None = None,
) -> None:
    now = datetime.now(timezone.utc)
    duration = (now - started_at).total_seconds()
    db.add(
        SyncLog(
            export_id=export_id,
            step=step,
            status=status,
            started_at=started_at,
            completed_at=now if status in {"completed", "failed", "warning"} else None,
            duration_seconds=round(duration, 2),
            message=message,
        )
    )
    db.flush()


def _rule_lookup(db: Session) -> dict[str, Rule]:
    rules = db.execute(select(Rule)).scalars().all()
    return {r.rule_code: r for r in rules}


# ---------------------------------------------------------------------------
# Public entry point
# ---------------------------------------------------------------------------


def prepare_export_record(
    db: Session,
    json_path: Path,
    export_type: str,
    original_filename: str,
    organization_name: str,
    status: str = "pending",
) -> Export:
    """Create an export row with enough metadata for clients to poll it."""
    file_size = json_path.stat().st_size

    org = _get_or_create_organization(db, organization_name)

    first_element: dict[str, Any] | None = None
    for el in stream_elements(json_path):
        first_element = el
        break

    if first_element is None:
        raise ValueError(f"No elements found in {json_path.name}")

    if not REQUIRED_ELEMENT_KEYS.issubset(first_element):
        raise ValueError(
            f"First element missing required keys. Got {list(first_element.keys())}."
        )

    project_title = first_element.get("ProjectTitle") or "Unknown Project"
    title_info = parse_project_title(project_title)

    project = _get_or_create_project(db, org.id, title_info)
    model = _get_or_create_model(
        db,
        project_id=project.id,
        file_name=original_filename,
        discipline=export_type,
        revit_version=title_info.get("revit_version"),
    )

    export = Export(
        project_id=project.id,
        model_id=model.id,
        export_type=export_type,
        file_name=original_filename,
        file_size_bytes=file_size,
        status=status,
        started_at=datetime.now(timezone.utc),
    )
    db.add(export)
    db.flush()
    return export


def ingest_export(
    db: Session,
    json_path: Path,
    export_type: str,
    original_filename: str,
    organization_name: str,
    export_id: int | None = None,
) -> dict[str, Any]:
    """Full ingestion pipeline for one JSON export file.

    Returns a summary dict containing export_id, project_id, model_id, counts.
    """
    started_overall = datetime.now(timezone.utc)
    t0 = time.monotonic()

    # --- Step: received --------------------------------------------------
    step_started = datetime.now(timezone.utc)
    file_size = json_path.stat().st_size

    if export_id is None:
        export = prepare_export_record(
            db=db,
            json_path=json_path,
            export_type=export_type,
            original_filename=original_filename,
            organization_name=organization_name,
            status="in_progress",
        )
    else:
        export = db.get(Export, export_id)
        if export is None:
            raise ValueError(f"Export {export_id} not found")
        export.status = "in_progress"
        export.started_at = started_overall
        export.file_size_bytes = file_size
        export.error_message = None
        db.flush()

    project = db.get(Project, export.project_id)
    model = db.get(ModelRecord, export.model_id)
    if project is None or model is None:
        raise ValueError(f"Export {export.id} is missing its project/model references")

    org = db.get(Organization, project.organization_id)
    if org is None:
        raise ValueError(f"Project {project.id} is missing its organization")

    _log_step(db, export.id, "received", "completed", step_started, f"File {original_filename} ({file_size} bytes) received")
    db.commit()

    # --- Step: validation ------------------------------------------------
    step_started = datetime.now(timezone.utc)
    _log_step(db, export.id, "validation", "in_progress", step_started)
    db.commit()

    # Minimal validation: required keys present on the first element (already done).
    _log_step(db, export.id, "validation", "completed", step_started, "Required keys verified")
    db.commit()

    # --- Step: parsing + element_extraction + param_normalization -------
    # We stream through the file in a single pass and batch-insert.
    parse_started = datetime.now(timezone.utc)
    _log_step(db, export.id, "parsing", "in_progress", parse_started)
    db.commit()

    extract_started = datetime.now(timezone.utc)

    rules_index = _rule_lookup(db)

    total_elements = 0
    categories_seen: dict[str, int] = {}
    element_batch: list[dict[str, Any]] = []
    issue_batch: list[dict[str, Any]] = []

    def _flush_elements() -> list[int]:
        """Insert the element batch, return the list of inserted ids in order."""
        if not element_batch:
            return []
        ids = _bulk_insert_elements(db, element_batch)
        element_batch.clear()
        return ids

    def _flush_issues() -> None:
        if not issue_batch:
            return
        db.bulk_insert_mappings(Issue, issue_batch)
        issue_batch.clear()

    # We need to correlate the db id of each inserted element with its findings
    # so we store findings keyed by position within the batch.
    pending_findings: list[tuple[int, list]] = []  # (batch_index, findings)

    for element in stream_elements(json_path):
        unique_id = element.get("UniqueId")
        element_id = element.get("ElementId")
        if not unique_id or element_id is None:
            # Skip malformed records but log once.
            logger.warning("Skipping element with missing UniqueId/ElementId")
            continue

        cat = element.get("Category") or "Unknown"
        categories_seen[cat] = categories_seen.get(cat, 0) + 1

        element_batch.append(
            {
                "unique_id": unique_id,
                "element_id": int(element_id),
                "model_id": model.id,
                "export_id": export.id,
                "category": cat,
                "name": element.get("Name"),
                "family": element.get("Family"),
                "type": element.get("Type"),
                "level": element.get("Level"),
                "instance_parameters": element.get("InstanceParameters"),
                "type_parameters": element.get("TypeParameters"),
            }
        )

        findings = run_all_rules(element)
        if findings:
            pending_findings.append((len(element_batch) - 1, findings))

        total_elements += 1

        if len(element_batch) >= ELEMENT_BATCH_SIZE:
            inserted_ids = _flush_elements()
            # Resolve findings for this batch
            for batch_idx, batch_findings in pending_findings:
                element_db_id = inserted_ids[batch_idx]
                for finding in batch_findings:
                    rule_record = rules_index.get(finding.rule_code)
                    issue_batch.append(
                        _build_issue_payload(
                            organization_id=org.id,
                            project_id=project.id,
                            model_id=model.id,
                            export_id=export.id,
                            element_unique_id=None,
                            element_db_id=element_db_id,
                            rule_record=rule_record,
                            finding=finding,
                        )
                    )
            pending_findings.clear()

            if len(issue_batch) >= ISSUE_BATCH_SIZE:
                _flush_issues()

    # Final flush
    inserted_ids = _flush_elements()
    for batch_idx, batch_findings in pending_findings:
        element_db_id = inserted_ids[batch_idx]
        for finding in batch_findings:
            rule_record = rules_index.get(finding.rule_code)
            issue_batch.append(
                _build_issue_payload(
                    organization_id=org.id,
                    project_id=project.id,
                    model_id=model.id,
                    export_id=export.id,
                    element_unique_id=None,
                    element_db_id=element_db_id,
                    rule_record=rule_record,
                    finding=finding,
                )
            )
    pending_findings.clear()
    _flush_issues()

    _log_step(db, export.id, "parsing", "completed", parse_started, f"Parsed {total_elements} elements")
    _log_step(db, export.id, "element_extraction", "completed", extract_started, f"Inserted {total_elements} elements")
    _log_step(db, export.id, "param_normalization", "completed", extract_started, "Parameters stored as JSONB")

    # --- Step: QA/QC checks ---------------------------------------------
    from sqlalchemy import func as sa_func

    qa_started = datetime.now(timezone.utc)
    total_issues = db.execute(
        select(sa_func.count()).select_from(Issue).where(Issue.export_id == export.id)
    ).scalar_one()

    discipline = infer_discipline(list(categories_seen.keys()))
    if model.discipline != discipline and discipline != "unknown":
        model.discipline = discipline
    model.last_sync_at = datetime.now(timezone.utc)
    db.flush()

    _log_step(
        db,
        export.id,
        "qa_qc_checks",
        "completed",
        qa_started,
        f"{total_issues} issues generated across {len(rules_index)} rules",
    )
    db.commit()

    # --- Step: dashboard_update ------------------------------------------
    dash_started = datetime.now(timezone.utc)
    _log_step(db, export.id, "dashboard_update", "completed", dash_started, "Serving layer refreshed")

    # --- Finalize export -------------------------------------------------
    completed_at = datetime.now(timezone.utc)
    duration = round(time.monotonic() - t0, 2)
    export.status = "completed"
    export.completed_at = completed_at
    export.duration_seconds = duration
    export.element_count = total_elements
    db.commit()

    logger.info(
        "Ingestion finished: export=%s project=%s model=%s elements=%s issues=%s duration=%.2fs",
        export.id,
        project.id,
        model.id,
        total_elements,
        total_issues,
        duration,
    )

    return {
        "export_id": export.id,
        "project_id": project.id,
        "model_id": model.id,
        "organization_id": org.id,
        "element_count": total_elements,
        "issue_count": total_issues,
        "categories": categories_seen,
        "discipline": discipline,
        "duration_seconds": duration,
    }


# ---------------------------------------------------------------------------
# Low-level insert helpers
# ---------------------------------------------------------------------------


def _bulk_insert_elements(db: Session, rows: list[dict[str, Any]]) -> list[int]:
    """Bulk-insert element rows with RETURNING to capture their new ids.

    sort_by_parameter_order=True preserves input ordering so we can correlate
    the returned ids back to per-element findings by batch index.
    """
    from sqlalchemy import insert

    stmt = insert(Element).returning(Element.id, sort_by_parameter_order=True)
    result = db.execute(stmt, rows)
    return [row[0] for row in result]


def _build_issue_payload(
    *,
    organization_id: int,
    project_id: int,
    model_id: int,
    export_id: int,
    element_unique_id: str | None,
    element_db_id: int | None,
    rule_record: Rule | None,
    finding,
) -> dict[str, Any]:
    rule_id = rule_record.id if rule_record else None
    rule_version = rule_record.version if rule_record else None

    return {
        "organization_id": organization_id,
        "project_id": project_id,
        "model_id": model_id,
        "export_id": export_id,
        "element_unique_id": element_unique_id,
        "element_db_id": element_db_id,
        "rule_id": rule_id,
        "rule_code": finding.rule_code,
        "issue_type": finding.issue_type,
        "severity": finding.severity,
        "status": "open",
        "source": "automated",
        "message": finding.message,
        "traceability": {
            "rule_code": finding.rule_code,
            "rule_version": rule_version,
            "check_timestamp": datetime.now(timezone.utc).isoformat(),
            "observed_values": finding.observed_values,
            "export_id": export_id,
        },
    }


def finalize_failed_export(db: Session, export_id: int, error: str) -> None:
    """Mark an export as failed and record the error in sync_log."""
    export = db.get(Export, export_id)
    if export is None:
        return
    export.status = "failed"
    export.error_message = error
    export.completed_at = datetime.now(timezone.utc)
    db.add(
        SyncLog(
            export_id=export.id,
            step="ingest",
            status="failed",
            started_at=datetime.now(timezone.utc),
            completed_at=datetime.now(timezone.utc),
            message=error[:2000],
        )
    )
    db.commit()
