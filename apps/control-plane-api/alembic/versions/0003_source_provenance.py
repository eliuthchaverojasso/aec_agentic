"""add source provenance columns to requirement and requirement_source_file

Revision ID: 0003_source_provenance
Revises: 0002_membership
Create Date: 2026-06-22

Adds columns for:
- requirement: source_sheet, source_row, source_cell_range,
  original_columns_json, parser_version, import_id
- requirement_source_file: sheet_names, parser_version

These enable full traceability from each requirement back to its
source file, sheet, and row (AGT-016).
"""

from __future__ import annotations

from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects.postgresql import JSONB

revision: str = "0003_source_provenance"
down_revision: Union[str, None] = "0002_membership"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # --- requirement_source_file ---
    op.add_column("requirement_source_file", sa.Column("sheet_names", sa.Text, nullable=True))
    op.add_column("requirement_source_file", sa.Column("parser_version", sa.String(20), nullable=True))

    # --- requirement ---
    op.add_column("requirement", sa.Column("source_sheet", sa.String(255), nullable=True))
    op.add_column("requirement", sa.Column("source_row", sa.Integer, nullable=True))
    op.add_column("requirement", sa.Column("source_cell_range", sa.String(50), nullable=True))
    op.add_column("requirement", sa.Column("original_columns_json", JSONB, nullable=True))
    op.add_column("requirement", sa.Column("parser_version", sa.String(20), nullable=True))
    op.add_column("requirement", sa.Column("import_id", sa.String(36), nullable=True))


def downgrade() -> None:
    op.drop_column("requirement", "import_id")
    op.drop_column("requirement", "parser_version")
    op.drop_column("requirement", "original_columns_json")
    op.drop_column("requirement", "source_cell_range")
    op.drop_column("requirement", "source_row")
    op.drop_column("requirement", "source_sheet")
    op.drop_column("requirement_source_file", "parser_version")
    op.drop_column("requirement_source_file", "sheet_names")
