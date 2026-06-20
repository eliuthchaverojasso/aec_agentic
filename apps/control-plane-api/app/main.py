"""FastAPI application entry point."""

from __future__ import annotations

import logging

from fastapi import Depends, FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api import (
    auth,
    clients,
    compliance,
    debug,
    dev,
    documents,
    evidence,
    exports,
    issues,
    landing,
    landing_admin,
    models as models_router,
    projects,
    readiness,
    requirement_audits,
    seion,
    viewpoints,
)
from app.config import settings
from app.database import engine, SessionLocal
from app.services.operation_log_service import finish_operation_failure, finish_operation_success, start_operation

logging.basicConfig(
    level=settings.log_level,
    format="%(asctime)s %(levelname)s %(name)s: %(message)s",
)

app = FastAPI(
    title=settings.api_title,
    version=settings.api_version,
    description=settings.api_description,
)

cors_origins = [
    origin.strip()
    for origin in settings.cors_origins.split(",")
    if origin.strip()
]

app.add_middleware(
    CORSMiddleware,
    allow_origins=cors_origins,
    allow_credentials=False,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health", tags=["system"], summary="Liveness and DB connectivity check")
def health() -> dict[str, object]:
    db = SessionLocal()
    op = start_operation(db, operation_type="health_check", operation_label="Health check", endpoint="/health", method="GET")
    try:
        with engine.connect() as conn:
            conn.exec_driver_sql("SELECT 1")
        db_status = "ok"
        finish_operation_success(db, op, response_summary={"database": db_status, "version": settings.api_version})
    except Exception as exc:  # noqa: BLE001
        db_status = f"error: {exc}"
        finish_operation_failure(db, op, errors=[db_status], response_summary={"database": db_status})
    finally:
        db.close()

    return {
        "status": "ok" if db_status == "ok" else "degraded",
        "database": db_status,
        "version": settings.api_version,
        "product_version": settings.ema_ai_product_version,
        "git_sha": settings.ema_ai_git_sha,
        "install_root": settings.ema_ai_install_root,
        "database_status": db_status,
    }


@app.get("/", tags=["system"], summary="API root")
def root() -> dict[str, str]:
    return {
        "name": settings.api_title,
        "version": settings.api_version,
        "docs": "/docs",
        "redoc": "/redoc",
        "openapi": "/openapi.json",
    }


# Routers.
# Public surface = unauthenticated system endpoints (/ and /health, defined on
# the app above) plus the auth router (login + gated register; /profile enforces
# its own dependency). Every other router requires an authenticated principal.
auth_required = [Depends(auth.get_current_user)]

app.include_router(auth.router)

for business_router in (
    exports.router,
    projects.router,
    models_router.router,
    issues.router,
    clients.router,
    documents.router,
    evidence.router,
    readiness.router,
    readiness.actions_router,
    landing.router,
    landing_admin.router,
    landing.projects_router,
    seion.router,
    dev.router,
    compliance.router,
    viewpoints.router,
    debug.router,
    requirement_audits.router,
):
    app.include_router(business_router, dependencies=auth_required)






