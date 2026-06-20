# MIGRATION_INVENTORY.md

**Generated:** 2026-06-20 (Phase 0 deliverable) · **Method:** `git ls-files` aggregation + direct inspection
**Companion docs:** [`CURRENT_STATE.md`](CURRENT_STATE.md) · [`DATA_CLASSIFICATION.md`](DATA_CLASSIFICATION.md) · [`LICENSE_INVENTORY.md`](LICENSE_INVENTORY.md)

Identifies, for every tracked area, whether files were **authored** for the new Control Plane,
**migrated** from the legacy EMA repository, **generated**, **third-party**, or **customer-derived**.

## Provenance legend

| Tag | Meaning |
| --- | --- |
| **A** | Authored for the AEC Agentic Control Plane (new) |
| **M** | Migrated from legacy EMA (`EMA-AI`) |
| **G** | Generated (build/test output) |
| **T** | Third-party (vendored) |
| **C** | Customer-derived (see DATA_CLASSIFICATION.md) |

---

## 1. Top-level inventory (tracked files)

| Area | Files | Provenance | Notes |
| --- | ---: | --- | --- |
| `apps/revit-addin` | 271 | **M** | C# Revit add-in + installers + tests. Migrated; legacy machine paths in `.iss` installers relativized (Phase 0). |
| `apps/control-plane-api` | 115 | **M** | FastAPI backend. Migrated; runs against `ema_ai` schema. 184 integration tests, now green. |
| `apps/web-console` | 93 | **M** | React/Vite dashboard. Migrated; hardcoded landing-root path made env-driven (Phase 0). |
| `apps/control-plane-worker`, `control-plane-cli`, `connector-runner` | 9 | **A** | New app stubs (3 each). |
| `docs/legacy/ema-ai` | 269 | **M** / some **C** | Preserved legacy documentation. Some demo content is customer-derived — see DATA_CLASSIFICATION.md. **Not a current source of truth** (AGENTS.md). |
| `docs/adr`, `docs/architecture`, `docs/domains` | 7 | **A** | New ADRs + architecture/domain docs. |
| `packages/python/*` | 47 | **A** | Domain core, policy/evidence/agent/connector/reporting, organism-runtime. Thin but new. |
| `.organism` | 26 | **A** | Constitution, governance, manifests, runtime schemas. Provenance pointer relativized (Phase 0). |
| `standard` | 12 | **A** | Open-standard schemas, events, rule definitions, conformance fixtures. |
| `scripts` | 10 | **A** | Dev commands (bootstrap, test, dev, migrate, seed, lint, release, package-revit, generate-contracts, organism). |
| `infra` | 6 (+1 new) | **M** schema / **A** compose | `database/ema-db/*` migrated EMA schema; `database/organism` + `compose/organism` authored. New `migrations/20260620_002_landing_documents.sql` (A) added in Phase 0. |
| `agents` | 6 | **A** | Agent manifests. |
| `connectors` | 3 | **A** | Connector manifests/stubs. |
| `tests` | 4 (+1 new) | **A** | Architecture/contract/conformance suite (the "12 tests"). New repo-root `conftest.py` (A). |
| root config | ~14 | **A** | `pyproject.toml`, `package.json`, `pnpm-workspace.yaml`, `docker-compose.yml`, `Directory.*.props`, `tsconfig.base.json`, `.gitignore`, `.env.example`, `LICENSE`, etc. |
| `data/taxonomies/ema-ai` | 1 | **M** (product IP) | Requirement-type matrix. |

**Generated (G):** none tracked. `__pycache__`, `.pytest_cache`, `node_modules`, `dist`, build
output, `.env` are gitignored and enforced by `tests/architecture/test_no_generated_or_secret_files.py`
(which checks git-tracked files).

**Third-party (T):** no vendored third-party source; all deps resolved via package managers
(see LICENSE_INVENTORY.md). The Autodesk Revit API DLLs are referenced, **not** tracked.

---

## 2. Files authored during Phase 0 closeout (new since baseline `e7f266e`)

| Path | Provenance | Purpose |
| --- | --- | --- |
| `conftest.py` | A | Auto-marks the EMA suite `integration` by path. |
| `apps/control-plane-api/tests/conftest.py` | A | Disposes the SQLAlchemy engine at session end (fixes hang, P1-2). |
| `infra/database/ema-db/migrations/20260620_002_landing_documents.sql` | A | Adds `landing_document`/`drawing_sheet`/`document_text_snippet` to a fresh DB (fixes P1-1). |
| `MIGRATION_INVENTORY.md`, `LICENSE_INVENTORY.md`, `DATA_CLASSIFICATION.md` | A | Phase 0 deliverables. |

---

## 3. Migration validation status (verified 2026-06-20)

The handoff "copied" EMA into the new tree. Copying ≠ working migration. Verified this session:

- ✅ **Imports survived relocation.** Full collection of all 196 tests succeeds; no import errors
  from the directory move.
- ✅ **Backend runs against the migrated schema.** `GET /health` → ok; `/openapi.json` → 103 paths
  (per CURRENT_STATE Phase 1).
- ✅ **`fresh clone → bootstrap → test` is now reproducible.** `bootstrap.ps1 -Clean` provisions
  Postgres + auto-loads the EMA schema (25 tables); `test.ps1 -All` → **196 passed**, clean exit.
- ✅ **Legacy machine paths removed** from all live files (frontend, `.organism`, installers);
  `docs/legacy/**` history intentionally preserved. See LEGACY_PATH_SCAN.txt / CURRENT_STATE.md §2.
- ✅ **Defects P1-1 (missing `landing_document`), P1-2 (post-run hang), P1-3 (broken compose mount) closed.**

## 4. Open follow-ups (beyond Phase 0)

- [ ] Endpoint-by-endpoint classification of the 103 migrated API paths (canonical / adapter /
      deprecate / remove) — register §3.1.
- [ ] Decide the fate of legacy-only modules in `apps/control-plane-api` as bounded contexts emerge.
- [ ] Replace raw EMA SQL with Alembic migrations when the canonical schema is built (Phase 2).
- [ ] Scrub/clear customer-derived demo content before any publication (DATA_CLASSIFICATION.md).
