# EMA AI Full Semantic Requirement Audit

Generated: 2026-06-08  
Scope: Read-only extraction and design-intelligence audit  
Sources: HTML report (574 MB), XLSX workbook (804 rows), Revit JSON (454 MB, 21,868 elements), system code (5 files)

---

## 1. Core Principle

> **Category + Level can prove presence or placement only.**  
> Category + Level **CANNOT** prove size, manufacturer, controls, DDC/EMCS, conduit length, installation method, spec restrictions, owner approval, demolition execution, or compliance with product constraints.

This principle is the foundation of the entire audit. Every finding below flows from it.

---

## 2. Input Source Summary

### 2.1 HTML Report Hidden JSON
- **Path**: `Pipeline/pipeline/landing/EMA_AI_Requirement_Check_MEP-NISD-MIDDLE SCHOOL 8_All Disciplines_20260608_071504.html`
- **Size**: 574 MB (302.8 MB hidden JSON at byte offset ~284,226,526)
- **Schema**: `schema_version`, `report_metadata`, `summary_counts`, `filter_context`, `discipline_summaries[5]`, `key_issues[10]`, `requirement_results[804]`
- **Project**: MEP-NISD-MIDDLE SCHOOL 8
- **Elements reviewed**: 21,868

### 2.2 XLSX Workbook
- **Path**: `Pipeline/pipeline/landing/NISD-MIDDLE SCHOOL/Owner Requirements/NORTHWEST ISD 06.02.2025.xlsx`
- **Sheet**: "query (84)" — 804 rows, 10 columns
- **Disciplines**: ELECTRICAL (392), PLUMBING (214), LIGHTING (82), MECHANICAL (61), TECHNOLOGY (55)
- **Category tags**: Manufacturers, Gym, MDF/IDF, Controls, Storm Shelter, CCTV, Contactors, Conductors, Conduits, Boxes, Transformers, Fire Protection, Data Cabling, Intercom, Building Access Control, Fire Alarm, Security

### 2.3 Revit JSON Export
- **Path**: `Pipeline/pipeline/landing/derived_local_project__revit_export__all__all__20260608_011533.json`
- **Size**: 454 MB
- **Elements**: 21,868 across 12 categories
- **Levels**: 4 (LEVEL 01: 7,674 | null: 7,141 | LEVEL 02: 5,165 | ROOF: 1,888)
- **Parameters**: 769 unique parameter names

---

## 3. Report Distribution Analysis

### 3.1 Status Distribution
| Status | Count | % |
|--------|-------|---|
| Needs Human Review | 662 | 82.3% |
| Met | 142 | 17.7% |
| Not Met | 0 | 0% |
| Insufficient Model Data | 0 | 0% |
| Not Applicable | 0 | 0% |

### 3.2 Requirement Type Distribution
| Type | Count | % |
|------|-------|---|
| unknown_ambiguous | 190 | 23.6% |
| panel_circuit_power | 112 | 13.9% |
| field_execution_demolition_protection | 89 | 11.1% |
| manufacturer_product_spec_submittal | 83 | 10.3% |
| level_location_mounting_placement | 72 | 9.0% |
| identification_labeling_nameplate | 54 | 6.7% |
| conduit_raceway | 39 | 4.9% |
| technology_low_voltage_security_fire_alarm | 38 | 4.7% |
| grounding_bonding_conductors | 34 | 4.2% |
| outlets_receptacles_devices | 25 | 3.1% |
| plumbing_hose_bibb_rpz_valves | 22 | 2.7% |
| mechanical_equipment_coverage | 13 | 1.6% |
| drawing_spec_manual_owner_approval | 12 | 1.5% |
| commissioning_testing_om_training | 11 | 1.4% |
| controls_bms_bas_contactors_relays | 10 | 1.2% |

### 3.3 Validation Type Distribution
| Type | Count | % |
|------|-------|---|
| Model | 450 | 56.0% |
| Manual | 114 | 14.2% |
| Hybrid | 109 | 13.6% |
| Specification | 83 | 10.3% |
| Drawing | 48 | 6.0% |

### 3.4 Evidence Alignment Distribution
| Level | Count | % |
|-------|-------|---|
| Weak | 322 | 40.0% |
| Manual Only | 189 | 23.5% |
| Mismatch Risk | 151 | 18.8% |
| Strong | 142 | 17.7% |

### 3.5 Rule Applied Distribution
| Rule | Count | Notes |
|------|-------|-------|
| (none/unmatched) | 134 | No evaluation rule fired |
| field_execution_demolition_protection | 89 | |
| manufacturer_product_spec_submittal | 83 | |
| panel_circuit_assignment | 72 | |
| plumbing_routing_coverage | 66 | Used as broad fallback |
| identification_labeling_nameplate | 54 | |
| outlet_circuit_assignment | 39 | |
| conduit_raceway | 39 | |
| grounding_bonding_conductors | 34 | |
| level_location_mounting_placement | 33 | |
| lighting_fixture_coverage | 33 | |
| technology_low_voltage_coverage | 30 | |
| plumbing_hose_bibb_rpz_valves | 22 | |
| mechanical_equipment_placement | 13 | |
| panel_circuit_power | 12 | |
| drawing_spec_manual_owner_approval | 12 | |
| commissioning_testing_om_training | 11 | |
| field_execution_or_demolition_requirement | 10 | |
| controls_bms_bas_contactors_relays | 5 | |
| technology_low_voltage_security_fire_alarm | 5 | |
| electrical_load_metadata | 4 | |
| identification_labeling_manufacturer_requirement | 3 | |
| level_assignment | 1 | |

### 3.6 Key Flags
| Flag | Count |
|------|-------|
| Fallback used | 0 |
| Full model fallback used | 0 |
| Human review needed | 662 |

---

## 4. Revit Export Inventory

### 4.1 Category Breakdown (12 categories)

| Category | Elements | Key Evidence |
|----------|----------|-------------|
| Pipes | 7,370 | Type encodes material (Copper L, PVC SCH40, Steel SCH40). Size/Length available. |
| Pipe Fittings | 6,131 | Type encodes fitting type and material. |
| Electrical Fixtures | 3,073 | Family encodes type (GP, GFI, IG), height, purpose. Panel/Circuit params. |
| Lighting Fixtures | 2,612 | Family encodes LED type, size, emergency. Panel/Circuit params. |
| Pipe Accessories | 572 | Valves (ball, PRV). Size from type. |
| Communication Devices | 484 | AV (210), clocks (121), displays (119), speakers (29). |
| Fire Alarm Devices | 443 | Strobe/speaker (321), smoke (45), CO (57), heat (1), panel (1). |
| Data Devices | 391 | Ceiling (210), wall (181). Port count in type. |
| Mechanical Equipment | 286 | Aaon RTU (103), exhaust fans (31), etc. Tonnage in type. |
| Security Devices | 181 | Cameras (128), card readers (53). |
| Plumbing Fixtures | 167 | Drains (69), hose bibbs (50), water heaters (9), RPZ (7). |
| Electrical Equipment | 158 | Panels (120+), transformers (22+). Voltage/amperage in type. |

### 4.2 Missing Categories (Critical Gaps)
1. **Conduit/Raceway** — 39 conduit requirements with ZERO conduit evidence
2. **Wire/Cable** — Conductor requirements with no wire elements
3. **Ductwork** — Mechanical duct requirements with no duct evidence
4. **Cable Tray** — Cable tray requirements with no cable tray evidence
5. **Controls/BMS Points** — No automation/controls elements
6. **Sprinkler Heads** — No dedicated fire sprinkler elements

### 4.3 Key Parameters Available
| Parameter | Elements | Evidence Value |
|-----------|----------|---------------|
| Panel | 7,470 | Circuit-to-panel assignment |
| Circuit Number | 7,628 | Specific circuit identification |
| System Classification | 14,526 | MEP system categorization |
| Level | 14,498 | Floor/level placement |
| Size | 14,073 | Pipe/fitting sizes |
| Length | 8,447 | Pipe lengths |
| Manufacturer | 21,868 | Exists on all but values mostly empty |
| Model | 21,868 | Exists on all but values mostly empty |

---

## 5. False Positive Analysis

### 5.1 Summary
Of 142 Met rows:
- **~72 confirmed false positives** — category presence was used to claim Met for requirements that need manufacturer, controls, specification, or field verification
- **~38 suspected false positives** — Met is questionable given requirement specificity
- **~32 borderline acceptable** — presence evidence is relevant but not fully conclusive

### 5.2 False Positive Pattern Taxonomy

| Pattern | ID | Count | Severity | Example Row |
|---------|----|-------|----------|-------------|
| Manufacturer/brand overclaim | FP01 | ~63 | Critical | 478 (Aaon RTU) |
| Controls/DDC/EMCS overclaim | FP02 | 6 | Critical | 480 (EMCS control) |
| Conduit size overclaim | FP03 | 5 | Critical | 155 (¾-inch conduit) |
| Field execution overclaim | FP04 | 3 | High | 608 (backfill) |
| Submittal/closeout overclaim | FP05 | 2 | High | 72 (O&M manual) |
| Wrong rule assignment | FP06 | 7 | High | 479 (plumbing rule for RTU) |
| Specification text overclaim | FP07 | 11 | Medium | 623 (pipe material spec) |
| Attic stock/spares overclaim | FP08 | 1 | Medium | 58 (keyed switches stock) |

### 5.3 Critical Row Deep Analysis

#### Row 155: Minimum Conduit Sizes
- **Requirement**: "The minimum conduit size shall be ¾-inch. The minimum conduit size for Technology/Voice/Data/Video shall be 1-inch."
- **Current**: Met | rule=lighting_fixture_coverage | 2612 matches | Strong
- **Problem**: Lighting fixture presence cannot prove conduit sizes. **No conduit elements exist in the model.**
- **Correct**: NeedsHumanReview | type=conduit_raceway_size_requirement | validation=Drawing

#### Row 478: RTU Manufacturer Bid Requirement
- **Requirement**: "RTU BASE BID: Aaon HVAC equipment. ALT BID: Lennox then Trane. No York/JCI."
- **Current**: Met | rule=mechanical_equipment_placement | 286 matches | Strong
- **Problem**: This is a procurement/bid requirement. Model has Aaon RTU families BUT Manufacturer parameter not verified. Bid structure is never model-checkable.
- **Correct**: NeedsHumanReview | type=manufacturer_brand_restriction | validation=Specification

#### Row 479: RTU Compressor Type
- **Requirement**: "RTUs with two-speed compressors, bi-polar ionization without demand control ventilation."
- **Current**: Met | rule=plumbing_routing_coverage | 739 matches | Strong
- **Problem**: **WRONG RULE entirely.** 739 plumbing elements used to evaluate an RTU compressor requirement. Compressor type, ionization type, DCV absence are not model-verifiable.
- **Correct**: NeedsHumanReview | type=mechanical_controls_ddc_emcs | validation=Specification

#### Row 480: Exhaust Fan Control Method
- **Requirement**: "Control small restroom exhaust fan with lights. Control large gang restroom exhaust fan with EMCS."
- **Current**: Met | rule=mechanical_equipment_placement | 286 matches | Strong
- **Problem**: Fan presence proven, but control method (lights vs EMCS) and size-based differentiation not verifiable. EMCS is not modeled.
- **Correct**: NeedsHumanReview | type=mechanical_controls_ddc_emcs | validation=Specification

#### Row 485: Venturi Meters Tied to DDC
- **Requirement**: "Venturi meters tied to DDC"
- **Current**: Met | rule=plumbing_routing_coverage | 739 matches | Strong
- **Problem**: **WRONG RULE.** Plumbing elements used for DDC controls requirement. Venturi meters not modeled. DDC connection not verifiable.
- **Correct**: NeedsHumanReview | type=mechanical_controls_ddc_emcs | validation=Specification

#### Row 491: In-Line Exhaust Fan Placement
- **Requirement**: "All Exhaust fans will be In-Line for general exhaust and located above attic space."
- **Current**: Met | rule=mechanical_equipment_placement | 286 matches | Strong
- **Problem**: Fan existence proven, but fan TYPE (In-Line vs direct) not encoded in family names. Location "above attic space" requires elevation verification not performed.
- **Correct**: NeedsHumanReview | type=mechanical_equipment_coverage | validation=Model

---

## 6. Canary Row Analysis

All 12 canary rows are **correctly handled** as NeedsHumanReview:

| Row | Type | Rule | EA | Assessment |
|-----|------|------|----|------------|
| 22 | panel_circuit_power | outlet_circuit_assignment | Weak | CORRECT |
| 100 | manufacturer_product_spec | manufacturer_product_spec | Mismatch Risk | CORRECT |
| 103 | field_execution | field_execution | Mismatch Risk | CORRECT (20369 matched = full model leak, but guardrail works) |
| 112 | field_execution | field_execution | Weak | CORRECT |
| 113 | field_execution | field_execution | Weak | CORRECT |
| 133 | grounding_bonding | grounding_bonding | Weak | CORRECT |
| 142 | grounding_bonding | grounding_bonding | Weak | CORRECT |
| 149 | grounding_bonding | grounding_bonding | Weak | CORRECT |
| 150 | grounding_bonding | grounding_bonding | Weak | CORRECT |
| 600 | plumbing_hose_bibb | plumbing_hose_bibb | Weak | CORRECT |
| 601 | plumbing_hose_bibb | plumbing_hose_bibb | Weak | CORRECT |
| 602 | plumbing_hose_bibb | plumbing_hose_bibb | Weak | CORRECT |

**Key insight**: The guardrail system works correctly for NeedsHumanReview rows. The false positive problem is entirely in the Met path.

---

## 7. Semantic Requirement Taxonomy

### 7.1 Current State: 15 Types
The `RequirementSemanticClassifier` has 15 rules at priorities 1-100. See `EMA_AI_REQUIREMENT_TYPE_TAXONOMY.json` for full definitions.

### 7.2 Recommended State: 37 Types
22 new types needed to cover the full requirement landscape. See taxonomy JSON for details.

### 7.3 Intent vs Secondary Words

**Intent words** — words that define what the requirement is actually asking for:
- "shall be", "provide", "install", "furnish", "ensure", "coordinate", "verify"
- These determine the validation type and what evidence is needed

**Secondary words** — words that add context but don't change the core intent:
- "per district", "as manufactured by", "or approved equal", "refer to LINKS"
- These add constraints but shouldn't change the evaluation path

**Dangerous secondary words** — words that LOOK like secondary context but actually change what evidence is needed:
- "tied to DDC" — transforms a plumbing/mechanical requirement into a controls requirement
- "controlled by EMCS" — transforms an equipment requirement into a controls requirement
- "per code" — requires code compliance verification, not just model presence
- "owner approval required" — makes the requirement Manual regardless of model evidence
- "camera inspected" — adds a testing requirement on top of the base requirement

### 7.4 Classification Gaps
| Gap | Current Behavior | Correct Behavior |
|-----|------------------|------------------|
| "Aaon" in text | Classified as mechanical_equipment | Should be manufacturer_brand_restriction |
| "tied to DDC" | Falls through to plumbing/mechanical | Should be mechanical_controls_ddc_emcs |
| "minimum conduit size" | Classified as technology | Should be conduit_raceway_size_requirement |
| "O&M Manual" | Classified as technology | Should be operation_maintenance_manual |
| "backfill" | Falls through to plumbing | Should be field_execution/backfill |
| "engraved" | Falls through to mechanical | Should be engraving_labeling_finishing |
| "attic stock" | Falls through to unknown | Should be attic_stock_spare_parts |
| "occupancy sensor" | Falls through to lighting | Should be lighting_control_scheme |

---

## 8. System Gap Analysis

### 8.1 RequirementSemanticClassifier Gaps
1. **15 types cover ~76% of rows** — 190 rows (24%) fall to unknown_ambiguous
2. **Missing high-priority types**: conduit_size, mechanical_controls_ddc, manufacturer_brand, O&M manual
3. **Pattern priority conflicts**: manufacturer detection suppressed when "protection" text detected (correct behavior, but may over-suppress)
4. **No manufacturer name extraction**: classifier detects general manufacturer patterns but doesn't extract specific brand names for parameter verification

### 8.2 RequirementComparisonEngine Gaps
1. **No Manufacturer parameter verification**: engine never checks Manufacturer/Model parameter values
2. **Discipline-blind rule cascade**: mechanical requirements can fall through to plumbing rules
3. **Category-only Met path**: `EvaluateCategoryAndLevelRequirement` allows Met when category matches even for requirements needing specification compliance
4. **Full model leak for field_execution**: 20,369 matched elements for field execution requirements (correctly guardrailed, but candidate count is wrong)
5. **No validation_type enforcement**: Drawing/Specification validation types don't prevent Met from category-only evidence

### 8.3 ValidationTypeClassifier Gaps
1. **Correct for most cases** — keyword scoring produces reasonable Model/Drawing/Specification/Manual/Hybrid assignments
2. **Missing override for DDC/EMCS** — requirements mentioning DDC should always be Specification or Manual
3. **Missing override for O&M manual** — should always be Manual

### 8.4 ConfidenceScorer Assessment
- **Working correctly** — confidence formula produces reasonable scores
- **Strong=1.0 for false positives** — confidence of 1.0 on row 155 (conduit sizes, no conduit evidence) is misleading
- **Root cause**: confidence measures how well the engine evaluated, not whether the evaluation logic is correct

### 8.5 KeyIssueRanker Assessment
- **Working correctly** — scoring formula and severity assignments are sound
- **CandidateScopeValid check** prevents 21K pool from inflating impact scores (good)
- **No issues identified** — this component is the most robust

---

## 9. XLSX-to-Report Alignment

### 9.1 Row Count Match
- XLSX: 804 rows
- Report: 804 requirement_results
- **Match**: Perfect 1:1

### 9.2 Discipline Alignment
| XLSX Discipline | XLSX Count | Report Count | Match |
|----------------|------------|-------------|-------|
| ELECTRICAL | 392 | 392 | Yes |
| PLUMBING | 214 | 214 | Yes |
| LIGHTING | 82 | 82 | Yes |
| MECHANICAL | 61 | 61 | Yes |
| TECHNOLOGY | 55 | 55 | Yes |

### 9.3 XLSX Category Tags vs Classifier
The XLSX has 17 "Category List" tags that provide semantic hints not fully utilized:
- **Manufacturers** → should bias toward manufacturer_brand_restriction type
- **Controls** → should bias toward controls_bms_bas type
- **Conduits** → should bias toward conduit_raceway type
- **Storm Shelter** → should bias toward storm_shelter_requirements type
- **Gym** → should bias toward gymnasium_specialty type
- These tags are currently unused by the classifier. They could improve classification accuracy.

---

## 10. Status Rules by Requirement Type

### Safe-to-Met Types (with evidence)
| Type | Safe Met Conditions |
|------|--------------------|
| panel_circuit_power | Panel exists AND Circuit Number assigned AND voltage matches |
| outlets_receptacles_devices | Outlet type family exists AND circuit assigned |
| level_location_mounting_placement | Element at correct level AND presence is sufficient |

### Never-Met Types (always NeedsHumanReview or Manual)
| Type | Reason |
|------|--------|
| manufacturer_brand_restriction | Cannot verify brand from category |
| conduit_raceway_size_requirement | No conduit elements in model |
| mechanical_controls_ddc_emcs | No controls elements in model |
| field_execution_demolition_protection | Field work not modeled |
| identification_labeling_nameplate | Physical labels not modeled |
| commissioning_testing_om_training | Closeout deliverables not modeled |
| operation_maintenance_manual | O&M manuals not modeled |
| engraving_labeling_finishing | Physical markings not modeled |
| attic_stock_spare_parts | Inventory not modeled |
| drawing_spec_manual_owner_approval | External documents not modeled |
| backfill_excavation_earthwork | Earth work not modeled |
| installation_method_execution | Methods not modeled |
| owner_approval_coordination | Approvals not modeled |
| testing_inspection_startup | Tests not modeled |

### Partial-Met Types (Met only with parameter evidence)
| Type | Parameter Evidence Required |
|------|---------------------------|
| grounding_bonding_conductors | GroundWireSize parameter populated |
| plumbing_hose_bibb_rpz_valves | Bibb/RPZ family match + reasonable count |
| technology_low_voltage | Device category matches requirement |
| mechanical_equipment_coverage | Family/type matches equipment type (not brand/controls) |
| pipe_material_specification | Pipe Type name contains required material |

---

## 11. Deliverable Files

| File | Content |
|------|---------|
| `EMA_AI_FULL_SEMANTIC_REQUIREMENT_AUDIT.md` | This document |
| `EMA_AI_REQUIREMENT_TYPE_TAXONOMY.json` | 37-type taxonomy with model-checkability and evidence expectations |
| `EMA_AI_EXPECTED_EVIDENCE_MATRIX.json` | Per-type evidence map against Revit export inventory |
| `EMA_AI_FALSE_POSITIVE_RISK_ROWS.json` | All 142 Met rows analyzed for false positive risk |
| `EMA_AI_IMPLEMENTATION_BACKLOG.md` | Prioritized backlog (P0-P3) with file change map |
| `EMA_AI_FULL_SEMANTIC_REQUIREMENT_AUDIT.json` | Machine-readable version of this audit |

---

## 12. Acceptance Criteria Checklist

- [x] All 4 input sources parsed and cross-referenced
- [x] 804 requirement results analyzed with per-field distributions
- [x] 6 critical rows (155, 478, 479, 480, 485, 491) deeply analyzed
- [x] 12 canary rows (22, 100, 103, 112, 113, 133, 142, 149, 150, 600, 601, 602) verified
- [x] All 142 Met rows analyzed for false positive risk
- [x] 37-type semantic requirement taxonomy defined
- [x] Intent vs secondary word analysis completed
- [x] False positive pattern taxonomy with 8 patterns identified
- [x] Evidence matrix mapping types to Revit evidence
- [x] Status rules per requirement type defined
- [x] Implementation backlog prioritized (P0-P3)
- [x] No code changes made
- [x] No staging, commits, or pushes
- [x] No compliance certification claimed
