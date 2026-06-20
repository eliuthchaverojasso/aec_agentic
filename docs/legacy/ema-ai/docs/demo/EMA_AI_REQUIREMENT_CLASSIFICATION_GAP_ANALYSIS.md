# EMA AI Requirement Classification Gap Analysis

Generated: 2026-06-08  
Baseline report: `Pipeline/pipeline/landing/EMA_AI_Requirement_Check_MEP-NISD-MIDDLE SCHOOL 8_All Disciplines_20260608_082009.html`

## Baseline Snapshot

- Requirements: 804
- Model elements reviewed: 21,868
- Status counts: 142 Met, 662 Needs Human Review, 0 Not Met, 0 Insufficient Model Data, 0 Not Applicable
- Validation type counts: 450 Model, 114 Manual, 109 Hybrid, 83 Specification, 48 Drawing
- Evidence alignment counts: 142 Strong, 322 Weak, 151 Mismatch Risk, 189 Manual Only
- Key issues: 10 visible in the latest report context

The report is now safer than the earlier baseline because obvious false positives are being pushed to `Needs Human Review` more often. The remaining problem is narrower but still important: a set of semantically wrong rows still reach `Met` because broad category / level evidence is being treated as sufficient for requirements that are actually about manufacturer, controls, installation method, or dimensional constraints.

## What The Current Code Already Does Well

- The report keeps a hidden machine-readable JSON block in `#ema-ai-report-context`.
- The report includes `rule_applied`, `validation_type`, `candidate_scope_valid`, `full_model_fallback_used`, `expected_parameters`, and `evidence_alignment`.
- High-risk rows such as grounding and hose bibb / RPZ can already be held at `Needs Human Review`.
- The latest report no longer contains `Not Met` rows, which shows the guardrail path is active.

## Where The Current Classification Still Breaks

### 1. The semantic classifier is still too coarse

`EMAExtractor/Requirements/RequirementSemanticClassifier.cs` currently recognizes only a narrow set of high-priority families. It already has useful rules for grounding, hose bibb / RPZ, manufacturer/spec, identification, drawing/manual review, field execution, controls, O&M, attic stock, panel power, outlets, technology, mechanical coverage, conduit, and level placement.

The gap is not only missing patterns. The bigger issue is that the classifier still allows broad presence-based families to win when the text actually contains a more restrictive constraint.

Examples:

- `minimum conduit size` should not land in a generic technology or lighting family.
- `Aaon`, `Lennox`, `Trane`, `York/JCI` should force a manufacturer / owner-standard interpretation.
- `DDC`, `EMCS`, and `without demand control ventilation` should force controls or performance-spec interpretation.
- `in-line` and `above attic space` should force an installation-method interpretation.

### 2. The validation classifier is still generic

`EMAExtractor/Requirements/ValidationTypeClassifier.cs` still uses broad keyword scoring.

That means:

- Constraint-heavy text can still be labeled `Model` when the correct path is `Drawing`, `Specification`, or `Manual`.
- Manufacturer, controls, and closeout language can still be over-assigned to model-friendly types because the words `provide`, `install`, `equipment`, or `device` are present.
- There is no hard override for the strongest safety cases such as conduit size, controls sequences, O&M manuals, or field execution.

### 3. The comparison engine still permits broad evidence to look authoritative

`EMAExtractor/Requirements/RequirementComparisonEngine.cs` is the main gap surface.

The current logic still:

- Builds candidate pools that are too broad for high-risk semantic families.
- Uses category / level as a strong signal for `Met` in too many cases.
- Escalates only when evidence is weak, mismatched, or manual-only.
- Leaves room for a semantically wrong rule to look strong if the candidate pool is large and the category ratio is high.

This is the key failure mode in the latest report:

- The engine can assign a wrong rule.
- The rule can still find a large candidate pool.
- `AssessEvidenceAlignment` can return `Strong`.
- `ApplySemanticGuardrail` then sees `Met` plus `Strong` and does not escalate.

So the false positive is not always a weak-evidence problem. Sometimes it is a wrong-semantics problem that still produces strong-looking category evidence.

### 4. The report shows rich evidence, but not an explicit close-vs-context contract

`EMAExtractor/Reporting/OwnerRequirementHtmlReportGenerator.cs` already emits a lot of useful JSON:

- requirement type
- validation type
- rule applied
- expected categories
- expected parameters
- candidate scope validity
- full model fallback usage
- evidence alignment

What it does not yet expose as first-class fields is:

- direct closing evidence
- supporting context only
- safe-close eligibility
- hard-constraint modifier status

That makes the hidden JSON useful for explanation, but not yet strict enough for taxonomy-driven enforcement.

## Row-Level Findings In The Latest Report

| Row | Current report behavior | Recommended type | Why |
|---|---|---|---|
| 155 | `Met`, `technology_low_voltage_security_fire_alarm`, `lighting_fixture_coverage`, `Strong` | `conduit_raceway_size_requirement` + `flexible_conduit_length_requirement` | This is a dimensional conduit constraint. Technology wording is only context. |
| 478 | `Met`, `mechanical_equipment_coverage`, `mechanical_equipment_placement`, `Strong` | `manufacturer_brand_restriction` + `owner_standard_product_constraint` | This is a brand / owner-standard constraint, not mechanical presence. |
| 479 | `Met`, `mechanical_equipment_coverage`, `plumbing_routing_coverage`, `Strong` | `mechanical_performance_feature` + `mechanical_controls_ddc_emcs` | This combines product performance and a controls exclusion. |
| 480 | `Met`, `mechanical_equipment_coverage`, `mechanical_equipment_placement`, `Strong` | `mechanical_controls_ddc_emcs` | The requirement is about control strategy, not exhaust-fan presence. |
| 485 | `Met`, `unknown_ambiguous`, `plumbing_routing_coverage`, `Strong` | `mechanical_controls_ddc_emcs` | DDC cannot be closed from plumbing presence. |
| 491 | `Met`, `mechanical_equipment_coverage`, `mechanical_equipment_placement`, `Strong` | `installation_method_constraint` + `level_location_mounting_placement` | This is installation method and placement, not generic mechanical coverage. |

## Current Code Gaps By File

### `EMAExtractor/Requirements/RequirementSemanticClassifier.cs`

- Missing a universal, priority-first modifier layer.
- Missing explicit `owner_standard_product_constraint`, `mechanical_performance_feature`, `installation_method_constraint`, `code_jurisdiction_requirement`, and `dimension_clearance_distance_separation` handling as first-class safety families.
- Still allows broad presence-based families to win when the text contains a more restrictive constraint.

### `EMAExtractor/Requirements/ValidationTypeClassifier.cs`

- No hard override for manufacturer, controls, conduit size, or closeout language.
- Still allows some specification problems to appear as `Model` or `Hybrid`.
- Does not independently encode direct-close vs supporting-context distinction.

### `EMAExtractor/Requirements/RequirementComparisonEngine.cs`

- Broad category / level paths still exist for the wrong semantic families.
- `ApplySemanticGuardrail` depends on alignment being weak or mismatched.
- A semantically wrong but category-rich result can still survive as `Met`.
- High-priority semantic routing does not yet reflect the universal taxonomy above.

### `EMAExtractor/Reporting/OwnerRequirementHtmlReportGenerator.cs`

- The report is traceable, but it does not yet surface the taxonomy’s direct-close contract as a first-class concept.
- The hidden JSON is useful for explanation and audit, but it does not yet prevent a wrong semantic type from appearing authoritative.

## Gap Summary

The current engine is closer to a safe review tool than before, but it is still not a universal owner-requirement taxonomy engine.

The remaining gap is not just “add more keywords.”

It is:

1. Separate hard-constraint modifiers from presence-based families.
2. Make manufacturer / spec / controls / installation / field-execution / closeout families outrank all generic model presence.
3. Treat category + level as context only, never as a close condition for those families.
4. Add explicit evidence profiles so the report can say what closes a requirement and what merely supports it.

