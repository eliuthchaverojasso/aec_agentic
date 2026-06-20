"""Project listing and project detail endpoints."""

from __future__ import annotations

from datetime import datetime, timezone
import hashlib
import shutil
import uuid

from pathlib import Path
import re

from fastapi import APIRouter, Depends, File, Form, HTTPException, UploadFile
from sqlalchemy import func, select
from sqlalchemy.orm import Session
from typing import List

from app.config import settings
from app.database import get_db
from app.ingestion.document_service import register_landing_document
from app.ingestion.file_classifier import classify_landing_file, infer_discipline_from_path_or_name
from app.ingestion.landing_scan_service import rebuild_project_manifest
from app.ingestion.manifest_loader import resolve_landing_path
from app.models import Client, Element, Export, Issue, Model as ModelRecord, Organization, Project
from app.services.bucket_upload_service import upload_file_to_bucket
from app.services.operation_log_service import finish_operation_failure, finish_operation_success, start_operation
from app.schemas import (
    ClientOut,
    ModelOut,
    ProjectCreateRequest,
    ProjectClientBindRequest,
    ProjectClientBindResponse,
    ProjectFileRegisterRequest,
    ProjectModelCreateRequest,
    ProjectModelSummary,
    ProjectSummary,
    ProjectUpdateRequest,
)

router = APIRouter(prefix="/api/v1/projects", tags=["projects"])

INTAKE_FOLDER_BY_TYPE = {
    "owner_requirements": "Owner Requirements",
    "drawing": "Drawings",
    "specification": "Specifications",
}

ALLOWED_EXTENSIONS_BY_TYPE = {
    "owner_requirements": {".xlsx", ".xls", ".xlsm", ".csv"},
    "drawing": {".pdf"},
    "specification": {".pdf", ".docx"},
}


def _compute_health_score(
    total_elements: int,
    critical_issues: int,
    high_issues: int,
    medium_issues: int,
    low_issues: int,
) -> float:
    """Simple score tunable via a weights table later.

    Starts at 100 and subtracts weighted issue penalties, floored at 0.
    """
    if total_elements == 0:
        return 100.0

    issue_penalty = (
        critical_issues * 5.0
        + high_issues * 2.0
        + medium_issues * 0.75
        + low_issues * 0.25
    )
    # Normalize by 100 elements so that larger models don't tank automatically.
    normalized = issue_penalty / max(total_elements / 100.0, 1.0)
    return round(max(0.0, 100.0 - normalized), 2)


@router.get("", response_model=list[ProjectSummary], summary="Portfolio list of projects")
def list_projects(db: Session = Depends(get_db)) -> list[ProjectSummary]:
    projects = db.execute(select(Project).order_by(Project.created_at.desc())).scalars().all()

    summaries: list[ProjectSummary] = []
    for project in projects:
        summaries.append(_build_project_summary(db, project))
    return summaries


@router.get("/{project_id}", response_model=ProjectSummary, summary="Project detail with KPIs")
def get_project(project_id: int, db: Session = Depends(get_db)) -> ProjectSummary:
    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found")
    return _build_project_summary(db, project)


@router.post("", response_model=ProjectSummary, summary="Create a project shell and optional client binding")
def create_project(payload: ProjectCreateRequest, db: Session = Depends(get_db)) -> ProjectSummary:
    op = start_operation(
        db,
        operation_type="project_create",
        operation_label="Create project",
        endpoint="/api/v1/projects",
        method="POST",
        request_summary=payload.model_dump(),
    )
    organization_id = _ensure_default_organization(db).id
    normalized_title = payload.name.strip()
    if not normalized_title:
        raise HTTPException(status_code=400, detail="Project name is required")
    existing = db.execute(
        select(Project).where(
            Project.organization_id == organization_id,
            func.lower(Project.project_title) == normalized_title.lower(),
        )
    ).scalar_one_or_none()
    if existing is not None:
        raise HTTPException(status_code=409, detail="Project with this name already exists")

    client = _resolve_client_binding(db, organization_id, payload.client_id, payload.client_code, payload.client_name)
    project = Project(
        organization_id=organization_id,
        client_id=client.id if client else None,
        project_title=normalized_title,
        project_code=payload.project_code or _generated_project_code(normalized_title),
        project_name=payload.landing_project_folder or _generated_project_folder(normalized_title),
        job_number=payload.project_number,
        client_name=client.display_name if client else payload.client_name,
        location=payload.location,
        jurisdiction=payload.project_type,
        phase=payload.current_milestone or "DD75",
    )
    db.add(project)
    db.commit()
    db.refresh(project)
    result = _build_project_summary(db, project)
    finish_operation_success(
        db,
        op,
        counts={"project_id": project.id},
        response_summary={"project_id": project.id, "project_title": project.project_title},
    )
    return result


@router.patch("/{project_id}", response_model=ProjectSummary, summary="Update editable project fields")
def update_project(project_id: int, payload: ProjectUpdateRequest, db: Session = Depends(get_db)) -> ProjectSummary:
    op = start_operation(
        db,
        operation_type="project_update",
        operation_label="Update project",
        project_id=project_id,
        endpoint=f"/api/v1/projects/{project_id}",
        method="PATCH",
        request_summary=payload.model_dump(exclude_none=True),
    )
    project = db.get(Project, project_id)
    if project is None:
        finish_operation_failure(db, op, errors=["Project not found"])
        raise HTTPException(status_code=404, detail="Project not found")
    for field in ("project_title", "project_code", "project_name", "job_number", "location", "phase", "revit_version"):
        value = getattr(payload, field)
        if value is not None:
            setattr(project, field, value)
    project.updated_at = datetime.now(timezone.utc)
    db.commit()
    db.refresh(project)
    result = _build_project_summary(db, project)
    finish_operation_success(db, op, response_summary={"project_id": project.id, "project_title": project.project_title})
    return result


@router.patch(
    "/{project_id}/client",
    response_model=ProjectClientBindResponse,
    summary="Assign or create a client/owner binding for a project",
)
def bind_project_client(
    project_id: int,
    payload: ProjectClientBindRequest,
    db: Session = Depends(get_db),
) -> ProjectClientBindResponse:
    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found")

    created_client = False
    if payload.client_id is not None:
        client = db.get(Client, payload.client_id)
        if client is None:
            raise HTTPException(status_code=404, detail="Client not found")
    else:
        if not payload.client_code and not payload.client_name and not payload.owner_name:
            raise HTTPException(
                status_code=400,
                detail="Provide client_id or client_code/client_name to bind a project.",
            )
        code = _normalize_client_code(payload.client_code or payload.client_name or payload.owner_name or "")
        display_name = (payload.client_name or payload.owner_name or code.replace("_", " ")).strip()
        client = db.execute(
            select(Client).where(Client.organization_id == project.organization_id, Client.code == code)
        ).scalar_one_or_none()
        if client is None:
            client = Client(
                organization_id=project.organization_id,
                code=code,
                display_name=display_name,
            )
            db.add(client)
            db.flush()
            created_client = True

    project.client_id = client.id
    project.client_name = client.display_name
    if payload.current_milestone or payload.project_stage:
        project.phase = payload.current_milestone or payload.project_stage
    project.updated_at = datetime.now(timezone.utc)
    db.commit()
    db.refresh(project)
    db.refresh(client)
    return ProjectClientBindResponse(
        project=_build_project_summary(db, project),
        client=ClientOut.model_validate(client),
        created_client=created_client,
        message=f"Project bound to client {client.code}.",
    )


@router.get(
    "/{project_id}/models",
    response_model=list[ModelOut],
    summary="Models associated with this project",
)
def list_project_models(project_id: int, db: Session = Depends(get_db)) -> list[ModelOut]:
    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found")

    stmt = (
        select(ModelRecord)
        .where(ModelRecord.project_id == project_id)
        .order_by(ModelRecord.last_sync_at.desc().nulls_last(), ModelRecord.id)
    )
    models = db.execute(stmt).scalars().all()
    return [ModelOut.model_validate(m) for m in models]


@router.post(
    "/{project_id}/models",
    response_model=ProjectModelSummary,
    summary="Create or bind a model shell for the project",
)
def create_project_model(
    project_id: int,
    payload: ProjectModelCreateRequest,
    db: Session = Depends(get_db),
) -> ProjectModelSummary:
    op = start_operation(
        db,
        operation_type="model_create",
        operation_label="Create model",
        project_id=project_id,
        endpoint=f"/api/v1/projects/{project_id}/models",
        method="POST",
        request_summary=payload.model_dump(),
    )
    project = db.get(Project, project_id)
    if project is None:
        finish_operation_failure(db, op, errors=["Project not found"])
        raise HTTPException(status_code=404, detail="Project not found")

    normalized_name = (payload.model_name or payload.revit_document_title or "").strip()
    if not normalized_name:
        raise HTTPException(status_code=400, detail="model_name is required")

    existing = db.execute(
        select(ModelRecord).where(
            ModelRecord.project_id == project_id,
            func.lower(ModelRecord.revit_file_name) == normalized_name.lower(),
            func.lower(ModelRecord.model_type) == (payload.model_type or "Revit").lower(),
        )
    ).scalar_one_or_none()
    if existing is not None:
        result = ProjectModelSummary(
            id=existing.id,
            project_id=existing.project_id,
            model_name=existing.revit_file_name or normalized_name,
            model_type=existing.model_type,
            discipline=existing.discipline,
            source_system=payload.source_system,
            created_at=existing.created_at,
        )
        finish_operation_success(db, op, status="partial", response_summary={"model_id": existing.id, "existing": True})
        return result

    model = ModelRecord(
        project_id=project_id,
        revit_file_name=normalized_name,
        revit_version=payload.revit_version,
        discipline=payload.discipline,
        model_type=payload.model_type,
        exported_by=payload.source_system,
    )
    db.add(model)
    db.commit()
    db.refresh(model)
    result = ProjectModelSummary(
        id=model.id,
        project_id=model.project_id,
        model_name=model.revit_file_name or normalized_name,
        model_type=model.model_type,
        discipline=model.discipline,
        source_system=payload.source_system,
        created_at=model.created_at,
    )
    finish_operation_success(db, op, response_summary={"model_id": model.id, "existing": False})
    return result


@router.post(
    "/{project_id}/files/upload",
    response_model=dict,
    summary="Upload files into a project intake folder and register them",
)
def upload_project_files(
    project_id: int,
    intake_type: str | None = Form(None, description="owner_requirements | drawing | specification"),
    category: str | None = Form(None, description="Alternative contract: drawings | specifications | owner_requirements | revit_exports | supporting"),
    auto_ingest: bool = Form(False, description="Category mode only: trigger ingest after upload"),
    files: list[UploadFile] = File(...),
    db: Session = Depends(get_db),
) -> dict:
    # Two parallel upload contracts share this path. When 'category' is supplied,
    # use the category contract (top-level uploaded list, dup_N collision naming,
    # manifest rebuild). Otherwise the 'intake_type' contract below is unchanged
    # for the existing frontend/landing flow.
    if category is not None:
        return _upload_files_by_category(project_id, category, auto_ingest, files, db)
    if intake_type is None:
        raise HTTPException(status_code=422, detail="Either 'category' or 'intake_type' is required")

    normalized_type = intake_type.strip().lower()
    if normalized_type not in INTAKE_FOLDER_BY_TYPE:
        raise HTTPException(status_code=400, detail="intake_type must be owner_requirements, drawing, or specification")

    if not files:
        raise HTTPException(status_code=400, detail="At least one file is required")

    op = start_operation(
        db,
        operation_type="file_upload",
        operation_label="Upload files",
        project_id=project_id,
        endpoint=f"/api/v1/projects/{project_id}/files/upload",
        method="POST",
        request_summary={"intake_type": normalized_type, "files": [f.filename for f in files]},
    )

    project = db.get(Project, project_id)
    if project is None:
        finish_operation_failure(db, op, errors=["Project not found"])
        raise HTTPException(status_code=404, detail="Project not found")

    project_folder = project.project_name or project.project_title
    target_folder = INTAKE_FOLDER_BY_TYPE[normalized_type]
    requested = len(files)
    uploaded = 0
    created = 0
    updated = 0
    bucket_uploaded = 0
    bucket_failed = 0
    warnings: list[str] = []
    errors: list[str] = []

    bucket_enabled = settings.upload_to_bucket
    if bucket_enabled and not settings.bucket_name:
        warnings.append("UPLOAD_TO_BUCKET=true but BUCKET_NAME is not set. Bucket upload disabled for this request.")
        bucket_enabled = False

    for upload in files:
        filename = Path(upload.filename or "upload.bin").name
        suffix = Path(filename).suffix.lower()
        allowed = ALLOWED_EXTENSIONS_BY_TYPE[normalized_type]
        if suffix not in allowed:
            errors.append(f"Unsupported extension for {normalized_type}: {filename}")
            continue

        rel_path = f"{project_folder}/{target_folder}/{filename}".replace("\\", "/").lstrip("/")
        try:
            destination = resolve_landing_path(settings.landing_dir, rel_path)
        except ValueError as exc:
            errors.append(str(exc))
            continue

        destination.parent.mkdir(parents=True, exist_ok=True)
        final_path = _resolve_conflict_path(destination)
        if final_path.name != filename:
            warnings.append(f"Filename already existed; stored as {final_path.name}")

        with final_path.open("wb") as out:
            shutil.copyfileobj(upload.file, out)

        uploaded += 1
        rel_final = str(final_path.relative_to(settings.landing_dir.resolve())).replace("\\", "/")

        if bucket_enabled:
            try:
                upload_file_to_bucket(final_path, rel_final)
                bucket_uploaded += 1
            except RuntimeError as exc:
                bucket_failed += 1
                warnings.append(str(exc))

        classification = classify_landing_file(final_path)
        values = {
            "project_id": project_id,
            "client_id": project.client_id,
            "project_folder": project_folder,
            "relative_path": rel_final,
            "file_name": final_path.name,
            "file_ext": final_path.suffix.lower(),
            "file_type": _file_type_for_upload(normalized_type, final_path.suffix.lower(), classification.type),
            "document_category": _document_category_for_upload(normalized_type),
            "discipline": infer_discipline_from_path_or_name(final_path),
            "checksum_sha256": _sha256(final_path),
            "file_size_bytes": final_path.stat().st_size,
            "manifest_path": f"{project_folder}/landing_manifest.json",
            "source_system": "landing",
            "ingestion_status": "indexed",
            "evidence_status": "candidate",
            "metadata_json": {"official_evidence": False, "intake_type": normalized_type},
        }
        persistence = register_landing_document(db, values=values, dry_run=False)
        if persistence.get("created"):
            created += 1
        if persistence.get("updated"):
            updated += 1

    result = {
        "ok": len(errors) == 0,
        "operation": "files-upload",
        "project_id": project_id,
        "project_name": project.project_title,
        "project_folder_name": project_folder,
        "endpoint": f"/api/v1/projects/{project_id}/files/upload",
        "intake_type": normalized_type,
        "target_folder": target_folder,
        "counts": {
            "requested": requested,
            "uploaded": uploaded,
            "created": created,
            "updated": updated,
            "failed": len(errors),
            "bucket_uploaded": bucket_uploaded,
            "bucket_failed": bucket_failed,
        },
        "bucket_upload": {
            "enabled": settings.upload_to_bucket,
            "active": bucket_enabled,
            "bucket_name": settings.bucket_name,
            "bucket_prefix": settings.bucket_prefix,
        },
        "warnings": warnings,
        "errors": errors,
        "next_actions": ["Scan landing", "Rebuild manifest", "Dry run ingest"],
    }

    finish_operation_success(
        db,
        op,
        counts=result["counts"],
        warnings=warnings,
        response_summary={"project_id": project_id, "uploaded": uploaded, "failed": len(errors)},
        status="partial" if errors or warnings else "success",
        severity="warning" if errors or warnings else "info",
    )
    return result


@router.post(
    "/{project_id}/files/register",
    response_model=dict,
    summary="Register existing landing files as evidence candidates",
)
def register_project_files(
    project_id: int,
    payload: ProjectFileRegisterRequest,
    db: Session = Depends(get_db),
) -> dict:
    op = start_operation(
        db,
        operation_type="file_register",
        operation_label="Register files",
        project_id=project_id,
        endpoint=f"/api/v1/projects/{project_id}/files/register",
        method="POST",
        request_summary=payload.model_dump(),
    )
    project = db.get(Project, project_id)
    if project is None:
        finish_operation_failure(db, op, errors=["Project not found"])
        raise HTTPException(status_code=404, detail="Project not found")

    project_folder = project.project_name or project.project_title
    created = 0
    updated = 0
    warnings: list[str] = []
    for item in payload.files:
        if Path(item.relative_path).is_absolute():
            raise HTTPException(status_code=400, detail="relative_path must be relative")
        rel_path = f"{project_folder}/{item.relative_path}".replace("\\", "/").lstrip("/")

        try:
            file_path = resolve_landing_path(settings.landing_dir, rel_path)
        except ValueError as exc:
            raise HTTPException(status_code=400, detail=str(exc)) from exc

        if not file_path.exists():
            raise HTTPException(status_code=404, detail=f"File not found: {item.relative_path}")

        classification = classify_landing_file(file_path)
        values = {
            "project_id": project_id,
            "client_id": project.client_id,
            "project_folder": project_folder,
            "relative_path": rel_path.replace("\\", "/"),
            "file_name": file_path.name,
            "file_ext": file_path.suffix.lower(),
            "file_type": classification.type,
            "document_category": (item.category or _document_category_for_classification(classification.type)),
            "discipline": item.discipline or infer_discipline_from_path_or_name(file_path),
            "checksum_sha256": _sha256(file_path),
            "file_size_bytes": file_path.stat().st_size,
            "manifest_path": f"{project_folder}/landing_manifest.json",
            "source_system": "landing",
            "ingestion_status": "indexed",
            "evidence_status": "candidate",
            "metadata_json": {"official_evidence": False},
        }
        persistence = register_landing_document(db, values=values, dry_run=False)
        if persistence.get("created"):
            created += 1
        if persistence.get("updated"):
            updated += 1
        warnings.extend(persistence.get("warnings", []))

    db.commit()
    result = {
        "ok": True,
        "operation": "files-register",
        "project_id": project_id,
        "project_name": project.project_title,
        "project_folder_name": project_folder,
        "endpoint": f"/api/v1/projects/{project_id}/files/register",
        "counts": {"created": created, "updated": updated, "requested": len(payload.files)},
        "warnings": warnings,
        "errors": [],
        "next_actions": ["Run Processing / Sync scan + ingest after registration."],
    }
    finish_operation_success(
        db,
        op,
        counts=result["counts"],
        warnings=warnings,
        response_summary={"project_id": project_id, "registered": created + updated},
        status="partial" if warnings else "success",
        severity="warning" if warnings else "info",
    )
    return result


_UPLOAD_CATEGORY_FOLDERS: dict[str, str] = {
    "drawings": "Drawings",
    "owner_requirements": "Owner Requirements",
    "specifications": "Specifications",
    "revit_exports": "Revit Exports",
    "supporting": "Supporting",
}


def _sanitize_upload_filename(raw: str) -> str:
    name = Path(raw).name  # strip any directory component
    name = re.sub(r"[^\w\s.\-]", "_", name)
    name = re.sub(r"\s+", "_", name.strip())
    return name[:200] or "upload"


def _unique_dest_path(dest_dir: Path, filename: str) -> Path:
    candidate = dest_dir / filename
    if not candidate.exists():
        return candidate
    stem = Path(filename).stem
    suffix = Path(filename).suffix
    counter = 1
    while True:
        candidate = dest_dir / f"{stem}_{counter}{suffix}"
        if not candidate.exists():
            return candidate
        counter += 1


def _upload_files_by_category(
    project_id: int,
    category: str,
    auto_ingest: bool,
    files: List[UploadFile],
    db: Session,
) -> dict:
    """Category-contract upload mode (see the upload_project_files dispatcher).

    Returns a top-level ``uploaded`` list with per-file ``saved_name``/``size_bytes``,
    uses ``dup_N`` collision naming, and rebuilds the landing manifest.
    """
    if category not in _UPLOAD_CATEGORY_FOLDERS:
        raise HTTPException(
            status_code=400,
            detail=f"Invalid category '{category}'. Valid values: {list(_UPLOAD_CATEGORY_FOLDERS)}",
        )

    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found")

    project_folder = project.project_name or project.project_title
    subfolder = _UPLOAD_CATEGORY_FOLDERS[category]
    rel_dir = f"{project_folder}/{subfolder}"

    try:
        dest_dir = resolve_landing_path(settings.landing_dir, rel_dir)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc

    dest_dir.mkdir(parents=True, exist_ok=True)

    uploaded: list[dict] = []
    warnings: list[str] = []

    for upload in files:
        raw_name = upload.filename or "upload"
        safe_name = _sanitize_upload_filename(raw_name)
        if safe_name != raw_name:
            warnings.append(f"Filename sanitized: '{raw_name}' → '{safe_name}'")

        dest_path = _unique_dest_path(dest_dir, safe_name)
        if dest_path.name != safe_name:
            warnings.append(f"Collision avoided: '{safe_name}' saved as '{dest_path.name}'")

        content = upload.file.read()
        dest_path.write_bytes(content)

        uploaded.append(
            {
                "original_name": raw_name,
                "saved_name": dest_path.name,
                "size_bytes": len(content),
                "path": str(dest_path.relative_to(settings.landing_dir)).replace("\\", "/"),
            }
        )

    manifest_result: dict = {}
    try:
        report = rebuild_project_manifest(
            project_folder=project_folder,
            preserve_existing=True,
            include_pdf_metadata=False,
            dry_run=False,
        )
        manifest_result = {
            "files_found": report.files_found,
            "manifest_updated": report.manifest_updated,
        }
    except Exception as exc:  # noqa: BLE001
        warnings.append(f"Manifest rebuild failed: {exc}")

    return {
        "ok": True,
        "operation": "files-upload",
        "project_id": project_id,
        "project_folder": project_folder,
        "category": category,
        "subfolder": subfolder,
        "uploaded": uploaded,
        "manifest": manifest_result,
        "auto_ingest": auto_ingest,
        "warnings": warnings,
    }


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _build_project_summary(db: Session, project: Project) -> ProjectSummary:
    active_models = db.execute(
        select(func.count()).select_from(ModelRecord).where(ModelRecord.project_id == project.id)
    ).scalar_one()

    # Latest export per model for this project
    latest_sync = db.execute(
        select(func.max(Export.completed_at))
        .where(Export.project_id == project.id, Export.status == "completed")
    ).scalar_one()

    # Issue counts for the latest completed exports (one per model, dedup)
    severity_counts = _issue_counts_for_project(db, project.id)

    total_elements = db.execute(
        select(func.count(Element.id))
        .join(Export, Export.id == Element.export_id)
        .where(Export.project_id == project.id)
    ).scalar_one()

    score = _compute_health_score(
        total_elements=total_elements,
        critical_issues=severity_counts.get("critical", 0),
        high_issues=severity_counts.get("high", 0),
        medium_issues=severity_counts.get("medium", 0),
        low_issues=severity_counts.get("low", 0),
    )

    return ProjectSummary(
        id=project.id,
        organization_id=project.organization_id,
        client_id=project.client_id,
        project_title=project.project_title,
        project_code=project.project_code,
        project_name=project.project_name,
        job_number=project.job_number,
        revit_version=project.revit_version,
        client_name=project.client_name,
        location=project.location,
        jurisdiction=project.jurisdiction,
        phase=project.phase,
        created_at=project.created_at,
        updated_at=project.updated_at,
        active_models=active_models,
        open_issues=sum(severity_counts.values()),
        critical_issues=severity_counts.get("critical", 0),
        high_issues=severity_counts.get("high", 0),
        medium_issues=severity_counts.get("medium", 0),
        low_issues=severity_counts.get("low", 0),
        model_health_score=score,
        last_sync_at=latest_sync,
    )


def _issue_counts_for_project(db: Session, project_id: int) -> dict[str, int]:
    stmt = (
        select(Issue.severity, func.count(Issue.id))
        .where(Issue.project_id == project_id, Issue.status == "open")
        .group_by(Issue.severity)
    )
    rows = db.execute(stmt).all()
    return {sev: cnt for sev, cnt in rows}


def _normalize_client_code(value: str) -> str:
    code = value.strip().upper().replace("&", "AND")
    code = "".join(ch if ch.isalnum() else "_" for ch in code)
    while "__" in code:
        code = code.replace("__", "_")
    return code.strip("_") or "OWNER"


def _ensure_default_organization(db: Session) -> Organization:
    org = db.get(Organization, 1)
    if org is None:
        org = Organization(id=1, name="EMA Engineering")
        db.add(org)
        db.flush()
    return org


def _generated_project_code(name: str) -> str:
    parts = re.findall(r"[A-Za-z0-9]+", name.upper())
    if not parts:
        return "PROJECT-001"
    return "-".join(parts[:3])[:100]


def _generated_project_folder(name: str) -> str:
    slug = re.sub(r"[^A-Za-z0-9 _-]+", "", name).strip()
    slug = re.sub(r"\s+", " ", slug)
    return slug or "Project"


def _resolve_client_binding(
    db: Session,
    organization_id: int,
    client_id: int | None,
    client_code: str | None,
    client_name: str | None,
) -> Client | None:
    if client_id is not None:
        client = db.get(Client, client_id)
        if client is None:
            raise HTTPException(status_code=404, detail="Client not found")
        return client
    if not client_code and not client_name:
        return None
    code = _normalize_client_code(client_code or client_name or "")
    client = db.execute(
        select(Client).where(Client.organization_id == organization_id, Client.code == code)
    ).scalar_one_or_none()
    if client is not None:
        return client
    client = Client(
        organization_id=organization_id,
        code=code,
        display_name=(client_name or code).strip(),
    )
    db.add(client)
    db.flush()
    return client


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _resolve_conflict_path(path: Path) -> Path:
    if not path.exists():
        return path
    stem = path.stem
    suffix = path.suffix
    ts = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S")
    uid = uuid.uuid4().hex[:6]
    return path.with_name(f"{stem}_{ts}_{uid}{suffix}")


def _file_type_for_upload(intake_type: str, suffix: str, detected: str) -> str:
    if intake_type == "owner_requirements":
        return "owner_requirements"
    if intake_type == "drawing":
        return "drawing_pdf"
    if intake_type == "specification":
        # Keep Specifications intake grouped in the same lane for now.
        return "specification_pdf"
    return detected


def _document_category_for_upload(intake_type: str) -> str:
    if intake_type == "owner_requirements":
        return "owner_requirements"
    if intake_type == "drawing":
        return "drawing"
    if intake_type == "specification":
        return "specification"
    return "unknown"


def _document_category_for_classification(file_type: str) -> str:
    if file_type == "owner_requirements":
        return "owner_requirements"
    if file_type == "drawing_pdf":
        return "drawing"
    if file_type == "specification_pdf":
        return "specification"
    if file_type == "pdf_document":
        return "supporting"
    if file_type == "dwfx_export":
        return "dwfx_export"
    if file_type == "viewpoint_json":
        return "viewpoint"
    if file_type == "timeline_excel":
        return "timeline"
    if file_type == "revit_export":
        return "revit_export"
    return "unknown"
