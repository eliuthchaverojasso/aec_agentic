# Owner Requirements Engine — Methodology

**Last updated:** 2026-06-08

---

## Overview

The Owner Requirements Engine is a deterministic pipeline that transforms an Excel requirements workbook into a structured compliance report with per-requirement status, evidence traceability, and actionable insights.

The engine lives in `EMAExtractor/Requirements/` (C# .NET Framework 4.8).

---

## Pipeline

```
XLSX → Parser → Discipline Classifier → Validation Classifier → Evidence Matcher
→ Status Assignment → Confidence Scorer → Reasoning Generator → Next Best Action
→ Key Issue Ranker → Score Calculator → Report Generator
```

---

## 1. Requirement Parsing

- Reads `.xlsx` / `.xlsm` using Excel parser
- Maps columns by header name through canonical normalization
- Captures: RowNumber, Discipline, RequirementId, RequirementText, Category, SourceFile, SourceSheet
- Flexible `Columns` dictionary preserves additional workbook columns
- Normalizes discipline names (standardizes variations)

---

## 2. Discipline Normalization

Disciplines detected from workbook columns and requirement context:
- Electrical
- Lighting  
- Mechanical
- Plumbing
- Technology

Each requirement is tagged with its discipline for scope filtering and discipline-specific evaluation.

---

## 3. Validation Type Classification

Each requirement is classified by evidence source type:

| Type | Description | Example Keywords |
|------|-------------|-----------------|
| Model | Revit model parameter/element check | "provide", "installed", "shown", "coordinated" |
| Drawing | Sheet/DWG check | "detail", "notation", "drawn", "scheduled" |
| Specification | Spec section check | "specification", "compliant with", "per" |
| Manual | Human review required | "verify", "confirm", "review", "approve" |
| Hybrid | Combination of types | Multiple keyword categories score >= 0.25 |

**Taxonomy categories** (13): equipment_presence, parameter_performance, manufacturer_standard, routing_location, sizing_capacity, installation_method, material_finish, testing_commissioning, documentation_submittal, coordination_clearance, labeling_tagging, access_clearance, fire_rating_protection

**Classifier:** Keyword-based scoring per type. Hybrid detection when multiple types score >= 0.25.

---

## 4. Rule Dispatch

The engine routes requirements to specific rule evaluators based on keyword analysis:

| Rule | Trigger Keywords | Evaluates |
|------|-----------------|-----------|
| Panel/Circuit | panel, circuit, breaker, disconnect, switchboard | Elements with panel/ circuit parameter values |
| Electrical Load | load, demand, VA, watt, amp | Load parameter values (not zero/empty) |
| Lighting | lighting, luminaire, fixture, lamp | Lighting fixture presence, level assignment |
| Level | level, floor, story, elevation | Element level assignment |
| Mechanical | mechanical, HVAC, air, duct, pipe, equipment | Mechanical equipment presence, parameters |
| Plumbing | plumbing, plumbing fixture, pipe, drain, water | Plumbing fixture presence, parameters |
| Technology | technology, data, communication, low voltage, telecom | Technology equipment presence |

Each rule has a `RuleContext` with:
- Rule name
- Rule family
- Trigger keywords
- Expected evidence categories
- Expected parameters

---

## 5. Evidence Matching

### EvidenceIndex
Pre-built index over the Revit model snapshot for O(1) category lookups:

- **Category index:** Maps Revit categories to lists of elements
- **Search blobs:** Pre-computed text strings per element for fast keyword matching
- **Parameter values:** Extracted parameter values available for comparison

### Evidence Match Flow
1. Category pre-filtering via EvidenceIndex
2. Keyword text search against search blobs
3. Parameter value extraction and comparison
4. Element ID collection for traceability

### Evidence Strength Determination
- Number of matched elements
- Quality of parameter value matches
- Category relevance to requirement
- Parameter completeness

---

## 6. Status Assignment

| Status | Criteria |
|--------|----------|
| **Met** | Strong evidence match with high confidence. Required parameters present and populated. |
| **Not Met** | No relevant evidence found in model. Required categories missing or empty. |
| **Needs Human Review** | Partial/ambiguous evidence. Guardrail triggered. Model evidence exists but may be weak/mismatched. |
| **Insufficient Model Data** | Model does not contain the required categories or the export is incomplete. |
| **Not Applicable** | Requirement does not apply to this discipline/scope. |

### Semantic Guardrail
The `ApplySemanticGuardrail()` method prevents weak or mismatched model evidence from confidently producing Met:

- If evidence alignment is Weak or MismatchRisk → downgrade Met to NeedsHumanReview
- If evidence categories don't match requirement discipline → flag as mismatch risk
- If parameter evidence is incomplete or missing expected parameters → downgrade confidence

**Known risk example:** An Electrical requirement about "IDENTIFICATION OF EQUIPMENT" and acceptable manufacturers (W.H. Brady, Carlton, Seton) should NOT be marked Met based only on Mechanical Equipment + Level evidence. The semantic guardrail catches this case.

---

## 7. Evidence Alignment

| Level | Description |
|-------|-------------|
| **Strong** | Evidence categories directly match requirement discipline. Parameters populated. |
| **Partial** | Evidence exists but missing some expected parameters or categories. |
| **Weak** | Evidence categories are related but not directly matching. Low confidence. |
| **MismatchRisk** | Evidence categories belong to different discipline. High risk of false positive. |
| **ManualOnly** | Only manual review evidence available. |

---

## 8. Confidence Scoring

6 weighted factors:

| Factor | Weight | Evaluates |
|--------|--------|-----------|
| DisciplineConfidence | 25% | How well evidence discipline matches requirement discipline |
| RequirementClarity | 20% | How clearly the requirement text describes what to check |
| EvidenceStrength | 25% | Quantity and quality of matched evidence |
| RuleSpecificity | 15% | How well the matched rule applies to the requirement |
| DataCompleteness | 10% | Whether all expected parameters are populated |
| SourceTraceability | 5% | Whether evidence source can be traced (Element IDs) |

Overall Confidence = weighted average of all factors.

---

## 9. Score Calculation

### Overall Score
Weighted by status value and confidence:
- Met: 1.0 × Confidence
- Needs Human Review: 0.55 × Confidence
- Insufficient Model Data: 0.40 × Confidence
- Not Met: 0.0 × Confidence
- Not Applicable: Excluded from calculation

### Discipline Score
Filtered to single discipline, then overall score calculation.

### Readiness Score
Multi-factor readiness:
| Factor | Weight |
|--------|--------|
| Requirement Coverage | 40% |
| Evidence Coverage | 25% |
| QA/QC Health | 20% |
| Drawing/Spec Coverage | 10% |
| Sync Freshness | 5% |

**Readiness labels:** Ready, OnTrack, AtRisk, Behind, Critical

---

## 10. Key Issue Ranking

6 weighted criteria for key issue scoring:

| Factor | Weight |
|--------|--------|
| Severity of gap | 30% |
| Discipline relevance | 20% |
| Deliverable impact | 20% |
| Evidence gap severity | 15% |
| Confidence level (inverse) | 10% |
| Actionability | 5% |

Key issues are filtered to exclude Met and Not Applicable results, then ranked by score. Top issues become the "Key Issues & Recommended Actions" section.

**Issue Severity:** Critical, High, Medium, Low, Info

---

## 11. Explainability Blocks

Each requirement result includes:

### Validation Type + Reason
Why Model/Drawing/Spec/Manual/Hybrid was chosen (keyword analysis)

### Rule Applied
- Rule name
- Rule family
- Trigger keywords matched
- Expected evidence categories
- Expected parameters

### Evidence Found
- Matched categories
- Matched families/types
- Matched parameter values (with examples)
- Matched Element IDs
- Number of inspected elements
- Evidence strength label

### Missing Evidence
- Missing expected parameters
- Not captured vs empty vs mismatch

### Reasoning
- Status rationale
- Evidence limitations
- Human review boundary

### Next Best Action
- Model fix (which parameter/category to add)
- Spec/drawing review
- Parameter population
- Manual review
- No action required

---

## 12. No-Overclaim Policy

- "Met" means AI-assisted first-pass model evidence review suggests the requirement is likely met
- "Met" is not final compliance, certification, or legal approval
- Needs Human Review requires actual human verification
- The methodology report explains the limitations of model-only evidence
- Overclaim language is banned: "certified", "approved", "guaranteed", "legally compliant"

---

## Examples

### Example: Electrical Panel/Circuit Requirement
1. Input: "Provide panelboard with 208/120V, 3-phase, 4-wire"
2. Validation Type: Model
3. Rule: Panel/Circuit
4. Evidence: Electrical Equipment category, Panel family, panel parameter populated with "DP-1"
5. Status: Met
6. Confidence: 0.87
7. Evidence Alignment: Strong
8. Element IDs: [12345, 12346]

### Example: Cross-Discipline Mismatch (Row 100 Risk)
1. Input: "IDENTIFICATION OF EQUIPMENT — Manufacturer: W.H. Brady, Carlton, Seton"
2. Discipline: Electrical
3. Validation Type: Model
4. Evidence Found: Mechanical Equipment + Level
5. Status: Needs Human Review (downgraded from Met by semantic guardrail)
6. Evidence Alignment: MismatchRisk
7. Reasoning: "Evidence categories (Mechanical Equipment) don't match requirement discipline (Electrical). Manufacturer keywords found in Mechanical context."
8. Next Best Action: Manual review of Electrical equipment in model
