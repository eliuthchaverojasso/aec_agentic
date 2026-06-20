"""Controlled AI query endpoint.

Monday stage: keyword-matched templates that return structured answers with
supporting tables. No LLM dependency. This satisfies the Dashboard Guidelines
section 8.11 response format (answer, table, source, filters, timestamp) and
keeps results evidence-based and traceable.

Later stages (week 3+) can swap the matcher for LLM-generated SQL against a
restricted schema, and eventually GraphRAG across Neo4j + Qdrant.
"""

from __future__ import annotations

import re
from datetime import datetime, timezone
from typing import Any, Callable

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.database import get_db
from app.models import Element, Export, Issue, Project
from app.schemas import AIQueryRequest, AIQueryResponse

router = APIRouter(prefix="/api/v1/ai", tags=["ai-query"])


def _resolve_project(db: Session, query: str, project_id: int | None) -> Project | None:
    """Find a project either by explicit id or by fuzzy match on name/code in the query."""
    if project_id is not None:
        return db.get(Project, project_id)

    q_lower = query.lower()
    projects = db.execute(select(Project)).scalars().all()
    # Exact project_title or project_name substring
    for project in projects:
        candidates = [project.project_title, project.project_name, project.project_code]
        for cand in candidates:
            if cand and cand.lower() in q_lower:
                return project
    # Try tokens of the project_name
    for project in projects:
        if project.project_name:
            first_token = project.project_name.split()[0].lower()
            if len(first_token) > 3 and first_token in q_lower:
                return project
    return None


# ---------------------------------------------------------------------------
# Query templates
# ---------------------------------------------------------------------------


def _tpl_critical_issues(db: Session, query: str, project: Project | None) -> AIQueryResponse:
    filters: dict[str, Any] = {"severity": "critical", "status": "open"}
    stmt = select(Issue).where(Issue.severity == "critical", Issue.status == "open")
    if project is not None:
        stmt = stmt.where(Issue.project_id == project.id)
        filters["project_id"] = project.id

    issues = db.execute(stmt.limit(50)).scalars().all()
    count = len(issues)
    scope = f" in project '{project.project_name or project.project_title}'" if project else " across all projects"

    return AIQueryResponse(
        query=query,
        answer=f"There are {count} open critical issues{scope}.",
        matched_template="critical_issues",
        table=[
            {
                "issue_id": i.id,
                "rule_code": i.rule_code,
                "severity": i.severity,
                "status": i.status,
                "message": i.message,
            }
            for i in issues
        ],
        filters=filters,
        timestamp=datetime.now(timezone.utc),
    )


def _tpl_unconnected_fixtures(db: Session, query: str, project: Project | None) -> AIQueryResponse:
    filters: dict[str, Any] = {"rule_code": "R002", "status": "open"}
    stmt = select(Issue).where(Issue.rule_code == "R002", Issue.status == "open")
    count_stmt = select(func.count()).select_from(Issue).where(
        Issue.rule_code == "R002", Issue.status == "open"
    )
    if project is not None:
        stmt = stmt.where(Issue.project_id == project.id)
        count_stmt = count_stmt.where(Issue.project_id == project.id)
        filters["project_id"] = project.id

    rows = db.execute(stmt.limit(100)).scalars().all()
    count = db.execute(count_stmt).scalar_one()

    scope = f" in project '{project.project_name or project.project_title}'" if project else " across all projects"

    return AIQueryResponse(
        query=query,
        answer=f"{count} fixtures are currently unconnected (no Panel assigned){scope}.",
        matched_template="unconnected_fixtures",
        table=[
            {
                "issue_id": r.id,
                "element_unique_id": r.element_unique_id,
                "message": r.message,
            }
            for r in rows
        ],
        filters=filters,
        timestamp=datetime.now(timezone.utc),
    )


def _tpl_missing_circuits(db: Session, query: str, project: Project | None) -> AIQueryResponse:
    filters: dict[str, Any] = {"rule_code": "R003", "status": "open"}
    stmt = select(Issue).where(Issue.rule_code == "R003", Issue.status == "open")
    if project is not None:
        stmt = stmt.where(Issue.project_id == project.id)
        filters["project_id"] = project.id

    rows = db.execute(stmt.limit(100)).scalars().all()
    count_stmt = select(func.count()).select_from(Issue).where(
        Issue.rule_code == "R003", Issue.status == "open"
    )
    if project is not None:
        count_stmt = count_stmt.where(Issue.project_id == project.id)
    count = db.execute(count_stmt).scalar_one()

    scope = f" in project '{project.project_name or project.project_title}'" if project else " across all projects"

    return AIQueryResponse(
        query=query,
        answer=f"{count} fixtures have a Panel assigned but no Circuit Number{scope}.",
        matched_template="missing_circuits",
        table=[{"issue_id": r.id, "message": r.message} for r in rows],
        filters=filters,
        timestamp=datetime.now(timezone.utc),
    )


def _tpl_last_sync(db: Session, query: str, project: Project | None) -> AIQueryResponse:
    stmt = (
        select(Export.id, Export.file_name, Export.completed_at, Export.element_count, Export.export_type)
        .where(Export.status == "completed")
        .order_by(Export.completed_at.desc())
        .limit(10)
    )
    if project is not None:
        stmt = stmt.where(Export.project_id == project.id)
    rows = db.execute(stmt).all()

    if not rows:
        return AIQueryResponse(
            query=query,
            answer="No completed exports found.",
            matched_template="last_sync",
            table=[],
            filters={"project_id": project.id} if project else {},
            timestamp=datetime.now(timezone.utc),
        )

    latest = rows[0]
    return AIQueryResponse(
        query=query,
        answer=(
            f"Latest completed sync: {latest.file_name} "
            f"({latest.element_count} elements, {latest.export_type}) "
            f"at {latest.completed_at.isoformat()}."
        ),
        matched_template="last_sync",
        table=[
            {
                "export_id": r.id,
                "file_name": r.file_name,
                "completed_at": r.completed_at.isoformat() if r.completed_at else None,
                "element_count": r.element_count,
                "export_type": r.export_type,
            }
            for r in rows
        ],
        filters={"project_id": project.id} if project else {},
        timestamp=datetime.now(timezone.utc),
    )


def _tpl_issues_by_discipline(db: Session, query: str, project: Project | None) -> AIQueryResponse:
    stmt = (
        select(Element.category, func.count(Issue.id).label("cnt"))
        .join(Element, Element.id == Issue.element_db_id)
        .where(Issue.status == "open")
        .group_by(Element.category)
    )
    if project is not None:
        stmt = stmt.where(Issue.project_id == project.id)
    rows = db.execute(stmt).all()

    total = sum(r.cnt for r in rows)
    scope = f" in project '{project.project_name or project.project_title}'" if project else ""

    top = max(rows, key=lambda r: r.cnt) if rows else None
    top_str = f"Top: {top.category} with {top.cnt} open issues." if top else "No open issues."

    return AIQueryResponse(
        query=query,
        answer=f"{total} open issues{scope}. {top_str}",
        matched_template="issues_by_discipline",
        table=[{"category": r.category or "Unknown", "open_issues": r.cnt} for r in rows],
        filters={"project_id": project.id} if project else {},
        timestamp=datetime.now(timezone.utc),
    )


def _tpl_element_counts(db: Session, query: str, project: Project | None) -> AIQueryResponse:
    stmt = (
        select(Element.category, func.count(Element.id).label("cnt"))
        .join(Export, Export.id == Element.export_id)
        .where(Export.status == "completed")
        .group_by(Element.category)
    )
    if project is not None:
        stmt = stmt.where(Element.model_id.in_(
            select(Export.model_id).where(Export.project_id == project.id)
        ))
    rows = db.execute(stmt).all()

    total = sum(r.cnt for r in rows)
    scope = f" in project '{project.project_name or project.project_title}'" if project else ""

    return AIQueryResponse(
        query=query,
        answer=f"Total {total} elements{scope} across {len(rows)} categories.",
        matched_template="element_counts",
        table=[{"category": r.category or "Unknown", "count": r.cnt} for r in rows],
        filters={"project_id": project.id} if project else {},
        timestamp=datetime.now(timezone.utc),
    )


# ---------------------------------------------------------------------------
# Matcher
# ---------------------------------------------------------------------------


Template = Callable[[Session, str, Project | None], AIQueryResponse]


# Patterns are ordered -- first match wins.
TEMPLATES: list[tuple[re.Pattern[str], Template, str]] = [
    (
        re.compile(r"\bcritical\b.*\b(issue|problem|finding)s?\b", re.IGNORECASE),
        _tpl_critical_issues,
        "critical_issues",
    ),
    (
        re.compile(r"\b(unconnected|not\s+connected|no\s+panel|without\s+panel)\b", re.IGNORECASE),
        _tpl_unconnected_fixtures,
        "unconnected_fixtures",
    ),
    (
        re.compile(r"\b(missing|no|without)\s+circuit", re.IGNORECASE),
        _tpl_missing_circuits,
        "missing_circuits",
    ),
    (
        re.compile(r"\b(last\s+sync|latest\s+sync|most\s+recent\s+(?:sync|export))\b", re.IGNORECASE),
        _tpl_last_sync,
        "last_sync",
    ),
    (
        re.compile(r"\b(issue|problem|finding)s?\b.*\b(by|per)\b.*\b(discipline|category|type)\b", re.IGNORECASE),
        _tpl_issues_by_discipline,
        "issues_by_discipline",
    ),
    (
        re.compile(r"\b(element|fixture|equipment)\s+count|\bhow\s+many\s+element", re.IGNORECASE),
        _tpl_element_counts,
        "element_counts",
    ),
]


SUGGESTIONS = [
    "How many open critical issues in project Rochell?",
    "Show unconnected fixtures in the latest export.",
    "Which fixtures are missing a circuit?",
    "When was the last sync?",
    "Open issues by discipline.",
    "How many elements in project Rochell?",
]


@router.post("/query", response_model=AIQueryResponse, summary="Controlled natural-language query")
def ai_query(payload: AIQueryRequest, db: Session = Depends(get_db)) -> AIQueryResponse:
    query = payload.query.strip()
    if not query:
        raise HTTPException(status_code=400, detail="query is empty")

    project = _resolve_project(db, query, payload.project_id)

    for pattern, handler, _name in TEMPLATES:
        if pattern.search(query):
            return handler(db, query, project)

    return AIQueryResponse(
        query=query,
        answer=(
            "I could not match that question to a known template yet. "
            f"Try one of: {'; '.join(SUGGESTIONS)}"
        ),
        matched_template=None,
        table=None,
        filters={"project_id": project.id} if project else {},
        timestamp=datetime.now(timezone.utc),
    )


@router.get("/suggestions", response_model=list[str], summary="Suggested queries for the AI Query screen")
def list_suggestions() -> list[str]:
    return SUGGESTIONS
