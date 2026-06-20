# Owner Requirements Report Specification

**Last updated:** 2026-06-08

---

## 1. Report Purpose

The Owner Requirements Report is the primary output of the EMA AI compliance check. It communicates to each discipline whether requirements are met, what evidence supports the finding, and what action to take next. It is both human-readable and machine-readable.

---

## 2. Audiences

| Audience | Needs | Section |
|----------|-------|---------|
| Project Manager | Overall status, key issues, discipline scores | Executive Summary, Key Issues, Discipline Allocation |
| Discipline Lead | Requirement-by-requirement detail, evidence, Element IDs | Discipline Sections, Requirement Cards, Traceability |
| BIM/VDC Coordinator | Model evidence quality, parameter completeness | Evidence Found, Missing Evidence, Evidence Alignment |
| Owner/Client | Readiness level, gap overview | Executive Summary, Key Issues, Report Notes |
| AI Assistant | Structured data for Q&A | Hidden Machine-Readable JSON |

---

## 3. Report Structure

```
1. HEADER — Report Identity (project name, date, version)
2. FILTER BAR — Discipline / Status / Urgency filter chips
3. ACTIVE FILTER CONTEXT — "Showing: Master View" or "Showing: Electrical"
4. EXECUTIVE SUMMARY — KPI cards (total, met, not met, needs review, score)
5. DISCIPLINE ALLOCATION — Per-discipline grid with status breakdowns
6. STATUS / URGENCY LEGEND — Color-coded legend (Met, Not Met, Needs Review, etc.)
7. TOP OVERALL ISSUES — Key issue cards with reasoning
8. ISSUES BY URGENCY — Grouped by Critical/High/Medium/Low
9. DISCIPLINE SECTIONS — One section per discipline with requirement cards
10. REQUIREMENT-BY-REQUIREMENT REVIEW — Detailed cards per requirement
11. EVIDENCE & TRACEABILITY — Evidence, validation type, rule, alignment, Element IDs
12. ASK EMA AI — Suggested questions and chat interface
13. REPORT NOTES — No-overclaim disclaimer, methodology reference
14. HIDDEN MACHINE-READABLE JSON — Structured data for AI
```

---

## 4. Executive Summary Specification

Layout: Row of KPI metric cards

| Metric | Description |
|--------|-------------|
| Total Requirements | Total applicable requirements |
| Met | Count and percentage |
| Not Met | Count and percentage |
| Needs Human Review | Count and percentage |
| Insufficient Model Data | Count and percentage |
| Not Applicable | Count and percentage |
| Overall Score | Weighted score (0-100%) |
| Readiness Score | Multi-factor readiness (0-100%) |
| Key Issues | Number of identified key issues |
| Disciplines Impacted | Count of impacted disciplines |

---

## 5. Active Filter Context

- **Master View:** "Showing all {N} requirements across {M} disciplines"
- **Discipline View:** "Showing {N} {Discipline} requirements"
- **Status Filter:** "Filtered by: {Status}"
- **Urgency Filter:** "Filtered by: {Urgency}"

---

## 6. Master vs Filtered View

- Master view = all requirements, all disciplines
- Filtered view = subset by discipline/status/urgency
- Both views maintain the same report structure
- Filter banner updates dynamically via JS

---

## 7. Discipline Allocation Specification

Grid showing per-discipline:

| Discipline | Met | Not Met | Review | Insufficient | NA | Total | Score |
|------------|-----|---------|--------|-------------|----|-------|-------|
| Electrical | 85 | 42 | 98 | 0 | 0 | 225 | 51.3% |
| Lighting | 62 | 31 | 65 | 0 | 0 | 158 | 52.8% |
| ... | ... | ... | ... | ... | ... | ... | ... |

Each discipline name uses its designated color.
Each discipline card links to that discipline's section.

---

## 8. Status / Urgency Legends

### Status Colors
| Status | Color | CSS Class |
|--------|-------|-----------|
| Met | Green (`#16a34a`) | `status-met` |
| Not Met | Red (`#dc2626`) | `status-not-met` |
| Needs Human Review | Amber (`#d97706`) | `status-needs-review` |
| Insufficient Model Data | Slate (`#64748b`) | `status-insufficient` |
| Not Applicable | Gray (`#9ca3af`) | `status-na` |

### Urgency Colors
| Urgency | Color | CSS Class |
|---------|-------|-----------|
| Critical | Red | `urgency-critical` |
| High | Orange | `urgency-high` |
| Medium | Yellow | `urgency-medium` |
| Low | Gray | `urgency-low` |

---

## 9. Key Issue Cards

Each key issue card shows:

- **Rank** (1, 2, 3...)
- **Issue Title** (one-line summary)
- **Status** (Not Met / Needs Human Review)
- **Discipline** (linked to section)
- **Responsible Role** (who owns the gap)
- **Requirement ID** / Source / Row
- **Evidence Summary** (one-line)
- **Reasoning** (why it's a key issue)
- **Next Best Action** (actionable guidance)
- **Confidence** (percentage)
- **Key Issue Score** (percentage)
- **Severity** (Critical/High/Medium/Low/Info)

---

## 10. Requirement Cards

Each requirement card shows:

| Field | Content |
|-------|---------|
| Requirement ID | Short identifier |
| Requirement Text | Full text from workbook |
| Status | Met / Not Met / Needs Human Review / Insufficient / NA |
| Discipline | Color-coded badge |
| Row Number | Source workbook row |
| Source File | Workbook filename |
| Category | Category tag |
| Urgency | Critical/High/Medium/Low label |

### Expandable Detail Section

#### Evidence Found
- Matched categories (comma-separated)
- Matched families/types (comma-separated)
- Matched parameter values (with examples)
- Inspected element count
- Evidence strength (Strong / Partial / Weak)

#### Validation Type
- Primary type (Model / Drawing / Spec / Manual / Hybrid)
- Secondary types (if hybrid)
- Type confidence
- Reasoning text

#### Rule Applied
- Rule name
- Rule family
- Trigger keywords matched
- Expected evidence categories
- Expected parameters

#### Evidence Alignment
- Alignment level (Strong / Partial / Weak / MismatchRisk / ManualOnly)
- Alignment reason text

#### Reasoning
- Full reasoning text explaining status assignment

#### Next Best Action
- Action text (model fix, spec review, manual review, etc.)

#### Revit Element IDs
- **Collapsed by default** with preview showing count + first 3-5 IDs
- "Copy Element IDs" button (JavaScript click handler)
- Full list shown on expand

---

## 11. Machine-Readable JSON Schema

Embedded as: `<script type="application/json" id="ema-ai-report-context">`

```json
{
  "schema_version": "1.0",
  "report_metadata": {
    "project_name": "string",
    "project_number": "string",
    "report_date": "ISO8601",
    "generator_version": "string",
    "data_hash": "string"
  },
  "summary": {
    "total_requirements": 804,
    "met": 322,
    "not_met": 159,
    "needs_human_review": 323,
    "insufficient_model_data": 0,
    "not_applicable": 0,
    "overall_score": 53.3,
    "readiness_score": 49.0,
    "key_issue_count": 23,
    "disciplines_impacted": 5
  },
  "key_issues": [
    {
      "rank": 1,
      "issue_title": "string",
      "status": "string",
      "discipline": "string",
      "responsible_role": "string",
      "requirement_id": "string",
      "source_file": "string",
      "source_worksheet": "string",
      "source_row": 0,
      "evidence_summary": "string",
      "reasoning": "string",
      "next_best_action": "string",
      "confidence": 0.0,
      "key_issue_score": 0.0,
      "severity": "string",
      "element_ids": [0, 0, 0]
    }
  ],
  "requirement_results": [
    {
      "requirement_id": "ORS-001",
      "row_number": 5,
      "discipline": "Electrical",
      "requirement_text": "string",
      "status": "Met",
      "confidence": 0.87,
      "validation_type": "Model",
      "validation_type_reason": "string",
      "rule_applied": {
        "name": "Panel Circuit Rule",
        "family": "Electrical Distribution",
        "trigger_keywords": ["panel", "circuit"],
        "expected_categories": ["Electrical Equipment"],
        "expected_parameters": ["Panel", "Circuit"]
      },
      "evidence_found": {
        "matched_categories": ["Electrical Equipment"],
        "matched_families": ["Panelboard"],
        "matched_parameter_values": {"Panel": "DP-1", "Circuit": "1"},
        "inspected_count": 15,
        "evidence_strength": "Strong"
      },
      "missing_evidence": {
        "missing_parameters": [],
        "missing_sources": []
      },
      "evidence_alignment": "Strong",
      "evidence_alignment_reason": "string",
      "reasoning": "string",
      "next_best_action": "string",
      "matched_element_ids": [12345, 12346],
      "element_id_copy_text": "12345, 12346"
    }
  ],
  "ai_lookup_hints": {
    "discipline_colors": {},
    "status_order": [],
    "urgency_order": [],
    "anchors": {
      "electrical": "#discipline-electrical",
      "lighting": "#discipline-lighting"
    }
  }
}
```

---

## 12. Ask EMA AI Section

- Suggested questions per discipline (e.g., "What are the top issues in Electrical?")
- Chat input field (if assistant widget is loaded)
- Responses include citations to specific requirement IDs and Element IDs
- See [docs/ai/ASK_EMA_AI_SPEC.md](../ai/ASK_EMA_AI_SPEC.md)

---

## 13. PDF / Print Requirements

- `@media print` CSS hides filter panel and non-essential interactive elements
- `break-inside: avoid` on requirement cards
- `color-adjust: exact` to preserve status colors
- Print styles for report identity header on each page
- Full-width layout for print

---

## 14. Accessibility Requirements

- High-contrast status and urgency classes
- Focus-visible styles on all interactive elements
- Semantic heading hierarchy (h1-h3)
- Color not the only indicator of status (text labels present)
- `aria-label` on icon-only buttons

---

## 15. No-Overclaim Language

**Required disclaimer in Report Notes section:**

> This report is an AI-assisted first-pass model evidence review. It is not a final compliance certification. All findings should be reviewed by a qualified professional before any compliance or deliverable decision. "Met" status indicates that model evidence suggests the requirement is addressed, but does not guarantee compliance with all applicable codes, standards, or contractual obligations.

---

## 16. Visual QA Checklist

- [ ] Executive Summary metrics are coherent (total = sum of all statuses)
- [ ] Discipline Allocation sums match Executive Summary totals
- [ ] Requirement cards show status, evidence, validation type, rule, reasoning, next action
- [ ] Element IDs visible with copy button
- [ ] Traceability collapsed by default, expands on click
- [ ] Filters work (discipline, status, urgency)
- [ ] Filter banner updates dynamically
- [ ] No "undefined" or "null" visible in rendered report
- [ ] No banned overclaim words
- [ ] Hidden JSON is valid and complete
- [ ] Print CSS hides filter panel
- [ ] All discipline sections have correct color coding
- [ ] Urgency labels are normalized (not mixed capitalization)
