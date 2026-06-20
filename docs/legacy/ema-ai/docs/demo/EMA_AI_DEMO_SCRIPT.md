# EMA AI — Demo Script

**Audience:** Paul / Broadleaf / EMA  
**Branch:** `docs/project-reference-reconciliation`  
**Validated product branch:** `feat/revit-first-owner-requirement-checker` (audited commit `ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e`)  
**Last updated:** 2026-06-15

---

## Setup

```powershell
# Ensure Ollama is running with models
ollama list
# Should show: qwen3.6:35b, granite4.1:30b, gemma4:31b, bge-m3

# Open Revit with the NISD Middle School model
# Revit 2023 or 2024
```

---

## Demo Steps

### 1. Open Revit → EMA AI Workflow
- Revit opens with NISD Middle School model loaded
- Switch to "EMA AI" ribbon tab
- "Compare Owner Requirements" button visible alongside "Open EMA AI Panel"

### 2. Load Requirements
- Click "Compare Owner Requirements"
- Selection dialog appears
- Browse to: `NORTHWEST ISD 06.02.2025.xlsx` (804 requirements)
- Select discipline scope (or "All Disciplines")
- Click "Load Requirements"
- Excel parser populates requirement rows

### 3. Sync Model Data
- Click "Sync Model Data"
- Revit snapshot captures current model state
- Progress indicator shows element processing
- 21,868 elements indexed into EvidenceIndex

### 4. Run Compliance Check
- Click "Run Compliance Check"
- Progress window shows stages:
  - Building EvidenceIndex
  - Evaluating requirements (parallel)
  - Scoring confidence
  - Ranking key issues
  - Generating report
- Expected runtime: < 60 seconds for 804 requirements × 21,868 elements

### 5. Open Report
- Report opens automatically in default browser
- Self-contained HTML with embedded CSS and JS

### 6. Show Executive Summary
- KPI cards at top:
  - Total: 804, Met: 55, Not Met: 250
  - Needs Human Review: 499
  - Evidence Review Score: 20.1%
  - Use the live report values for any filter-specific tile counts and score-scale caveats

### 7. Show Discipline Allocation
- Grid showing Electrical / Lighting / Mechanical / Plumbing / Technology
- Each row with Met / Not Met / Review / NA counts and score
- Color-coded discipline badges

### 8. Show Key Issues
- Top 5 key issue cards
- Each card shows: Issue Title, Status, Discipline, Responsible Role
- Evidence Summary, Reasoning, Next Best Action
- Confidence + Key Issue Score
- Severity label (Critical/High/Medium/Low)

### 9. Jump to Electrical Section
- Click "Electrical" in Discipline Allocation or filter
- Filter banner updates to the selected discipline and recomputes the visible counts
- Discipline-specific section with colored header

### 10. Show Requirement Card
- Click a requirement to expand detail
- Shows:
  - **Status** with confidence
  - **Evidence Found:** matched categories, families, parameters
  - **Validation Type:** Model / Drawing / Spec / Manual / Hybrid
  - **Rule Applied:** rule name, family, trigger keywords
  - **Evidence Alignment:** Strong / Partial / Weak / MismatchRisk
  - **Reasoning:** why status was assigned
  - **Next Best Action:** what to do
  - **Element IDs:** collapsed with preview

### 11. Show Revit Element IDs
- Click "Show Element IDs" on a requirement card
- Full list of Element IDs displayed
- Click "Copy Element IDs" — IDs copied to clipboard
- "Select Elements by ID" in Revit (if available)

### 12. Show Ask EMA AI / Report Assistant
- "Ask about this report" input at bottom
- Pre-populated suggested questions
- Type: "What are the top issues in Lighting?"
- Response shows Requirement IDs and Element IDs
- Verify: response declines to certify compliance
- Verify: response cites only report context

### 13. Close
- Report is self-contained — can be saved as HTML
- PDF export via browser print
- Last report path saved in local settings
- Close Revit or continue to next workflow

---

## Key Talking Points

- "EMA AI helps us know whether we are meeting Owner Requirements before we submit."
- "The deterministic engine is the source of truth — AI only explains the results."
- "Every finding has Element IDs so you can trace back to the actual model elements."
- "This is a first-pass model evidence review, not final compliance."
- "Discipline leads get their own section with actionable next steps."

---

## No-Overclaim Boundary

- "This is an AI-assisted first-pass model evidence review."
- "Not a compliance certification."
- "All findings should be reviewed by a qualified professional."
- "Ask EMA AI explains the report — it does not assign official statuses."

---

## Fallback

If report generation fails:
- Check workbook format
- Check model is loaded in Revit
- Check output folder is writable
- Check Antivirus is not blocking browser launch
