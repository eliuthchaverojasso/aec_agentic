# EMA AI — Data Schema Reference

**Last updated:** 2026-06-08

---

## Core Models

### OwnerRequirementRow

| Field | Type | Description |
|-------|------|-------------|
| RowNumber | int | Source workbook row |
| SourceFile | string | Workbook filename |
| SourceSheet | string | Worksheet name |
| Discipline | string | Discipline (Electrical, Lighting, Mechanical, Plumbing, Technology) |
| RequirementId | string | Short identifier |
| RequirementText | string | Full requirement text |
| Category | string | Category tag |
| Status | string | Initial status (from workbook) |
| Columns | Dictionary | Flexible additional columns |

### ExportElementRecord

| Field | Type | Description |
|-------|------|-------------|
| ElementId | long | Revit Element ID |
| UniqueId | string | Revit Unique ID |
| Category | string | Revit category name |
| Family | string | Family name |
| Type | string | Type name |
| Level | string | Associated level |
| Parameters | Dictionary | Parameter name → value |

### EvidenceIndex

| Field | Type | Description |
|-------|------|-------------|
| CategoryIndex | Dictionary<string, List> | Category → elements (O(1) lookup) |
| SearchBlobs | Dictionary<long, string> | ElementId → pre-computed search text |
| AllElements | List | All elements for fallback search |

### RequirementCheckResult

| Field | Type | Description |
|-------|------|-------------|
| RequirementId | string | Requirement identifier |
| RowNumber | int | Source workbook row |
| Discipline | string | Discipline tag |
| RequirementText | string | Full requirement text |
| Status | enum | Met, NotMet, NeedsHumanReview, InsufficientModelData, NotApplicable |
| Confidence | double | 0.0–1.0 |
| ValidationType | string | Model, Drawing, Specification, Manual, Hybrid |
| ValidationTypeReason | string | Why this type was assigned |
| RuleApplied | RuleContext | Rule name, family, keywords, expectations |
| EvidenceFound | EvidenceSummary | Matched categories, families, parameters, Element IDs |
| EvidenceAlignment | enum | Strong, Partial, Weak, MismatchRisk, ManualOnly |
| EvidenceAlignmentReason | string | Why this alignment level |
| MissingEvidence | MissingEvidenceDetail | Missing parameters/sources |
| Reasoning | string | Status rationale |
| NextBestAction | string | Recommended action |
| ResponsibleRole | string | Who should act |
| IssueTitle | string | Short issue summary |
| IsKeyIssue | bool | Whether this is a key issue |
| KeyIssueScore | double | 0.0–1.0 (if key issue) |
| Urgency | enum | Critical, High, Medium, Low |
| MatchedElementIds | List<long> | Revit Element IDs supporting finding |
| MatchedElements | List<MatchedElementEvidence> | Full element detail |
| SourceFile | string | Source workbook filename |
| SourceWorksheet | string | Source worksheet name |
| Category | string | Category tag |

### MatchedElementEvidence

| Field | Type | Description |
|-------|------|-------------|
| ElementId | long | Revit Element ID |
| UniqueId | string | Revit Unique ID |
| Category | string | Revit category |
| Family | string | Family name |
| Type | string | Type name |
| Level | string | Associated level |
| MatchedParameters | List<string> | Parameters that matched |
| MissingParameters | List<string> | Expected parameters not found |
| ParameterValues | Dictionary | Actual parameter values found |

### RequirementCheckReport

| Field | Type | Description |
|-------|------|-------------|
| ProjectName | string | Project name |
| ProjectNumber | string | Project number |
| ReportDate | DateTime | Generation date |
| GeneratorVersion | string | Version string |
| DataHash | string | Hash of source data |
| Summary | RequirementCheckSummary | Aggregate counts |
| DisciplineSummaries | List<DisciplineSummary> | Per-discipline breakdown |
| Results | List<RequirementCheckResult> | All requirement results |
| KeyIssues | List<KeyIssue> | Ranked key issues |
| FilterContext | ReportFilterContext | Current filter state |
| SuggestedQuestions | List<string> | Discipline-specific AI questions |

### RequirementCheckSummary

| Field | Type |
|-------|------|
| TotalRequirements | int |
| Met | int |
| NotMet | int |
| NeedsHumanReview | int |
| InsufficientModelData | int |
| NotApplicable | int |
| OverallScore | double |
| ReadinessLabel | string |

### DisciplineSummary

| Field | Type |
|-------|------|
| Discipline | string |
| Total | int |
| Met | int |
| NotMet | int |
| NeedsHumanReview | int |
| Insufficient | int |
| NotApplicable | int |
| Score | double |

### KeyIssue

| Field | Type |
|-------|------|
| Rank | int |
| IssueTitle | string |
| Status | string |
| Discipline | string |
| ResponsibleRole | string |
| RequirementId | string |
| SourceFile | string |
| SourceWorksheet | string |
| SourceRow | int |
| EvidenceSummary | string |
| Reasoning | string |
| NextBestAction | string |
| Confidence | double |
| KeyIssueScore | double |
| Severity | enum (Critical, High, Medium, Low, Info) |
| Urgency | string |
| MatchedElementIds | List<long> |

---

## Hidden Report JSON Schema

Embedded in HTML as:

```html
<script type="application/json" id="ema-ai-report-context">
```

### Top-Level Structure

```json
{
  "schema_version": "1.0",
  "report_metadata": {
    "project_name": "string",
    "project_number": "string",
    "report_date": "ISO8601 datetime",
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
    "readiness_label": "AtRisk",
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
      "category": "Equipment",
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
        "expected_parameters": ["Panel", "Circuit"],
        "matched_keywords": ["panel", "circuit"]
      },
      "evidence_found": {
        "matched_categories": ["Electrical Equipment"],
        "matched_families": ["Panelboard"],
        "matched_types": ["Panelboard 208/120V"],
        "matched_parameter_values": {"Panel": "DP-1", "Circuit": "1"},
        "missing_expected_parameters": [],
        "inspected_count": 15,
        "evidence_strength": "Strong"
      },
      "missing_evidence": {
        "missing_parameters": [],
        "missing_sources": [],
        "not_captured": [],
        "empty_values": [],
        "mismatch_details": []
      },
      "evidence_alignment": "Strong",
      "evidence_alignment_reason": "string",
      "reasoning": "string",
      "next_best_action": "string",
      "responsible_role": "Electrical Engineer",
      "is_key_issue": false,
      "key_issue_score": 0.0,
      "urgency": "Low",
      "matched_element_ids": [12345, 12346],
      "element_id_copy_text": "12345, 12346"
    }
  ],
  "ai_lookup_hints": {
    "discipline_colors": {
      "Electrical": "#8b5cf6",
      "Lighting": "#f59e0b",
      "Mechanical": "#3b82f6",
      "Plumbing": "#10b981",
      "Technology": "#ec4899"
    },
    "status_order": ["Met", "Not Met", "Needs Human Review", "Insufficient Model Data", "Not Applicable"],
    "urgency_order": ["Critical", "High", "Medium", "Low"],
    "anchors": {
      "electrical": "#discipline-electrical",
      "lighting": "#discipline-lighting",
      "mechanical": "#discipline-mechanical",
      "plumbing": "#discipline-plumbing",
      "technology": "#discipline-technology"
    }
  }
}
```

---

## Scoring Models

### ConfidenceScore

| Field | Weight | Description |
|-------|--------|-------------|
| DisciplineConfidence | 25% | Evidence discipline matches requirement discipline |
| RequirementClarity | 20% | Requirement text clarity |
| EvidenceStrength | 25% | Quality/quantity of matched evidence |
| RuleSpecificity | 15% | Rule applicability |
| DataCompleteness | 10% | Parameters populated |
| SourceTraceability | 5% | Element IDs available |
| OverallScore | — | Weighted average |

### ReadinessMetrics

| Field | Weight | Description |
|-------|--------|-------------|
| RequirementCoverage | 40% | % of requirements with accepted evidence |
| EvidenceCoverage | 25% | % of evidence sources populated |
| QAQCHealth | 20% | QA/QC issue resolution rate |
| DrawingSpecCoverage | 10% | Drawing/spec evidence coverage |
| SyncFreshness | 5% | Time since last model sync |

### KeyIssueScore

| Field | Weight | Description |
|-------|--------|-------------|
| Severity | 30% | Gap severity |
| DisciplineRelevance | 20% | Discipline impact |
| DeliverableImpact | 20% | Impact on deliverables |
| EvidenceGap | 15% | Evidence missing |
| Confidence (inverse) | 10% | Low confidence = higher score |
| Actionability | 5% | How actionable the fix is |

---

## Validation Type Taxonomy

### Types

| Type | Description |
|------|-------------|
| Model | Revit model parameter/element check |
| Drawing | Sheet/DWG check |
| Specification | Spec section check |
| Manual | Human review required |
| Hybrid | Combination of above |

### Taxonomy Labels

| Label | Type |
|-------|------|
| equipment_presence | Model |
| parameter_performance | Model |
| manufacturer_standard | Specification |
| routing_location | Model/Drawing |
| sizing_capacity | Model/Spec |
| installation_method | Manual/Spec |
| material_finish | Specification |
| testing_commissioning | Manual |
| documentation_submittal | Manual |
| coordination_clearance | Drawing/Model |
| labeling_tagging | Model |
| access_clearance | Drawing/Model |
| fire_rating_protection | Specification/Model |

---

## Enums

### RequirementCheckStatus
- Met
- NotMet
- NeedsHumanReview
- NotApplicable
- InsufficientModelData

### EvidenceAlignmentLevel
- Strong
- Partial
- Weak
- MismatchRisk
- ManualOnly

### MissingEvidenceReason
- NotCaptured
- EmptyValue
- NotInExport

### IssueSeverity
- Critical
- High
- Medium
- Low
- Info

### ReadinessLabel
- Ready
- OnTrack
- AtRisk
- Behind
- Critical

### ValidationType
- Model
- Drawing
- Specification
- Manual
- Hybrid

### Urgency
- Critical
- High
- Medium
- Low
