# EMA AI Evidence Audit

## Executive Findings
- Workbook parsed: 804 requirements across 1 sheet(s).
- Revit export parsed: 21868 elements.
- Latest available report parsed: `C:\Users\Eliuth Chavero\AppData\Local\Temp\EMA_AI_Report_Tests\EMA_AI_Requirement_Check_MEP-NISD-MIDDLE SCHOOL 8_All Disciplines_20260608_034711.html` (requested 041149 was not present, so 034711 was used).
- Report status counts: {'met': 213, 'not_met': 8, 'needs_human_review': 583, 'insufficient_model_data': 0, 'not_applicable': 0}
- High-risk semantic failures concentrate in grounding/bonding/conductors, hose bibb/RPZ/valve, identification/manufacturer/spec, and demolition/manual requirements.
- The current report overuses generic level/category logic for requirements that are not actually about level or generic presence.
- Full-model fallback leaks broad candidate pools into unrelated rows; the plumbing hose bibb / RPZ family is the clearest example.

## Files Analyzed
- Workbook: `C:\Documents\Hyperghaps EMA\EMA-AI\Pipeline\pipeline\landing\NISD-MIDDLE SCHOOL\Owner Requirements\NORTHWEST ISD 06.02.2025.xlsx`
- Revit export: `C:\Documents\Hyperghaps EMA\EMA-AI\Pipeline\pipeline\landing\derived_local_project__revit_export__all__all__20260608_011533.json`
- HTML report: `C:\Users\Eliuth Chavero\AppData\Local\Temp\EMA_AI_Report_Tests\EMA_AI_Requirement_Check_MEP-NISD-MIDDLE SCHOOL 8_All Disciplines_20260608_034711.html`

## XLSX Structure Summary
- Sheets: ['query (84)']
- Active sheet: query (84)
- Requirement rows: 804
- Columns: STATUS, DISCIPLINE, REQUIREMENT, LINKS, CATEGORY LIST, DATE UPDATED, MODIFIED BY, RESOURCE, Item Type, Path

## Requirement Taxonomy Extracted from Workbook
- level_location_mounting_placement: 122 rows; samples [2, 9, 12, 32, 36]
- conduit_raceway: 106 rows; samples [8, 17, 18, 43, 49]
- plumbing_piping_water_drain_valves: 94 rows; samples [20, 25, 184, 188, 409]
- unknown_ambiguous: 89 rows; samples [3, 4, 6, 10, 13]
- grounding_bonding_conductors: 44 rows; samples [119, 120, 121, 122, 123]
- manufacturer_product_spec_submittal: 44 rows; samples [16, 68, 79, 179, 242]
- field_execution_demolition_protection: 43 rows; samples [60, 62, 81, 82, 103]
- controls_bms_bas_contactors_relays: 42 rows; samples [7, 15, 29, 34, 55]
- technology_low_voltage_security_fire_alarm: 39 rows; samples [53, 65, 142, 150, 155]
- lighting_fixture_controls_emergency: 31 rows; samples [50, 124, 283, 331, 336]
- identification_labeling_nameplate: 30 rows; samples [83, 85, 91, 92, 99]
- panel_circuit_power: 27 rows; samples [30, 31, 33, 48, 66]

## JSON Schema Summary
- Requirement results: 804
- Report metadata: {'project_name': 'MEP-NISD-MIDDLE SCHOOL 8', 'model_name': 'MEP-NISD-MIDDLE SCHOOL 8', 'requirements_file': 'NORTHWEST ISD 06.02.2025.xlsx', 'generated_at': '2026-06-08T03:47:11.3844734-04:00', 'scope': 'Entire Model', 'model_elements_reviewed': 21868, 'total_requirements': 804}

## Revit Category Inventory
- Pipes: 7370
- Pipe Fittings: 6131
- Electrical Fixtures: 3073
- Lighting Fixtures: 2612
- Pipe Accessories: 572
- Communication Devices: 484
- Fire Alarm Devices: 443
- Data Devices: 391
- Mechanical Equipment: 286
- Security Devices: 181
- Plumbing Fixtures: 167
- Electrical Equipment: 158

## Parameter Inventory and Coverage
- Instance parameters: 764
- Type parameters: 6
- Special focus groups: Electrical, Identification, Location, Demolition, Conduit, Plumbing, Technology, Controls

## Hidden JSON Coverage
- requirement_type: 0 (0.0%)
- requirement_intent: 804 (100.0%)
- validation_type: 804 (100.0%)
- validation_type_reason: 804 (100.0%)
- rule_applied: 804 (100.0%)
- rule_family: 804 (100.0%)
- trigger_keywords: 594 (73.9%)
- expected_evidence_sources: 594 (73.9%)
- expected_categories: 586 (72.9%)
- expected_family_type_hints: 594 (73.9%)
- expected_parameters: 594 (73.9%)
- filter_trace: 804 (100.0%)
- parameter_checks: 594 (73.9%)
- matched_family_type_summary: 594 (73.9%)
- actual_parameter_value_examples: 594 (73.9%)
- missing_expected_parameters: 316 (39.3%)
- missing_evidence_details: 316 (39.3%)
- evidence_alignment: 804 (100.0%)
- evidence_alignment_reason: 804 (100.0%)
- status_reason: 804 (100.0%)
- confidence_reason: 804 (100.0%)
- next_best_action: 804 (100.0%)
- human_review_needed: 804 (100.0%)
- model_evidence_limitations: 804 (100.0%)
- matched_element_examples: 594 (73.9%)
- matched_element_ids: 594 (73.9%)
- element_id_copy_text: 804 (100.0%)
- anchors: 804 (100.0%)
- ai_lookup_hints: 804 (100.0%)

## Candidate Pool Leakage Findings
- Row 2: rule=outlet_circuit_assignment count=21868 status=Needs Human Review
- Row 5: rule=outlet_circuit_assignment count=21868 status=Needs Human Review
- Row 8: rule=field_execution_or_demolition_requirement count=21868 status=Needs Human Review
- Row 16: rule=identification_labeling_manufacturer_requirement count=21868 status=Needs Human Review
- Row 19: rule=field_execution_or_demolition_requirement count=21868 status=Needs Human Review
- Row 24: rule=outlet_circuit_assignment count=21868 status=Needs Human Review
- Row 28: rule=outlet_circuit_assignment count=21868 status=Needs Human Review
- Row 35: rule=outlet_circuit_assignment count=21868 status=Needs Human Review
- Row 52: rule=field_execution_or_demolition_requirement count=21868 status=Needs Human Review
- Row 68: rule=technology_low_voltage_coverage count=1499 status=Met

## Contradictory Reasoning Findings

## Overbroad Rule Findings
- Row 2: recommended=level_location_mounting_placement report=outlet_circuit_assignment
- Row 3: recommended=unknown_ambiguous report=(none)
- Row 4: recommended=unknown_ambiguous report=panel_circuit_assignment
- Row 5: recommended=outlets_receptacles_devices report=outlet_circuit_assignment
- Row 6: recommended=unknown_ambiguous report=(none)
- Row 7: recommended=controls_bms_bas_contactors_relays report=(none)
- Row 8: recommended=conduit_raceway report=field_execution_or_demolition_requirement
- Row 9: recommended=level_location_mounting_placement report=(none)
- Row 10: recommended=unknown_ambiguous report=(none)
- Row 11: recommended=outlets_receptacles_devices report=outlet_circuit_assignment

## Level/Category Overuse Findings
- Row 600: type=plumbing_hose_bibb_rpz_valves report_rule=level_assignment
- Row 601: type=plumbing_hose_bibb_rpz_valves report_rule=level_assignment
- Row 602: type=plumbing_hose_bibb_rpz_valves report_rule=level_assignment
- Row 636: type=plumbing_piping_water_drain_valves report_rule=level_assignment
- Row 683: type=plumbing_piping_water_drain_valves report_rule=level_assignment
- Row 685: type=field_execution_demolition_protection report_rule=level_assignment
- Row 686: type=plumbing_piping_water_drain_valves report_rule=level_assignment

## Data Available but Not Used
- Row 6: type=unknown_ambiguous report_count=1
- Row 35: type=outlets_receptacles_devices report_count=21868
- Row 154: type=unknown_ambiguous report_count=1
- Row 265: type=unknown_ambiguous report_count=1
- Row 369: type=unknown_ambiguous report_count=1
- Row 370: type=unknown_ambiguous report_count=1
- Row 371: type=unknown_ambiguous report_count=1
- Row 372: type=unknown_ambiguous report_count=1
- Row 373: type=unknown_ambiguous report_count=1
- Row 472: type=unknown_ambiguous report_count=1

## Data Missing from Export
- Row 16: type=manufacturer_product_spec_submittal rule=identification_labeling_manufacturer_requirement status=Needs Human Review alignment=Mismatch Risk
- Row 27: type=code_standard_jurisdiction_owner_standard rule=(none) status=Needs Human Review alignment=Manual Only
- Row 60: type=field_execution_demolition_protection rule=panel_circuit_assignment status=Needs Human Review alignment=Weak
- Row 62: type=field_execution_demolition_protection rule=field_execution_or_demolition_requirement status=Needs Human Review alignment=Weak
- Row 67: type=commissioning_testing_om_training rule=technology_low_voltage_coverage status=Needs Human Review alignment=Weak
- Row 68: type=manufacturer_product_spec_submittal rule=technology_low_voltage_coverage status=Met alignment=Strong
- Row 69: type=commissioning_testing_om_training rule=technology_low_voltage_coverage status=Met alignment=Strong
- Row 70: type=commissioning_testing_om_training rule=technology_low_voltage_coverage status=Met alignment=Strong
- Row 72: type=commissioning_testing_om_training rule=technology_low_voltage_coverage status=Met alignment=Strong
- Row 73: type=commissioning_testing_om_training rule=(none) status=Needs Human Review alignment=Manual Only
- Row 74: type=commissioning_testing_om_training rule=(none) status=Needs Human Review alignment=Manual Only
- Row 75: type=commissioning_testing_om_training rule=identification_labeling_manufacturer_requirement status=Needs Human Review alignment=Mismatch Risk
- Row 77: type=commissioning_testing_om_training rule=identification_labeling_manufacturer_requirement status=Needs Human Review alignment=Mismatch Risk
- Row 78: type=commissioning_testing_om_training rule=(none) status=Needs Human Review alignment=Manual Only
- Row 79: type=manufacturer_product_spec_submittal rule=technology_low_voltage_coverage status=Needs Human Review alignment=Weak
- Row 80: type=commissioning_testing_om_training rule=identification_labeling_manufacturer_requirement status=Needs Human Review alignment=Weak
- Row 81: type=field_execution_demolition_protection rule=(none) status=Needs Human Review alignment=Manual Only
- Row 82: type=field_execution_demolition_protection rule=technology_low_voltage_coverage status=Met alignment=Strong
- Row 83: type=identification_labeling_nameplate rule=technology_low_voltage_coverage status=Needs Human Review alignment=Weak
- Row 85: type=identification_labeling_nameplate rule=panel_circuit_assignment status=Needs Human Review alignment=Mismatch Risk
## Canary Row Audit Summary
- Row 22: inferred=outlets_receptacles_devices report_rule=outlet_circuit_assignment report_status=Needs Human Review risk=0.0
- Row 100: inferred=identification_labeling_nameplate report_rule=identification_labeling_manufacturer_requirement report_status=Needs Human Review risk=0.0
- Row 103: inferred=field_execution_demolition_protection report_rule=lighting_fixture_coverage report_status=Needs Human Review risk=0.2
- Row 112: inferred=field_execution_demolition_protection report_rule=field_execution_or_demolition_requirement report_status=Needs Human Review risk=0.0
- Row 113: inferred=field_execution_demolition_protection report_rule=outlet_circuit_assignment report_status=Needs Human Review risk=0.0
- Row 133: inferred=grounding_bonding_conductors report_rule=field_execution_or_demolition_requirement report_status=Needs Human Review risk=0.0
- Row 142: inferred=technology_low_voltage_security_fire_alarm report_rule=technology_low_voltage_coverage report_status=Met risk=0.0
- Row 149: inferred=grounding_bonding_conductors report_rule=mechanical_equipment_placement report_status=Met risk=0.45
- Row 150: inferred=technology_low_voltage_security_fire_alarm report_rule=technology_low_voltage_coverage report_status=Met risk=0.0
- Row 600: inferred=plumbing_hose_bibb_rpz_valves report_rule=level_assignment report_status=Not Met risk=0.6
- Row 601: inferred=plumbing_hose_bibb_rpz_valves report_rule=level_assignment report_status=Not Met risk=0.6
- Row 602: inferred=plumbing_hose_bibb_rpz_valves report_rule=level_assignment report_status=Not Met risk=0.6

## Recommended Next Implementation Sequence
1. Add semantic priority rules for grounding/bonding/conductors, hose bibb/RPZ/valve, and identification/manufacturer/spec intents.
2. Forbid full-model fallback on these specific semantic families.
3. Require scoped category and family/type hints before allowing Met.
4. Emit Insufficient Model Data or Needs Human Review when relevant candidates are absent.
5. Rework report wording so Met cannot be paired with weak/mismatch/manual evidence.
