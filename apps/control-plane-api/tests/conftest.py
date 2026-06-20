"""Fixtures for the migrated EMA (control-plane-api) integration suite.

These tests require a live PostgreSQL database (or a SQLite ``get_db`` override)
and are marked ``integration`` by the repo-root ``conftest.py``.
"""

from __future__ import annotations

import pytest


@pytest.fixture(autouse=True)
def _inject_authenticated_user(request):
    """Bypass authentication for the migrated EMA suite by default.

    ``app/main.py`` now requires an authenticated principal on every business
    router. This suite predates that boundary, so we override
    ``get_current_user`` with a synthetic user. It is re-applied per test (and
    popped on teardown) so a test's own ``app.dependency_overrides.clear()``
    does not permanently strip it. Opt out with ``@pytest.mark.noauth`` to
    exercise the real dependency (see ``test_api_auth.py``).

    Imports are lazy so the fast (non-integration) suite never needs ``app`` on
    the path just to collect this conftest.
    """
    if request.node.get_closest_marker("noauth"):
        yield
        return

    from app.api.auth import get_current_user
    from app.main import app
    from app.models import AppUser

    app.dependency_overrides[get_current_user] = lambda: AppUser(
        id=1,
        name="Test User",
        email="test-user@example.com",
        role="admin",
        auth_provider="local",
        is_active=True,
        is_locked=False,
        failed_login_attempts=0,
        must_change_password=False,
    )
    try:
        yield
    finally:
        app.dependency_overrides.pop(get_current_user, None)


@pytest.fixture(scope="session", autouse=True)
def _dispose_engine_at_session_end():
    """Dispose the module-level SQLAlchemy engine pool when the session ends.

    Without this, the never-disposed connection pool in ``app.database`` keeps
    the process alive after pytest reports completion, so a plain ``pytest``
    run appears to hang until killed (P1-2). Import lazily so the fixture does
    not require ``app`` on the path unless integration tests actually run.
    """
    yield
    try:
        from app.database import engine

        engine.dispose()
    except Exception:  # pragma: no cover - teardown must never fail the run
        pass
