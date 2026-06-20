# EMA AI — Roadmap

**Branch:** `docs/project-reference-reconciliation`  
**Validated product branch:** `feat/revit-first-owner-requirement-checker` (audited commit `ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e`)  
**Last updated:** 2026-06-15

---

## Phase 0: Demo Readiness (Current)

| Status | Item |
|--------|------|
| ✅ | Revit workflow (ribbon, panel, selection dialog) |
| ✅ | Excel parser |
| ✅ | Deterministic comparison engine |
| ✅ | EvidenceIndex |
| ✅ | Validation type classification |
| ✅ | Confidence scoring |
| ✅ | Score calculation |
| ✅ | Key issue ranking |
| ✅ | HTML report generator |
| ✅ | Executive Summary, Discipline Allocation, Key Issues |
| ✅ | Requirement cards with evidence, validation type, rule, alignment |
| ✅ | Revit Element ID traceability (collapsed by default, copyable) |
| ✅ | Hidden machine-readable JSON (`#ema-ai-report-context`) |
| ✅ | Discipline-specific filtering and navigation |
| ✅ | Status / Urgency legends |
| ✅ | Print CSS |
| ✅ | No-overclaim language |
| 🟡 | Ask EMA AI preview (basic implementation) |
| 🟡 | Report visual QA |

## Phase 1: Pilot Hardening (Next 2-3 Weeks)

| Priority | Item |
|----------|------|
| P0 | Revit smoke test in host Revit |
| P0 | Report visual polish and consistency |
| P1 | Performance optimization for large models |
| P1 | Installer validation on clean Revit machines |
| P1 | Report refinement — discipline tuning, edge cases |
| P1 | Semantic false-positive reduction (Row 100 / cross-discipline guardrail validation) |
| P1 | Element ID click-to-select in Revit |
| P2 | PDF export quality improvement |

## Phase 2: Ask EMA AI Full Assistant (Next 3-4 Weeks)

| Priority | Item |
|----------|------|
| P0 | Revit-native chat panel |
| P1 | Response formatting with citations and Element IDs |
| P1 | Provider chain hardening (Ollama → deterministic fallback) |
| P1 | Response quality improvement |
| P2 | Confidence indicators in AI responses |
| P2 | Suggested questions per discipline |

## Phase 3: Cloud / Deployment Alignment (Future)

| Priority | Item |
|----------|------|
| P2 | Azure resource group setup |
| P2 | Database hosting (PostgreSQL Flexible) |
| P2 | Storage (Data Lake Gen2) |
| P3 | Logging and monitoring |
| P3 | Security review and hardening |

## Phase 4: RAG / Qdrant (Future)

| Priority | Item |
|----------|------|
| P3 | Requirements semantic search |
| P3 | Specification content search |
| P3 | Drawing/sheet metadata search |
| P3 | Report and issue history |

## Phase 5: Graph / KGE (Future)

| Priority | Item |
|----------|------|
| P4 | Knowledge graph for requirement → evidence relationships |
| P4 | SEION advisory layer |
| P4 | Not blocking current demo |

---

## Key Principles

- **KGE/Graph/SEION are not blocking current demo**
- Deterministic engine remains source of truth
- AI explains, does not decide
- Dashboard/backend sync are optional
- Designer workflow stays in Revit, local, and offline-capable
