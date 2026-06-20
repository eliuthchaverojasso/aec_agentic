"""POST /exports -- accept a Revit JSON export, run ingestion pipeline."""

from __future__ import annotations

import logging
import shutil
import uuid
from datetime import datetime, timezone
from pathlib import Path

from fastapi import APIRouter, BackgroundTasks, Depends, File, Form, HTTPException, UploadFile
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.config import settings
from app.database import SessionLocal, get_db
from app.ingestion.loader import finalize_failed_export, ingest_export, prepare_export_record
from app.models import Export, SyncLog
from app.schemas import ExportCreateResponse, ExportOut, SyncLogOut

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/v1/exports", tags=["exports"])


VALID_EXPORT_TYPES = {"all", "electrical", "mechanical", "lighting", "plumbing", "technology"}


def _save_upload(upload: UploadFile, dest_dir: Path) -> tuple[Path, str]:
    """Persist the uploaded file to the landing zone with a timestamped name."""
    dest_dir.mkdir(parents=True, exist_ok=True)
    original = Path(upload.filename or "upload.json").name
    ts = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S")
    uid = uuid.uuid4().hex[:8]
    stored_name = f"{ts}_{uid}_{original}"
    stored_path = dest_dir / stored_name

    with open(stored_path, "wb") as out:
        shutil.copyfileobj(upload.file, out)

    return stored_path, original


def _resolve_landing_path(relative_path: str) -> Path:
    landing_root = settings.landing_dir.resolve()
    stored_path = (landing_root / relative_path).resolve()
    try:
        stored_path.relative_to(landing_root)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail="Path must be inside the landing directory") from exc
    return stored_path


def _run_ingestion_task(
    stored_path_str: str,
    export_type: str,
    original_filename: str,
    export_id: int,
) -> None:
    """Background task: open its own DB session to run ingestion."""
    stored_path = Path(stored_path_str)
    db: Session = SessionLocal()
    try:
        result = ingest_export(
            db=db,
            json_path=stored_path,
            export_type=export_type,
            original_filename=original_filename,
            organization_name=settings.default_organization,
            export_id=export_id,
        )
        logger.info("Ingestion success: %s", result)
    except Exception as exc:  # noqa: BLE001
        logger.exception("Ingestion failed for %s", stored_path)
        if export_id is not None:
            try:
                finalize_failed_export(db, export_id, str(exc))
            except Exception:
                logger.exception("Could not write failure status for export %s", export_id)
    finally:
        db.close()


@router.post(
    "",
    response_model=ExportCreateResponse,
    summary="Upload a Revit JSON export; triggers asynchronous ingestion",
)
def create_export(
    background_tasks: BackgroundTasks,
    file: UploadFile = File(..., description="Revit JSON export file"),
    export_type: str = Form(
        "all",
        description="Export discipline/type: all, electrical, mechanical, lighting, plumbing, technology",
    ),
    db: Session = Depends(get_db),
) -> ExportCreateResponse:
    export_type = export_type.lower().strip()
    if export_type not in VALID_EXPORT_TYPES:
        raise HTTPException(
            status_code=400,
            detail=f"export_type must be one of {sorted(VALID_EXPORT_TYPES)}",
        )

    if not file.filename or not file.filename.lower().endswith(".json"):
        raise HTTPException(status_code=400, detail="File must be a .json export")

    stored_path, original = _save_upload(file, settings.landing_dir)
    try:
        export = prepare_export_record(
            db=db,
            json_path=stored_path,
            export_type=export_type,
            original_filename=original,
            organization_name=settings.default_organization,
            status="pending",
        )
        db.commit()
    except Exception as exc:
        db.rollback()
        logger.exception("Could not prepare export record for %s", stored_path)
        raise HTTPException(status_code=400, detail=f"Invalid export file: {exc}") from exc

    # We schedule ingestion in the background so the client gets an immediate response.
    # The dashboard polls /exports/{id} for progress.
    background_tasks.add_task(
        _run_ingestion_task,
        str(stored_path),
        export_type,
        original,
        export.id,
    )

    return ExportCreateResponse(
        export_id=export.id,
        status=export.status,
        file_name=stored_path.name,
        message=(
            f"Export accepted and queued. File stored at landing zone as {stored_path.name}. "
            f"Poll GET /api/v1/exports/{export.id} for status and sync logs."
        ),
    )


@router.post(
    "/sync",
    response_model=ExportOut,
    summary="Synchronous variant -- upload and ingest inline (useful for demos and testing)",
)
def create_export_sync(
    file: UploadFile = File(...),
    export_type: str = Form("all"),
    db: Session = Depends(get_db),
) -> ExportOut:
    export_type = export_type.lower().strip()
    if export_type not in VALID_EXPORT_TYPES:
        raise HTTPException(
            status_code=400,
            detail=f"export_type must be one of {sorted(VALID_EXPORT_TYPES)}",
        )
    if not file.filename or not file.filename.lower().endswith(".json"):
        raise HTTPException(status_code=400, detail="File must be a .json export")

    stored_path, original = _save_upload(file, settings.landing_dir)

    result = ingest_export(
        db=db,
        json_path=stored_path,
        export_type=export_type,
        original_filename=original,
        organization_name=settings.default_organization,
    )

    export = db.get(Export, result["export_id"])
    if export is None:
        raise HTTPException(status_code=500, detail="Export record missing after ingestion")
    return ExportOut.model_validate(export)


@router.post(
    "/ingest-path",
    response_model=ExportOut,
    summary="Ingest a JSON file that already exists in the landing zone (no upload)",
)
def ingest_existing_file(
    relative_path: str = Form(..., description="Path relative to the landing directory"),
    export_type: str = Form("all"),
    db: Session = Depends(get_db),
) -> ExportOut:
    export_type = export_type.lower().strip()
    if export_type not in VALID_EXPORT_TYPES:
        raise HTTPException(
            status_code=400,
            detail=f"export_type must be one of {sorted(VALID_EXPORT_TYPES)}",
        )

    stored_path = _resolve_landing_path(relative_path)
    if not stored_path.exists():
        raise HTTPException(status_code=404, detail=f"File not found: {relative_path}")

    result = ingest_export(
        db=db,
        json_path=stored_path,
        export_type=export_type,
        original_filename=stored_path.name,
        organization_name=settings.default_organization,
    )

    export = db.get(Export, result["export_id"])
    if export is None:
        raise HTTPException(status_code=500, detail="Export record missing after ingestion")
    return ExportOut.model_validate(export)


@router.get("", response_model=list[ExportOut], summary="List recent exports")
def list_exports(
    limit: int = 50,
    offset: int = 0,
    db: Session = Depends(get_db),
) -> list[ExportOut]:
    stmt = (
        select(Export)
        .order_by(Export.started_at.desc())
        .limit(min(limit, 200))
        .offset(max(offset, 0))
    )
    exports = db.execute(stmt).scalars().all()
    return [ExportOut.model_validate(e) for e in exports]


@router.get("/{export_id}", response_model=ExportOut, summary="Get export status + metadata")
def get_export(export_id: int, db: Session = Depends(get_db)) -> ExportOut:
    export = db.get(Export, export_id)
    if export is None:
        raise HTTPException(status_code=404, detail="Export not found")
    return ExportOut.model_validate(export)


@router.get(
    "/{export_id}/sync-logs",
    response_model=list[SyncLogOut],
    summary="Pipeline step statuses for this export",
)
def get_export_sync_logs(export_id: int, db: Session = Depends(get_db)) -> list[SyncLogOut]:
    export = db.get(Export, export_id)
    if export is None:
        raise HTTPException(status_code=404, detail="Export not found")

    stmt = select(SyncLog).where(SyncLog.export_id == export_id).order_by(SyncLog.started_at)
    logs = db.execute(stmt).scalars().all()
    return [SyncLogOut.model_validate(log) for log in logs]
