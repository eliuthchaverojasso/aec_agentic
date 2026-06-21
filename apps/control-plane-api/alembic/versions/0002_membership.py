"""tenant & project authorization: membership + project_membership

Revision ID: 0002_membership
Revises: 0001_baseline
Create Date: 2026-06-21

Adds the authorization model for Pending Work Register Item 8. ``organization``
is the tenant boundary; ``membership`` grants org-wide access and
``project_membership`` grants single-project access. See app/authz.py.
"""

from __future__ import annotations

from typing import Sequence, Union

from alembic import op

revision: str = "0002_membership"
down_revision: Union[str, None] = "0001_baseline"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


_UPGRADE = [
    """
    CREATE TABLE membership (
        id SERIAL PRIMARY KEY,
        user_id INT NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
        organization_id INT NOT NULL REFERENCES organization(id) ON DELETE CASCADE,
        role VARCHAR(50) NOT NULL DEFAULT 'member',
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        CONSTRAINT uq_membership_user_org UNIQUE (user_id, organization_id),
        CONSTRAINT chk_membership_role CHECK (role IN ('owner','admin','member','viewer'))
    )
    """,
    "CREATE INDEX idx_membership_user ON membership(user_id)",
    "CREATE INDEX idx_membership_org ON membership(organization_id)",
    """
    CREATE TABLE project_membership (
        id SERIAL PRIMARY KEY,
        user_id INT NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
        project_id INT NOT NULL REFERENCES project(id) ON DELETE CASCADE,
        role VARCHAR(50) NOT NULL DEFAULT 'member',
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        CONSTRAINT uq_project_membership_user_project UNIQUE (user_id, project_id),
        CONSTRAINT chk_project_membership_role CHECK (role IN ('owner','admin','member','viewer'))
    )
    """,
    "CREATE INDEX idx_project_membership_user ON project_membership(user_id)",
    "CREATE INDEX idx_project_membership_project ON project_membership(project_id)",
]


def upgrade() -> None:
    bind = op.get_bind()
    for statement in _UPGRADE:
        bind.exec_driver_sql(statement)


def downgrade() -> None:
    bind = op.get_bind()
    bind.exec_driver_sql("DROP TABLE IF EXISTS project_membership CASCADE")
    bind.exec_driver_sql("DROP TABLE IF EXISTS membership CASCADE")
