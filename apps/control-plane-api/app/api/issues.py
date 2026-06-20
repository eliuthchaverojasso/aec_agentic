"""Issue endpoints (enterprise list, filter, detail, status update)."""

from __future__ import annotations

from typing import Literal

from fastapi import APIRouter, Depends, HTTPException, Query
from datetime import datetime, timezone

from sqlalchemy import func, or_, select
from sqlalchemy.orm import Session

from app.database import get_db
from app.models import Element, Issue, Project, Rule
from app.schemas import IssueDetail, IssueListResponse, IssueOut, IssueUpdate

router = APIRouter(prefix="/api/v1/issues", tags=["issues"])

VALID_STATUSES = {"open", "in_review", "reviewed", "closed", "reopened"}
VALID_SEVERITIES = {"low", "medium", "high", "critical"}


@router.get(
    "",
    response_model=IssueListResponse,
    summary="Enterprise issues list with filters + pagination",
)
def list_issues(
    project_id: int | None = Query(None),
    model_id: int | None = Query(None),
    export_id: int | None = Query(None),
    severity: Literal["low", "medium", "high", "critical"] | None = Query(None),
    status: Literal["open", "in_review", "reviewed", "closed", "reopened"] | None = Query(None),
    rule_code: str | None = Query(None),
    search: str | None = Query(None, min_length=1),
    category: str | None = Query(None, description="Filter by element category (join)"),
    page: int = Query(1, ge=1),
    page_size: int = Query(50, ge=1, le=500),
    db: Session = Depends(get_db),
) -> IssueListResponse:
    filters = []
    if project_id is not None:
        filters.append(Issue.project_id == project_id)
    if model_id is not None:
        filters.append(Issue.model_id == model_id)
    if export_id is not None:
        filters.append(Issue.export_id == export_id)
    if severity is not None:
        filters.append(Issue.severity == severity)
    if status is not None:
        filters.append(Issue.status == status)
    if rule_code is not None:
        filters.append(Issue.rule_code == rule_code)
    if search is not None:
        token = f"%{search}%"
        filters.append(
            or_(
                Issue.message.ilike(token),
                Issue.issue_type.ilike(token),
                Issue.rule_code.ilike(token),
                Issue.element_unique_id.ilike(token),
            )
        )

    count_stmt = select(func.count()).select_from(Issue)
    list_stmt = select(Issue)

    if category is not None:
        count_stmt = count_stmt.join(Element, Element.id == Issue.element_db_id).where(
            Element.category == category
        )
        list_stmt = list_stmt.join(Element, Element.id == Issue.element_db_id).where(
            Element.category == category
        )

    for f in filters:
        count_stmt = count_stmt.where(f)
        list_stmt = list_stmt.where(f)

    total = db.execute(count_stmt).scalar_one()

    list_stmt = list_stmt.order_by(
        Issue.severity.desc(), Issue.created_at.desc()
    ).offset((page - 1) * page_size).limit(page_size)

    items = db.execute(list_stmt).scalars().all()

    return IssueListResponse(
        total=total,
        page=page,
        page_size=page_size,
        items=[IssueOut.model_validate(i) for i in items],
    )


@router.get("/{issue_id}", response_model=IssueDetail, summary="Issue detail for drawer view")
def get_issue(issue_id: int, db: Session = Depends(get_db)) -> IssueDetail:
    issue = db.get(Issue, issue_id)
    if issue is None:
        raise HTTPException(status_code=404, detail="Issue not found")

    project = db.get(Project, issue.project_id)
    element = db.get(Element, issue.element_db_id) if issue.element_db_id else None
    rule = db.get(Rule, issue.rule_id) if issue.rule_id else None

    return IssueDetail(
        id=issue.id,
        organization_id=issue.organization_id,
        project_id=issue.project_id,
        model_id=issue.model_id,
        export_id=issue.export_id,
        element_unique_id=issue.element_unique_id,
        element_db_id=issue.element_db_id,
        rule_id=issue.rule_id,
        rule_code=issue.rule_code,
        issue_type=issue.issue_type,
        severity=issue.severity,
        status=issue.status,
        source=issue.source,
        message=issue.message,
        traceability=issue.traceability,
        assigned_to_user_id=issue.assigned_to_user_id,
        created_at=issue.created_at,
        due_date=issue.due_date,
        reviewed_by_user_id=issue.reviewed_by_user_id,
        reviewed_at=issue.reviewed_at,
        resolution_notes=issue.resolution_notes,
        project_title=project.project_title if project else None,
        project_code=project.project_code if project else None,
        element_category=element.category if element else None,
        element_name=element.name if element else None,
        element_family=element.family if element else None,
        element_type=element.type if element else None,
        element_level=element.level if element else None,
        rule_name=rule.name if rule else None,
        rule_description=rule.description if rule else None,
    )


@router.patch(
    "/{issue_id}",
    response_model=IssueOut,
    summary="Update issue status, assignment, or resolution notes",
)
def update_issue(issue_id: int, payload: IssueUpdate, db: Session = Depends(get_db)) -> IssueOut:
    issue = db.get(Issue, issue_id)
    if issue is None:
        raise HTTPException(status_code=404, detail="Issue not found")

    if payload.status is not None:
        if payload.status not in VALID_STATUSES:
            raise HTTPException(
                status_code=400,
                detail=f"status must be one of {sorted(VALID_STATUSES)}",
            )
        issue.status = payload.status

    if payload.assigned_to_user_id is not None:
        issue.assigned_to_user_id = payload.assigned_to_user_id

    if payload.resolution_notes is not None:
        issue.resolution_notes = payload.resolution_notes
    if payload.reviewed_by_user_id is not None:
        issue.reviewed_by_user_id = payload.reviewed_by_user_id
    if payload.reviewed_by is not None and issue.resolution_notes is None:
        issue.resolution_notes = f"Reviewed by {payload.reviewed_by}"
    if payload.reviewed_at is not None:
        issue.reviewed_at = payload.reviewed_at
    elif payload.status in {"reviewed", "closed"} and issue.reviewed_at is None:
        issue.reviewed_at = datetime.now(timezone.utc)

    db.commit()
    db.refresh(issue)
    return IssueOut.model_validate(issue)
