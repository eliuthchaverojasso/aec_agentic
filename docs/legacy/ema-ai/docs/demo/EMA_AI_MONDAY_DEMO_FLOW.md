# EMA AI Monday Demo Flow

## Primary Demo Flow

```
Revit
→ EMA AI Panel
→ Load Owner Requirements
→ Sync Model Data
→ Run Compliance Check
→ HTML/PDF Report
→ Ask EMA AI (optional assistant)
```

## Ribbon Structure

```
EMA AI
├── Owner Requirements Check
│   ├── Load Owner Requirements
│   ├── Run Compliance Check
│   ├── Open Last Report
│   ├── Export PDF
│   └── Copy Summary
├── Reports
│   └── Open Last Report
└── Support
    ├── Settings
    ├── Diagnostics
    └── Open Dashboard
```

## Demo Steps

### Step 1: Load Owner Requirements
- Click **Load Owner Requirements** in the EMA AI panel
- Select the Owner Requirements workbook (XLSX/XLSM)
- System parses worksheets, detects disciplines, counts actionable requirements
- Select focused discipline (All, Electrical, Lighting, etc.)
- Select scope (Entire Model or Current View)
- Confirm loaded count and detected disciplines

### Step 2: Sync Model Data
- Click **Sync Model Data**
- System captures model snapshot from active Revit document
- Extracts elements, categories, families, parameters
- Stores evidence index for comparison
- Confirms element count and sync status

### Step 3: Run Compliance Check
- Click **Run Compliance Check**
- Deterministic engine processes requirements against model evidence
- Generates professional HTML report
- Opens report in browser automatically
- Copies summary to clipboard

### Step 4: Review Report
- Management Overview with metric cards
- Discipline Allocation table
- Key Issues & Recommended Actions
- Requirement-by-Requirement Review
- Evidence & Traceability
- Readiness Score & Coverage

### Step 5: Export & Share
- **Export PDF**: Opens print-ready HTML for browser Print to PDF
- **Copy Summary**: Pastes summary into Teams/email
- **Ask EMA AI**: Optional chat with report data

## Demo Project

- **Project**: MEP-NISD-MIDDLE SCHOOL 8
- **Client**: Northwest ISD
- **Milestone**: DD 30%
- **Workbook**: NORTHWEST ISD 06.02.2025.xlsx
- **Discipline**: Electrical (focused) / All Disciplines
- **Model Elements**: 21,868

## Key Talking Points

1. Simple 3-step workflow inside Revit
2. No dependency on external AI, cloud, or Docker
3. Deterministic, traceable results
4. Professional report with management overview
5. Each requirement has evidence, reasoning, and next action
6. Key Issues are elevated by intelligence logic, not just failures
7. Filtered deliverables by discipline/role
8. AI Assistant explains but never approves compliance

## No-Overclaim Boundary

> This report is an AI-assisted first-pass model evidence review.
> It does not certify code compliance, contract compliance, or professional approval.
> Final validation remains subject to engineering review, drawings, specifications, and owner acceptance.
