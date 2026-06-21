"""Migration tests — Alembic is the single schema-authoring mechanism (Item 13).

Each test provisions a throwaway PostgreSQL database (the configured server must
permit CREATE DATABASE; the Docker `ema` superuser does) and proves:

  * ``alembic upgrade head`` builds the full schema — every ORM table, the two
    tables that used to be created lazily at request time
    (``pipeline_operation_log``/``seion_prediction``), the baseline seed, and
    the constraints the Pending Work Register called out as drift-prone;
  * ``alembic downgrade base`` removes the schema again (rollback works);
  * the migrated schema matches the ORM models at table + column granularity
    (a regression guard against migration/model drift).

Auto-marked ``integration`` by the repo-root ``conftest.py``. ``alembic`` is
guarded with ``importorskip`` so a fast (no-DB) collection never breaks if the
dependency is not yet installed.
"""

from __future__ import annotations

import contextlib
from pathlib import Path
from uuid import uuid4

import pytest

pytest.importorskip("alembic")

from alembic import command  # noqa: E402
from alembic.config import Config  # noqa: E402
from alembic.script import ScriptDirectory  # noqa: E402
from sqlalchemy import create_engine  # noqa: E402
from sqlalchemy.engine import make_url  # noqa: E402

from app.config import settings  # noqa: E402
from app.models import Base  # noqa: E402

_API_ROOT = Path(__file__).resolve().parents[1]
_ALEMBIC_INI = _API_ROOT / "alembic.ini"
_ALEMBIC_DIR = _API_ROOT / "alembic"

# Tables that previously existed only because the app created them on first use.
_FORMERLY_LAZY = {"pipeline_operation_log", "seion_prediction"}


def _alembic_config(url: str) -> Config:
    cfg = Config(str(_ALEMBIC_INI))
    # Absolute script_location so the config works regardless of cwd.
    cfg.set_main_option("script_location", str(_ALEMBIC_DIR))
    cfg.set_main_option("sqlalchemy.url", url)
    return cfg


@contextlib.contextmanager
def _temporary_database():
    base = make_url(settings.database_url)
    if not base.get_backend_name().startswith("postgresql"):
        pytest.skip("migration tests require PostgreSQL")
    name = f"mig_test_{uuid4().hex[:12]}"
    admin = create_engine(base.set(database="postgres"), isolation_level="AUTOCOMMIT")
    try:
        with admin.connect() as conn:
            conn.exec_driver_sql(f'CREATE DATABASE "{name}"')
        try:
            yield str(base.set(database=name))
        finally:
            with admin.connect() as conn:
                conn.exec_driver_sql(
                    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity "
                    f"WHERE datname = '{name}' AND pid <> pg_backend_pid()"
                )
                conn.exec_driver_sql(f'DROP DATABASE IF EXISTS "{name}"')
    finally:
        admin.dispose()


@pytest.fixture
def temp_db_url():
    with _temporary_database() as url:
        yield url


def _public_tables(engine) -> set[str]:
    with engine.connect() as conn:
        rows = conn.exec_driver_sql(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"
        ).fetchall()
    return {row[0] for row in rows}


def _columns_by_table(engine) -> dict[str, frozenset[str]]:
    with engine.connect() as conn:
        rows = conn.exec_driver_sql(
            "SELECT table_name, column_name FROM information_schema.columns "
            "WHERE table_schema = 'public'"
        ).fetchall()
    grouped: dict[str, set[str]] = {}
    for table, column in rows:
        grouped.setdefault(table, set()).add(column)
    return {table: frozenset(cols) for table, cols in grouped.items()}


def test_upgrade_head_creates_full_schema(temp_db_url):
    command.upgrade(_alembic_config(temp_db_url), "head")
    engine = create_engine(temp_db_url)
    try:
        tables = _public_tables(engine)

        missing = set(Base.metadata.tables) - tables
        assert not missing, f"migration head is missing ORM tables: {sorted(missing)}"
        assert "alembic_version" in tables
        assert _FORMERLY_LAZY <= tables

        with engine.connect() as conn:
            assert conn.exec_driver_sql("SELECT count(*) FROM organization").scalar() == 1
            assert conn.exec_driver_sql("SELECT count(*) FROM client").scalar() == 3
            assert conn.exec_driver_sql("SELECT count(*) FROM rule").scalar() == 4
            # Unique index the register flagged as ORM/SQL drift — must be present.
            assert (
                conn.exec_driver_sql(
                    "SELECT 1 FROM pg_indexes "
                    "WHERE indexname = 'uq_req_evidence_project_requirement_source'"
                ).first()
                is not None
            )
            # app_user updated_at trigger must be present.
            assert (
                conn.exec_driver_sql(
                    "SELECT 1 FROM pg_trigger WHERE tgname = 'trg_app_user_set_updated_at'"
                ).first()
                is not None
            )
            revision = conn.exec_driver_sql("SELECT version_num FROM alembic_version").scalar()

        head = ScriptDirectory.from_config(_alembic_config(temp_db_url)).get_current_head()
        assert revision == head
    finally:
        engine.dispose()


def test_downgrade_base_removes_schema(temp_db_url):
    cfg = _alembic_config(temp_db_url)
    command.upgrade(cfg, "head")
    command.downgrade(cfg, "base")
    engine = create_engine(temp_db_url)
    try:
        remaining = set(Base.metadata.tables) & _public_tables(engine)
        assert not remaining, f"tables left after downgrade base: {sorted(remaining)}"
    finally:
        engine.dispose()


def test_head_matches_orm_models():
    with _temporary_database() as url_alembic, _temporary_database() as url_orm:
        command.upgrade(_alembic_config(url_alembic), "head")
        eng_alembic = create_engine(url_alembic)
        eng_orm = create_engine(url_orm)
        try:
            Base.metadata.create_all(eng_orm)
            cols_alembic = _columns_by_table(eng_alembic)
            cols_orm = _columns_by_table(eng_orm)
            cols_alembic.pop("alembic_version", None)

            assert set(cols_alembic) == set(cols_orm), (
                "table set differs between alembic head and ORM models: "
                f"only-alembic={sorted(set(cols_alembic) - set(cols_orm))}, "
                f"only-orm={sorted(set(cols_orm) - set(cols_alembic))}"
            )
            for table in sorted(cols_orm):
                assert cols_alembic[table] == cols_orm[table], (
                    f"column mismatch in {table}: "
                    f"alembic-only={sorted(cols_alembic[table] - cols_orm[table])}, "
                    f"orm-only={sorted(cols_orm[table] - cols_alembic[table])}"
                )
        finally:
            eng_alembic.dispose()
            eng_orm.dispose()
