# EMA AI Implementation Backlog

Generated: 2026-06-08

Source: Full semantic extraction audit of HTML report, XLSX workbook, Revit JSON, and system code.

## Priority Legend
- **P0 CRITICAL** — False positives in production. Must fix before next report.
- **P1 HIGH** — Significant accuracy gap. Fix in P1 milestone.
- **P2 MEDIUM** — Improvement opportunity. Schedule appropriately.
- **P3 LOW** — Nice to have. Backlog.

---

## P0: Critical False Positive Fixes

### P0-1: Block Met status for manufacturer/brand requirements without parameter verification
- **Impact**: ~63 of 142 Met rows are false positives because engine proves category presence but requirement specifies exact manufacturer (Aaon, ETC, Sloan, Brady, Square D, etc.)
- **Root cause**: No check of Manufacturer or Model parameter values in evaluation path
- **Fix**: In `RequirementComparisonEngine`, when requirement text contains a known manufacturer name, require `Manufacturer` parameter to contain matching value before allowing Met. Otherwise force NeedsHumanReview.
- **Files**: `RequirementComparisonEngine.cs`, `RequirementSemanticClassifier.cs`
- **Test rows**: 478 (Aaon RTU), 473 (ETC theatrical), 529 (Reliable-Enviromatics), 539 (Sloan Royal)

### P0-2: Add conduit_raceway_size_requirement type
- **Impact**: Row 155 (minimum conduit sizes) is Met via lighting_fixture_coverage. No conduit elements exist in the model.
- **Root cause**: No conduit-size-specific classifier rule. Requirement classified as technology_low_voltage.
- **Fix**: Add new rule in `RequirementSemanticClassifier` at priority ~95 matching `conduit.*inch|minimum conduit size|raceway size|trade size`. Set `AllowsModelOnlyMet = false`, `ModelEvidenceSufficiency = "None"`.
- **Files**: `RequirementSemanticClassifier.cs`
- **Test rows**: 155, 188, 250

### P0-3: Add mechanical_controls_ddc_emcs type
- **Impact**: Rows 480 (EMCS control), 485 (DDC), 529 (controls manufacturer) falsely Met
- **Root cause**: No specific type for DDC/EMCS/BAS requirements that prevents Met status
- **Fix**: Add new rule at priority ~88 matching `DDC|EMCS|demand control ventilation|building automation|BMS|BAS|humidity sensor|venturi.*DDC|control.*sequence`. Set `AllowsModelOnlyMet = false`.
- **Files**: `RequirementSemanticClassifier.cs`
- **Test rows**: 480, 485, 529

### P0-4: Fix wrong-rule assignment for mechanical requirements
- **Impact**: Row 479 (RTU compressor type) evaluated via plumbing_routing_coverage with 739 plumbing elements
- **Root cause**: Mechanical requirements falling through to plumbing rules in evaluation cascade
- **Fix**: In `RequirementComparisonEngine`, tighten the evaluation cascade so mechanical-classified requirements never fall through to plumbing rules. Add discipline-aware rule gating.
- **Files**: `RequirementComparisonEngine.cs`
- **Test rows**: 479, 485, 530

---

## P1: High-Priority Accuracy Improvements

### P1-1: Add operation_maintenance_manual type
- **Impact**: Row 72 (O&M Manual sections) falsely Met via technology_low_voltage_coverage
- **Root cause**: O&M manual requirements not caught by commissioning classifier, classified as technology
- **Fix**: Add rule at priority ~90 matching `operation.*maintenance.*manual|O&M manual|maintenance manual|operating manual|content for each unit`. Set as Manual validation type.
- **Files**: `RequirementSemanticClassifier.cs`
- **Test rows**: 72, 699

### P1-2: Add backfill_excavation_earthwork type
- **Impact**: Rows 608, 610, 734 (BACKFILL requirements) falsely Met via plumbing_routing_coverage
- **Root cause**: Backfill/earthwork not caught by field_execution_demolition_protection patterns
- **Fix**: Expand field_execution patterns or add new rule matching `backfill|excavat|compacting|sand bed|pipe burial|geotechnical|stabilized sand`.
- **Files**: `RequirementSemanticClassifier.cs`
- **Test rows**: 608, 610, 734

### P1-3: Add engraving_labeling_finishing type
- **Impact**: Row 42 (kitchen switch plates engraved), Row 455 (fixture finish color) falsely Met
- **Root cause**: Engraving/finishing requirements not identified as non-model-checkable
- **Fix**: Add rule matching `engrav|finish.*color|stainless steel.*cover|vandal proof|nameplate.*text|label.*text`. Set as Manual.
- **Files**: `RequirementSemanticClassifier.cs`
- **Test rows**: 42, 455

### P1-4: Add attic_stock_spare_parts type
- **Impact**: Row 58 (keyed switches attic stock zero) falsely Met
- **Root cause**: No attic stock classifier rule
- **Fix**: Add rule matching `attic stock|spare.*part|stock.*zero|stock.*quantity`. Set as Manual or NotApplicable.
- **Files**: `RequirementSemanticClassifier.cs`
- **Test rows**: 58

### P1-5: Add lighting_control_scheme type
- **Impact**: Rows 403, 418, 450 (lighting controls, occupancy sensors, switching) falsely Met via fixture presence
- **Root cause**: Lighting control requirements evaluated as fixture presence requirements
- **Fix**: Add rule matching `lighting control|occupancy sensor|switchpack|dimming|switching zone|lighting.*switch`. Set `AllowsModelOnlyMet = false`.
- **Files**: `RequirementSemanticClassifier.cs`
- **Test rows**: 403, 418, 450

### P1-6: Reduce unknown_ambiguous count (currently 190/804 = 23.6%)
- **Impact**: 190 requirements get no specific rule, fall to unknown_ambiguous, then matched via broad fallback rules
- **Root cause**: Classifier doesn't cover enough requirement types
- **Fix**: Adding P0 and P1 types above should reduce unknown_ambiguous by ~60-80 rows. Remaining should be analyzed for additional patterns.
- **Target**: Reduce to under 80 (10%)

---

## P2: Medium-Priority Improvements

### P2-1: Add pipe_material_specification type
- **Impact**: Pipe Type names encode material (COPPER TYPE L, PVC SCH40, STEEL SCH40). Currently matched broadly.
- **Fix**: Add rule that checks pipe Type names against requirement text for material matches.
- **Test rows**: 589, 593, 618, 623

### P2-2: Add plumbing_fixture_model_specification type
- **Impact**: Plumbing fixture family names encode some model info (Josam, MPH-24D, AO Smith DEL 50).
- **Fix**: Add rule that checks Plumbing Fixture family/type against requirement brand names.

### P2-3: Add testing_inspection_startup type
- **Impact**: Camera inspection, smoke test, startup requirements incorrectly evaluated
- **Fix**: Add rule matching `camera inspect|smoke test|test.*startup|video prior|commissioning test`.

### P2-4: Implement Manufacturer parameter verification
- **Impact**: Manufacturer parameter exists on all 21868 elements but values not checked
- **Fix**: For requirements containing brand names, check if Manufacturer parameter on matched elements contains the brand. This is more granular than P0-1 which blocks Met; this enables future partial verification.
- **Prerequisite**: Revit export must populate Manufacturer parameter (currently mostly empty)

### P2-5: Add Drawing/Specification validation type enforcement
- **Impact**: 11 rows with Drawing validation type are Met despite requiring drawing review
- **Fix**: When validation_type is Drawing or Specification, require stronger evidence than category-only presence. Possibly downgrade to NeedsHumanReview unless parameter-level evidence exists.

---

## P3: Low-Priority / Future

### P3-1: Add storm_shelter_requirements type
### P3-2: Add kitchen_cafeteria_specialty type  
### P3-3: Add gymnasium_specialty type
### P3-4: Add fire_protection_sprinkler type
### P3-5: Parameter-level evidence scoring (use Size, Length, etc. when available)
### P3-6: Room-level placement verification (if room data becomes available)
### P3-7: Cross-element relationship checking (valve-to-bibb pairing, etc.)

---

## Metrics

| Metric | Current | After P0 | After P0+P1 |
|--------|---------|----------|-------------|
| Total Met | 142 | ~70 | ~40 |
| False Positive Met | ~72 | ~5 | ~0 |
| NeedsHumanReview | 662 | ~734 | ~764 |
| unknown_ambiguous | 190 | ~170 | ~110 |
| Requirement types | 15 | 18 | 25 |

---

## File Change Map

| File | P0 Changes | P1 Changes |
|------|------------|------------|
| RequirementSemanticClassifier.cs | +2 rules (conduit_size, mechanical_controls_ddc_emcs) | +5 rules (O&M, backfill, engraving, attic_stock, lighting_control) |
| RequirementComparisonEngine.cs | Manufacturer param check, discipline-aware rule gating | Drawing/Spec enforcement |
| RequirementCheckModels.cs | None | None (types are string-based) |
| ValidationTypeClassifier.cs | None | None |
| ConfidenceScorer.cs | None | None |
| KeyIssueRanker.cs | None | None |
