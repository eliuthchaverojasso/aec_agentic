"""Tenant & project authorization (Pending Work Register Item 8).

`organization` is the tenant boundary. A user may access a project when they are
a global superuser (role ``admin``/``owner``), a member of the project's
organization (``membership``), or a member of the project itself
(``project_membership``). These helpers are the single place that decision is
made, so routers stay thin and the rule is testable in isolation.

Cross-tenant denial and project-level data filtering both fall out of
``accessible_project_ids`` returning the bounded set of project ids a user may
see (``None`` means "no restriction" — a superuser).
"""

from __future__ import annotations

from fastapi import Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.api.auth import get_current_user
from app.database import get_db
from app.models import AppUser, Membership, Project, ProjectMembership

# Roles that bypass per-resource checks. Kept small and explicit.
SUPERUSER_ROLES = {"admin", "owner"}


def is_superuser(user: AppUser) -> bool:
    return (user.role or "").strip().lower() in SUPERUSER_ROLES


def accessible_organization_ids(db: Session, user: AppUser) -> set[int] | None:
    """Org ids the user belongs to, or ``None`` for a superuser (no restriction)."""
    if is_superuser(user):
        return None
    rows = db.execute(
        select(Membership.organization_id).where(Membership.user_id == user.id)
    ).scalars().all()
    return set(rows)


def accessible_project_ids(db: Session, user: AppUser) -> set[int] | None:
    """Project ids the user may see, or ``None`` for a superuser (no restriction).

    A user sees every project in an organization they belong to, plus any project
    they were granted direct membership on.
    """
    if is_superuser(user):
        return None

    project_ids: set[int] = set(
        db.execute(
            select(ProjectMembership.project_id).where(ProjectMembership.user_id == user.id)
        ).scalars().all()
    )

    org_ids = db.execute(
        select(Membership.organization_id).where(Membership.user_id == user.id)
    ).scalars().all()
    if org_ids:
        project_ids.update(
            db.execute(
                select(Project.id).where(Project.organization_id.in_(org_ids))
            ).scalars().all()
        )
    return project_ids


def user_can_access_project(db: Session, user: AppUser, project: Project) -> bool:
    if is_superuser(user):
        return True
    org_member = db.execute(
        select(Membership.id).where(
            Membership.user_id == user.id,
            Membership.organization_id == project.organization_id,
        )
    ).first()
    if org_member is not None:
        return True
    project_member = db.execute(
        select(ProjectMembership.id).where(
            ProjectMembership.user_id == user.id,
            ProjectMembership.project_id == project.id,
        )
    ).first()
    return project_member is not None


def require_project_access(
    project_id: int,
    db: Session = Depends(get_db),
    user: AppUser = Depends(get_current_user),
) -> Project:
    """FastAPI dependency: load the project and enforce the access boundary.

    404 when the project does not exist; 403 when the caller is not authorized.
    Returns the ``Project`` so handlers can reuse it without a second query.
    """
    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Project not found")
    if not user_can_access_project(db, user, project):
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Not authorized for this project",
        )
    return project


def grant_org_membership(
    db: Session, *, user_id: int, organization_id: int, role: str = "member"
) -> Membership:
    """Idempotently grant a user organization membership."""
    existing = db.execute(
        select(Membership).where(
            Membership.user_id == user_id,
            Membership.organization_id == organization_id,
        )
    ).scalar_one_or_none()
    if existing is not None:
        return existing
    membership = Membership(user_id=user_id, organization_id=organization_id, role=role)
    db.add(membership)
    db.flush()
    return membership


def grant_project_membership(
    db: Session, *, user_id: int, project_id: int, role: str = "member"
) -> ProjectMembership:
    """Idempotently grant a user project membership."""
    existing = db.execute(
        select(ProjectMembership).where(
            ProjectMembership.user_id == user_id,
            ProjectMembership.project_id == project_id,
        )
    ).scalar_one_or_none()
    if existing is not None:
        return existing
    membership = ProjectMembership(user_id=user_id, project_id=project_id, role=role)
    db.add(membership)
    db.flush()
    return membership
