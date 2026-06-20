"""FastAPI application entry point."""

from __future__ import annotations

import logging

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api import landing_admin, auth, clients, compliance, debug, dev, documents, evidence, exports, issues, landing, readiness, seion, viewpoints
from app.api import landing_admin, models as models_router
from app.api import landing_admin, projects
from app.api import requirement_audits
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


# Routers
app.include_router(exports.router)
app.include_router(projects.router)
app.include_router(models_router.router)
app.include_router(issues.router)
app.include_router(clients.router)
app.include_router(documents.router)
app.include_router(evidence.router)
app.include_router(readiness.router)
app.include_router(readiness.actions_router)
app.include_router(landing.router)
app.include_router(landing_admin.router)
app.include_router(landing.projects_router)
app.include_router(seion.router)
app.include_router(auth.router)
app.include_router(dev.router)
app.include_router(compliance.router)
app.include_router(viewpoints.router)
app.include_router(debug.router)
app.include_router(requirement_audits.router)






