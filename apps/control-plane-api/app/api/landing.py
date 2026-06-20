"""Landing-zone ingestion endpoints."""

from __future__ import annotations

import logging
from pathlib import Path
import hashlib
import re
from typing import Any

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import func
from sqlalchemy.orm import Session

from app.config import settings
from app.database import get_db
from app.ingestion.document_service import register_landing_document
from app.ingestion.file_classifier import classify_landing_file, infer_discipline_from_path_or_name
from app.ingestion.landing_service import ingest_landing_manifest, resolve_client_code_for_owner_reqs
from app.ingestion.manifest_loader import load_landing_manifest
from app.ingestion.landing_scan_service import rebuild_project_manifest, scan_landing
from app.ingestion.manifest_loader import resolve_landing_path
from app.models import Client, Project
from app.services.operation_log_service import finish_operation_failure, finish_operation_success, start_operation
from app.schemas import (
    LandingBootstrapRequest,
    LandingBootstrapResponse,
    LandingDiscoverRequest,
    LandingDiscoverResponse,
    LandingDiscoveredProject,
    LandingIngestAllRequest,
    LandingIngestAllResponse,
    LandingIngestReport,
    LandingIngestRequest,
    LandingManifestBatchRequest,
    LandingManifestBatchResponse,
    LandingManifestBatchProjectResult,
    LandingProjectBindRequest,
    LandingProjectBindResponse,
    LandingProjectCounts,
    LandingProjectSummary,
    LandingProjectsDiscoveryResponse,
    LandingRebuildManifestRequest,
    LandingScanReport,
    LandingScanRequest,
    ProjectLandingConfigureRequest,
    ProjectLandingStatusOut,
    ProjectOperationOut,
)

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/v1/landing", tags=["landing"])
projects_router = APIRouter(prefix="/api/v1/projects", tags=["landing"])


@router.get("/projects", response_model=LandingProjectsDiscoveryResponse, summary="Discover all landing projects")
def discover_landing_projects_v2(db: Session = Depends(get_db)) -> LandingProjectsDiscoveryResponse:
    root = settings.landing_dir.resolve()
    projects: list[LandingProjectSummary] = []
    totals = LandingProjectCounts()
    for project_root in _iter_landing_projects(root):
        summary = _build_landing_project_summary(db, root, project_root)
        projects.append(summary)
        _merge_landing_counts(totals, summary.counts)
    return LandingProjectsDiscoveryResponse(
        landing_root=str(root),
        project_count=len(projects),
        totals=totals,
        projects=projects,
    )


@router.post("/rebuild-all-manifests", response_model=LandingManifestBatchResponse, summary="Rebuild manifests for all landing projects")
def rebuild_all_manifests(payload: LandingManifestBatchRequest) -> LandingManifestBatchResponse:
    root = settings.landing_dir.resolve()
    selected = set(payload.project_folders or [])
    project_roots = [p for p in _iter_landing_projects(root) if not selected or p.name in selected]
    results: list[LandingManifestBatchProjectResult] = []
    updated = 0
    skipped = 0
    for project_root in project_roots:
        summary = _summarize_project_files(root, project_root)
        suggestion = _infer_client_suggestion(project_root, summary["documents"].get("owner_requirements", []))
        report = rebuild_project_manifest(
            project_folder=project_root.name,
            preserve_existing=payload.preserve_existing,
            include_pdf_metadata=False,
            dry_run=payload.dry_run,
        )
        has_errors = report.status == "failed" or bool(report.errors)
        if has_errors:
            skipped += 1
        else:
            updated += 1
        results.append(
            LandingManifestBatchProjectResult(
                project_folder=project_root.name,
                manifest_path=f"{project_root.name}/landing_manifest.json",
                would_write=not payload.dry_run,
                file_count=summary["file_count"],
                counts=summary["counts"],
                client_suggestion=suggestion,
                warnings=report.warnings,
                errors=report.errors,
            )
        )
    return LandingManifestBatchResponse(
        dry_run=payload.dry_run,
        landing_root=str(root),
        project_count=len(project_roots),
        updated=updated,
        skipped=skipped,
        projects=results,
    )


@router.post("/ingest-all", response_model=LandingIngestAllResponse, summary="Ingest all landing projects")
def ingest_all_landing(payload: LandingIngestAllRequest, db: Session = Depends(get_db)) -> LandingIngestAllResponse:
    root = settings.landing_dir.resolve()
    selected = set(payload.project_folders or [])
    project_roots = [p for p in _iter_landing_projects(root) if not selected or p.name in selected]
    success = 0
    partial = 0
    failed = 0
    results = []
    for project_root in project_roots:
        project = _find_project_by_folder(db, project_root.name)
        summary = _summarize_project_files(root, project_root)
        project_errors: list[str] = []
        project_warnings: list[str] = []
        if not (project_root / "landing_manifest.json").exists():
            results.append(
                {
                    "project_folder": project_root.name,
                    "project_id": project.id if project else None,
                    "status": "partial",
                    "counts": {"documents": summary["file_count"]},
                    "readiness": {},
                    "warnings": [],
                    "errors": ["Missing landing_manifest.json. Rebuild manifest first."],
                    "next_action": "Rebuild manifest",
                }
            )
            partial += 1
            continue
        if payload.require_client_for_owner_requirements and summary["counts"].owner_requirements > 0 and (project is None or project.client_id is None):
            manifest_binding: dict[str, Any] = {}
            manifest_entries: list[Any] = []
            try:
                manifest_path_str = f"{project_root.name}/landing_manifest.json"
                manifest = load_landing_manifest(root, manifest_path_str)
                manifest_binding = manifest.project_binding
                manifest_entries = manifest.files
            except Exception:
                manifest_binding = {}
                manifest_entries = []
            resolved_code, _source, warning, blocker = resolve_client_code_for_owner_reqs(
                db=db,
                manifest_binding=manifest_binding,
                project_folder_name=project_root.name,
                manifest_entries=manifest_entries,
            )
            if blocker:
                results.append(
                    {
                        "project_folder": project_root.name,
                        "project_id": project.id if project else None,
                        "status": "partial",
                        "counts": {
                            "documents": summary["file_count"],
                            "revit_exports": summary["counts"].revit_exports,
                            "drawings": summary["counts"].drawings,
                            "owner_requirements": summary["counts"].owner_requirements,
                            "specifications": summary["counts"].specifications,
                            "sidecars": summary["counts"].revit_meta,
                        },
                        "readiness": {},
                        "warnings": project_warnings,
                        "errors": project_errors + [blocker],
                        "next_action": "Bind project/client",
                    }
                )
                partial += 1
                continue
            if warning:
                project_warnings.append(warning)
        manifest_path = f"{project_root.name}/landing_manifest.json"
        report = ingest_landing_manifest(
            db=db,
            manifest_path=manifest_path,
            dry_run=payload.dry_run,
            recalculate_readiness=not payload.dry_run,
        )
        project_warnings.extend(report.warnings)
        project_errors.extend(report.errors)
        if report.status == "failed":
            project_status = "failed"
            failed += 1
        elif report.errors:
            project_status = "partial"
            partial += 1
        else:
            project_status = "success"
            success += 1
        results.append(
            {
                "project_folder": project_root.name,
                "project_id": project.id if project else None,
                "status": project_status,
                "counts": {
                    "documents": summary["file_count"],
                    "revit_exports": summary["counts"].revit_exports,
                    "drawings": summary["counts"].drawings,
                    "owner_requirements": summary["counts"].owner_requirements,
                    "specifications": summary["counts"].specifications,
                    "sidecars": summary["counts"].revit_meta,
                },
                "readiness": {},
                "warnings": project_warnings,
                "errors": project_errors,
                "next_action": "Create readiness snapshot" if project_status == "success" else "Review warnings/errors",
            }
        )
    return LandingIngestAllResponse(
        dry_run=payload.dry_run,
        project_count=len(project_roots),
        processed=len(project_roots),
        success=success,
        partial=partial,
        failed=failed,
        projects=results,
    )


@router.post("/projects/{project_folder}/bind", response_model=LandingProjectBindResponse, summary="Bind landing folder to project and client")
def bind_landing_project(project_folder: str, payload: LandingProjectBindRequest, db: Session = Depends(get_db)) -> LandingProjectBindResponse:
    root = settings.landing_dir.resolve()
    _resolve_project_path(root, project_folder)
    project = db.get(Project, payload.project_id) if payload.project_id else _find_project_by_folder(db, project_folder)
    if project is None and payload.create_project:
        project = _get_or_create_project(
            db=db,
            org_id=1,
            name=payload.project_name or project_folder,
            project_code=None,
            folder_name=project_folder,
            client=_get_or_create_client(db, 1, payload.client_code or "OWNER", payload.client_name or payload.client_code or "Owner"),
        )
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found. Provide project_id or set create_project=true.")
    client = None
    if payload.client_id is not None:
        client = db.get(Client, payload.client_id)
        if client is None:
            raise HTTPException(status_code=404, detail="Client not found for client_id.")
    elif payload.client_code or payload.client_name:
        if not payload.client_code:
            raise HTTPException(status_code=400, detail="client_code is required when binding/creating client by code/name.")
        client = _get_or_create_client(db, project.organization_id, payload.client_code, payload.client_name or payload.client_code)
    if client is not None:
        project.client_id = client.id
        project.client_name = client.display_name
    if payload.milestone:
        project.phase = payload.milestone
    project.project_name = project_folder
    db.commit()
    status = "ready" if (project.client_id or 0) > 0 else "needs_client_binding"
    return LandingProjectBindResponse(
        project_folder=project_folder,
        project_id=project.id,
        project_name=project.project_title,
        client_id=project.client_id,
        client_name=project.client_name,
        client_code=client.code if client else None,
        status=status,
        warnings=[],
        errors=[],
        next_actions=["Run dry-run ingest", "Run ingest"],
    )


@router.post(
    "/ingest",
    response_model=LandingIngestReport,
    summary="Ingest files from the landing zone using a manifest",
)
def ingest_landing(
    payload: LandingIngestRequest,
    db: Session = Depends(get_db),
) -> LandingIngestReport:
    try:
        return ingest_landing_manifest(
            db=db,
            manifest_path=payload.manifest_path,
            dry_run=payload.dry_run,
            recalculate_readiness=payload.recalculate_readiness,
        )
    except FileNotFoundError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except Exception as exc:  # noqa: BLE001
        logger.exception("Landing ingestion failed")
        raise HTTPException(status_code=500, detail=f"Landing ingestion failed: {exc}") from exc


@router.post(
    "/scan",
    response_model=LandingScanReport,
    summary="Scan landing folders and optionally preview manifest updates",
)
def scan_landing_folder(payload: LandingScanRequest) -> LandingScanReport:
    report = scan_landing(
        project_folder=payload.project_folder,
        update_manifest=payload.update_manifest,
        include_pdf_metadata=payload.include_pdf_metadata,
        dry_run=payload.dry_run,
        preserve_existing=payload.preserve_existing,
    )
    if report.status == "failed":
        raise HTTPException(status_code=400, detail=report.model_dump())
    return report


@router.post(
    "/rebuild-manifest",
    response_model=LandingScanReport,
    summary="Rebuild or update a project landing manifest from local files",
)
def rebuild_manifest(payload: LandingRebuildManifestRequest) -> LandingScanReport:
    report = rebuild_project_manifest(
        project_folder=payload.project_folder,
        preserve_existing=payload.preserve_existing,
        include_pdf_metadata=payload.include_pdf_metadata,
        dry_run=payload.dry_run,
    )
    if report.status == "failed":
        raise HTTPException(status_code=400, detail=report.model_dump())
    return report


@router.post(
    "/projects/discover",
    response_model=LandingDiscoverResponse,
    summary="Discover candidate projects inside a landing root",
)
def discover_landing_projects(payload: LandingDiscoverRequest) -> LandingDiscoverResponse:
    root = _resolve_landing_root(payload.landing_root)
    projects: list[LandingDiscoveredProject] = []
    for child in sorted(root.iterdir()):
        if not child.is_dir():
            continue
        counts = _count_project_files(child)
        latest_revit = _latest_revit_export(child)
        projects.append(
            LandingDiscoveredProject(
                project_folder_name=child.name,
                has_manifest=(child / "landing_manifest.json").exists(),
                latest_revit_export=latest_revit,
                counts=counts,
                warnings=[],
            )
        )
    return LandingDiscoverResponse(
        landing_root=str(root),
        projects=projects,
        warnings=[],
        errors=[],
    )


@router.post(
    "/projects/bootstrap-from-folder",
    response_model=LandingBootstrapResponse,
    summary="Create/update project shell from an existing landing project folder",
)
def bootstrap_project_from_folder(payload: LandingBootstrapRequest, db: Session = Depends(get_db)) -> LandingBootstrapResponse:
    op = start_operation(
        db,
        operation_type="project_bootstrap",
        operation_label="Bootstrap project from landing folder",
        endpoint="/api/v1/landing/projects/bootstrap-from-folder",
        method="POST",
        request_summary=payload.model_dump(),
    )
    root = _resolve_landing_root(payload.landing_root)
    project_path = _resolve_project_path(root, payload.project_folder_name)
    if not project_path.exists():
        finish_operation_failure(db, op, errors=["Project folder not found in landing root"])
        raise HTTPException(status_code=404, detail="Project folder not found in landing root")

    org_id = 1
    client = _get_or_create_client(
        db,
        org_id,
        payload.client_code or payload.client_name or payload.project_folder_name,
        payload.client_name or payload.client_code or payload.project_folder_name,
    )
    project = _get_or_create_project(
        db=db,
        org_id=org_id,
        name=payload.project_display_name or payload.project_folder_name,
        project_code=payload.project_code,
        folder_name=payload.project_folder_name,
        client=client,
    )
    folder_status = _ensure_standard_folders(project_path, create_folders=True)
    discovered_files = _register_project_folder_files(db, project, root, project_path)
    db.commit()
    landing_status = _build_project_landing_status(project, project_path, folder_status, operation="bootstrap-from-folder")
    result = LandingBootstrapResponse(
        project_id=project.id,
        client_id=project.client_id,
        project_name=project.project_title,
        project_folder_name=project.project_name or project.project_title,
        project_landing_path=str(project_path),
        discovered_files=discovered_files,
        landing_status=landing_status,
        warnings=[],
        errors=[],
        next_actions=[
            "Select project in dashboard.",
            "Run Scan Landing.",
            "Run Dry Run Ingest.",
            "Run Ingest.",
        ],
    )
    finish_operation_success(
        db,
        op,
        counts={"discovered_files": discovered_files},
        response_summary={"project_id": project.id, "client_id": project.client_id, "project_folder_name": result.project_folder_name},
    )
    return result


@router.post(
    "/projects/{project_id}/landing/configure",
    response_model=ProjectLandingStatusOut,
    summary="Configure landing root + project folder for a project",
)
def configure_project_landing(
    project_id: int,
    payload: ProjectLandingConfigureRequest,
    db: Session = Depends(get_db),
) -> ProjectLandingStatusOut:
    op = start_operation(
        db,
        operation_type="landing_configure",
        operation_label="Configure project landing",
        project_id=project_id,
        endpoint=f"/api/v1/projects/{project_id}/landing/configure",
        method="POST",
        request_summary=payload.model_dump(),
    )
    project = db.get(Project, project_id)
    if project is None:
        finish_operation_failure(db, op, errors=["Project not found"])
        raise HTTPException(status_code=404, detail="Project not found")
    root = _resolve_landing_root(payload.landing_root)
    if Path(payload.project_folder_name).is_absolute() or ".." in Path(payload.project_folder_name).parts:
        raise HTTPException(status_code=400, detail="project_folder_name must be a relative folder name")

    project.project_name = payload.project_folder_name.strip()
    project_path = _resolve_project_path(root, project.project_name)
    folder_status = _ensure_standard_folders(project_path, create_folders=payload.create_folders)
    db.commit()
    result = _build_project_landing_status(project, project_path, folder_status, operation="landing-configure")
    finish_operation_success(db, op, counts=result.counts, warnings=result.warnings, response_summary=result.model_dump())
    return result


@projects_router.post(
    "/{project_id}/landing/configure",
    response_model=ProjectLandingStatusOut,
    summary="Configure landing root + project folder for a project",
)
def configure_project_landing_v2(
    project_id: int,
    payload: ProjectLandingConfigureRequest,
    db: Session = Depends(get_db),
) -> ProjectLandingStatusOut:
    return configure_project_landing(project_id, payload, db)


@router.get(
    "/projects/{project_id}/landing/status",
    response_model=ProjectLandingStatusOut,
    summary="Get landing status for selected project",
)
def get_project_landing_status(project_id: int, db: Session = Depends(get_db)) -> ProjectLandingStatusOut:
    op = start_operation(
        db,
        operation_type="landing_status",
        operation_label="Get project landing status",
        project_id=project_id,
        endpoint=f"/api/v1/projects/{project_id}/landing/status",
        method="GET",
    )
    project = db.get(Project, project_id)
    if project is None:
        finish_operation_failure(db, op, errors=["Project not found"])
        raise HTTPException(status_code=404, detail="Project not found")
    root = settings.landing_dir.resolve()
    project_folder = project.project_name or project.project_title
    try:
        project_path = _resolve_project_path(root, project_folder)
    except HTTPException:
        project_path = None
    if project_path is None or not project_path.exists():
        available = _find_available_project_folders(root)
        suggested = _suggest_best_match(project_folder, available)
        finish_operation_success(db, op, response_summary={"folder_found": False})
        return ProjectLandingStatusOut(
            operation="landing-status",
            project_id=project.id,
            project_name=project.project_title,
            project_folder_name=project_folder,
            endpoint=f"/api/v1/projects/{project.id}/landing/status",
            project_landing_path=None,
            folder_status={},
            counts={},
            next_actions=["Bind project to a landing folder", "Run Discover to see available folders"],
            folder_found=False,
            landing_root=str(root),
            requested_folder=project_folder,
            available_folders=available,
            suggested_folder=suggested,
        )
    folder_status = _ensure_standard_folders(project_path, create_folders=False)
    result = _build_project_landing_status(project, project_path, folder_status, operation="landing-status")
    finish_operation_success(db, op, counts=result.counts, warnings=result.warnings, response_summary=result.model_dump())
    return result


@projects_router.get(
    "/{project_id}/landing/status",
    response_model=ProjectLandingStatusOut,
    summary="Get landing status for selected project",
)
def get_project_landing_status_v2(project_id: int, db: Session = Depends(get_db)) -> ProjectLandingStatusOut:
    return get_project_landing_status(project_id, db)


@router.post("/projects/{project_id}/landing/scan", response_model=ProjectOperationOut, summary="Project-scoped scan alias")
def project_scan(project_id: int, db: Session = Depends(get_db)) -> ProjectOperationOut:
    op = start_operation(
        db,
        operation_type="scan_landing",
        operation_label="Scan landing",
        project_id=project_id,
        endpoint=f"/api/v1/projects/{project_id}/landing/scan",
        method="POST",
    )
    project = _require_project(db, project_id)
    folder = project.project_name or project.project_title
    report = scan_landing(project_folder=folder, update_manifest=False, include_pdf_metadata=True, dry_run=True, preserve_existing=True)
    if report.status == "failed":
        finish_operation_failure(db, op, errors=report.errors or ["Scan failed"], warnings=report.warnings)
        raise HTTPException(status_code=400, detail=report.model_dump())
    result = ProjectOperationOut(
        operation="scan",
        project_id=project_id,
        project_name=project.project_title,
        project_folder_name=folder,
        endpoint=f"/api/v1/projects/{project_id}/landing/scan",
        dry_run=True,
        counts={"files_found": report.files_found, "documents": len(report.documents)},
        warnings=report.warnings,
        errors=report.errors,
        next_actions=["Rebuild manifest", "Dry run ingest"],
    )
    finish_operation_success(db, op, counts=result.counts, warnings=result.warnings, response_summary=result.model_dump())
    return result


@projects_router.post("/{project_id}/landing/scan", response_model=ProjectOperationOut, summary="Project-scoped scan alias")
def project_scan_v2(project_id: int, db: Session = Depends(get_db)) -> ProjectOperationOut:
    return project_scan(project_id, db)


@router.post(
    "/projects/{project_id}/landing/rebuild-manifest",
    response_model=ProjectOperationOut,
    summary="Project-scoped rebuild manifest alias",
)
def project_rebuild_manifest(project_id: int, db: Session = Depends(get_db)) -> ProjectOperationOut:
    op = start_operation(
        db,
        operation_type="rebuild_manifest",
        operation_label="Rebuild manifest",
        project_id=project_id,
        endpoint=f"/api/v1/projects/{project_id}/landing/rebuild-manifest",
        method="POST",
    )
    project = _require_project(db, project_id)
    folder = project.project_name or project.project_title
    report = rebuild_project_manifest(project_folder=folder, preserve_existing=True, include_pdf_metadata=True, dry_run=False)
    if report.status == "failed":
        finish_operation_failure(db, op, errors=report.errors or ["Rebuild manifest failed"], warnings=report.warnings)
        raise HTTPException(status_code=400, detail=report.model_dump())
    result = ProjectOperationOut(
        operation="rebuild-manifest",
        project_id=project_id,
        project_name=project.project_title,
        project_folder_name=folder,
        endpoint=f"/api/v1/projects/{project_id}/landing/rebuild-manifest",
        dry_run=False,
        counts={"files_found": report.files_found, "documents": len(report.documents)},
        warnings=report.warnings,
        errors=report.errors,
        next_actions=["Dry run ingest", "Run ingest"],
    )
    finish_operation_success(db, op, counts=result.counts, warnings=result.warnings, response_summary=result.model_dump())
    return result


@projects_router.post(
    "/{project_id}/landing/rebuild-manifest",
    response_model=ProjectOperationOut,
    summary="Project-scoped rebuild manifest alias",
)
def project_rebuild_manifest_v2(project_id: int, db: Session = Depends(get_db)) -> ProjectOperationOut:
    return project_rebuild_manifest(project_id, db)


@router.post(
    "/projects/{project_id}/landing/ingest/dry-run",
    response_model=ProjectOperationOut,
    summary="Project-scoped dry-run ingest alias",
)
def project_ingest_dry_run(project_id: int, db: Session = Depends(get_db)) -> ProjectOperationOut:
    return _project_ingest(project_id, db, dry_run=True)


@projects_router.post(
    "/{project_id}/landing/ingest/dry-run",
    response_model=ProjectOperationOut,
    summary="Project-scoped dry-run ingest alias",
)
def project_ingest_dry_run_v2(project_id: int, db: Session = Depends(get_db)) -> ProjectOperationOut:
    return project_ingest_dry_run(project_id, db)


@router.post(
    "/projects/{project_id}/landing/ingest",
    response_model=ProjectOperationOut,
    summary="Project-scoped ingest alias",
)
def project_ingest(project_id: int, db: Session = Depends(get_db)) -> ProjectOperationOut:
    return _project_ingest(project_id, db, dry_run=False)


@projects_router.post(
    "/{project_id}/landing/ingest",
    response_model=ProjectOperationOut,
    summary="Project-scoped ingest alias",
)
def project_ingest_v2(project_id: int, db: Session = Depends(get_db)) -> ProjectOperationOut:
    return project_ingest(project_id, db)


def _project_ingest(project_id: int, db: Session, dry_run: bool) -> ProjectOperationOut:
    op = start_operation(
        db,
        operation_type="dry_run_ingest" if dry_run else "run_ingest",
        operation_label="Ingest landing manifest",
        project_id=project_id,
        endpoint=f"/api/v1/projects/{project_id}/landing/ingest" + ("/dry-run" if dry_run else ""),
        method="POST",
    )
    project = _require_project(db, project_id)
    folder = project.project_name or project.project_title
    manifest = f"{folder}/landing_manifest.json"
    report = ingest_landing_manifest(db=db, manifest_path=manifest, dry_run=dry_run, recalculate_readiness=not dry_run)
    if report.status == "failed":
        finish_operation_failure(db, op, errors=report.errors or ["Ingest failed"], warnings=report.warnings, response_summary=report.model_dump())
        raise HTTPException(status_code=400, detail=report.model_dump())
    result = ProjectOperationOut(
        operation="ingest",
        project_id=project_id,
        project_name=project.project_title,
        project_folder_name=folder,
        endpoint=f"/api/v1/projects/{project_id}/landing/ingest" + ("/dry-run" if dry_run else ""),
        dry_run=dry_run,
        counts={"processed": sum(report.processed.values()) if report.processed else 0, "files": len(report.files)},
        warnings=report.warnings,
        errors=report.errors,
        next_actions=["Review dashboard updates", "Create readiness snapshot"],
    )
    finish_operation_success(
        db,
        op,
        counts=result.counts,
        warnings=result.warnings,
        status="partial" if result.warnings else "success",
        severity="warning" if result.warnings else "info",
        response_summary=result.model_dump(),
    )
    return result


def _require_project(db: Session, project_id: int) -> Project:
    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found")
    return project


def _resolve_landing_root(landing_root: str) -> Path:
    root = Path(landing_root).expanduser().resolve()
    configured = settings.landing_dir.resolve()
    if root != configured:
        raise HTTPException(status_code=400, detail=f"landing_root must match configured local landing root: {configured}")
    if not root.exists():
        raise HTTPException(status_code=404, detail="landing_root does not exist")
    return root


def _resolve_project_path(root: Path, project_folder: str) -> Path:
    try:
        return resolve_landing_path(root, project_folder)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


STANDARD_FOLDERS = (
    "Revit Exports",
    "Drawings",
    "Specifications",
    "Owner Requirements",
    "3D Exports",
    "Viewpoints",
    "Timeline",
    "Supporting",
)


def _ensure_standard_folders(project_path: Path, *, create_folders: bool) -> dict[str, bool]:
    if create_folders:
        project_path.mkdir(parents=True, exist_ok=True)
    status: dict[str, bool] = {}
    for folder in STANDARD_FOLDERS:
        path = project_path / folder
        if create_folders:
            path.mkdir(parents=True, exist_ok=True)
        status[folder] = path.exists()
    return status


def _build_project_landing_status(
    project: Project,
    project_path: Path,
    folder_status: dict[str, bool],
    *,
    operation: str,
) -> ProjectLandingStatusOut:
    counts = _count_project_files(project_path)
    return ProjectLandingStatusOut(
        operation=operation,
        project_id=project.id,
        project_name=project.project_title,
        project_folder_name=project.project_name or project.project_title,
        endpoint=f"/api/v1/projects/{project.id}/landing/status",
        project_landing_path=str(project_path),
        folder_status=folder_status,
        counts=counts,
        warnings=[],
        errors=[],
        next_actions=["Scan landing", "Rebuild manifest", "Dry run ingest", "Run ingest"],
    )


def _find_available_project_folders(root: Path) -> list[str]:
    if not root.exists():
        return []
    folders: list[str] = []
    for child in sorted(root.iterdir()):
        if child.is_dir() and not child.name.startswith(".") and child.name.lower() not in {"__pycache__", "processed", "archive", "rejected"}:
            folders.append(child.name)
    return folders


def _suggest_best_match(requested: str, available: list[str]) -> str | None:
    if not available:
        return None
    requested_lower = requested.lower()
    direct = [f for f in available if f.lower() == requested_lower]
    if direct:
        return direct[0]
    contained = [f for f in available if f.lower() in requested_lower or requested_lower in f.lower()]
    if len(contained) == 1:
        return contained[0]
    from difflib import SequenceMatcher
    scored = [(SequenceMatcher(None, requested_lower, f.lower()).ratio(), f) for f in available]
    scored.sort(key=lambda x: x[0], reverse=True)
    if scored and scored[0][0] > 0.4:
        return scored[0][1]
    return None


def _count_project_files(project_path: Path) -> dict[str, int]:
    def count(folder: str, pattern: str = "*") -> int:
        path = project_path / folder
        if not path.exists():
            return 0
        return len(list(path.rglob(pattern)))

    return {
        "revit_exports": count("Revit Exports", "*.json"),
        "drawings": count("Drawings", "*.pdf"),
        "specifications": count("Specifications", "*.pdf"),
        "owner_requirements": count("Owner Requirements", "*.xlsx") + count("Owner Requirements", "*.xlsm"),
        "docx": count("Specifications", "*.docx") + count("Supporting", "*.docx"),
        "dwfx": count("3D Exports", "*.dwfx"),
        "viewpoints": count("Viewpoints", "*.json"),
        "timeline": count("Timeline", "*.xlsx") + count("Timeline", "*.xlsm"),
        "supporting": count("Supporting"),
    }


def _latest_revit_export(project_path: Path) -> str | None:
    exports_dir = project_path / "Revit Exports"
    if not exports_dir.exists():
        return None
    files = sorted(exports_dir.glob("*.json"), key=lambda p: p.stat().st_mtime, reverse=True)
    return files[0].name if files else None


def _get_or_create_client(db: Session, organization_id: int, code: str, display_name: str) -> Client:
    normalized = "".join(ch if ch.isalnum() else "_" for ch in code.upper()).strip("_") or "OWNER"
    client = db.query(Client).filter(Client.organization_id == organization_id, Client.code == normalized).one_or_none()
    if client is not None:
        return client
    client = Client(organization_id=organization_id, code=normalized, display_name=display_name)
    db.add(client)
    db.flush()
    return client


def _get_or_create_project(
    db: Session,
    *,
    org_id: int,
    name: str,
    project_code: str | None,
    folder_name: str,
    client: Client,
) -> Project:
    project = (
        db.query(Project)
        .filter(Project.organization_id == org_id, func.lower(Project.project_title) == name.lower())
        .one_or_none()
    )
    if project is not None:
        project.client_id = client.id
        project.client_name = client.display_name
        project.project_name = folder_name
        return project
    project = Project(
        organization_id=org_id,
        client_id=client.id,
        project_title=name,
        project_code=project_code or name[:32].upper().replace(" ", "-"),
        project_name=folder_name,
        client_name=client.display_name,
        phase="DD75",
    )
    db.add(project)
    db.flush()
    return project


def _register_project_folder_files(db: Session, project: Project, root: Path, project_path: Path) -> int:
    count = 0
    for path in project_path.rglob("*"):
        if not path.is_file():
            continue
        if path.name == "landing_manifest.json" or path.name.endswith(".meta.json"):
            continue
        rel = str(path.relative_to(root)).replace("\\", "/")
        cls = classify_landing_file(path)
        values = {
            "project_id": project.id,
            "client_id": project.client_id,
            "project_folder": project.project_name or project.project_title,
            "relative_path": rel,
            "file_name": path.name,
            "file_ext": path.suffix.lower(),
            "file_type": cls.type,
            "document_category": _document_category_for_type(cls.type),
            "discipline": infer_discipline_from_path_or_name(path),
            "checksum_sha256": _sha256(path),
            "file_size_bytes": path.stat().st_size,
            "manifest_path": f"{project.project_name or project.project_title}/landing_manifest.json",
            "source_system": "landing",
            "ingestion_status": "indexed",
            "evidence_status": "candidate",
            "metadata_json": {"official_evidence": False},
        }
        register_landing_document(db, values=values, dry_run=False)
        count += 1
    return count


def _document_category_for_type(file_type: str) -> str:
    mapping = {
        "drawing_pdf": "drawing",
        "specification_pdf": "specification",
        "owner_requirements": "owner_requirements",
        "dwfx_export": "dwfx_export",
        "viewpoint_json": "viewpoint",
        "timeline_excel": "timeline",
        "revit_export": "revit_export",
        "pdf_document": "supporting",
    }
    return mapping.get(file_type, "unknown")


def _sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def _iter_landing_projects(root: Path) -> list[Path]:
    if not root.exists():
        return []
    projects: list[Path] = []
    for child in sorted(root.iterdir()):
        if not child.is_dir():
            continue
        if child.name.startswith(".") or child.name in {"__pycache__", "processed", "archive", "rejected"}:
            continue
        projects.append(child)
    return projects


def _find_project_by_folder(db: Session, folder: str) -> Project | None:
    lowered = folder.lower()
    return db.query(Project).filter(func.lower(Project.project_name) == lowered).one_or_none()


def _find_project_by_title(db: Session, title: str) -> Project | None:
    lowered = title.lower()
    return db.query(Project).filter(func.lower(Project.project_title) == lowered).one_or_none()


def _build_landing_project_summary(db: Session, root: Path, project_root: Path) -> LandingProjectSummary:
    summary = _summarize_project_files(root, project_root)
    project = _find_project_by_folder(db, project_root.name) or _find_project_by_title(db, project_root.name)
    suggestion = _infer_client_suggestion(project_root, summary["documents"].get("owner_requirements", []))
    has_manifest = (project_root / "landing_manifest.json").exists()
    warnings: list[str] = []
    errors: list[str] = []
    if summary["file_count"] == 0:
        status = "empty"
        next_action = "Add landing files"
    elif not has_manifest:
        status = "needs_manifest"
        next_action = "Rebuild manifest"
    elif summary["counts"].owner_requirements > 0 and (project is None or project.client_id is None):
        status = "needs_client_binding"
        next_action = "Bind project/client"
    else:
        status = "ready"
        next_action = "Dry-run ingest"
    if summary["counts"].unknown > 0:
        warnings.append("Unknown files detected. They will be skipped during ingest.")
    client = db.get(Client, project.client_id) if project and project.client_id else None
    return LandingProjectSummary(
        project_folder=project_root.name,
        project_name=project.project_title if project else project_root.name,
        project_id=project.id if project else None,
        client_id=project.client_id if project else None,
        client_name=project.client_name if project else None,
        client_code=client.code if client else None,
        manifest_exists=has_manifest,
        manifest_path=f"{project_root.name}/landing_manifest.json" if has_manifest else None,
        counts=summary["counts"],
        documents=summary["documents"],
        client_suggestion=suggestion,
        status=status,
        warnings=warnings,
        errors=errors,
        next_action=next_action,
    )


def _summarize_project_files(root: Path, project_root: Path) -> dict[str, Any]:
    docs: dict[str, list[str]] = {
        "revit_exports": [],
        "drawings": [],
        "owner_requirements": [],
        "specifications": [],
        "sidecars": [],
        "unknown": [],
    }
    counts = LandingProjectCounts()
    for path in project_root.rglob("*"):
        if not path.is_file():
            continue
        if path.name == ".gitkeep" or "__pycache__" in path.parts:
            continue
        rel = str(path.relative_to(root)).replace("\\", "/")
        parent = path.parent.name.lower()
        if path.name == "landing_manifest.json":
            counts.manifests += 1
            continue
        if path.suffix.lower() == ".docx":
            counts.docx += 1
        if path.name.lower().endswith(".meta.json"):
            counts.revit_meta += 1
            docs["sidecars"].append(rel)
            continue
        if "revit exports" in parent and path.suffix.lower() == ".json":
            counts.revit_exports += 1
            docs["revit_exports"].append(rel)
        elif "drawings" in parent and path.suffix.lower() == ".pdf":
            counts.drawings += 1
            docs["drawings"].append(rel)
        elif "owner requirements" in parent and path.suffix.lower() in {".xlsx", ".xlsm"}:
            counts.owner_requirements += 1
            docs["owner_requirements"].append(rel)
        elif "specifications" in parent and path.suffix.lower() in {".pdf", ".docx"}:
            counts.specifications += 1
            docs["specifications"].append(rel)
        else:
            counts.unknown += 1
            docs["unknown"].append(rel)
    return {"counts": counts, "documents": docs, "file_count": sum(len(v) for v in docs.values())}


def _infer_client_suggestion(project_root: Path, owner_requirement_files: list[str]) -> dict[str, Any] | None:
    if not owner_requirement_files:
        return None
    source = owner_requirement_files[0]
    filename = Path(source).stem
    cleaned = re.sub(r"\d{1,2}[.\-_]\d{1,2}[.\-_]\d{2,4}", "", filename).strip(" -_")
    cleaned = re.sub(r"\s+", " ", cleaned).strip()
    if not cleaned:
        return None
    code = re.sub(r"[^A-Z0-9]+", "_", cleaned.upper()).strip("_")
    return {
        "client_name": cleaned.title(),
        "client_code": code,
        "source": source,
        "confidence": "filename",
    }


def _merge_landing_counts(target: LandingProjectCounts, source: LandingProjectCounts) -> None:
    target.revit_exports += source.revit_exports
    target.revit_meta += source.revit_meta
    target.drawings += source.drawings
    target.owner_requirements += source.owner_requirements
    target.specifications += source.specifications
    target.docx += source.docx
    target.manifests += source.manifests
    target.unknown += source.unknown
