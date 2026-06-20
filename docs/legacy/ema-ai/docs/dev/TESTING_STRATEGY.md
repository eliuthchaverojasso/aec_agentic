# EMA AI — Testing Strategy

**Last updated:** 2026-06-08

---

## Testing Layers

```
┌─────────────────────────────────────┐
│  Manual Revit Smoke Tests           │  ← Requires host Revit
├─────────────────────────────────────┤
│  Visual QA Tests                    │  ← Manual browser walk
├─────────────────────────────────────┤
│  AI Assistant Tests                 │  ← Provider chain validation
├─────────────────────────────────────┤
│  Report Tests (xUnit)               │  ← Deterministic HTML/JSON
├─────────────────────────────────────┤
│  Performance Tests (xUnit)          │  ← Parallel evaluation
├─────────────────────────────────────┤
│  Engine Unit Tests (xUnit)           │  ← Comparison engine
├─────────────────────────────────────┤
│  Backend Tests (pytest)             │  ← FastAPI + PostgreSQL
└─────────────────────────────────────┘
```

---

## 1. Unit Tests (xUnit — Revit Add-in Engine)

| Test File | Tests | Description |
|-----------|-------|-------------|
| `RequirementComparisonEngineTests.cs` | 10 | Evaluate, EvaluateParallel, EvidenceIndex, CoherenceChecker, synthetic data |
| `OwnerRequirementReportTests.cs` | 30 | Report generation, hidden JSON, filters, discipline views, traceability, no-overclaim |

### Key Tests
- `Evaluate_PopulatesNarrativeFields` — verifies IssueTitle, Reasoning, NextBestAction, EvidenceSummary
- `EvaluateParallel_ProducesDeterministicResults` — 100 req × 500 elem, sequential == parallel
- `EvaluateParallel_800RequirementsAnd20000Elements_CompletesReasonably` — < 120s
- `CoherenceChecker_DetectsAllNotApplicable` — fails when all NA
- `Generate_WritesMasterHtmlReportWithExecutiveSections` — 50+ HTML element checks
- `Generate_EmbedsValidMachineReadableJsonAndKeepsItHidden` — JSON schema, overclaim check
- `Generate_ReportDoesNotContainUndefinedOrVisibleNull`
- `Generate_ReportAvoidsBannedOverclaimWords`

## 2. Performance Tests

- 800 requirements × 20,000 elements — sequential completion < 120s
- Parallel evaluation produces same results as sequential
- EvidenceIndex O(1) category lookup pre-built at startup

## 3. Report Tests

- Master report structure (Executive Summary, Discipline Allocation, Key Issues, etc.)
- Discipline-filtered report (Electrical, Lighting, etc.)
- Machine-readable JSON validity
- No-undefined/null visible content
- Banned word absence
- Traceability collapsed by default
- Element ID copy JS
- Print CSS behavior
- Filter context banners
- Design system CSS variables

## 4. AI Assistant Tests

- Provider chain validation (deterministic fallback → Ollama)
- Context boundary — AI must not invent evidence
- Response format — citations include Requirement IDs and Element IDs
- Overclaim guardrails — AI must decline certification requests

## 5. Manual Revit Smoke Tests

See [docs/demo/EMA_AI_DEMO_SMOKE_CHECKLIST.md](../demo/EMA_AI_DEMO_SMOKE_CHECKLIST.md)

- Add-in installs
- Ribbon visible
- Workbook loads
- Model syncs
- Compliance check runs
- Report opens
- Filters work
- Element IDs copy

## 6. Visual QA Tests

- Report rendering (all sections, spacing, colors, typography)
- Status/Urgency colors correct
- Discipline colors correct
- No horizontal overflow
- Print preview
- PDF export (browser print)

## 7. Validation Commands

```powershell
# Backend tests
cd Pipeline\pipeline
py -3.12 -m pytest tests -v

# Revit add-in tests
cd EMAExtractor.Tests
dotnet test

# Frontend typecheck
cd Pipeline\pipeline\frontend
npx tsc -b --noEmit

# Frontend build
npm run build

# Ollama availability
ollama list

# Git hygiene
git status --short
git diff --name-only
```
