# EMA AI — Project Reference Baseline Audit

<!--
STATUS: AUDIT DOCUMENT
Generated: 2026-06-14T00:00:00Z (UTC)
Audited branch: docs/project-reference-reconciliation
Audited commit: ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e
Audited from: feat/revit-first-owner-requirement-checker HEAD
Auditor: Claude Sonnet 4.6 (automated), supervised by Eliuth Chavero
-->

## Audit Metadata

| Field | Value |
|---|---|
| Audit date (UTC) | 2026-06-14 |
| Audited branch | `docs/project-reference-reconciliation` (from `feat/revit-first-owner-requirement-checker`) |
| Audited commit SHA | `ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e` |
| Commit date | 2026-06-10T12:39:51-04:00 |
| Audit tool | Grep, Git, dotnet test, pytest (with running stack) |

---

## Repository Canonical Identity (Verified)

| Field | Verified Value |
|---|---|
| Remote (origin) | `https://github.com/echavero-shock/EMA-AI.git` |
| Owner | `echavero-shock` |
| Repository name | `EMA-AI` |
| Full name | `echavero-shock/EMA-AI` |
| Visibility | Private |
| Default branch | `main` (confirmed by `origin/HEAD → origin/main`) |
| Current working branch | `feat/revit-first-owner-requirement-checker` |
| HEAD SHA | `ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e` |
| Open pull requests | **0** (verified via `gh pr list`) |
| Available tags | **0** |
| Additional remotes | `both`, `shock`, `shokworks` (all confirmed as aliases or legacy push mirrors) |

---

## Validation Evidence (Executed This Audit)

| Test Suite | Command | Result | Count | Environment | Date (UTC) |
|---|---|---|---|---|---|
| C# add-in tests | `dotnet test EMAExtractor.Tests/EMAExtractor.Tests.csproj --no-build --verbosity minimal` | **PASS** | **246 passed, 0 failed** | Local, no Revit host | 2026-06-14 |
| Python backend tests | `cd Pipeline/pipeline && python -m pytest tests -q` | **PARTIAL** | **127 passed, 47 failed** | Local, Docker/DB unavailable | 2026-06-14 |
| Frontend typecheck | Not executed this audit pass | UNKNOWN | — | — | — |
| Frontend build | Not executed this audit pass | UNKNOWN | — | — | — |
| Browser smoke | Not executed | NOT_EXECUTED | — | Manual required | — |
| Docker/PostgreSQL smoke | Not executed (Docker unavailable locally) | BLOCKED | — | Docker daemon required | — |
| Revit runtime smoke | Not executed | PENDING | — | Host Revit required | — |
| Azure deployment | Not deployed | NOT_STARTED | — | — | — |
| CI (GitHub Actions) | `gh run list` | **FAILURE** (5/5 recent runs) | — | GitHub | 2026-06-08 (last run) |

### Python test failure note

The 47 Python failures are all database-connection or route-binding failures requiring a running PostgreSQL stack (Docker Compose). The 127 that pass are unit tests with no database dependency. This is expected behavior when Docker is unavailable, but the prior claim of "126 passed" must not be repeated without noting the database requirement and that some tests were failing.

---

## Findings Table

| ID | File | Line/Section | Category | Current Reference | Verified Fact | Classification | Required Action |
|---|---|---|---|---|---|---|---|
| F001 | `README.md` | L288 | Repository identity | `https://github.com/eliuthchaverojasso/EMA-AI` | Canonical URL is `https://github.com/echavero-shock/EMA-AI` | **STALE** | Replace with canonical URL |
| F002 | `README.md` | L289 | Branch | `feat/landing-documents-end-to-end` described as "current work" | Current branch is `feat/revit-first-owner-requirement-checker`; `feat/landing-documents-end-to-end` exists remotely but is not HEAD | **STALE** | Replace with current branch or remove volatile branch claim |
| F003 | `README.md` | L65 | Demo/date | `See [Thursday Demo Plan]` for detailed walkthrough | "Thursday Demo" is a calendar-relative target. The plan was written circa 2026-05-26. No fixed date exists in the document name. | **HISTORICAL_UNLABELED** | Retarget link or add historical label to THURSDAY_DEMO_PLAN.md |
| F004 | `README.md` | L80 | Relative date | "Running the demo this week?" | Relative date with no anchor | **STALE** | Replace with "Running a demo?" (date-neutral) |
| F005 | `README.md` | L213 | Relative date | "P0: Thursday Demo (This Week)" | Calendar-relative; Thursday target predates 2026-06-14 audit date | **STALE** | Replace with date-neutral milestone language |
| F006 | `README.md` | L181 | Test count | `pytest: 126 passed` | Actual: 127 passed, 47 failed (DB tests require running stack) | **STALE** | Remove hardcoded count; reference TEST_MATRIX |
| F007 | `README.md` | L197 | Test status | "Browser smoke (core routes): passing" | Not re-executed this audit pass; last manual pass undated | **CURRENT_BUT_UNVERIFIED** | Add "(last verified 2026-05-26)" or direct to TEST_MATRIX |
| F008 | `README.md` | L197 | Test status | "Docker/PostgreSQL smoke: passing" | Docker is unavailable in current environment; last verified locally pre-2026-06-14 | **CURRENT_BUT_UNVERIFIED** | Add caveat; direct to TEST_MATRIX |
| F009 | `README.md` | L247 | Test count | "Should show 126 passed" | Stale count; actual count differs | **STALE** | Remove specific count from README; direct to TEST_MATRIX |
| F010 | `README.md` | L82–113 | Broken links | `docs/12_OPERATIONS/`, `docs/06_API_REFERENCE/`, `docs/07_FRONTEND_GUIDE/`, `docs/08_BACKEND_GUIDE/`, `docs/09_REVIT_ADDIN/`, `docs/10_DEPLOYMENT/`, `docs/11_TESTING/`, `docs/14_SECURITY_AND_COMPLIANCE/`, `docs/15_ROADMAP/` | None of these numbered subdirectories exist in `docs/` | **BROKEN_LINK** | Replace with links to actual existing paths |
| F011 | `.ai/CONTEXT_RECOVERY.md` | L19 | Branch | `feat/landing-documents-end-to-end` as "Current Branch" | Stale; current branch is `feat/revit-first-owner-requirement-checker` | **STALE** | Update to current branch |
| F012 | `.ai/CONTEXT_RECOVERY.md` | L22–30 | Commit SHAs | Seven hardcoded SHAs from old branch | These SHAs belong to the old branch; do not represent current HEAD | **HISTORICAL_UNLABELED** | Label as historical or replace with git-log reference |
| F013 | `.ai/DECISIONS.md` | L81 | Branch + PR | "feat/landing-documents-end-to-end is the official branch tied to PR #2" | Branch is stale; PR #2 does not exist (0 open PRs) | **STALE** | Label D020 as historical decision, note superseded by current branch |
| F014 | `.ai/FEATURE_GAP_MATRIX.md` | L6 | PR reference | "Need to keep PR #2 on this consolidated branch only" | PR #2 does not exist | **STALE** | Remove active PR #2 framing; label as historical |
| F015 | `.ai/PR_REVIEW_SUMMARY.md` | L138 | PR reference | "move PR #2 from draft to ready" | PR #2 does not exist | **STALE** | Add historical header; classify whole document as historical |
| F016 | `.ai/AGENT_HANDOFF.md` | L95 | PR reference | "Do not create duplicate PRs — update existing PR #2 only" | PR #2 does not exist | **STALE** | Remove or label as historical |
| F017 | `.ai/AGENT_HANDOFF.md` | L72 | Branch | `feat/landing-documents-end-to-end` as parent branch context | Stale branch reference | **HISTORICAL_UNLABELED** | Label document as historical handoff from old sprint |
| F018 | `.ai/CHANGELOG_MEMORY.md` | L374 | Repository URL | `origin https://github.com/eliuthchaverojasso/EMA-AI.git` | Old owner | **HISTORICAL_VALID** | Retain with label "former remote at time of log entry" |
| F019 | `.ai/TEST_MATRIX.md` | L9, L39 | Test count | `126 passed` (dated 2026-05-26) | Current: 127 passed, 47 failed (with DB) on 2026-06-14 | **STALE** | Update with current validated results and date |
| F020 | `.ai/TEST_MATRIX.md` | L17 | Relative date | "Required for Thursday" in column header | Calendar-relative | **STALE** | Replace with milestone-neutral language |
| F021 | `.ai/TEST_MATRIX.md` | L97–98 | Docker test status | "PASS" | Blocked locally; last external pass undated | **CURRENT_BUT_UNVERIFIED** | Add date of last external pass and blockage note |
| F022 | `.ai/DEMO_READINESS.md` | L29 | Test count | `126 passed in the latest validation run` | Stale count | **STALE** | Replace with pointer to TEST_MATRIX |
| F023 | `.ai/DEMO_READINESS.md` | L33 | Docker status | "Blocked in this environment" | Accurate but inconsistent with README and TEST_MATRIX which say PASS | **CONTRADICTORY** | Reconcile: TEST_MATRIX must carry the honest blocked note |
| F024 | `docs/DOCUMENTATION_INDEX.md` | L99 | Broken link | `docs/demo/FINAL_DEMO_SIGNOFF_REPORT.md` | File does not exist | **BROKEN_LINK** | Remove link or create stub with MISSING_TARGET label |
| F025 | `docs/DOCUMENTATION_INDEX.md` | L100 | Broken link | `docs/demo/FINAL_BROWSER_SMOKE_REPORT.md` | File does not exist | **BROKEN_LINK** | Remove link or create stub |
| F026 | `docs/DOCUMENTATION_INDEX.md` | L107 | Broken link | `docs/security/DATA_HANDLING_POLICY.md` | File does not exist | **BROKEN_LINK** | Remove or create stub |
| F027 | `docs/DOCUMENTATION_INDEX.md` | Header | Stale date | "Last refreshed: 2026-05-25" | Audit is 2026-06-14; document not refreshed since | **STALE** | Update on each reconciliation |
| F028 | `docs/demo/THURSDAY_DEMO_PLAN.md` | Throughout | Relative date | Repeated "Thursday" as a live deadline | Thursday target predates this audit | **HISTORICAL_UNLABELED** | Add historical header; move to `docs/archive/2026-05/` |
| F029 | `docs/demo/DEMO_MENU_CLEANUP.md` | L4, L127 | Relative date | "Thursday demo" | Calendar-relative; demo target was circa 2026-05-26 | **HISTORICAL_UNLABELED** | Add historical header |
| F030 | `docs/13_CURRENT_STATE/00_STATE_INDEX.md` | L211 | Relative date | "P0: Thursday Demo (This Week)" | Stale milestone language | **STALE** | Replace with milestone-neutral wording |
| F031 | `docs/16_CHANGELOG/2026-05-28-state.md` | L253 | Relative date | "For Thursday Demo" | Dated changelog; Thursday is in the past | **HISTORICAL_VALID** | Retain as historical changelog; no action needed |
| F032 | `.ai/PR_REVIEW_SUMMARY.md` | L39, L55 | Test count | `126 passed` | Stale | **STALE** | Add historical header; whole doc is historical |
| F033 | `AGENTS.md` | L63 | Branch | `Current branch: feat/revit-first-owner-requirement-checker` | Accurate (verified) | **CURRENT_AND_VERIFIED** | No action |
| F034 | `.ai/CURRENT_STATE.md` | L2 | Date | `Last updated: 2026-06-10` | Accurate relative to HEAD commit date | **CURRENT_AND_VERIFIED** | No action |
| F035 | `.ai/CURRENT_STATE.md` | L39–40 | Test counts | `30 OwnerRequirementReportTests`, `10 RequirementComparisonEngineTests` | These plus others give 246 total (verified by dotnet test) | **CURRENT_BUT_UNVERIFIED** (partial) | Note: 246 total C# tests; partial count is accurate |
| F036 | `.ai/CURRENT_STATE.md` | L53–54 | Report size claim | "516.1 MB → 44.9 MB … 244/244 tests pass" | C# tests are now 246 (2 new tests added per L58-60); 244 was before those 2 | **STALE** | Correct to 246/246 (consistent with L59) |
| F037 | `docs/architecture/AZURE_DEPLOYMENT_RECOMMENDATION.md` | All | Maturity | Azure deployment recommendation presented as a plan | Azure is not deployed. Repository already has KNOWN_BLOCKERS confirming this. | **CURRENT_AND_VERIFIED** | Confirm header says "recommendation / planned" not "deployed" |
| F038 | `.ai/ROADMAP.md` | Various | Status | May contain "Azure deployed" type claim | Need verify | **REQUIRES_HUMAN_DECISION** | Review .ai/ROADMAP.md for overclaim |
| F039 | `docs/demo/EMA_AI_DEMO_SMOKE_CHECKLIST.md` | All | Demo status | Checkbox-style demo checklist | Status of checkboxes is point-in-time; unclear which items were completed | **CURRENT_BUT_UNVERIFIED** | Add audit date to any item marked complete |
| F040 | `.ai/RISKS.md` | All | Duplication | Separate from `.ai/RISK_REGISTER.md` | Two risk files exist with potentially overlapping content | **DUPLICATE_SOURCE_OF_TRUTH** | Merge or designate one canonical; cross-link the other |
| F041 | `docs/RISKS_AND_LIMITATIONS.md` | All | Duplication | Third risk-adjacent document | Same concern as F040 | **DUPLICATE_SOURCE_OF_TRUTH** | Link to canonical risk register |
| F042 | `README.md` | L82–113 | Cross-ref | Documentation section references the `docs/README.md` as "Documentation Hub (coming soon)" | `docs/README.md` exists and is populated | **STALE** | Remove "(coming soon)" |
| F043 | `.ai/METHODOLOGY_REPORT_AUDIT.md` | All | Status | Untracked file in working tree | Not yet committed | **REQUIRES_HUMAN_DECISION** | Commit or .gitignore after review |
| F044 | `.ai/REPORT_QUALITY_MEMORY.md` | All | Status | Untracked file in working tree | Not yet committed | **REQUIRES_HUMAN_DECISION** | Commit or .gitignore after review |
| F045 | `docs/demo/` (various JSON) | All | Generated data | Multiple `.json` audit files in untracked working tree | Generated artifacts; should not enter source control without review | **GENERATED_DO_NOT_EDIT** | Add to .gitignore or archive; do not commit raw generated data |
| F046 | `docs/owner_requirements/` (LaTeX artifacts) | All | Generated data | `.aux`, `.fdb_latexmk`, `.fls`, `.out`, `.toc`, `.pdf` files in working tree | LaTeX build artifacts; should not be committed | **GENERATED_DO_NOT_EDIT** | Add to .gitignore; do not commit build artifacts |
| F047 | Root directory | All | Generated data | `ema_ai_owner_requirements_intelligence_framework.*` (`.aux`, `.pdf`, etc.) at repo root | Generated at wrong location | **GENERATED_DO_NOT_EDIT** | Add to .gitignore; do not commit |
| F048 | `AGENTS.md` | L63 | Test counts | "Recent totals: 322 Met, 159 Not Met, 323 Needs Human Review, 53.3% score" | CURRENT_STATE.md (verified 2026-06-10) shows 55/250/499, score 20.1%; old totals were pre-taxonomy-guardrails | **STALE** | Update to current verified totals |
| F049 | `docs/DOCUMENTATION_INDEX.md` | L24 | Relative date | "Last refreshed: 2026-05-25" | Not refreshed since May | **STALE** | Update refresh date when reconciliation is committed |
| F050 | `Pipeline/pipeline/frontend/README.md` | All | Origin | References `shokworks` remote in content | shokworks is a legacy push mirror; not the canonical remote | **HISTORICAL_UNLABELED** | Add note that shokworks is a legacy mirror |
| F051 | `.ai/DEMO_READINESS.md` + README.md + `.ai/TEST_MATRIX.md` | Multiple | Contradictory | README + TEST_MATRIX: Docker smoke "PASS"; DEMO_READINESS: "Blocked" | Docker is blocked in current environment; prior pass was on external machine | **CONTRADICTORY** | Consolidate to TEST_MATRIX; both pass and blocked states need a dated provenance |
| F052 | `.ai/TEST_MATRIX.md` | L9 | C# tests | C# tests not listed at all | 246 C# tests verified 2026-06-14 | **MISSING_TARGET** | Add C# test row to TEST_MATRIX |
| F053 | `.ai/CURRENT_STATE.md` | L59 | Test count | "246/246 tests pass" | Verified 2026-06-14: 246/246 | **CURRENT_AND_VERIFIED** | No action |
| F054 | `docs/demo/THURSDAY_DEMO_PLAN.md` | L180, L192 | Relative date | "next sprint", "next week" | Calendar-relative in a historical document | **HISTORICAL_UNLABELED** | Historical header covers these |
| F055 | `.ai/DECISIONS.md` | L81 (D020) | Historical | Describes branch consolidation for MVP closure | Decision was valid at time of writing; situation has evolved | **HISTORICAL_VALID** | Retain; add "(as of 2026-05-23, superseded by current branch)" |
| F056 | `scripts/` (various) | All | Path references | Scripts may contain hardcoded local paths | Found `C:\Documents\Hyperghaps EMA\EMA-AI` in `.ai/CONTEXT_RECOVERY.md` | **REQUIRES_HUMAN_DECISION** | Scripts with absolute paths should not be distributed as-is |
| F057 | `docs/demo/EMA_AI_WEEK_BY_WEEK_PLAN.md` | All | Relative dates | Week-by-week plan with relative week references | Week numbers become meaningless without anchor date | **HISTORICAL_UNLABELED** | Add anchor date or archive |
| F058 | CI (GitHub Actions) | All | Test failure | Python tests CI workflow: 5/5 recent runs FAIL | Backend tests require database stack; CI environment may not provision one | **REQUIRES_HUMAN_DECISION** | Either provision DB in CI or skip DB-dependent tests with marks |

---

## Broken Internal Links Summary

| File | Link | Target Exists? |
|---|---|---|
| `README.md` | `docs/12_OPERATIONS/03_DEMO_RUNBOOK.md` | No |
| `README.md` | `docs/12_OPERATIONS/01_LOCAL_STARTUP.md` | No |
| `README.md` | `docs/12_OPERATIONS/04_DATA_RESET_CLEANUP.md` | No |
| `README.md` | `docs/07_FRONTEND_GUIDE/00_FRONTEND_INDEX.md` | No |
| `README.md` | `docs/08_BACKEND_GUIDE/00_BACKEND_INDEX.md` | No |
| `README.md` | `docs/06_API_REFERENCE/00_API_INDEX.md` | No |
| `README.md` | `docs/09_REVIT_ADDIN/00_REVIT_INDEX.md` | No |
| `README.md` | `docs/10_DEPLOYMENT/02_AZURE_DEPLOYMENT.md` | No |
| `README.md` | `docs/10_DEPLOYMENT/03_ENVIRONMENT_VARIABLES.md` | No |
| `README.md` | `docs/11_TESTING/00_TESTING_INDEX.md` | No |
| `README.md` | `docs/14_SECURITY_AND_COMPLIANCE/02_LIMITATIONS.md` | No |
| `README.md` | `docs/15_ROADMAP/00_ROADMAP_INDEX.md` | No |
| `docs/DOCUMENTATION_INDEX.md` | `docs/demo/FINAL_DEMO_SIGNOFF_REPORT.md` | No |
| `docs/DOCUMENTATION_INDEX.md` | `docs/demo/FINAL_BROWSER_SMOKE_REPORT.md` | No |
| `docs/DOCUMENTATION_INDEX.md` | `docs/security/DATA_HANDLING_POLICY.md` | No |

---

## Contradictions Summary

| Contradiction | File A | File B | Resolution |
|---|---|---|---|
| Docker smoke status | `README.md` (PASS), `TEST_MATRIX.md` (PASS) | `DEMO_READINESS.md` (Blocked) | DEMO_READINESS is correct: blocked locally. TEST_MATRIX and README overclaim. |
| Test count (Python) | `README.md`, `TEST_MATRIX.md`, `PR_REVIEW_SUMMARY.md`, `DEMO_READINESS.md` all say "126 passed" | Actual execution 2026-06-14: 127 passed, 47 failed (DB-dependent) | Old count was without DB failures. New count needs honest framing: "127 unit+integration pass; 47 DB-stack tests require running Compose." |
| Current branch | `README.md` L289, `CONTEXT_RECOVERY.md` L19, `DECISIONS.md` L81 say `feat/landing-documents-end-to-end` | Git HEAD is `feat/revit-first-owner-requirement-checker` | Current branch wins. Old branch is stale. |
| PR #2 | `DECISIONS.md`, `FEATURE_GAP_MATRIX.md`, `AGENT_HANDOFF.md`, `PR_REVIEW_SUMMARY.md` reference active PR #2 | `gh pr list` returns empty array | PR #2 does not exist. All references must be labeled historical or removed. |
| NISD demo totals | `AGENTS.md` L69 says 322 Met / 159 Not Met / 323 Needs Human Review / 53.3% | `CURRENT_STATE.md` L47 says 55 Met / 250 Not Met / 499 Needs Human Review / 20.1% (verified 2026-06-10 after taxonomy guardrails) | CURRENT_STATE.md is authoritative. Old totals were pre-guardrail and are explicitly superseded. |
| C# test counts | `CURRENT_STATE.md` L54 says "244/244 tests pass"; L59 says "246/246 tests pass" | `dotnet test` 2026-06-14: 246 passed | 246 is current. 244 was the count before two tests were added (L59 already corrects this). |

---

## Stale References: Quick Reference

### Old Repository Owner
- `eliuthchaverojasso` → `echavero-shock` (canonical)
- Files: `README.md:288`, `.ai/CHANGELOG_MEMORY.md:374`

### Old Branch
- `feat/landing-documents-end-to-end` → `feat/revit-first-owner-requirement-checker` (current)
- Files: `README.md:289`, `.ai/CONTEXT_RECOVERY.md:19`, `.ai/DECISIONS.md:81`, `.ai/FEATURE_GAP_MATRIX.md:6`, `.ai/AGENT_HANDOFF.md:72`

### Stale PR Reference
- PR #2 (never existed or was closed) → No active PR
- Files: `.ai/AGENT_HANDOFF.md:95`, `.ai/DECISIONS.md:81`, `.ai/FEATURE_GAP_MATRIX.md:6`, `.ai/PR_REVIEW_SUMMARY.md:138`

### Stale Test Counts
- "126 passed" → 127 passed (Python unit); 246 passed (C#); Python DB-stack tests require running Compose
- Files: `README.md:181,247`, `.ai/TEST_MATRIX.md`, `.ai/DEMO_READINESS.md:29`, `.ai/PR_REVIEW_SUMMARY.md:39,55`

### Relative Dates
- "Thursday Demo (This Week)" → Historical milestone, circa 2026-05-26
- Files: `README.md:65,80,213`, `docs/13_CURRENT_STATE/00_STATE_INDEX.md:211`, `docs/demo/THURSDAY_DEMO_PLAN.md`, `docs/demo/DEMO_MENU_CLEANUP.md`

### Broken Paths (numbered doc subdirs that don't exist)
- `docs/12_OPERATIONS/`, `docs/06_API_REFERENCE/`, `docs/07_FRONTEND_GUIDE/`, etc.
- All in `README.md` Documentation section

---

## Product Claims Review

| Claim | Location | Supported? | Verdict |
|---|---|---|---|
| "Not production compliance software" | `README.md:29` | Yes — D011 | CURRENT_AND_VERIFIED |
| "AI is advisory only" | `README.md:31`, `AGENTS.md:26` | Yes — D004 | CURRENT_AND_VERIFIED |
| "Local MVP now. Azure and RBAC planned for P1–P2." | `README.md:33` | Yes — D010, D012 | CURRENT_AND_VERIFIED |
| "pytest: 126 passed" | `README.md:181` | No — actual: 127 pass, 47 fail | STALE / OVERCLAIM |
| "Browser smoke (core routes): passing" | `README.md:197` | Unverified this pass | CURRENT_BUT_UNVERIFIED |
| "Docker/PostgreSQL smoke: passing" | `README.md:197` | Blocked locally; prior external pass | CURRENT_BUT_UNVERIFIED |
| Azure Pilot Target URLs at `.azurestaticwebapps.net` etc. | `README.md:133–140` | Target only; not deployed | OVERCLAIM (missing "planned" qualifier) |
| "322 Met, 159 Not Met" | `AGENTS.md:69` | Superseded by 55/250/499 after guardrails | STALE |
| "EMA AI is currently focused on Owner Requirements Readiness through a Revit-first deterministic workflow" | `AGENTS.md:60` | Yes — matches codebase | CURRENT_AND_VERIFIED |
| "Current branch: feat/revit-first-owner-requirement-checker" | `AGENTS.md:63` | Yes — verified | CURRENT_AND_VERIFIED |

---

## Non-Negotiable Product Boundaries — Status

| Boundary | Upheld? | Notes |
|---|---|---|
| PostgreSQL is official source of truth | YES | D001, AGENTS.md, architecture docs all consistent |
| Readiness is deterministic | YES | D002, engine code, tests consistent |
| Evidence Candidate ≠ Accepted Evidence | YES | D003, DEMO_READINESS consistent |
| Accepted Evidence ≠ official compliance | YES | D011 consistent |
| AI/LLM/SEION outputs are advisory only | YES | D004, AGENTS.md consistent |
| AI must not approve evidence/compliance | YES | AGENTS.md:25 explicit |
| EMA AI is pilot/local MVP | YES | D012, README, PRODUCTION_READINESS_GAP consistent |
| Not described as production-ready | YES | Multiple explicit disclaimers |
| Local demo auth is not production auth | YES | D009 explicit |
| Revit compilation ≠ runtime operation | YES | TEST_MATRIX, REVIT_ADDIN_MEMORY explicit |
| Azure architecture ≠ deployed | YES | D010, KNOWN_BLOCKERS explicit |
| APS strategy ≠ implemented viewer | YES | KNOWN_BLOCKERS explicit |

---

## Items Requiring Human Decision

| ID | Description | Options | Recommendation |
|---|---|---|---|
| F043/F044 | `.ai/METHODOLOGY_REPORT_AUDIT.md` and `.ai/REPORT_QUALITY_MEMORY.md` are untracked | (a) Commit as agent memory, (b) .gitignore | Commit — they document decisions about report quality |
| F045 | Generated JSON artifacts in `docs/demo/` (audit files, taxonomy files) | (a) Commit as documentation artifacts, (b) .gitignore, (c) archive | Decision needed: are these outputs or documentation? |
| F046/F047 | LaTeX build artifacts scattered in `docs/owner_requirements/` and repo root | (a) Add to .gitignore, (b) clean up | Add to .gitignore; never commit build artifacts |
| F058 | CI Python test failures | (a) Add pytest marks to skip DB-dependent tests in CI, (b) provision DB in CI workflow | Add `@pytest.mark.requires_db` and skip by default in CI, or add a docker-compose service to CI |
| F038 | `.ai/ROADMAP.md` needs review for Azure overclaim | Manual review needed | Review and qualify any "deployed" language |
| F056 | Absolute Windows paths in `.ai/CONTEXT_RECOVERY.md` | (a) Remove, (b) document as local-machine-only | Remove or clearly label as machine-specific |

---

*This baseline reflects the state of the repository at commit `ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e`, audited 2026-06-14. No files were modified to produce this report.*
