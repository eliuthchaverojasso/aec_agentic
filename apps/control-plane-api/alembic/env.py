"""Alembic environment for the control-plane-api.

The DSN is resolved from ``app.config.settings`` (DATABASE_URL) so Alembic shares
exactly one database-URL source with the application. A caller may override it by
setting ``sqlalchemy.url`` on the Config (the migration test harness does this to
target a throwaway database).

``app`` is importable because Alembic prepends the directory containing
``alembic.ini`` to ``sys.path`` (``prepend_sys_path = .``); in the Docker image
that is ``/app`` and locally it is ``apps/control-plane-api``.
"""

from __future__ import annotations

from logging.config import fileConfig

from alembic import context
from sqlalchemy import engine_from_config, pool

from app.config import settings
from app.models import Base

config = context.config

if config.config_file_name is not None:
    fileConfig(config.config_file_name)

# Resolve the URL: an explicit override (tests) wins, else the app setting.
resolved_url = config.get_main_option("sqlalchemy.url") or settings.database_url
config.set_main_option("sqlalchemy.url", resolved_url)

# Used by --autogenerate to diff models against the live DB in later revisions.
target_metadata = Base.metadata


def run_migrations_offline() -> None:
    """Emit SQL to stdout without a DB connection (``alembic upgrade --sql``)."""
    context.configure(
        url=resolved_url,
        target_metadata=target_metadata,
        literal_binds=True,
        dialect_opts={"paramstyle": "named"},
        compare_type=True,
    )
    with context.begin_transaction():
        context.run_migrations()


def run_migrations_online() -> None:
    """Run migrations against a live connection."""
    connectable = engine_from_config(
        config.get_section(config.config_ini_section, {}),
        prefix="sqlalchemy.",
        poolclass=pool.NullPool,
    )
    with connectable.connect() as connection:
        context.configure(
            connection=connection,
            target_metadata=target_metadata,
            compare_type=True,
        )
        with context.begin_transaction():
            context.run_migrations()


if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()
