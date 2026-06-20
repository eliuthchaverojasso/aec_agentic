# EMA AI — Validation Certificate

<!-- Evidence map: suite -> command -> environment -> SHA -> result -> failures -> log. -->
<!-- Counts here are dated execution evidence; canonical machine values live in -->
<!-- .ai/PROJECT_REFERENCE_MANIFEST.yaml. -->

**Run date:** 2026-06-15 (UTC)
**Branch:** `docs/project-reference-reconciliation`
**Commit (SHA):** `b0cb42bb0a229a3242c9c14ee9938a9709e5842c`
**Environment:** Local Windows 11 Pro 10.0.26200 · .NET 8.0.421 · Node 24.15.0 · Python 3.12.10 · Docker 29.4.3 · PostgreSQL 16-alpine

> Each row was executed against the SHA above in this environment. Build success is
> never reported as runtime validation; "last known" values are not inherited as
> "current". Revit-host gates are explicitly marked externally blocked.

---

## 1. Static / Governance

| Suite | Command | Result | Exit | Evidence |
|---|---|---|---|---|
| Reference drift validator | `python scripts/validate_project_references.py` | **PASS** — 0 errors, 3 warnings | 0 | Was 31 errors at start of session (self-scan of allowlist registry + audit-record docs). Fixed by excluding the allowlist registry from scanning and quoting the YAML pattern so `#` is not parsed as a comment. The 3 warnings are the validator's own pattern definitions (inherently self-referential). |
| `git diff --check` | `git diff --check` | whitespace clean (only CRLF/LF advisories) | 0 | — |

## 2. C# Revit Add-in

| Suite | Command | Result | Exit | Evidence |
|---|---|---|---|---|
| Full C# test suite | `dotnet test EMAExtractor.Tests/EMAExtractor.Tests.csproj` | **265 passed / 0 failed / 0 skipped** | 0 | net8.0, VSTest 17.11.1, duration 7s. One restore warning (NU1603: xunit.runner.visualstudio 2.9.0 → 3.0.0 approximate match) — non-fatal. |

**Proves:** the deterministic engine, report generator, evidence embed caps, and the
new requirement-audit/coherence/Evaluation-Bundle layer compile and pass unit tests
on net8.0.
**Does NOT prove:** behavior inside a Revit host (the add-in targets net4.8/Revit;
not built or loaded here).

## 3. Frontend (React / TypeScript / Vite)

| Suite | Command | Result | Exit | Evidence |
|---|---|---|---|---|
| Dependency install | `npm install --no-audit --no-fund` | added 144 packages | 0 | `npm ci` fails on this host: Windows file lock on `@esbuild/win32-x64/esbuild.exe`. Use `npm install`. |
| TypeScript typecheck | `node_modules/.bin/tsc -b` | **PASS** — no type errors | 0 | tsc 5.9.3 |
| Production build | `node_modules/.bin/vite build` | **PASS** — 2251 modules transformed, built in 3.30s | 0 | Output: `index.html` 0.41 kB; `index.css` 160.71 kB (gzip 26.21); `index.js` **960.41 kB (gzip 257.54)**. Vite warns the JS chunk exceeds 500 kB — recorded as FE performance-budget item. |

**Resolves** the manifest's prior `UNKNOWN` status for `frontend_typecheck` and
`frontend_build`.

## 4. Backend (Python / FastAPI / PostgreSQL)

| Suite | Command | Result | Exit | Evidence |
|---|---|---|---|---|
| DB migrations | `python scripts/apply_migrations.py` | **idempotent** — "No pending migrations; Already applied 20260615_001_requirement_audit_v1.sql" | 0 | Confirms the audit-layer migration is applied and the runner is re-entrant. |
| Full test suite (DB up) | `python -m pytest tests -q` | **172 passed / 12 failed** (184 collected) | 1 | duration 8.64s. DB: localhost:5432 via default `database_url`. |

### The 12 failures (full list)

```
FAILED tests/test_api_auth.py::test_register_user_success_and_case_insensitive_conflict   (assert 404 == 201)
FAILED tests/test_api_auth.py::test_login_user_success_and_invalid_password
FAILED tests/test_api_auth.py::test_profile_requires_bearer_and_returns_user
FAILED tests/test_api_evidence.py::test_project_requirements_surfaces_evidence_review_status  (assert 'covered' == 'compliant')
FAILED tests/test_api_project_file_upload.py::test_upload_project_not_found
FAILED tests/test_api_project_file_upload.py::test_upload_invalid_category
FAILED tests/test_api_project_file_upload.py::test_upload_path_traversal_blocked
FAILED tests/test_api_project_file_upload.py::test_upload_one_drawing
FAILED tests/test_api_project_file_upload.py::test_upload_multiple_files
FAILED tests/test_api_project_file_upload.py::test_upload_rebuild_manifest_called
FAILED tests/test_api_project_file_upload.py::test_upload_no_overwrite_collision   (FileNotFoundError on Windows temp 'Drawings' path)
FAILED tests/test_readiness_coverage_semantics.py::test_qaqc_health_returns_zero_for_no_elements  (assert 100.0 == 0.0)
```

Root-cause classification: auth router not wired (404) ×3; covered/compliant
semantic mismatch ×1; `qaqc_health_score` returns 100.0 for an empty model
(violates "missing data ≠ perfect score") ×1; Windows upload temp-path
`FileNotFoundError` ×7. See MASTER_EXECUTION_BASELINE §4. These match the 12
pre-existing failures recorded in project memory.

**Proves:** 172 of 184 backend tests pass against a real PostgreSQL 16 instance.
**Does NOT prove:** CI green (CI lacks a DB service and these 12 fail regardless),
nor test isolation (suite writes to the live dev DB).

## 5. Docker / API Happy Path

| Check | Command | Result | Evidence |
|---|---|---|---|
| Compose up | `docker compose up -d` | both services started; postgres healthy | exit 0 |
| Container status | `docker compose ps` | `ema_api` Up (healthy), `ema_postgres` Up (healthy) | — |
| API health | `curl http://localhost:8010/health` | `{"status":"ok","database":"ok","version":"0.1.0"}` | DB connectivity confirmed |
| Projects API | `curl http://localhost:8010/api/v1/projects` | returns JSON project array | — |

## 6. Externally Blocked Gates (BLOCKED_EXTERNAL)

| Gate | Blocker | Closure procedure |
|---|---|---|
| Revit add-in runtime smoke | No Revit host in this environment | `docs/revit/REVIT_RUNTIME_SMOKE_CHECKLIST.md` — run on a machine with Revit 2023/2024 |
| Installer / updater signing | No code-signing certificate; no clean VM | PILOT/installer workstream — build unsigned dev package + clean-machine matrix scripts, sign on a machine with the cert |
| Azure / enterprise | No cloud authorization | PROD workstream — IaC + plan only, no resource creation |

## 7. Reproduction (copy-paste)

```powershell
# C#
dotnet test EMAExtractor.Tests\EMAExtractor.Tests.csproj --verbosity minimal

# Frontend (use npm install, not npm ci, on Windows hosts with AV file locks)
cd Pipeline\pipeline\frontend
npm install
node_modules\.bin\tsc -b
node_modules\.bin\vite build

# Backend full suite
cd Pipeline\pipeline
docker compose up -d
python scripts\apply_migrations.py
python -m pytest tests -q

# Governance
python scripts\validate_project_references.py
```
