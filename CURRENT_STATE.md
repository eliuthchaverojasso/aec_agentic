# CURRENT_STATE.md

**Verified:** 2026-06-20 · **Method:** direct inspection of `C:\Documents\AEC_Agentic` (Phase 0 of the Pending Work Register)
**Commit:** `e7f266e` — "Initial AEC Agentic and ORGANISM scaffold" · branch `main` (tracks `origin/main`) · working tree clean

This document records what was *independently verified*, replacing the prior handoff's assumption-based claims. Where the handoff guessed, this is ground truth. Statements are tagged **CONFIRMED**, **CORRECTED**, or **DEFECT**.

---

## 1. Repository integrity

| Check | Result |
| --- | --- |
| Canonical workspace is `C:\Documents\AEC_Agentic` | **CONFIRMED** |
| Initialized as Git repo, single baseline commit `e7f266e` | **CONFIRMED** — baseline commit already exists |
| Working tree clean, no uncommitted migration residue | **CONFIRMED** |
| Tracked file count | **CONFIRMED** — 895 files; `.git` is 2.7 MB |
| No secrets / keys / certs tracked | **CONFIRMED** — only `.env.example`; no `*.pem/*.key/*.pfx/*.crt` |
| No model binaries / Revit files / DB dumps tracked | **CONFIRMED** — no `*.rvt/*.rfa/*.ifc/*.gguf/*.onnx/*.safetensors/*.db/*.dump`; largest tracked file is a 1.2 MB legacy JSON inventory |
| `.gitignore` covers AEC binaries, secrets, artifacts | **CONFIRMED** — ignores `.env*`, `*.rvt/*.rfa/*.ifc`, `*.xlsx/*.xls`, `*.db/*.dump`, `*.pdf/*.zip`, `artifacts/`, `landing/` |
| Repo can be cloned to another path | **DEFECT** — blocked by hardcoded legacy absolute paths (see §2) |
| No source depends on the original EMA directory | **DEFECT** — see §2 |

**Net:** data hygiene is genuinely good. The repository is *clean of sensitive artifacts*. Portability is the open problem.

---

## 2. Legacy-path leakage (DEFECT)

Full evidence in [`LEGACY_PATH_SCAN.txt`](LEGACY_PATH_SCAN.txt). 91 total tracked hits for `Hyperghaps EMA` / `EMA-AI`; **86 are inside `docs/legacy/**`** (intentionally preserved history — acceptable). **The remaining live/active files must be remediated:**

| File | Issue | Severity |
| --- | --- | --- |
| `apps/web-console/src/pages/ProjectSetupPage.tsx:61` | `DEFAULT_LANDING_ROOT = "C:\\Documents\\Hyperghaps EMA\\EMA-AI\\Pipeline\\pipeline\\landing"` — a **functional UI default** prefilling a form field with the legacy machine path | **High** (shipping frontend; portability + branding) |
| `.organism/manifest.yaml:6` | `source_system.path: "C:\\Documents\\Hyperghaps EMA\\EMA-AI"` — live config provenance pointer to the legacy tree | **Medium** (config; should be relativized or made a non-path reference) |
| `apps/revit-addin/installer/EMAExtractor.iss`, `EMA_AI_Professional.iss`, `EMAExtractor.MultiYear.iss` | Inno Setup `StageDir`/`OutputDir` hardcoded to `C:\Documents\Hyperghaps EMA\EMA-AI\...` | **Medium** (installer builds will fail on any other machine) |
| `apps/web-console/README.md:8` | Doc references a `D:\Documents\Shokworks\...` path | **Low** (doc only) |

No symlinks point back to the legacy repo. No source references user-specific Codex attachment paths.

---

## 3. Test status — the "twelve tests" claim (CORRECTED)

The handoff reported "twelve passing tests." Verified reality:

- **CONFIRMED:** `pwsh .\scripts\test.ps1` runs `pytest tests/architecture tests/contracts tests/conformance` → **exactly 12 passed in 0.61s**. These are *genuine behavior tests*, not import-only smoke tests:
  - `tests/contracts/test_core_domain.py` — 3 tests exercising real domain invariants (obligation accept→allocate; work-package release requires an assigned actor; value recognition requires an approval reference).
  - `tests/conformance/test_required_field_rule.py` — runs the Python policy engine against a `standard/` rule + fixture + expected-result triple (`fixture-missing-circuit`).
  - plus `tests/contracts/test_organism_runtime.py` and `tests/architecture/test_no_generated_or_secret_files.py`.

- **DEFECT — `test.ps1` silently hides the bulk of the suite.** There are **~40 test files / 184 collected tests** in `apps/control-plane-api/tests/` (the migrated EMA suite). `test.ps1` passes explicit paths that **override** `pyproject.toml`'s `testpaths`, so those 184 tests **never run** under the project's test command.

- **DEFECT — `pyproject.toml` `testpaths` is broken.** It lists `packages/python/control-plane-core/tests` and `packages/python/policy-engine/tests`, **neither of which exists**. A bare `pytest` invocation therefore errors immediately (`file or directory not found`, no tests run). `test.ps1` and `pyproject.toml` point at **disjoint, partly-nonexistent** sets.

- **CORRECTED — the 184 migrated tests are healthy once Postgres is present: `183 passed, 1 failed` (8.3 s).** They are **integration tests requiring PostgreSQL** (default DSN `postgresql+psycopg://ema:ema_dev_pw@localhost:5432/ema_ai`). With no DB they all error (`OperationalError: connection failed`) and the run appears to hang; with a Postgres 16 container + `infra/database/ema-db/init.sql` loaded, only **one** test fails. Imports/collection always succeeded — relocation did **not** break imports. (See "Phase 1 verification" below.)

**Conclusion:** "12 passing tests" is true *for the new scaffold suite only*. The migrated EMA suite is real, substantial, and effectively green (183/184) **when the DB is up** — but (a) the canonical `test.ps1` never runs it, and (b) `bootstrap.ps1` never starts the DB, so the exit criterion "fresh clone → test → success" is **not met today** without manual DB setup.

Environment observed: **Python 3.12.10** (note: `pyproject.toml` only requires `>=3.11`, unpinned), `pytest 8.3.4`, `fastapi 0.115.5`.

---

## 4. Scaffold vs. implementation (CORRECTED / CONFIRMED)

The handoff's core caveat holds. New "Control Plane" + "ORGANISM" Python code is **thin typed scaffolding**; the code mass is **migrated EMA**.

| Python package | Files | LOC | Assessment |
| --- | --- | --- | --- |
| `control-plane-core` | 15 | 255 | Domain dataclasses + kernel (ids/money/quantities) + obligation/work/value modules. Real but minimal. |
| `organism-runtime` | 16 | 342 | Typed mission/lease/watchdog/routing/risk/artifact/context/supervision **contracts only**. |
| `policy-engine` | 2 | 27 | Single `evaluate_required_fields` rule evaluator. |
| `agent-runtime` | 2 | 17 | Stub. |
| `connector-sdk` | 2 | 16 | Stub. |
| `evidence-engine` | 2 | 17 | Stub. |
| `reporting` | 1 | 2 | Empty. |

Migrated app mass: `apps/revit-addin` (271 files, C#), `apps/control-plane-api` (115 files, FastAPI), `apps/web-console` (93 files, React/Vite).

**ORGANISM is modeled, not running (CONFIRMED §14 hypothesis):**
- `langgraph` / `StateGraph` / `checkpointer` appear **only** in `.organism/manifest.yaml` as a declared stack — **never imported in any code**.
- `ollama` appears only in `.organism/` config and the C# Revit add-in's OpenAI-compatible provider — **no Python Ollama client exists**.
- **No Alembic.** Migrations are raw SQL: a single file `infra/database/ema-db/migrations/20260615_001_requirement_audit_v1.sql`.

`scripts/bootstrap.ps1` is a **no-op stub** — it prints banner text and installs/starts nothing (no prereq checks, no env, no DB, no migrations, no deps). Section 2.2's bootstrap is effectively unimplemented.

---

## 5. Licensing & data classification (first pass)

- **LICENSE:** Proprietary, all rights reserved, "Copyright (c) 2026." No OSS license. (Full `LICENSE_INVENTORY.md` — third-party dependency licenses — not yet produced.)
- **Data classification:** No customer workbooks, model exports, or proprietary reports are tracked. `docs/legacy/ema-ai/docs/demo/` contains JSON *inventories* and PNG diagram previews derived from a "NORTHWEST ISD" project — **review these for customer-derived content** before any public/open-standard publication. (Full `DATA_CLASSIFICATION.md` not yet produced.)

---

## 5b. Phase 1 verification — "get EMA running" (2026-06-20)

Reproduced the migrated EMA stack end-to-end on this machine:

1. **Postgres** — `docker run postgres:16-alpine` with the legacy creds (`ema`/`ema_dev_pw`/`ema_ai`), then loaded `infra/database/ema-db/init.sql` (18 tables + seed) and `migrations/20260615_001_requirement_audit_v1.sql` (→ 22 tables). Clean.
2. **Tests** — `pytest apps/control-plane-api/tests` against the live DB → **183 passed, 1 failed in 8.3 s**.
3. **Backend** — `uvicorn app.main:app` booted against the live DB:
   - `GET /health` → `{"status":"ok","database":"ok"}`
   - `GET /openapi.json` → **103 paths** (full migrated API surface live).

**New DEFECTs found during Phase 1:**

| # | Finding | Evidence / impact |
| --- | --- | --- |
| P1-1 | **`landing_document` table missing from `init.sql`.** It is a model in `app/models.py` created lazily by `document_service.py`'s `Base.metadata.create_all` only when that service is first used. `test_dev_status_endpoint_available` (and `GET /dev/status`) query it directly → `UndefinedTable: relation "landing_document" does not exist` on a fresh DB. | The single test failure. Fix: add `landing_document` (and any other `create_all`-managed tables) to `init.sql`, or `create_all` on app startup. |
| P1-2 | **Test process does not exit cleanly.** pytest reports completion in 8.3 s, but the process then hangs until killed (outer `timeout` → exit 124). Cause: the module-level SQLAlchemy `engine` pool in `app/database.py` is never disposed; no teardown fixture. | Masquerades as a "hang"; breaks CI time budgets and any naive `pytest` run. Fix: a session-scoped fixture that `engine.dispose()`s, or `pool_class=NullPool` for tests. |
| P1-3 | **`infra/compose/ema-local.compose.yml` is broken.** It bind-mounts `./db/init.sql` (= `infra/compose/db/init.sql`), which **does not exist**; the real schema is at `infra/database/ema-db/init.sql`. | The documented "start legacy EMA stack" command cannot initialize the schema. Fix: correct the mount path. |

**Net:** the migrated EMA backend genuinely works. It is not broken by the migration — it is only *un-bootstrapped*. Closing P1-1/P1-2/P1-3 plus the Phase 0 test-config fixes would make `fresh clone → bootstrap → test` reproducible.

---

## 6. Phase 0 exit-criteria scorecard

| Criterion | Status |
| --- | --- |
| Clean `git status` | ✅ |
| Baseline commit exists | ✅ |
| No sensitive/generated artifacts tracked | ✅ |
| Repository can be cloned to another path | ✅ live legacy machine paths removed (Phase 0 closeout, §8) |
| No source depends on original EMA directory | ✅ only `docs/legacy/**` references remain (preserved history) |
| Migration inventory complete | ✅ [`MIGRATION_INVENTORY.md`](MIGRATION_INVENTORY.md) produced |
| Fresh clone → bootstrap → build → migrate → test → success | ✅ `bootstrap.ps1 -Clean` + `test.ps1 -All` → **196 passed**, verified 2026-06-20 (§8) |

---

## 7. Recommended immediate Phase 0 closeout (small, high-leverage)

1. **Relativize/remove the 4 live legacy paths** (§2) — biggest one is `ProjectSetupPage.tsx`; make `DEFAULT_LANDING_ROOT` env-driven or empty.
2. **Reconcile the test config** — make `test.ps1` and `pyproject.toml` agree; either create the missing `tests/` dirs or fix `testpaths`; ensure one command can discover *all* suites and that failures return non-zero.
3. **Separate unit from integration tests** — mark the 184 DB-gated tests (`pytest` marker) so a no-Docker run is green and CI can gate them behind a Postgres service. This makes "fresh clone → test" honest.
4. **Make `bootstrap.ps1` real or rename it** — today it misrepresents itself; at minimum it should do prereq checks + `.env` generation + `docker compose up` for Postgres.
5. **Produce remaining deliverables** — `MIGRATION_INVENTORY.md`, `LICENSE_INVENTORY.md`, `DATA_CLASSIFICATION.md`.

> The decisive next step after Phase 0 remains unchanged from the register: prove the requirement→evidence chain end-to-end with real data. But the items above are the cheap, factual prerequisites that make the rest reproducible.

---

## 8. Phase 0 closeout — executed & verified (2026-06-20)

All five §7 items are done. Verified on this machine (`pwsh`, Docker, Postgres 16):

| §7 item | Change | Verification |
| --- | --- | --- |
| 1. Legacy paths | `ProjectSetupPage.tsx` → `import.meta.env.VITE_DEFAULT_LANDING_ROOT ?? ""`; `.organism/manifest.yaml` provenance relativized; 3 Inno Setup `.iss` installers → `SourcePath`-relative; `web-console/README.md` path generic | `grep` for legacy paths outside `docs/legacy/**` → **0 live hits** |
| 2+3. Test config | `pyproject.toml` `testpaths` fixed (dropped 2 nonexistent dirs, added `tests`); root `conftest.py` auto-marks EMA suite `integration`; `addopts` default-deselects integration; `--strict-markers`; `test.ps1` gains `-Integration` / `-All` | `pytest` → **12 passed, 184 deselected**, exit 0; `-m integration` selects exactly 184 |
| 4. bootstrap | `bootstrap.ps1` rewritten: prereq checks, idempotent `.env`, `docker compose up postgres` with auto-loaded schema + health wait + schema verification; `-Clean` / `-NoDocker` modes | `bootstrap.ps1 -Clean` → 25 tables; `test.ps1 -All` → **196 passed** in 6.15s, clean exit |
| 5. deliverables | `MIGRATION_INVENTORY.md`, `LICENSE_INVENTORY.md`, `DATA_CLASSIFICATION.md` produced | this commit |

**Defects closed:** P1-1 (added `landing_document` et al. to schema via `20260620_002_landing_documents.sql` → 184/184), P1-2 (engine disposed via session fixture → no post-run hang), P1-3 (fixed `infra/compose/ema-local.compose.yml` init-script mount path).

**Also fixed:** `tests/architecture/test_no_generated_or_secret_files.py` now checks **git-tracked** files (was scanning the working tree, so it false-failed the moment a gitignored local `.env` existed after bootstrap).

**Canonical local DB:** `docker-compose.yml` now provisions the `ema_ai`/`ema` database the migrated backend + tests actually use, with the EMA schema auto-loaded from `infra/database/ema-db`. The future canonical `aec_control_plane` schema (Phase 2, Alembic) will be introduced when it exists.

---

## 9. PR 3 — Compose repair (Pending Work Register Item 2) — 2026-06-20

Closes the remaining gap in critical-path step 1 (reproducible repo + Compose).

| Change | Detail |
| --- | --- |
| **Root `docker-compose.yml` is the single canonical stack** | API port mapping corrected `8010:8010` → `${API_PORT:-8010}:8000` — the container's uvicorn listens on 8000 (`Dockerfile`), so the old mapping left the API unreachable. Added: API `/health` healthcheck, persistent `landing_data` volume, a named `control-plane` network, explicit env interpolation with safe `:-` defaults, and the optional object-store/AWS passthrough that previously lived only in the local override. |
| **Removed `infra/compose/ema-local.compose.yml`** | Both broken (api `build.context: .` + `./app` mounts resolved to `infra/compose/`, which has no Dockerfile/app) and redundant with the root file `scripts/dev.ps1` already uses. `README.md` repointed to `docker compose up -d --build`. (The §5b P1-3 entry above is the historical record of that file; it no longer exists.) |
| **ORGANISM compose deferred (P3)** | `infra/compose/organism/*` (floating `apache/age:latest` / OTEL `:latest`, debug-only exporter, no runtime worker) is left for the P3 ORGANISM-runtime work; pinning its images needs a verified tag. |

**Verification:** `docker compose config` renders `published: "8010" → target: 8000`, `DATABASE_URL …@postgres:5432`, with healthcheck + `control-plane` network + `landing_data`/`postgres_data` volumes present; `scripts/test.ps1` → **12 passed, 188 deselected**; no live references to the removed file remain outside this log. A live container boot was not run (host `:8010` is occupied by a non-container process and no API image is prebuilt); the container side of the mapping is unchanged from the existing `Dockerfile`.
