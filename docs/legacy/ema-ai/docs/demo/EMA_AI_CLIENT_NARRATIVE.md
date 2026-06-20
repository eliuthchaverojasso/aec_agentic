# EMA AI — Client Narrative

**Audience:** Executive / Client stakeholders  
**Last updated:** 2026-06-15

---

## One Sentence

**EMA AI helps EMA know whether they are meeting Owner Requirements by project, discipline, and deliverable stage.**

---

## The Problem

Engineering teams at EMA spend hours before every deliverable submission manually checking Owner Requirements against their Revit models. Evidence is scattered across Excel workbooks, PDF specifications, drawing sheets, and Revit views. There is no single view that answers the question:

> "Are we meeting our Owner Requirements, and if not, what needs to be fixed?"

---

## Core Sentence

> **Owner Requirements → Evidence → Status → Reasoning → Next Action.**

Every requirement is traced from the owner's workbook through the Revit model or other evidence sources, assigned a clear status (Met / Not Met / Needs Human Review), explained with reasoning, and given an actionable next step — with Revit Element IDs so you can find and fix the gap directly in the model.

---

## How It Works

1. **Load Owner Requirements** — Select your workbook (`.xlsx`). EMA AI parses 800+ requirements in seconds.
2. **Sync Model Data** — Capture your current Revit model state. 20,000+ elements indexed with all parameters.
3. **Run Compliance Check** — The deterministic engine evaluates every requirement against model evidence. Results in under a minute.
4. **Review the Report** — A self-contained HTML report shows:
   - Executive Summary with overall scores
   - Discipline-by-discipline breakdown
   - Key Issues with reasoning and next actions
   - Requirement-by-requirement detail with evidence
   - Element IDs for every finding
5. **Ask EMA AI** — Ask any question about the report. Get answers with citations.

---

## What Makes EMA AI Different

| Aspect | EMA AI |
|--------|--------|
| **Source of truth** | Deterministic engine — not AI black box |
| **Traceability** | Every finding links to Revit Element IDs |
| **Actionability** | Every gap has a recommended next action and responsible discipline |
| **Explainability** | Status, confidence, validation type, rule, alignment — all explained |
| **Transparency** | Clear about what is found, what is missing, and what needs human review |

---

## Demo Story

> "Let me show you the NISD Middle School project. We loaded 804 Owner Requirements from the workbook and ran them against the Revit model. The current verified totals are 55 Met, 250 Not Met, and 499 needing Human Review — with an evidence review score of 20.1%. The Electrical section still has traceable gaps, each with evidence, reasoning, and Element IDs so the discipline lead can jump straight into the model and fix the issues."

---

## Current Status

- Revit add-in with full Owner Requirements workflow
- Self-contained HTML report with Executive Summary, Discipline Allocation, Key Issues, full requirement detail, Element ID traceability
- Ask EMA AI assistant with local Ollama (qwen3.6:35b)
- NISD Middle School pilot-ready dataset
- Deterministic engine is the source of truth — AI explains, does not decide

---

## Next Steps

- Pilot validation with live Revit model
- Report visual polish and PDF export
- Ask EMA AI full implementation with Revit-native chat
- Extended pilot with additional projects and disciplines

---

## No-Overclaim Statement

EMA AI is an AI-assisted first-pass model evidence review tool. It is not a final compliance certification. All findings should be reviewed by a qualified professional before any compliance or deliverable decision. "Met" status indicates that model evidence suggests the requirement is addressed, but does not guarantee compliance with all applicable codes, standards, or contractual obligations.
