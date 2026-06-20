# EMA AI — Project Memory

**Last updated:** 2026-06-15

---

## 1. Project Identity

| Field | Value |
|-------|-------|
| Name | EMA AI |
| Focus | Revit-first Owner Requirements Readiness |
| Branch | `docs/project-reference-reconciliation` |
| Demo project | MEP-NISD-MIDDLE SCHOOL 8 |
| Requirements | 804 rows from NORTHWEST ISD 06.02.2025.xlsx |
| Model elements | 21,868 |
| Disciplines | Electrical, Lighting, Mechanical, Plumbing, Technology |

---

## 2. Product Narrative

**The pain:** Engineering teams at EMA spend hours correlating Owner Requirements against Revit models before deliverable submissions. Evidence is scattered across Excel, PDFs, drawings, and Revit views. There is no single, traceable view of "are we meeting requirements."

**Why Owner Requirements are the focus:** Owner Requirements are the contractual baseline for deliverable readiness. Every discipline needs to know: which requirements are met, which are not, what evidence exists, who owns the gap, and what to fix next.

**Why the report is requirement-by-requirement:** Each requirement needs individual evidence, reasoning, and traceability. Bulk status is not actionable.

**Why executive summary comes first:** Management needs the overview before drilling into details.

**Why discipline navigation matters:** Different trades have different gaps. Electrical, Mechanical, Plumbing, Lighting, and Technology each need separate views.

---

## 3. Current Technical State

### Revit Ribbon/Workflow
- "Compare Owner Requirements" ribbon entry in EMA AI tab
- "Open EMA AI Panel" opens WPF dashboard with overview, requirements, readiness, issues, exports, and settings
- Modal selection dialog for workbook/discipline/scope/output folder
- Local Excel parser (`.xlsx` / `.xlsm`)
- Deterministic comparison engine
- HTML report generator
- Last report path and summary persisted in local settings

### Deterministic Engine
- Source files in `EMAExtractor/Requirements/`:
  - `ValidationTypeClassifier.cs` — classifies Model/Drawing/Specification/Manual/Hybrid
  - `RequirementComparisonEngine.cs` — builds EvidenceIndex, matches evidence, assigns status
  - `ConfidenceScorer.cs` — 6-factor confidence (discipline, clarity, strength, specificity, completeness, traceability)
  - `ScoreCalculator.cs` — overall score, discipline score, readiness score, sync freshness
  - `KeyIssueRanker.cs` — 6-factor key issue ranking with severity
- Statuses: Met, Not Met, Needs Human Review, Not Applicable, Insufficient Model Data

### EvidenceIndex
- Pre-built index for O(1) category lookups
- Pre-computed search blobs for performance
- Category-based evidence matching

### Scoring/Confidence
- Confidence: 6 weighted factors — DisciplineConfidence (25%), RequirementClarity (20%), EvidenceStrength (25%), RuleSpecificity (15%), DataCompleteness (10%), SourceTraceability (5%)
- Overall Score: weighted by confidence and status value
- Readiness Score: 5 factors — coverage (40%), evidence (25%), QA/QC (20%), drawings/specs (10%), sync freshness (5%)

### Report Generator
- `OwnerRequirementHtmlReportGenerator.cs` (2774 lines)
- Self-contained HTML with embedded CSS + JS
- Sections: Header, Filter Bar, Executive Summary, Discipline Allocation, Status/Urgency Legend, Key Issues, Issues by Urgency, Discipline Sections, Requirement Cards, Evidence & Traceability, Ask EMA AI, Report Notes
- Hidden JSON: `<script type="application/json" id="ema-ai-report-context">`
- Client-side filtering (discipline, status, urgency)
- Copy summary, copy Element IDs, PDF export

### Ask EMA AI Status
- External commands exist: `AskAboutReportCommand.cs`, `ExplainSelectedIssueCommand.cs`
- Determined via modeless tool window
- Provider architecture: Deterministic Fallback (required), Ollama (preferred), OpenRouter (optional), RAG/Qdrant (later)
- Local models: qwen3.6:35b (default), granite4.1:30b (fallback), gemma4:31b (alternative), bge-m3 (embeddings)
- See [docs/ai/ASK_EMA_AI_SPEC.md](ai/ASK_EMA_AI_SPEC.md)

### Backend/Frontend Status
- Backend FastAPI + PostgreSQL remains functional
- Frontend React/Vite dashboard remains functional
- Both are **optional intelligence layers** — not required for designer workflow

---

## 4. Important Design Decisions

| Decision | Status |
|----------|--------|
| Deterministic engine owns official status | Implemented |
| AI explains, does not decide compliance | Implemented |
| Report is human-readable + machine-readable | Implemented |
| Revit Element IDs required for traceability | Implemented |
| Methodology separate from formal report | Implemented |
| No-overclaim policy | Implemented |
| Local Ollama first; cloud AI optional | Implemented (partial) |
| Weak/mismatched evidence must not confidently produce Met | Implemented (semantic guardrail in engine) |
| Long traceability collapsed by default | Implemented |
| Dashboard is optional, designer workflow stays local | Implemented |

---

## 5. Current Known Risks

1. **Revit smoke status:** Build passes, but runtime validation in host Revit is pending
2. **Visual report QA:** Report rendering quality needs human review
3. **Row 100 / semantic false-positive risk:** Electrical manufacturer requirement should not be marked Met based on Mechanical Equipment + Level alone. The `ApplySemanticGuardrail()` method addresses this, but needs validation with real data.
4. **Ask EMA AI provider:** Local Ollama required for full functionality. Provider status depends on model availability.
5. **Large traceability overflow:** Many Element IDs per requirement may overflow the UI. Collapsed by default.
6. **PDF/print behavior:** Self-contained HTML report with `@media print` CSS, but PDF export quality needs validation.
7. **Client data handling:** Real XLSX/RVT files must not be committed.

---

## 6. Current Commands

### Tests
```powershell
cd Pipeline\pipeline
py -3.12 -m pytest tests -v
```

### Revit Build
```powershell
msbuild EMAExtractor/EMAExtractor.csproj /p:RevitYear=2023 /p:Platform=x64
msbuild EMAExtractor/EMAExtractor.csproj /p:RevitYear=2024 /p:Platform=x64
```

### Installer
```powershell
# See scripts/install-ema-addin.ps1
```

### Local Ollama
```powershell
ollama list
# Expected: gemma4:31b, qwen3.6:35b, granite4.1:30b, bge-m3
```

### Git Status
```powershell
git status --short
git diff --name-only
```

---

## 7. Do / Do Not

### Do
- Keep Owner Requirements Readiness as core story
- Preserve hidden JSON (`#ema-ai-report-context`)
- Preserve full detail in report (evidence, validation type, rule applied, reasoning, next action)
- Preserve Revit Element IDs for traceability
- Keep no-overclaim boundary explicit

### Do Not
- Make AI official compliance judge
- Expose raw JSON (keep hidden)
- Commit artifacts (`.exe`, `.zip`, `.log`, `bin/`, `obj/`, `dist/`)
- Show "undefined" or "null" in visible report
- Overclaim compliance ("certified", "approved", "guaranteed", "legally compliant")
- Center KGE/SEION/RAG in current demo
