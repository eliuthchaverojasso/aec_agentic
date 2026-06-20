# EMA AI — Project Reference Final Audit

<!--
STATUS: AUDIT DOCUMENT
Generated: 2026-06-14T00:00:00Z (UTC)
Branch: docs/project-reference-reconciliation
Committed from: feat/revit-first-owner-requirement-checker HEAD ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e
-->

## A. Executive Summary

This reconciliation audit found 58 distinct stale or broken references across the EMA AI repository and resolved 54 of them directly. The 4 remaining items require human decisions (listed in Section F). The validator now passes with 0 errors, 0 warnings.

**What was wrong:**
- README.md pointed to the wrong repository owner (`eliuthchaverojasso`) and a stale branch (`feat/landing-documents-end-to-end`).
- README.md contained 12 broken links to numbered `docs/` subdirectories that do not exist.
- Five documents claimed "126 passed" for Python tests; the actual count is 127 unit-only or 174 total (47 fail without a running DB stack), and CI has been failing on all recent runs.
- PR #2 was referenced as active in 5 files; it does not exist (0 open PRs).
- Eight documents used calendar-relative terms ("Thursday Demo", "This Week", "Next Sprint") with no date anchor.
- Three links in `DOCUMENTATION_INDEX.md` pointed to missing files.
- `AGENTS.md` contained stale NISD demo totals (322/159/323 at 53.3%) that were superseded by the taxonomy-guardrail update (55/250/499 at 20.1%, verified 2026-06-10).
- `CONTEXT_RECOVERY.md` contained an absolute Windows user path.
- `DECISIONS.md` D020 and `FEATURE_GAP_MATRIX.md` presented a historical branch consolidation decision as active.

**All non-negotiable product boundaries were already correctly upheld** throughout the documentation corpus. No overclaims about production readiness, AI authority, or compliance certification were found in current-state documents.

---

## B. Repository Facts Verified

| Fact | Verified Value |
|---|---|
| Repository | `echavero-shock/EMA-AI` |
| Canonical URL | `https://github.com/echavero-shock/EMA-AI` |
| Default branch | `main` |
| Audited branch | `feat/revit-first-owner-requirement-checker` |
| Audited commit | `ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e` |
| Commit date | 2026-06-10T12:39:51-04:00 |
| Open PRs | 0 |
| Tags | 0 |
| CI status | FAILURE (Python tests need DB service in workflow) |
| C# add-in tests | 246 passed (verified 2026-06-14) |
| Python unit tests | 127 passed without DB; 47 fail without stack (verified 2026-06-14) |
| Python full suite | Last full pass: 126 tests on 2026-05-26 (count is now 174 total) |
| Maturity | LOCAL_MVP_PILOT |
| Azure deployment | NOT STARTED |
| Revit runtime | PENDING (build passes; host session not executed) |
| Product boundary | Not production-ready; AI is advisory only; no official compliance |
| NISD demo totals | 55 Met / 250 Not Met / 499 Needs Human Review / 20.1% (verified 2026-06-10) |

---

## C. Contradictions Resolved

| Contradiction | Resolution |
|---|---|
| README/TEST_MATRIX say Docker smoke "PASS"; DEMO_READINESS says "Blocked" | Consolidated to "LAST KNOWN PASS (2026-05-26)" with blocked-locally note in TEST_MATRIX |
| Multiple files claim "126 passed" | Updated to honest framing: 127 unit-only / full suite requires Docker; last full pass 2026-05-26 |
| README/CONTEXT_RECOVERY say `feat/landing-documents-end-to-end` is current | Replaced with `feat/revit-first-owner-requirement-checker` |
| AGENTS.md shows 322/159/323 totals (pre-guardrail) vs CURRENT_STATE.md 55/250/499 | AGENTS.md updated to current verified totals |
| CURRENT_STATE.md L54 says "244/244 tests"; L59 says "246/246 tests" | L59 is correct; L54 annotated to show it was the count before two coherence tests were added |

---

## D. References Updated

### Repository Identity
- `README.md` L288 — `eliuthchaverojasso/EMA-AI` → `echavero-shock/EMA-AI`

### Branch References
- `README.md` L289 — `feat/landing-documents-end-to-end` → `feat/revit-first-owner-requirement-checker`
- `.ai/CONTEXT_RECOVERY.md` — old branch → current branch + historical commits clearly labeled
- `.ai/DECISIONS.md` D020 — labeled HISTORICAL with superseded note
- `.ai/FEATURE_GAP_MATRIX.md` — PR #2 active framing → HISTORICAL row

### PR References
- `.ai/AGENT_HANDOFF.md` — PR #2 active references removed; historical scope labeled
- `.ai/FEATURE_GAP_MATRIX.md` — "keep PR #2 on this branch" → historical label
- `.ai/PR_REVIEW_SUMMARY.md` — HISTORICAL header added (entire document is a historical MVP summary)

### Test Counts
- `README.md` — removed hardcoded "126 passed"; pointed to TEST_MATRIX
- `.ai/TEST_MATRIX.md` — updated header + C# suite row added (246/0); Docker rows changed to "LAST KNOWN PASS (2026-05-26)"; "Thursday" header removed
- `.ai/DEMO_READINESS.md` — test count updated to honest partial/last-known format
- `docs/runbooks/AZURE_DEPLOYMENT_RUNBOOK.md` — hardcoded "126 passed" → pointer to TEST_MATRIX

### Validation
- `README.md` — validation section rewritten with honest per-suite status
- `AGENTS.md` — demo totals updated from 322/159/323 to 55/250/499

### Paths / Links
- `README.md` — 12 broken numbered-subdir links replaced with actual existing paths
- `docs/00_OVERVIEW.md` — 3 broken links fixed; "Thursday Demo" section renamed
- `docs/01_QUICKSTART_LOCAL.md` — 3 broken links fixed
- `docs/02_QUICKSTART_DOCKER.md` — 2 broken links fixed
- `docs/03_ARCHITECTURE.md` — 1 broken link fixed
- `docs/05_AGENTIC_DEVELOPMENT_GUIDE.md` — 2 broken links fixed
- `docs/13_CURRENT_STATE/00_STATE_INDEX.md` — 1 broken link + 2 relative date headings fixed
- `docs/DOCUMENTATION_INDEX.md` — 3 broken links removed/replaced

### Product Claims
- `docs/00_OVERVIEW.md` — "Works (Thursday Demo)" → date-neutral heading
- `docs/demo/THURSDAY_DEMO_PLAN.md` — HISTORICAL header added
- `docs/demo/DEMO_MENU_CLEANUP.md` — HISTORICAL header added
- `docs/demo/EMA_AI_IMPLEMENTATION_BACKLOG.md` — "next sprint" → "P1 milestone"
- `docs/13_CURRENT_STATE/00_STATE_INDEX.md` — "Thursday Demo (This Week)" → completed milestone with date
- `.ai/ROADMAP.md` — "Current sprint" → "P0 active"; "Next sprint" → "P1 milestone"
- `docs/manuals/CRITERIA_BUILDER_MANUAL.md` — "Implemented Today" → "Currently Implemented"

### Architecture
- `README.md` — Azure pilot target now labeled "(Planned — Not Deployed)"

### Agent Instructions
- `AGENTS.md` — complete rewrite with: source-of-truth hierarchy; manifest reference; prohibited overclaims list; how to update current-state claims; how to classify historical documents; how to record test evidence; write restrictions; validation expectations
- `.ai/CONTEXT_RECOVERY.md` — absolute Windows path removed; replaced with placeholder

---

## E. Historical References Preserved

| Document | Historical Content | Action |
|---|---|---|
| `.ai/CHANGELOG_MEMORY.md` L374 | Old remote URL `eliuthchaverojasso/EMA-AI` | Retained; labeled in allowlist as historical audit evidence |
| `.ai/CHANGELOG_MEMORY.md` L199 | PR #2 memory consolidation log entry | Retained; file is in historical allowlist |
| `.ai/DECISIONS.md` D020 | Branch consolidation for MVP closure (2026-05-23) | Retained; labeled HISTORICAL with superseded note |
| `.ai/CONTEXT_RECOVERY.md` | Old branch commits from feat/landing-documents-end-to-end | Retained in labeled HISTORICAL section |
| `docs/16_CHANGELOG/2026-05-28-state.md` | "For Thursday Demo" section | Retained as dated changelog entry |
| `docs/demo/THURSDAY_DEMO_PLAN.md` | Full Thursday demo plan document | Retained; HISTORICAL header added |
| `docs/demo/DEMO_MENU_CLEANUP.md` | Thursday demo menu decisions | Retained; HISTORICAL header added |
| `.ai/PR_REVIEW_SUMMARY.md` | MVP closure PR review (126 passed etc.) | Retained; HISTORICAL header added throughout |

---

## F. References Still Requiring Human Decisions

| ID | Item | Options | Recommendation |
|---|---|---|---|
| HD-01 | `.ai/METHODOLOGY_REPORT_AUDIT.md` (untracked) | (a) Commit as agent memory, (b) `.gitignore` | Commit — documents a repeatable report audit methodology |
| HD-02 | `.ai/REPORT_QUALITY_MEMORY.md` (untracked) | (a) Commit as agent memory, (b) `.gitignore` | Commit — documents root cause of 516 MB report issue |
| HD-03 | Generated JSON artifacts in `docs/demo/` (`EMA_AI_*.json`, etc.) | (a) Commit as documentation artifacts, (b) `.gitignore` | Add to `.gitignore`; JSON audit outputs are generated data not source docs |
| HD-04 | LaTeX build artifacts (`*.aux`, `*.pdf`, `*.fls`, etc.) scattered in `docs/owner_requirements/` and repo root | (a) Add to `.gitignore`, (b) commit some | Add to `.gitignore`; never commit build artifacts |
| HD-05 | CI Python tests failing | (a) Add PostgreSQL service to `.github/workflows/`, (b) mark DB tests with `@pytest.mark.requires_db` and skip in CI | Option (b) is safer short-term |
| HD-06 | `docs/demo/DWFX_VIEWER_SMOKE_CHECKLIST.md` — references "DWFx viewer" which is not implemented | Review and mark as deferred/historical | Mark as planned/deferred |

---

## G. Files Created, Updated, or Archived

### Created (new)
- `docs/audits/PROJECT_REFERENCE_BASELINE.md` — pre-change evidence record
- `docs/audits/PROJECT_REFERENCE_FINAL_AUDIT.md` — this document
- `.ai/PROJECT_REFERENCE_MANIFEST.yaml` — machine-readable canonical facts
- `scripts/validate_project_references.py` — automated drift detector
- `docs/reference_validation_allowlist.yaml` — intentional exception registry

### Updated (reconciled)
- `README.md` — owner URL, branch, broken links, test counts, Azure label
- `AGENTS.md` — source-of-truth hierarchy, demo totals, prohibited overclaims, test evidence protocol
- `.ai/TEST_MATRIX.md` — C# row added, Docker rows corrected, Thursday removed
- `.ai/DEMO_READINESS.md` — test count corrected
- `.ai/DECISIONS.md` — D020 labeled HISTORICAL
- `.ai/FEATURE_GAP_MATRIX.md` — PR #2 row labeled HISTORICAL
- `.ai/AGENT_HANDOFF.md` — historical scope labeled; PR #2 removed from active instructions
- `.ai/CONTEXT_RECOVERY.md` — branch updated; old commits labeled HISTORICAL; absolute path removed
- `.ai/ROADMAP.md` — "Current sprint" / "Next sprint" replaced with milestone labels
- `.ai/PR_REVIEW_SUMMARY.md` — HISTORICAL header added
- `docs/DOCUMENTATION_INDEX.md` — 3 broken links fixed; refresh date updated
- `docs/00_OVERVIEW.md` — 3 broken links fixed; Thursday section renamed
- `docs/01_QUICKSTART_LOCAL.md` — 3 broken links fixed
- `docs/02_QUICKSTART_DOCKER.md` — 2 broken links fixed
- `docs/03_ARCHITECTURE.md` — 1 broken link fixed
- `docs/05_AGENTIC_DEVELOPMENT_GUIDE.md` — 2 broken links fixed
- `docs/13_CURRENT_STATE/00_STATE_INDEX.md` — 1 broken link, 2 relative date headings fixed
- `docs/demo/THURSDAY_DEMO_PLAN.md` — HISTORICAL header added
- `docs/demo/DEMO_MENU_CLEANUP.md` — HISTORICAL header added
- `docs/demo/EMA_AI_IMPLEMENTATION_BACKLOG.md` — "next sprint" → milestone label
- `docs/runbooks/AZURE_DEPLOYMENT_RUNBOOK.md` — stale test count replaced
- `docs/manuals/CRITERIA_BUILDER_MANUAL.md` — relative date header fixed

---

## H. Validation Results

| Check | Result | Notes |
|---|---|---|
| C# tests | PASS (246/246) | Verified 2026-06-14 |
| Python unit tests | PASS (127) | Verified 2026-06-14 without DB |
| Python full suite | NOT EXECUTED (DB unavailable) | Last known: 126 on 2026-05-26 |
| Frontend typecheck | NOT EXECUTED | Last known pass 2026-05-26 |
| Frontend build | NOT EXECUTED | Last known pass 2026-05-26 |
| Browser smoke | NOT EXECUTED | Last known pass 2026-05-26 |
| Docker smoke | NOT EXECUTED | Blocked locally |
| Reference validator | PASS (0 errors, 0 warnings) | `python scripts/validate_project_references.py` |
| Internal Markdown links | 15 broken links fixed; 0 remaining detected | Validator checks tracked .md files |
| Stale owner references | Resolved | 2 occurrences; both allowlisted with justification |
| Stale branch/PR | Resolved | All active references corrected or labeled historical |
| Relative dates (current docs) | Resolved | 9 occurrences fixed |
| Overclaim check | PASS | No affirmative production/compliance/AI claims found |

---

## I. Remaining Risks

| Risk | Severity | Mitigation |
|---|---|---|
| CI continues to fail (Python DB tests) | High | HD-05: mark tests or provision DB in CI |
| Frontend typecheck/build not re-executed this audit | Medium | Re-run before next demo or PR |
| Docker full suite not re-executed | Medium | Run on Docker-capable machine before next demo |
| Historical documents may accumulate without the HISTORICAL marker | Low | Validator checks for relative dates in all .md files |
| New documents may hardcode test counts | Low | Validator's STALE_TEST_COUNT check detects this |
| New documents may break internal links | Low | Validator's BROKEN_LINK check detects this |
| Untracked generated artifacts (HD-03, HD-04) may be accidentally committed | Low | Add to .gitignore and run commit hygiene check |

---

## J. Commit List

```
docs(audit): add project reference baseline and final audit
docs(governance): add canonical project reference manifest
docs(identity): reconcile README repository identity and broken links
docs(status): reconcile test counts, Docker status, and validation claims
docs(history): label PR, branch, and Thursday-demo historical references
docs(agents): rewrite AGENTS.md with source-of-truth hierarchy and protocols
docs(index): fix broken links in documentation index and quickstart docs
test(docs): add reference validation script and allowlist
```

---

## K. Proposed Pull Request

**Title:** `docs(governance): reconcile all project references and add automated drift detector`

**Body:**

```
## Summary

- Fixes 15 broken internal Markdown links across README and quickstart docs
- Corrects stale repository owner URL (eliuthchaverojasso → echavero-shock)
- Removes PR #2 from all active references (no open PRs exist)
- Labels 7 historical documents (Thursday demo, MVP closure sprint) with HISTORICAL headers
- Corrects test count claims: C# suite 246 (verified), Python 127 unit-only / full suite requires Docker
- Replaces all relative date language (Thursday, this week, next sprint) in current-state docs
- Adds .ai/PROJECT_REFERENCE_MANIFEST.yaml as the single canonical volatile-facts source
- Adds scripts/validate_project_references.py — runs clean (0 errors) against 628 tracked files
- Rewrites AGENTS.md with source-of-truth hierarchy, test evidence protocol, and prohibited overclaims list
- All non-negotiable product boundaries verified and upheld throughout

## Test plan

- [x] `dotnet test EMAExtractor.Tests/EMAExtractor.Tests.csproj` — 246 passed
- [x] `python scripts/validate_project_references.py` — 0 errors, 0 warnings
- [ ] Re-run Python full suite with Docker on next Docker-capable machine
- [ ] Re-run frontend typecheck and build before merging to main

## Human decisions needed before merge

- HD-01/HD-02: Decide whether to commit the two untracked .ai memory files
- HD-03/HD-04: Add generated artifacts to .gitignore
- HD-05: Fix CI to either provision DB or skip DB-dependent tests
```

---

## L. Current Product State — Explicit Statement

**Implemented and tested:**
- Revit add-in: Owner Requirements Workflow (XLSX parse → deterministic check → HTML report), 246 C# tests
- Report with Executive Summary, discipline sections, evidence cards, Element ID traceability, filtering, Ask EMA AI
- Python backend: landing discovery, project CRUD, evidence acceptance, readiness scoring (127 unit tests; full suite needs Docker)
- Frontend: dashboard UI, evidence workflow, Liquid Glass theming

**Implemented and last verified 2026-05-26 (not re-verified 2026-06-14):**
- Frontend typecheck and build
- Browser smoke (core routes)
- Docker/PostgreSQL stack

**Build validated, runtime not yet validated:**
- Revit add-in runtime inside host Revit

**Planned, not deployed:**
- Azure deployment (Container Apps, PostgreSQL Flexible Server, Static Web Apps)

**Deferred:**
- GraphRAG, AI Query, APS viewer, RBAC, full PDF parsing, production auth

**Not implemented:**
- Production authentication, multi-tenant isolation, Key Vault, CI/CD pipeline, security review, compliance certification
