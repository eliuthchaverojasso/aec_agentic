"""Root pytest configuration.

Tests under ``apps/control-plane-api/tests`` are the migrated EMA suite and
require a live PostgreSQL database. Rather than annotate ~37 files by hand, we
auto-apply the ``integration`` marker to every item collected from that tree.

Selection policy (see ``pyproject.toml`` -> ``addopts``):
- default run deselects ``integration`` -> the fast suite is green with no Docker.
- CI / ``scripts/test.ps1 -Integration`` runs ``-m integration`` against Postgres.

This is explicit, reported test *selection* (pytest prints "N deselected"),
not silent skipping of a missing dependency.
"""

from __future__ import annotations

import pathlib

_INTEGRATION_ROOT = (
    pathlib.Path(__file__).parent / "apps" / "control-plane-api" / "tests"
).resolve()


def pytest_collection_modifyitems(config, items):  # noqa: ARG001 (pytest hook signature)
    for item in items:
        try:
            item_path = pathlib.Path(str(item.path)).resolve()
        except Exception:  # pragma: no cover - defensive; never block collection
            continue
        if _INTEGRATION_ROOT == item_path or _INTEGRATION_ROOT in item_path.parents:
            item.add_marker("integration")
