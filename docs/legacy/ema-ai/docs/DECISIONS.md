# EMA AI — Architecture Decision Records

**Branch:** `docs/project-reference-reconciliation`  
**Validated product branch:** `feat/revit-first-owner-requirement-checker` (audited commit `ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e`)  
**Last updated:** 2026-06-15

---

## ADR-001: Deterministic Engine Owns Official Status

**Status:** Implemented

**Decision:** The deterministic engine (`RequirementComparisonEngine`, `ConfidenceScorer`, `ScoreCalculator`) is the sole source of truth for requirement status assignment. AI/LLM outputs do not mutate official statuses.

**Reason:** Status must be explainable, repeatable, and verifiable. An LLM black box cannot provide deterministic audit trail.

**Consequence:** All status changes go through deterministic comparison logic. AI is relegated to explanation and suggestion only.

---

## ADR-002: AI Explains Report, Does Not Assign Compliance

**Status:** Implemented

**Decision:** Ask EMA AI operates on the report context only. It may explain findings, summarize, and provide Element IDs. It must not change statuses, certify compliance, or replace engineering review.

**Reason:** Regulatory and contractual boundaries require human accountability.

**Consequence:** System prompt explicitly forbids overclaim. Provider chain degrades gracefully when LLM unavailable.

---

## ADR-003: Report is Human-Readable and Machine-Readable

**Status:** Implemented

**Decision:** The report contains both a visible HTML layer (for humans) and a hidden JSON block `<script id="ema-ai-report-context">` (for AI processing).

**Reason:** Humans need readable report. AI needs structured data for Q&A. Both must describe the same truth.

**Consequence:** Report generation produces self-contained HTML with embedded CSS/JS and JSON context. No external API calls needed for basic review.

---

## ADR-004: Revit Element IDs Required for Traceability

**Status:** Implemented

**Decision:** Every evidence match must include the supporting Revit Element IDs. The report displays them collapsed by default with copyable text.

**Reason:** Engineers must be able to trace from report finding to model element.

**Consequence:** Element IDs are collected during evidence matching and persisted in report context. UI has "Copy Element IDs" and (in Revit) "Select Elements by ID".

---

## ADR-005: Methodology Separate from Formal Report

**Status:** Implemented

**Decision:** The methodology document (`OWNER_REQUIREMENTS_ENGINE.md`) explains how the engine works. The report itself includes explainability blocks (Validation Type, Rule Applied, Evidence Alignment, Reasoning) but does not document the full methodology.

**Reason:** Users need explainability inline. Developers and auditors need full methodology separately.

**Consequence:** Report includes concise explainability. Methodology document covers full pipeline, formulas, and no-overclaim policy.

---

## ADR-006: Discipline Colors Separate from Status Colors

**Status:** Implemented

**Decision:** Discipline colors (Electrical=purple, Lighting=amber, etc.) are independent of status colors (Met=green, Not Met=red, Review=amber).

**Reason:** Combining discipline and status into a single color would lose information.

**Consequence:** Report uses discipline badges for navigation and status badges for status, visually separate.

---

## ADR-007: Weak/Mismatched Evidence Must Not Produce Met

**Status:** Implemented

**Decision:** The `ApplySemanticGuardrail()` method downgrades Met to NeedsHumanReview when evidence alignment is Weak or MismatchRisk.

**Reason:** A Mechanical Equipment match for an Electrical requirement should not confidently produce Met.

**Consequence:** Semantic guardrail reduces false positives. Known risk requires validation with real data.

---

## ADR-008: Long Traceability Must Be Collapsed by Default

**Status:** Implemented

**Decision:** Element ID sections in requirement cards are collapsed by default. Preview shows count + first 3-5 IDs. Expand to full list.

**Reason:** Requirements with 50+ Element IDs would overflow the UI.

**Consequence:** JS toggle for expand/collapse. Copy Element IDs button copies full list regardless of collapse state.

---

## ADR-009: Local Ollama First; Cloud AI Optional

**Status:** Implemented (partial)

**Decision:** Default AI provider is local Ollama. Cloud providers (OpenRouter, OpenCode) are optional and require environment variable configuration.

**Reason:** Demo must work offline. Cloud AI keys cannot be guaranteed in all environments.

**Consequence:** Provider chain: Deterministic Fallback → Ollama → OpenRouter → (future RAG). Each level degrades gracefully.

---

## ADR-010: No-Overclaim Language

**Status:** Implemented

**Decision:** The report and all documentation use explicit no-overclaim language. "Met" means AI-assisted first-pass model evidence review, not final compliance.

**Banned terms:** certified, approved, guaranteed, legally compliant

**Reason:** Legal and reputational risk from overclaiming compliance.

**Consequence:** Report Notes section includes disclaimer. Test validates absence of banned words. All docs maintain consistent language.

---

## ADR-011: Owner Requirements Readiness is Primary Focus (This Branch)

**Status:** Implemented

**Decision:** The current product direction is Revit-first Owner Requirements Readiness. Backend dashboard is optional intelligence layer.

**Reason:** Designer workflow starts in Revit, not a web dashboard. Local deterministic report is more valuable than cloud dashboard for day-to-day requirement checking.

**Consequence:** Revit add-in contains the full deterministic engine, report generator, and Ask EMA AI. Backend/frontend are de-emphasized in current documentation.

---

## ADR-012: Dashboard is Optional Intelligence Layer

**Status:** Implemented

**Decision:** The FastAPI backend + React dashboard are optional. Designers can complete the full workflow without ever opening a browser dashboard.

**Reason:** Reduces friction for Revit users. Keeps core workflow offline-capable.

**Consequence:** If run submission is added later, it must remain opt-in. Dashboard sync is never required for the local checker.
