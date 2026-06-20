# EMA AI — Demo Smoke Checklist

**Branch:** `docs/project-reference-reconciliation`  
**Last updated:** 2026-06-08

---

## Pre-Demo

- [ ] Revit 2023/2024 installed
- [ ] EMA AI add-in installed (see `scripts/install-ema-addin.ps1`)
- [ ] NISD Middle School model accessible
- [ ] Requirements workbook accessible (`NORTHWEST ISD 06.02.2025.xlsx`, 804 rows)
- [ ] Ollama running (`ollama list` shows qwen3.6:35b)
- [ ] Output folder writable

## Ribbon Verification

- [ ] "EMA AI" ribbon tab visible
- [ ] "Compare Owner Requirements" button present
- [ ] "Open EMA AI Panel" button present
- [ ] No missing icons or broken ribbon entries

## Workflow Smoke

- [ ] "Compare Owner Requirements" opens selection dialog
- [ ] Workbook loads without error
- [ ] Discipline selection shows available disciplines
- [ ] "Sync Model Data" completes
- [ ] "Run Compliance Check" starts
- [ ] Progress window shows stages
- [ ] Report opens automatically in browser
- [ ] Self-contained HTML (no external dependencies)

## Report Verification

- [ ] Executive Summary metric cards visible
- [ ] Total = Met + Not Met + Needs Review + Insufficient + NA
- [ ] Overall Score and Readiness Score displayed
- [ ] Discipline Allocation grid present
- [ ] Status Legend colors correct
- [ ] Urgency Legend colors correct
- [ ] Key Issues section populated
- [ ] Issues by Urgency grouped correctly

### Requirement Cards
- [ ] Requirement text visible
- [ ] Status badge displayed
- [ ] Discipline badge displayed
- [ ] Expandable detail works
- [ ] Evidence Found block present
- [ ] Validation Type block present
- [ ] Rule Applied block present
- [ ] Evidence Alignment shown
- [ ] Reasoning text present
- [ ] Next Best Action present
- [ ] Element ID traceability collapsed by default
- [ ] Element ID preview shows count + first IDs
- [ ] "Copy Element IDs" button works

### Machine-Readable JSON
- [ ] `#ema-ai-report-context` script tag present
- [ ] JSON is valid
- [ ] Summary counts match visible report
- [ ] Requirement results array populated
- [ ] Key issues match visible section

### No-Overclaim
- [ ] Report Notes section present
- [ ] No "certified", "approved", "guaranteed", "legally compliant" visible
- [ ] Disclaimer about professional review present

## Filter Verification

- [ ] Discipline filter chips work
- [ ] Status filter chips work
- [ ] Urgency filter chips work
- [ ] Filter banner updates dynamically
- [ ] Master view shows all requirements

## Coherence Checks

- [ ] Not all NA (would indicate parser failure)
- [ ] Not all Needs Human Review (would indicate missing evidence)
- [ ] Not all Met (would indicate overclaim)
- [ ] Multiple statuses present (Met, Not Met, Review)
- [ ] Discipline totals sum to overall totals
- [ ] No "undefined" or "null" visible

## Export / Summary

- [ ] "Copy Summary" button works
- [ ] Clipboard content is informative
- [ ] PDF via browser print works

## Ask EMA AI

- [ ] Suggested questions visible
- [ ] Question input present
- [ ] Question receives response
- [ ] Response cites Requirement IDs
- [ ] Response includes Element IDs
- [ ] Response declines to certify
- [ ] Without Ollama, deterministic fallback works

## Revit Integration

- [ ] "Select Elements by ID" works (if available)
- [ ] Last report path persisted in local settings
- [ ] EMA AI Panel shows last run summary
