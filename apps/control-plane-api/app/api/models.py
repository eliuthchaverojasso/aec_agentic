"""Model detail and model health endpoints."""

from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.database import get_db
from app.models import Element, Export, Issue, Model as ModelRecord
from app.schemas import ModelHealth, ModelOut

router = APIRouter(prefix="/api/v1/models", tags=["models"])


@router.get("/{model_id}", response_model=ModelOut, summary="Model detail")
def get_model(model_id: int, db: Session = Depends(get_db)) -> ModelOut:
    model = db.get(ModelRecord, model_id)
    if model is None:
        raise HTTPException(status_code=404, detail="Model not found")
    return ModelOut.model_validate(model)


@router.get(
    "/{model_id}/health",
    response_model=ModelHealth,
    summary="Model health metrics (categories, levels, issue counts, health score)",
)
def get_model_health(model_id: int, db: Session = Depends(get_db)) -> ModelHealth:
    model = db.get(ModelRecord, model_id)
    if model is None:
        raise HTTPException(status_code=404, detail="Model not found")

    latest_export = db.execute(
        select(Export)
        .where(Export.model_id == model_id, Export.status == "completed")
        .order_by(Export.completed_at.desc())
        .limit(1)
    ).scalar_one_or_none()

    if latest_export is None:
        return ModelHealth(
            model_id=model_id,
            total_elements=0,
            elements_by_category={},
            elements_by_level={},
            open_issues=0,
            critical_issues=0,
            high_issues=0,
            medium_issues=0,
            low_issues=0,
            model_health_score=100.0,
            last_export_id=None,
            last_sync_at=model.last_sync_at,
        )

    total_elements = db.execute(
        select(func.count(Element.id)).where(Element.export_id == latest_export.id)
    ).scalar_one()

    category_rows = db.execute(
        select(Element.category, func.count(Element.id))
        .where(Element.export_id == latest_export.id)
        .group_by(Element.category)
    ).all()
    elements_by_category = {cat or "Unknown": cnt for cat, cnt in category_rows}

    level_rows = db.execute(
        select(Element.level, func.count(Element.id))
        .where(Element.export_id == latest_export.id)
        .group_by(Element.level)
    ).all()
    elements_by_level = {lv or "(none)": cnt for lv, cnt in level_rows}

    sev_rows = db.execute(
        select(Issue.severity, func.count(Issue.id))
        .where(Issue.export_id == latest_export.id, Issue.status == "open")
        .group_by(Issue.severity)
    ).all()
    severity_counts = {sev: cnt for sev, cnt in sev_rows}

    critical = severity_counts.get("critical", 0)
    high = severity_counts.get("high", 0)
    medium = severity_counts.get("medium", 0)
    low = severity_counts.get("low", 0)
    open_issues = critical + high + medium + low

    # Same health score formula as projects -- keep consistent
    if total_elements == 0:
        score = 100.0
    else:
        penalty = critical * 5.0 + high * 2.0 + medium * 0.75 + low * 0.25
        normalized = penalty / max(total_elements / 100.0, 1.0)
        score = round(max(0.0, 100.0 - normalized), 2)

    return ModelHealth(
        model_id=model_id,
        total_elements=total_elements,
        elements_by_category=elements_by_category,
        elements_by_level=elements_by_level,
        open_issues=open_issues,
        critical_issues=critical,
        high_issues=high,
        medium_issues=medium,
        low_issues=low,
        model_health_score=score,
        last_export_id=latest_export.id,
        last_sync_at=latest_export.completed_at,
    )
