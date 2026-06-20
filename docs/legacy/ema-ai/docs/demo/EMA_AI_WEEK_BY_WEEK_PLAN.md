# EMA AI — Week-by-Week Plan

**Branch:** `docs/project-reference-reconciliation`  
**Last updated:** 2026-06-08

---

## Assumptions

- Revit add-in build pass is confirmed
- Deterministic engine is complete in source
- Report generator is complete in source
- Ask EMA AI has basic implementation
- Backend/frontend optional stack is functional
- Pilot timeline assumes ~6 weeks remaining

---

## Phase 0: Demo Validation (Current Week)

| Task | Owner | Status |
|------|-------|--------|
| Smoke Revit add-in in host Revit | Dev | Pending |
| Validate report generation with NISD data | Dev | Pending |
| Verify Executive Summary counts are coherent | QA | Pending |
| Verify discipline filtering works | QA | Pending |
| Verify Element ID traceability | QA | Pending |
| Run visual QA checklist on report | Design | Pending |
| Validate Ask EMA AI with Ollama | Dev | Pending |
| Validate no-overclaim language | PM | Pending |
| Fix any P0 bugs from smoke validation | Dev | Pending |

## Phase 1: Report + Ask EMA AI Hardening (Week 1)

| Task | Owner |
|------|-------|
| Polish report visual design — CSS consistency, spacing, typography | Frontend |
| Enhance Ask EMA AI response quality | Dev |
| Improve provider fallback chain (Ollama → deterministic) | Dev |
| Add suggested questions per discipline section | Dev |
| Fix any visual QA regressions | Frontend |
| Validate PDF export quality | QA |
| Validate print CSS behavior | QA |

## Phase 2: Logic + Workflow Hardening (Week 2)

| Task | Owner |
|------|-------|
| Run semantic guardrail validation with real data | Dev |
| Validate Row 100 risk (cross-discipline mismatch) | QA |
| Harden discipline detection for edge cases | Dev |
| Harden evidence strength calibration | Dev |
| Improve rule dispatch accuracy | Dev |
| Add progress reporting for long compliance checks | Dev |
| Validate parallel evaluation determinism | QA |
| Run performance benchmark (804 req × 21,868 elem) | QA |

## Phase 3: Pilot Handoff Preparation (Weeks 3-4)

| Task | Owner |
|------|-------|
| Installer validation on clean Revit machines | QA |
| Write user manual for Revit workflow | PM |
| Write quick-start guide for pilot users | PM |
| Validate with alternative project/model | QA |
| Validate with alternative requirements workbook | QA |
| Create pilot onboarding checklist | PM |
| Set up support channel for pilot users | PM |

## Phase 4: Ask EMA AI Full Implementation (Week 5)

| Task | Owner |
|------|-------|
| Implement Revit-native chat panel | Dev |
| Improve response formatting with citations | Dev |
| Add Element ID click-to-select in Revit | Dev |
| Validate with all disciplines (Elec, Light, Mech, Plumb, Tech) | QA |
| Add confidence indicator to AI responses | Dev |
| Performance optimize Ollama response time | Dev |

## Phase 5: Extended Validation (Week 6+)

| Task | Owner |
|------|-------|
| Run full regression suite | QA |
| Validate with larger model (50,000+ elements) | QA |
| Security review | Security |
| Documentation final review | PM |
| Pilot go/no-go decision | PM |

---

## Extended Plan (If 7-8 Weeks)

| Week | Focus |
|------|-------|
| Week 7 | Backend sync opt-in for management history |
| Week 7 | Dashboard integration for pilot reporting |
| Week 8 | PDF export full implementation |
| Week 8 | Pilot feedback incorporation |

---

## Key Milestones

- [ ] Revit smoke test passed
- [ ] Report visual QA signed off
- [ ] Semantic guardrails validated
- [ ] Ask EMA AI functional with local Ollama
- [ ] Pilot handoff package complete
- [ ] Pilot go/no-go decision
