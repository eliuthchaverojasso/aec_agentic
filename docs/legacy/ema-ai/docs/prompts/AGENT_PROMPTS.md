# EMA AI — Reusable Agent Prompts

**Last updated:** 2026-06-08

---

## Docs-Only Synchronization

```
Perform a documentation-only synchronization pass for the EMA AI project.

Rules:
- DO NOT modify production system code.
- DO NOT modify the deterministic engine.
- DO NOT modify report generator code.
- DO NOT modify UI code.
- DO NOT modify tests.
- DO NOT modify build scripts.
- DO NOT modify .csproj files.
- DO NOT modify backend/frontend runtime code.

Allowed: docs/**, .ai/**, README.md, AGENTS.md

Focus:
1. Read the current repo structure and understand actual state
2. Update docs to reflect Revit-first Owner Requirements Readiness focus
3. Update architecture docs
4. Update methodology docs
5. Update report specs
6. Update Ask EMA AI specs
7. Update demo scripts and checklists
8. Update dev/build/test docs
9. Update roadmap, decisions, risks
10. Update prompts and skills maps
11. Update .ai context files

Final validation:
- git status --short shows only docs/.ai changes
- No source code, tests, .csproj, build scripts, or installer scripts modified
```

---

## Report Visual Polish

```
Goal: Polish the Owner Requirements HTML report visual design.

Scope:
- CSS consistency (spacing, typography, colors)
- Requirement card layout
- Executive Summary metric cards alignment
- Discipline Allocation grid styling
- Key Issues card styling
- Element ID traceability expand/collapse
- Filter chip styling
- Print CSS behavior
- No horizontal overflow
- Status/urgency colors consistent with design system

Do not change:
- Report structure or content
- Hidden JSON
- Engine logic
- Backend/frontend code

Test: Run OwnerRequirementReportTests to verify no regressions.
```

---

## Dropdown/Overflow Traceability

```
Goal: Ensure large Element ID lists are handled gracefully in the report.

Current behavior: Traceability collapsed by default with "Show {N} Element IDs" preview.
Preview shows count + first 3-5 IDs.

Verify:
1. Preview shows correct count
2. Expand shows full list without overflow
3. "Copy Element IDs" copies full list
4. Collapse state persists until manual toggle
5. Edge case: 0 Element IDs (should show "No IDs found")
6. Edge case: 1 Element ID (preview shows 1 ID)

Test: Run OwnerRequirementReportTests traceability tests.
```

---

## Evidence Explainability

```
Goal: Ensure every requirement card has complete explainability blocks.

Checklist:
1. Validation Type — displayed with reason
2. Rule Applied — rule name, family, trigger keywords
3. Evidence Found — categories, families, parameter values, count
4. Missing Evidence — missing parameters listed
5. Evidence Alignment — level + reason
6. Reasoning — full explanation text
7. Next Best Action — actionable guidance

For each block: verify content is present, meaningful, and not duplicate.
For each block: verify content does not contain "undefined" or "null".
For each block: verify content has no banned overclaim words.

Source: RequirementCheckResult fields populated by RequirementComparisonEngine.
```

---

## Validation Type / Rule Applied

```
Goal: Audit ValidationType and RuleApplied in the report.

Validation types:
- Model (keyword-based, equipment/parameter checks)
- Drawing (detail/notation keywords)
- Specification (spec/manufacturer keywords)
- Manual (verify/confirm keywords)
- Hybrid (multiple types score >= 0.25)

For each requirement card:
1. Does Validation Type match the requirement content?
2. Is the reason for this type clear?
3. Does Rule Applied name and family make sense?
4. Are trigger keywords shown?
5. Are expected categories/parameters shown?

Sources: ValidationTypeClassifier.cs, RequirementComparisonEngine.cs
```

---

## Ask EMA AI Implementation

```
Goal: Review and improve Ask EMA AI behavior.

Current:
- Provider chain: Deterministic Fallback → Ollama → OpenRouter → (future RAG)
- Default model: qwen3.6:35b
- Fallback: granite4.1:30b
- System prompt prevents overclaim

Verify:
1. Report context JSON is loaded
2. Question receives response with citations
3. Response includes Requirement IDs and Element IDs
4. Response declines to certify compliance
5. Without LLM, deterministic fallback works
6. Response format is readable

Do not change: Engine logic, report structure, system prompt core rules.
```

---

## Git Hygiene

```
Goal: Clean up repository and stage only intended files.

Commands:
git status --short
git diff --name-only

Stage only:
- docs/**
- .ai/**
- README.md
- AGENTS.md

Do NOT stage:
- EMAExtractor/**
- EMAExtractor.Tests/**
- Pipeline/**
- scripts/**
- *.csproj
- artifacts/**
- bin/**, obj/**, dist/**, node_modules/**
- *.exe, *.zip, *.log
- TestWorkflow.cs, TestWorkflowApp/
- test_*.ps1
- installer_comand.txt
- LaTeX aux/fdb/fls/out/pdf
- Real client files

Commit format: "docs(scope): description"
Example: "docs(report): update report spec with explainability requirements"
```

---

## Final QA

```
Goal: Final quality assurance before documentation commit.

1. Read all changed docs files
2. Verify:
   - No overclaim language
   - No "undefined" or "null"
   - Consistent terminology (Owner Requirements, Evidence Found, Validation Type, etc.)
   - Current branch and date correct
   - Links between docs are valid
   - Status markers (✅🟡 pending) accurate
3. Run git status --short
4. Verify only docs/.ai files changed
5. Report: files created, files updated, anything missing
```

---

## Revit Smoke

```
Goal: Manual Revit smoke test in host Revit.

Prerequisites: Revit 2023/2024 installed, NISD Middle School model, requirements workbook.

Steps:
1. Install add-in (scripts/install-ema-addin.ps1)
2. Open Revit with NISD model
3. Verify EMA AI ribbon tab visible with all buttons
4. Click "Compare Owner Requirements"
5. Select workbook, choose discipline, click Load
6. Click "Sync Model Data"
7. Click "Run Compliance Check"
8. Verify progress window shows stages
9. Report opens in browser
10. Verify Executive Summary counts are coherent
11. Apply discipline filter
12. Expand a requirement card
13. Verify Evidence Found / Validation Type / Rule Applied / Element IDs
14. Click "Copy Element IDs"
15. Ask a question to Ask EMA AI
16. Verify response

Report: Pass/Fail for each step. Screenshots if applicable.
```
