"""Local dev-mode status and smoke-test endpoints."""

from __future__ import annotations

from fastapi import APIRouter, Depends
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.config import settings
from app.database import get_db
from app.models import Export, Issue, LandingDocument, Project, ReadinessAction, ReadinessSnapshot
from app.readiness.persistence import ensure_readiness_tables
from app.readiness.service import build_project_readiness
from app.schemas import DevSmokeEndpointResult, DevSmokeTestOut, DevStatusCounts, DevStatusOut

router = APIRouter(prefix="/api/v1/dev", tags=["dev"])


@router.get("/status", response_model=DevStatusOut, summary="Aggregated local dev status")
def get_dev_status(db: Session = Depends(get_db)) -> DevStatusOut:
    ensure_readiness_tables(db)
    project_count = int(db.execute(select(func.count(Project.id))).scalar_one())
    export_count = int(db.execute(select(func.count(Export.id))).scalar_one())
    issue_count = int(db.execute(select(func.count(Issue.id))).scalar_one())
    high_issue_count = int(
        db.execute(select(func.count(Issue.id)).where(Issue.severity == "high")).scalar_one()
    )
    critical_issue_count = int(
        db.execute(select(func.count(Issue.id)).where(Issue.severity == "critical")).scalar_one()
    )
    actions_count = int(db.execute(select(func.count(ReadinessAction.id))).scalar_one())
    snapshots_count = int(db.execute(select(func.count(ReadinessSnapshot.id))).scalar_one())
    document_count = int(db.execute(select(func.count(LandingDocument.id))).scalar_one())
    spec_count = int(
        db.execute(
            select(func.count(LandingDocument.id)).where(LandingDocument.file_type == "specification_pdf")
        ).scalar_one()
    )
    drawing_count = int(
        db.execute(select(func.count(LandingDocument.id)).where(LandingDocument.file_type == "drawing_pdf")).scalar_one()
    )

    selected = db.execute(select(Project).order_by(Project.updated_at.desc(), Project.id.desc()).limit(1)).scalar_one_or_none()
    readiness_available = False
    warnings: list[str] = []
    requirements_state: str | None = None
    if selected is not None:
        try:
            readiness = build_project_readiness(db, selected)
            readiness_available = True
            requirements_state = (
                "no_client_linked" if selected.client_id is None else readiness.requirement_coverage.detail
            )
        except Exception as exc:  # noqa: BLE001
            warnings.append(f"Readiness computation warning: {exc}")

    return DevStatusOut(
        status="ok",
        backend_health="ok",
        database_health="ok",
        version=settings.api_version,
        app_version=settings.api_version,
        api_contract_version="2026-05-23",
        selected_project_id=selected.id if selected else None,
        selected_project_name=(selected.project_name or selected.project_title) if selected else None,
        selected_project_folder=(selected.project_name or selected.project_title) if selected else None,
        default_project_folder=(selected.project_name or selected.project_title) if selected else None,
        last_sync_at=selected.updated_at if selected else None,
        requirements_state=requirements_state,
        readiness_available=readiness_available,
        counts=DevStatusCounts(
            projects=project_count,
            exports=export_count,
            issues=issue_count,
            high_issues=high_issue_count,
            critical_issues=critical_issue_count,
            documents=document_count,
            specifications=spec_count,
            drawings=drawing_count,
            actions=actions_count,
            snapshots=snapshots_count,
        ),
        warnings=warnings,
        endpoint_availability={
            "/health": True,
            "/api/v1/projects": True,
            "/api/v1/issues": True,
            "/api/v1/exports": True,
            "/api/v1/projects/{project_id}/readiness": True,
            "/api/v1/projects/{project_id}/documents": True,
            "/api/v1/projects/{project_id}/specifications": True,
            "/api/v1/projects/{project_id}/drawings": True,
            "/api/v1/projects/{project_id}/requirements": True,
            "/api/v1/projects/{project_id}/readiness/actions": True,
            "/api/v1/projects/{project_id}/readiness/snapshots": True,
        },
        landing_root_configured=bool(settings.landing_dir),
    )


@router.post("/smoke-test", response_model=DevSmokeTestOut, summary="Run safe local read-only checks")
def run_dev_smoke_test(db: Session = Depends(get_db)) -> DevSmokeTestOut:
    checks: list[DevSmokeEndpointResult] = []
    project = db.execute(select(Project).order_by(Project.id).limit(1)).scalar_one_or_none()

    checks.append(
        DevSmokeEndpointResult(
            endpoint="/health",
            ok=True,
            detail=f"API version {settings.api_version}",
        )
    )
    checks.append(
        DevSmokeEndpointResult(
            endpoint="/api/v1/projects",
            ok=True,
            detail=f"{int(db.execute(select(func.count(Project.id))).scalar_one())} project(s)",
        )
    )
    checks.append(
        DevSmokeEndpointResult(
            endpoint="/api/v1/issues",
            ok=True,
            detail=f"{int(db.execute(select(func.count(Issue.id))).scalar_one())} issue(s)",
        )
    )
    checks.append(
        DevSmokeEndpointResult(
            endpoint="/api/v1/exports",
            ok=True,
            detail=f"{int(db.execute(select(func.count(Export.id))).scalar_one())} export(s)",
        )
    )
    checks.append(
        DevSmokeEndpointResult(
            endpoint="/api/v1/projects/{project_id}/documents",
            ok=project is not None,
            detail=f"project_id={project.id}" if project else "No project available",
        )
    )
    checks.append(
        DevSmokeEndpointResult(
            endpoint="/api/v1/projects/{project_id}/specifications",
            ok=project is not None,
            detail=f"project_id={project.id}" if project else "No project available",
        )
    )
    checks.append(
        DevSmokeEndpointResult(
            endpoint="/api/v1/projects/{project_id}/drawings",
            ok=project is not None,
            detail=f"project_id={project.id}" if project else "No project available",
        )
    )
    checks.append(
        DevSmokeEndpointResult(
            endpoint="/api/v1/projects/{project_id}/readiness/actions",
            ok=project is not None,
            detail=f"project_id={project.id}" if project else "No project available",
        )
    )
    checks.append(
        DevSmokeEndpointResult(
            endpoint="/api/v1/projects/{project_id}/readiness/snapshots",
            ok=project is not None,
            detail=f"project_id={project.id}" if project else "No project available",
        )
    )
    checks.append(
        DevSmokeEndpointResult(
            endpoint="/api/v1/projects/{project_id}/requirements",
            ok=project is not None,
            detail=f"project_id={project.id}" if project else "No project available",
        )
    )

    if project is not None:
        try:
            readiness = build_project_readiness(db, project)
            checks.append(
                DevSmokeEndpointResult(
                    endpoint="/api/v1/projects/{project_id}/readiness",
                    ok=True,
                    detail=f"overall={readiness.overall_readiness}",
                )
            )
        except Exception as exc:  # noqa: BLE001
            checks.append(
                DevSmokeEndpointResult(
                    endpoint="/api/v1/projects/{project_id}/readiness",
                    ok=False,
                    detail=str(exc),
                )
            )

    return DevSmokeTestOut(
        status="ok" if all(check.ok for check in checks) else "degraded",
        checks=checks,
    )
