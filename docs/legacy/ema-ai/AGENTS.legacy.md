# EMA AI — Agent Instructions

<!-- Last reconciled: 2026-06-14. See .ai/PROJECT_REFERENCE_MANIFEST.yaml for canonical volatile facts. -->

## Source-of-Truth Hierarchy

When sources conflict, use this priority:

1. Current executable code
2. Current automated tests
3. Current PostgreSQL schema and migrations
4. Actual Git metadata and repository configuration
5. Current Git history
6. AGENTS.md (this file)
7. Current `.ai` memory files
8. Architecture and API documentation
9. Historical notes, plans, and previous reports

Do not resolve a contradiction by choosing the newest-looking Markdown file. Verify the underlying fact.

---

## Canonical Reference Manifest

Machine-readable source of truth for all volatile project facts (repository identity, test counts, validation status, architecture, maturity):

→ **[.ai/PROJECT_REFERENCE_MANIFEST.yaml](.ai/PROJECT_REFERENCE_MANIFEST.yaml)**

When citing test counts, branch names, repository URLs, or validation status in any document, reference the manifest rather than hardcoding values.

---

## Product Identity

EMA AI is a **Revit-first Owner Requirements Readiness** platform.

It is not a generic chatbot.
It is not only a Revit QA/QC tool.

Core flow:

```
Revit Export → Load Owner Requirements (XLSX) → Sync Model Data → Run Compliance Check
→ HTML Report (Executive Summary → Discipline Sections → Evidence → Element IDs)
→ Ask EMA AI (advisory only)
```

---

## Development Rules

- Work in small branches.
- Use Plan mode before implementation.
- Do not touch secrets or .env files.
- Do not push automatically.
- Do not modify Docker/local AI stack unless the branch is explicitly for infrastructure.
- Do not modify EMAExtractor unless the branch is explicitly for Revit add-in work.
- Deterministic engines are the source of truth.
- AI may suggest, explain, summarize and search.
- **AI must not approve official readiness or compliance.**
- **AI must not modify official results automatically.**
- The Revit-first owner-requirements check flow is the designer-facing path; dashboard sync stays optional.

---

## Prohibited Overclaims

Agents must never produce documentation that:

- Claims production readiness without verified production gates
- Grants official authority to AI, SEION, or LLM outputs
- Conflates Evidence Candidate with Accepted Evidence
- Conflates Accepted Evidence with Official Evidence
- Conflates Official Evidence with official compliance
- Claims Azure is deployed (it is planned, not deployed)
- Claims APS viewer is implemented (it is not)
- Claims GraphRAG is implemented (it is deferred)
- Presents historical demo targets as current priorities
- States test counts without execution evidence or a dated provenance note
- Describes local demo authentication as production authentication
- Claims Revit runtime is validated based solely on build success

---

## Current Architecture Rule

```
PostgreSQL         = official source of truth (dashboard stack)
Readiness Engine   = deterministic calculation
Deterministic C# engine = source of truth for Revit-first checker
Qdrant             = semantic retrieval only (advisory)
LLMs               = explanation, search, draft suggestions, developer acceleration
SEION-KGE          = advisory predictions only; no write to official records
```

---

## Protected Files Unless Explicitly Scoped

- `Pipeline/pipeline/docker-compose.ai.yml`
- `opencode.json`
- `Pipeline/pipeline/app/database.py`
- `Pipeline/pipeline/db/init.sql`
- `EMAExtractor/`
- `Pipeline/pipeline/app/ai/`

---

## How to Update Current-State Claims

1. Execute the relevant command (pytest, dotnet test, tsc, etc.).
2. Record: command, environment, branch, commit SHA, date UTC, pass/fail, count.
3. Update `.ai/PROJECT_REFERENCE_MANIFEST.yaml` with the new validated value.
4. Update `.ai/TEST_MATRIX.md` with the execution evidence.
5. Do not update `.ai/CURRENT_STATE.md` counts without updating the manifest.
6. Do not convert "previously passed" into "currently passed."
7. Do not convert "script exists" into "smoke test passed."
8. Do not convert "build passes" into "runtime validated."
9. Do not convert "architecture documented" into "deployed."

---

## How to Classify Historical Documents

A document is **historical** when:
- It describes a past state no longer reflected in code or tests.
- It contains relative dates (Thursday, this week, next sprint) that have already passed.
- It describes an active PR or branch that no longer exists.

For historical documents:
1. Add a header block: `<!-- STATUS: HISTORICAL | Date: YYYY-MM-DD | Superseded by: ... -->`
2. Or move to `docs/archive/YYYY-MM/`
3. Never erase evidence of previous states merely to appear cleaner.

---

## How to Record Test Evidence

Every test-result claim must include:

```yaml
# Example evidence block
test_suite: python_backend
status: PARTIAL
count_pass: 127
count_fail: 47
command: "cd Pipeline/pipeline && python -m pytest tests -q"
branch: docs/project-reference-reconciliation
commit: ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e
executed_at_utc: "2026-06-14T00:00:00Z"
environment: local (Docker unavailable)
notes: "47 DB-dependent tests require running Compose stack."
```

---

## Skill Loading

Before performing any task, agents must:

1. Read `AGENTS.md` (this file) first.
2. Read `.ai/PROJECT_REFERENCE_MANIFEST.yaml` for canonical volatile facts.
3. Read `.ai/MEMORY_INDEX.md`, `.ai/CURRENT_STATE.md`, `.ai/NEXT_STEPS.md` when available.
4. Read relevant `.ai/SKILLS/*.skill.md` for the task at hand.
5. Report which skills were loaded before editing.

Skills do not override `AGENTS.md`.
Skills do not authorize touching secrets, real project files, generated files, commits, or pushes.

See `.ai/SKILLS/README.md` for the routing table.

---

## Current Priority

EMA AI is focused on **Owner Requirements Readiness** through a **Revit-first deterministic workflow**. The deterministic engine in the Revit add-in is the source of truth for status assignment.

**Current working branch:** `docs/project-reference-reconciliation`
**Validated product commit:** `ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e` (2026-06-10)

Known demo dataset (verified 2026-06-10 via TestWorkflowApp real-data run):
- Project: MEP-NISD-MIDDLE SCHOOL 8
- Workbook: NORTHWEST ISD 06.02.2025.xlsx (804 requirements)
- Model elements: 21,868
- Status: 55 Met / 250 Not Met / 499 Needs Human Review / Score: 20.1%
- Note: These totals supersede the pre-taxonomy-guardrail totals (322/159/323 at 53.3%).
  See `.ai/CURRENT_STATE.md` and `.ai/PROJECT_REFERENCE_MANIFEST.yaml` for provenance.

Current next focus:

1. Validate Revit runtime smoke in host Revit
2. Complete report visual QA
3. Harden Ask EMA AI provider chain
4. Validate semantic guardrails with real data
5. Keep AI Query and GraphRAG deferred unless explicitly scoped
6. Do not touch runtime code in documentation/deployment passes

---

## Write Restrictions

- Do not commit: `.env`, `dist/`, `node_modules/`, `*.tsbuildinfo`, real landing files, generated PDFs, DLLs, PDBs, RVT, DWFX, XLSX, DB dumps.
- Do not commit LaTeX build artifacts (`.aux`, `.fdb_latexmk`, `.fls`, `.out`, `.toc`).
- Do not commit generated JSON audit artifacts unless explicitly approved.
- Do not push without explicit user authorization.
- Do not change the default branch without explicit authorization.
- Do not modify branch protection rules.

---

## Branch/Write Restrictions

- Only create branches when explicitly authorized.
- Only push when explicitly authorized.
- Documentation branches (`docs/*`) must not contain runtime product changes.
- Do not mix documentation and code changes in a single commit unless they are tightly coupled (e.g., updating AGENTS.md alongside a new feature).

---

## Validation Expectations

Before claiming any gate is passing, an agent must:
- Execute the command.
- Report the exact result (pass/fail/count).
- Record the environment (local, CI, machine with Docker, etc.).
- Note any limitations (Docker unavailable, Revit host not present, etc.).
- Update `.ai/PROJECT_REFERENCE_MANIFEST.yaml` with the new validated value.

Never inherit a "PASS" status from a prior document without re-executing the command.

---

## Protected Data

The following must never enter source control:
- Real client names, project names, or project data
- Client credentials or contact information
- `.env` files or any file containing secrets, tokens, or keys
- Real landing folder contents (drawings, specifications, Revit exports)
- Database dumps containing real project data
- Screenshots or documents containing personally identifiable information
