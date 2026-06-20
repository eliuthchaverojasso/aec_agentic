# EMA AI — Master Execution Baseline

<!-- Canonical execution baseline for the program. Volatile facts here are dated and -->
<!-- carry their own evidence; the machine-readable source of truth remains -->
<!-- .ai/PROJECT_REFERENCE_MANIFEST.yaml. -->

**Established:** 2026-06-15 (UTC)
**Established on branch:** `docs/project-reference-reconciliation`
**Established at HEAD:** `b0cb42bb0a229a3242c9c14ee9938a9709e5842c`
**Environment:** Local Windows 11 Pro (10.0.26200); Docker Desktop 29.4.3; .NET SDK 8.0.421; Node 24.15.0; Python 3.12.10.

This document is the verified ground-truth snapshot used to plan and execute the
EMA AI hardening program. Every status below is backed by a command executed
against the HEAD above, or is explicitly marked as not-run / externally blocked.
It supersedes assumptions in older planning documents but does **not** erase them.

---

## 1. Repository Identity

| Fact | Value | Source |
|---|---|---|
| Canonical repo | `echavero-shock/EMA-AI` | `git remote -v`, manifest |
| Local path | `C:\Documents\Hyperghaps EMA\EMA-AI` | environment |
| Default branch | `main` | `origin/HEAD -> origin/main` |
| Current branch | `docs/project-reference-reconciliation` | `git branch --show-current` |
| Current HEAD | `b0cb42b` (`chore: reconcile docs and audit flow`) | `git rev-parse HEAD` |
| Formally audited product baseline | `feat/revit-first-owner-requirement-checker` @ `ae6ded2` (2026-06-10) | manifest |

### Remotes (verified)

```
origin     https://github.com/echavero-shock/EMA-AI.git   (canonical)
shock      https://github.com/echavero-shock/EMA-AI.git   (alias of origin)
both       fetch echavero-shock/EMA-AI ; push shokworks/ema-ai   (MIXED — unsafe)
shokworks  https://github.com/shokworks/ema-ai.git        (legacy mirror)
```

**Risk:** the `both` remote fetches from the canonical repo but **pushes to the
legacy `shokworks/ema-ai` mirror**. A `git push both` from any branch would write
to the legacy repo. This is a foot-gun and is dispositioned in
[GIT_CONSOLIDATION_AUDIT.md](GIT_CONSOLIDATION_AUDIT.md) (GIT-05).

### Branch topology (local)

- `main` → tracks `shokworks/main` (NOT `origin/main`). Local `main` is at `9f20d31`; this is a divergence-of-tracking finding (GIT-02).
- 16 feature/`docs`/`chore` local branches; most point at `1674ad8` and are reported "behind `shokworks/main` by 33".
- `feat/revit-first-owner-requirement-checker` @ `ae6ded2` is the formally audited product baseline.
- `codex/cloud-url-defaults` @ `52e4dbf` carries an out-of-tree fix (`fix(readiness): treat empty model QA health as passing baseline`) relevant to a current test failure (see §4).

### Tags / worktrees / stashes

- **Tags:** none. (No immutable release anchors exist — required before any pilot release; PILOT/GIT workstream.)
- **Worktrees:** single working tree only.
- **Stashes:** 2 — `stash@{0}` on `feat/demo-hardening-finish-ema-ai` (`temp-pre-consolidation`); `stash@{1}` on `feat/qdrant-rag-foundation` (`wip: qdrant ...`). Preserved; not applied.

---

## 2. Pre-existing Uncommitted Changes (preserved, not authored by this baseline)

Already present in the working tree at the start of this program:

| File | Nature |
|---|---|
| `.ai/AGENT_HANDOFF.md`, `.ai/BRANCHES.md`, `.ai/CONTEXT_RECOVERY.md`, `.ai/DECISIONS.md`, `.ai/NEXT_STEPS.md`, `.ai/ROADMAP_P1_P3.md` | Documentation reconciliation: branch/HEAD references updated to `docs/project-reference-reconciliation`; P4 "Pilot Delivery Packaging" roadmap added. Coherent, retained. |
| `Pipeline/pipeline/frontend/tsconfig.app.tsbuildinfo` | Generated TypeScript build-info. Already `.gitignore`-listed yet **tracked** (committed before the ignore rule). Untrack candidate — see GIT-08. |

These were left intact. No pre-existing change was reverted or overwritten.

---

## 3. Branch-Hygiene Finding on HEAD

HEAD commit `b0cb42b` is titled `chore: reconcile docs and audit flow` and lives on
a `docs/*` branch, but it introduced **~5,000 lines of runtime product code** — the
full Evaluation Bundle / requirement-audit layer across C# (`EMAExtractor/Requirements/Audit/*`,
`OwnerRequirementHtmlReportGenerator.cs`), Python (`app/api/requirement_audits.py`,
`app/services/requirement_audit_ingest.py`, `app/models.py`, a migration), and the
React frontend (`RequirementAuditsPage.tsx`, `types.ts`, `client.ts`).

`AGENTS.md` states: *"Documentation branches (`docs/*`) must not contain runtime
product changes."* This commit violates that contract. It is already committed, so
it is recorded here as **HISTORICAL fact**, not reversed. Consequence: the docs
branch now contains validated runtime code that should be reconciled onto a
product branch during GIT consolidation (GIT-04). All new runtime work for the
remainder of the program must move to an authorized feature branch.

---

## 4. Validation Results (executed 2026-06-15 against `b0cb42b`)

Full evidence map in [VALIDATION_CERTIFICATE.md](VALIDATION_CERTIFICATE.md).

| Suite | Command | Result | Exit |
|---|---|---|---|
| C# add-in tests | `dotnet test EMAExtractor.Tests/EMAExtractor.Tests.csproj` | **265 passed / 0 failed** (net8.0, 7s) | 0 |
| Frontend typecheck | `node_modules/.bin/tsc -b` (tsc 5.9.3) | **PASS**, no type errors | 0 |
| Frontend prod build | `node_modules/.bin/vite build` (vite 6.4.2) | **PASS** — 2251 modules, 3.30s | 0 |
| Backend full suite (Postgres up) | `python -m pytest tests -q` | **172 passed / 12 failed** (184 collected, 8.64s) | 1 |
| DB migrations | `python scripts/apply_migrations.py` | **idempotent** — "No pending migrations" | 0 |
| Docker stack | `docker compose up -d` + `curl /health` | both containers **healthy**; `/health` → `{status:ok, database:ok}` | 0 |
| Reference drift validator | `python scripts/validate_project_references.py` | **0 errors / 3 warnings** (was 31 errors; fixed this session) | 0 |
| Revit runtime | host Revit session | **BLOCKED_EXTERNAL** — no Revit host in this environment | — |

### Material corrections to prior documents (drift)

1. **C# count:** manifest/README/TEST_MATRIX say **246**; actual at `b0cb42b` is **265** (+19 audit-layer tests). The 246 figure was correct for `ae6ded2` (pre-audit-layer).
2. **Backend reality:** prior docs framed the suite as "127 unit pass / 47 fail without DB; full suite needs Docker." With Docker **up**, the truth is **172 passed / 12 failed of 184**. The 12 are **real defects**, not DB-availability. This is the first recorded full-stack run (prior audits had Docker "blocked locally").
3. **Frontend:** manifest had typecheck/build as **UNKNOWN** ("last verified 2026-05-26"). Both are now **PASS** with dated evidence.
4. **CI:** still `FAILURE` (no DB service in workflow). Root cause now confirmed: the 12 non-DB failures plus the missing Postgres service.

### The 12 backend failures — classified root causes

| # | Test(s) | Root cause | Class | Workstream |
|---|---|---|---|---|
| 1–3 | `test_api_auth.py` (register/login/profile) | `POST /api/v1/auth/register` → **404**; auth router not wired on this branch (auth lives on the separate `feat/login` line) | Feature-incomplete | BE / GIT |
| 4 | `test_api_evidence.py::...surfaces_evidence_review_status` | returns `'covered'`, expects `'compliant'` — evaluated/covered/compliant semantics not separated | Logic/semantics | BE-04 |
| 5 | `test_readiness_coverage_semantics.py::test_qaqc_health_returns_zero_for_no_elements` | `qaqc_health_score(0,0,0,0,0)` returns `100.0`, expects `0.0` | **Product-truth #8 violation** (missing data ≠ perfect score) | BE |
| 6–12 | `test_api_project_file_upload.py` (7) | `FileNotFoundError` on Windows temp `Drawings` path during upload | Env/path-handling (Windows); needs triage | BE / VAL |

These failures match the "12 pre-existing failures" recorded in project memory and
are confirmed reproducible. They are **backend runtime defects** and therefore
belong on an authorized feature branch, not this `docs/*` branch. The failing
tests themselves are the regression tests.

---

## 5. Toolchain & Service State

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 8.0.421 | Test project targets `net8.0`; add-in targets net4.8/Revit (not built here) |
| Node | 24.15.0 | npm 11.12.1 |
| Python | 3.12.10 (also 3.14.5 present) | psycopg 3.2.3, pytest 8.3.4, fastapi, sqlalchemy installed in 3.12 |
| Docker | 29.4.3 | Compose stack functional |
| PostgreSQL | 16-alpine | container `ema_postgres`, port 5432, db `ema_ai`, user `ema` |

**Environment caveat:** `npm ci` fails on this machine due to a Windows file lock
on `node_modules/@esbuild/win32-x64/esbuild.exe` (antivirus/in-use). `npm install`
succeeds. Recorded so the frontend gate is reproducible.

**Test isolation finding:** pytest runs against the **live dev database** (no
transactional rollback / no isolated test DB). The projects list now contains
pytest-generated rows (e.g. `DISPLAY-NAME-…`). Tracked as a VAL/BE hardening item.

---

## 6. Artifact / Working-Tree Hygiene

- `.gitignore` hardened this session: LaTeX intermediates (`*.aux`, `*.fls`, `*.fdb_latexmk`, `*.toc`, `*.bbl`, `*.blg`, …), `*.out`, `*.pdf`, `artifacts/`, `tmp/`, `docs/owner_requirements/diagram_preview/`. Verified zero tracked `*.pdf`/`*.out` before globalizing. Untracked count fell from 200+ to 12 with **no tracked file affected**.
- **Tracked compiled binaries** remain under `installer/package/payload/EMA AI/`: `EMAExtractor.dll`, `EMAExtractor.pdb` (debug symbols), and ~9 dependency DLLs — committed against AGENTS.md policy. `.gitignore` cannot untrack them; remediation requires `git rm --cached` and is dispositioned in the GIT runbook (GIT-07), gated on authorization.
- 12 untracked files remain undecided (source vs generated): `data/feedback/.gitkeep`, `data/taxonomy/requirement_type_matrix.json`, and `docs/demo/EMA_AI_*.json` + `EMA_AI_RULES_TABLES.{csv,json}`. They were **not** ignored because tracked sibling files share the `EMA_AI_*.json` naming; a human decision is required (see GIT-09).

---

## 7. Immediate Risks

| ID | Risk | Severity |
|---|---|---|
| R1 | `both` remote pushes to legacy `shokworks/ema-ai`; accidental `git push both` writes to the wrong repo | HIGH |
| R2 | Local `main` tracks `shokworks/main`, not `origin/main`; "canonical" is ambiguous in tooling | HIGH |
| R3 | Validated runtime code (audit layer) sits only on a `docs/*` branch; not on a product branch | MEDIUM |
| R4 | 12 backend tests fail with the stack up; CI cannot be made green honestly until fixed or quarantined with documented reason | MEDIUM |
| R5 | `qaqc_health_score` returns 100.0 for an empty model — a product-truth violation that could overstate readiness | MEDIUM |
| R6 | Compiled DLL/PDB tracked in source; no signed-release pipeline; no tags | MEDIUM |
| R7 | Revit runtime never validated in a host; "supported versions" list (2022–2027) is unproven | HIGH (for pilot claims) |

---

## 8. Decisions Required from the User

These are gated because they are destructive, change remote/tracked state, or
change a product authority/claim:

1. **Branch consolidation (GIT-01..05):** confirm `main` (on `echavero-shock/EMA-AI`) as the single canonical branch; reconcile `main`'s tracking from `shokworks/main` to `origin/main`; decide the fate of the legacy `shokworks` / `both` remotes. *No remote changes will be made without this.*
2. **Audit-layer placement (GIT-04):** authorize creating a product feature branch to carry the `b0cb42b` runtime code off the `docs/*` branch.
3. **Tracked binaries (GIT-07):** authorize `git rm --cached` for `installer/package/payload/EMA AI/*.dll|*.pdb`.
4. **Untracked demo JSON / data (GIT-09):** classify the 12 undecided files as source (commit) or generated (ignore).
5. **Feature-branch authorization for runtime fixes:** the 12 backend failures and all ENG/BE/BND/REP/AI/FE/DOC work require runtime code changes that must not land on a `docs/*` branch.

---

## 9. What This Baseline Does Not Cover

- Revit runtime behavior (no host available) — see RVT workstream, all host-only gates `BLOCKED_EXTERNAL`.
- Installer/updater/signing (no signing cert / clean VM) — see PILOT/installer workstream.
- Azure/enterprise (no cloud authorization) — see PROD workstream.

The dependency-ordered backlog and per-ID disposition are in
[.ai/MASTER_BACKLOG_EXECUTION.yaml](../../.ai/MASTER_BACKLOG_EXECUTION.yaml); the
per-ID final report is in
[MASTER_EXECUTION_FINAL_REPORT.md](MASTER_EXECUTION_FINAL_REPORT.md).
