# EMA AI — Agent Skill Handoff

**Purpose:** Instructions for AI coding agents working on this repo.  
**Branch:** `docs/project-reference-reconciliation`  
**Last updated:** 2026-06-15

---

## Mission Summary

EMA AI is an Engineering Intelligence platform focused on **Revit-first Owner Requirements Readiness**. Report the deterministic engine as source of truth. Do not overclaim. Keep AI advisory-only.

---

## Current Product Focus

Owner Requirements Readiness workflow:
1. Load Requirements (XLSX) → 2. Sync Model Data → 3. Run Compliance Check → 4. Generate Master HTML/PDF Report → 5. Discipline Sections with Evidence/Validation Type/Rule Applied/Element IDs → 6. Ask EMA AI

---

## Files Most Likely to Inspect

| Area | Key files |
|------|-----------|
| Engine models | `EMAExtractor/Requirements/RequirementCheckModels.cs` |
| Comparison engine | `EMAExtractor/Requirements/RequirementComparisonEngine.cs` |
| Validation type | `EMAExtractor/Requirements/ValidationTypeClassifier.cs` |
| Confidence | `EMAExtractor/Requirements/ConfidenceScorer.cs` |
| Scoring | `EMAExtractor/Requirements/ScoreCalculator.cs` |
| Key issues | `EMAExtractor/Requirements/KeyIssueRanker.cs` |
| Report generator | `EMAExtractor/Reporting/OwnerRequirementHtmlReportGenerator.cs` |
| Workflow service | `EMAExtractor/Services/RequirementCheckWorkflowService.cs` |
| AI commands | `EMAExtractor/Commands/Ai/AskAboutReportCommand.cs` |
| Tests | `EMAExtractor.Tests/OwnerRequirementReportTests.cs` |
| Tests | `EMAExtractor.Tests/RequirementComparisonEngineTests.cs` |

---

## Files NOT to Modify During Docs-Only Pass

- `EMAExtractor/**` (source code)
- `EMAExtractor.Tests/**` (tests)
- `Pipeline/**` (backend/frontend)
- `scripts/**` (build scripts)
- `*/.csproj` (project files)
- `installer/**` (installer scripts)
- `artifacts/**` (generated files)
- `*.exe`, `*.zip`, `*.log`
- `bin/`, `obj/`, `dist/`, `node_modules/`

---

## Old-Style .csproj Warning

`EMAExtractor/EMAExtractor.csproj` uses explicit file includes (not SDK-style). New `.cs` files must be explicitly added to the `.csproj` compile includes. Do not modify the `.csproj` during docs-only passes.

---

## Report Quality Checklist

- [ ] Master report contains Executive Summary, Discipline Allocation, Status Legend, Urgency Legend, Key Issues, Issues by Urgency, Discipline Sections
- [ ] Each requirement card shows: status, evidence found, validation type, rule applied, reasoning, next best action, Element IDs
- [ ] Traceability collapsed by default with expand/collapse toggle
- [ ] Element IDs copyable via JS click handler
- [ ] Hidden JSON `#ema-ai-report-context` present and structurally valid
- [ ] No "undefined" or "null" visible in report
- [ ] No banned overclaim words (certified, approved, guaranteed, legally compliant)
- [ ] Filter context banner present for master and discipline views
- [ ] Print CSS hides filter panel, avoids page breaks

## Explainability Checklist

- [ ] Validation Type: why Model/Drawing/Spec/Manual/Hybrid was chosen
- [ ] Rule Applied: rule name, family, trigger keywords, expected evidence, expected categories, expected parameters
- [ ] Evidence Found: matched categories, families/types, parameter values, Element IDs, inspected count, evidence strength
- [ ] Missing Evidence: missing parameters, missing sources, not captured vs empty vs mismatch
- [ ] Evidence Alignment: Strong / Partial / Weak / Mismatch risk / Manual-only with reason
- [ ] Reasoning: status rationale, evidence limitation, human review boundary
- [ ] Next Best Action: model fix, spec/drawing review, parameter population, manual review, no action required

## Revit Element ID Traceability Checklist

- [ ] Each requirement report shows Element IDs that support the finding
- [ ] Element IDs include copy-to-clipboard JS
- [ ] IDs are visible in collapsed traceability section
- [ ] Preview shows count and first few IDs before expand
- [ ] Select Elements by ID works in Revit

## Ask EMA AI Guardrails

- AI explains only from report context
- AI does not certify, approve, change statuses, invent evidence, or replace engineering review
- Provider chain: Deterministic Fallback → Ollama → OpenRouter → (future) RAG/Qdrant
- Default system prompt prevents overclaim
- Citations and Element IDs included in responses

## Build/Test Commands (for future code-change passes)

```powershell
# Backend tests
cd Pipeline\pipeline
py -3.12 -m pytest tests -v

# Revit add-in build
msbuild EMAExtractor/EMAExtractor.csproj /p:RevitYear=2023 /p:Platform=x64

# Frontend typecheck + build
cd Pipeline\pipeline\frontend
npx tsc -b --noEmit
npm run build
```

## Installer Command (for future code-change passes)

```powershell
# See scripts/install-ema-addin.ps1
```

## Manual Revit Smoke Checklist

- [ ] Add-in installs without errors
- [ ] EMA AI ribbon tab visible
- [ ] "Compare Owner Requirements" opens selection dialog
- [ ] Workbook loads (XLSX)
- [ ] Model sync completes
- [ ] Compliance check runs
- [ ] Report opens in browser
- [ ] Filters work (discipline, status, urgency)
- [ ] Numbers are coherent
- [ ] Element IDs copy
- [ ] "Select Elements by ID" works
- [ ] PDF export works (if configured)

## Git Hygiene

```powershell
# Never use:
git add .

# Never commit:
# - artifacts/
# - *.exe, *.zip, *.log
# - bin/, obj/, dist/, node_modules/
# - TestWorkflow.cs, TestWorkflowApp/
# - test_*.ps1
# - installer_comand.txt
# - LaTeX aux/fdb/fls/out
# - real client XLSX/RVT files

# Always:
git status --short
git diff --name-only
git add <explicit-paths-only>
git commit -m "type(scope): short description"
```

## For Documentation-Only Passes

- Do not edit source code
- Do not edit tests
- Do not edit `.csproj`
- Do not edit build/installer scripts
- Do not run destructive commands
- Do not stage artifacts
- Allowed: `docs/**`, `.ai/**`, `README.md`, `AGENTS.md`

## Final Response Format

After task completion, report:
1. Files created
2. Files updated
3. Current project state captured
4. Confirmation no source code modified
5. Recommended `git add` using explicit paths only
6. Recommended commit message
