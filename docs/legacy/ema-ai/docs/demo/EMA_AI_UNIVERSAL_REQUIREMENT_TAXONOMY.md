# EMA AI Universal Requirement Taxonomy

Generated: 2026-06-08  
Baseline report: `Pipeline/pipeline/landing/EMA_AI_Requirement_Check_MEP-NISD-MIDDLE SCHOOL 8_All Disciplines_20260608_082009.html`  
Baseline counts: 142 Met, 662 Needs Human Review, 0 Not Met, 0 Insufficient Model Data, 0 Not Applicable

## Core Rule

Category + Level can support context only.

Category + Level cannot close manufacturer/spec, conduit size, flexible conduit length, controls/DDC/EMCS/BAS/BMS, minimum/maximum/only/no constraints, installation methods, demolition, owner approval, O&M, attic stock, code/jurisdiction, or field execution.

## Evidence Model

Three evidence tiers are used across the taxonomy:

1. Direct closing evidence
2. Supporting context
3. Not sufficient for closure

Direct evidence can close a requirement. Supporting context can narrow the candidate pool or explain why a requirement is in the right domain, but it cannot by itself assign `Met`. If the only evidence is category + level, the safe outcomes are `Needs Human Review`, `Manual`, or `Not Applicable` depending on scope.

## Canonical Types

| Priority | Type | Family | Safe closing evidence | Safe validation type | Support only? |
|---|---|---|---|---|---|
| 100 | `grounding_bonding_conductors` | Electrical | Ground wire size, ground bar, bonding jumper, conductor metadata | Hybrid | Yes |
| 99 | `conduit_raceway_size_requirement` | Conduit | Conduit size / trade size from details, specs, or parameters | Drawing | Yes |
| 98 | `flexible_conduit_length_requirement` | Conduit | Flexible conduit length from detail/spec/parameter | Drawing | Yes |
| 97.5 | `conduit_raceway_presence` | Conduit | Conduit/raceway family, route, or presence | Hybrid | Partial |
| 97 | `manufacturer_brand_restriction` | Products / Specs | Manufacturer parameter, model, submittal, approved manufacturer list | Specification | Yes |
| 96 | `owner_standard_product_constraint` | Owner Standards | District standard, base-bid / alt-bid / approved-equal text | Specification | Yes |
| 95 | `mechanical_controls_ddc_emcs` | Controls | Controls drawings, sequences, points list, DDC / EMCS schedule | Specification | Yes |
| 94 | `installation_method_constraint` | Installation / Field | Drawing detail, installation note, field verification | Drawing | Yes |
| 93 | `drawing_spec_manual_owner_approval` | Documents / Approval | Drawing/spec/owner review record | Manual | Yes |
| 92 | `field_execution_demolition_protection` | Field Execution | Phase data, demolition notes, protection plan, field inspection | Manual | Yes |
| 91 | `commissioning_testing_om_training` | Closeout | Commissioning report, test record, O&M manual, training log | Manual | Yes |
| 90 | `manufacturer_product_spec_submittal` | Products / Specs | Spec section, submittal package, product data sheet | Specification | Yes |
| 89 | `identification_labeling_nameplate` | Identification | Tag, label, nameplate, marking data, field inspection | Specification | Yes |
| 88 | `dimension_clearance_distance_separation` | Constraints | Measured clearance, distance, separation, offset, detail note | Drawing | Yes |
| 87 | `code_jurisdiction_requirement` | Code / Jurisdiction | Code citation, code matrix, jurisdiction review record | Specification | Yes |
| 86 | `panel_circuit_power` | Electrical | Panel, Circuit Number, Supply From, Voltage | Model | Partial |
| 85 | `electrical_load_metadata` | Electrical | Load, Connected Load, Apparent Load, Voltage | Model | Partial |
| 84 | `receptacle_outlet_device_power` | Electrical | Outlet family/type plus panel/circuit/voltage | Model | Partial |
| 83 | `lighting_fixture_coverage` | Lighting | Lighting fixture family/type and level | Model | Partial |
| 82 | `lighting_control_scheme` | Lighting | Controls schedule, occupancy sensor, dimming, switching data | Specification | Partial |
| 81 | `level_location_mounting_placement` | Placement | Level, elevation, host, room, or space | Model | Partial |
| 80 | `mechanical_equipment_coverage` | Mechanical | Mechanical equipment family/type and level | Model | Partial |
| 79 | `mechanical_performance_feature` | Mechanical | Product data, manufacturer metadata, spec/sequence evidence | Specification | Partial |
| 78 | `plumbing_fixture_coverage` | Plumbing | Plumbing fixture family/type and level | Model | Partial |
| 77 | `plumbing_piping_valves_routing` | Plumbing | Pipe, fitting, accessory, size, material, routing | Model | Partial |
| 76 | `plumbing_hose_bibb_rpz_valves` | Plumbing | Hose bibb / RPZ / backflow family/type and location | Hybrid | Partial |
| 75 | `technology_low_voltage_security_fire_alarm` | Technology | Device family/type, device ID, level, panel, circuit | Model | Partial |
| 74 | `av_multimedia_display_audio` | AV | Display, speaker, clock, AV device family/type | Model | Partial |
| 73 | `security_access_cctv` | Security | Camera, card reader, access-control family/type | Model | Partial |
| 72 | `fire_alarm_devices` | Fire Alarm | Fire alarm device family/type, address, zone, level | Model | Partial |
| 71 | `attic_stock_spare_parts` | Closeout | Stock list, spare-part list, procurement record | Manual | Yes |
| 70 | `unknown_ambiguous` | Fallback | None | Manual | No |

## Priority Logic

Priority is not a score. It is a safety order.

Higher-priority types must win first when their text is present, especially when the requirement includes:

- `no`, `only`, `minimum`, `maximum`, `without`
- manufacturer names or brand lists
- `DDC`, `EMCS`, `BAS`, `BMS`
- `O&M`, training, commissioning, or owner approval
- conduit size / length / separation text
- demolition, removal, salvage, backfill, or protection language
- installation method language such as `in-line`, `roof penetration`, `curb mounted`, or `above attic`

## Direct Closing vs Supporting Context

| Type | Direct closing evidence | Supporting context only | Never close with |
|---|---|---|---|
| `panel_circuit_power` | Panel, Circuit Number, Supply From, Voltage | Electrical equipment / fixture category, level | Category + level alone |
| `conduit_raceway_presence` | Conduit/raceway family/type, route, size | Electrical or technology context | Conduit size, flexible length, category + level alone |
| `manufacturer_brand_restriction` | Manufacturer / model / submittal / approved list | Family or type name hints | Category + level alone |
| `mechanical_controls_ddc_emcs` | Controls drawings, sequences, points list | Mechanical equipment presence | Equipment presence alone |
| `conduit_raceway_size_requirement` | Conduit size text / parameter / detail | Technology or electrical keywords | Lighting fixtures, device count, category + level |
| `field_execution_demolition_protection` | Phase, demolition, protection, field verification | Model element presence | Model presence, category, level |
| `commissioning_testing_om_training` | Commissioning / test / O&M / training deliverables | Equipment or system presence | Model presence, category, level |
| `level_location_mounting_placement` | Level, elevation, host, room, space | Family name, category | Exact spacing, installation method, product compliance |

## Recommended Types For The Six Audit Rows

| Row | Requirement summary | Recommended type(s) | Why |
|---|---|---|---|
| 155 | Minimum conduit size and flexible conduit length | `conduit_raceway_size_requirement` + `flexible_conduit_length_requirement` | The text is a dimensional constraint, not a device-placement check. Technology wording is only context. |
| 478 | Aaon base bid, Lennox / Trane alternates, no York/JCI | `manufacturer_brand_restriction` + `owner_standard_product_constraint` | This is a manufacturer and owner-standard restriction, not mechanical presence. |
| 479 | Two-speed compressors, bi-polar ionization, no DCV | `mechanical_performance_feature` + `mechanical_controls_ddc_emcs` | Product performance plus a negative controls constraint. |
| 480 | Exhaust fan control with lights vs EMCS | `mechanical_controls_ddc_emcs` | The requirement is about control sequence / control authority, not fan presence. |
| 485 | Venturi meters tied to DDC | `mechanical_controls_ddc_emcs` | DDC is a controls requirement and cannot be closed from plumbing presence. |
| 491 | In-line exhaust fans, above attic space | `installation_method_constraint` + `level_location_mounting_placement` | This is installation method and placement, not generic mechanical coverage. |

## Current Baseline Implications

- The latest report has no `Not Met` rows, but that does not mean every `Met` is correct.
- The false-positive surface is concentrated in requirements that are currently being routed through generic category / level evidence.
- The taxonomy below is intentionally stricter than the current runtime behavior so later code changes can use it as the source of truth.
