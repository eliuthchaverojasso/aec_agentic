# EMA AI Owner Requirements Rules Tables

Source of truth: current implementation files in `EMAExtractor/Requirements` and `EMAExtractor/Reporting`, plus the latest real-data report hidden JSON.

Latest real-data report used:

`C:\Users\Eliuth Chavero\AppData\Local\Temp\EMA_AI_Report_Tests\EMA_AI_Requirement_Check_MEP-NISD-MIDDLE SCHOOL 8_All Disciplines_20260608_055511.html`

This document describes a first-pass evidence review workflow for Owner Requirements readiness. Final project decisions remain subject to engineering, drawing, specification, owner, and field review.

## Table 1 - Semantic Rule Priority

| Priority | Requirement Type | Rule Family | What It Detects | Why It Must Outrank Generic Rules | Typical Validation Type | Can Model Alone Close It? |
|---:|---|---|---|---|---|---|
| 100 | `grounding_bonding_conductors` | grounding_bonding | Grounding, bonding, ground bars, ground conductors, grounding electrode conductors. | Grounding words can appear beside technology, mechanical, roof, or level terms; generic category/level evidence is not enough. | Hybrid | Partial |
| 99 | `plumbing_hose_bibb_rpz_valves` | plumbing | Hose bibbs, RPZ, backflow preventers, valves, roof zone plumbing coordination. | Roof/elevation/location language must not turn hose bibb/RPZ rows into level-assignment rows. | Model | Partial |
| 98 | `manufacturer_product_spec_submittal` | specification | Manufacturer, product data, model/catalog, submittal, approved-equal requirements. | Equipment presence does not prove product/spec/submittal acceptance. | Specification | No |
| 97 | `identification_labeling_nameplate` | specification_and_marking | Identification, labels, tags, nameplates, markers. | Category presence does not prove labels/nameplates/tags are provided. | Specification | Partial |
| 97 | `field_execution_demolition_protection` | manual_or_drawing_review | Demolition, abandoned work, salvage, protection, field execution, removal. | Device/equipment presence must not close demolition or field-execution requirements. | Manual | No |
| 96 | `drawing_spec_manual_owner_approval` | manual_or_drawing_review | Drawing, specification, owner approval, manual coordination, verify-in-field language. | Model evidence cannot replace drawing/spec/owner direction. | Hybrid | No |
| 90 | `panel_circuit_power` | electrical_connection | Panel, circuit, feeder, breaker, voltage, load, power assignment. | Electrical connection requirements need parameter evidence, not just electrical category presence. | Model | Partial |
| 89 | `outlets_receptacles_devices` | electrical_connection | Outlets, receptacles, duplex, GFCI/GFI, 120V/208V devices. | Outlet rows can include room/level language but require outlet/circuit evidence. | Model | Partial |
| 88 | `technology_low_voltage_security_fire_alarm` | technology | Data, voice, CCTV, MATV, security, fire alarm, low-voltage devices. | Technology coverage must not close grounding/spec/manual requirements. | Model | Partial |
| 87 | `conduit_raceway` | raceway | Conduit, raceway, pathways, sleeves, underground connectors, ductbank. | Conduit installation intent must not be treated as generic equipment or level evidence. | Hybrid | Partial |
| 86 | `controls_bms_bas_contactors_relays` | controls | Controls, BMS/BAS, contactors, relays, starters, control wiring. | Controls coordination often spans model, equipment, and drawings. | Hybrid | Partial |
| 85 | `commissioning_testing_om_training` | closeout | Commissioning, testing, balancing, O&M, warranty, training. | Closeout deliverables are external evidence; model elements cannot close them. | Manual | No |
| 50 | `mechanical_equipment_coverage` | mechanical | Mechanical equipment presence and placement. | Must not absorb grounding, plumbing, spec, or manual requirements that mention mechanical terms. | Model | Yes, when truly mechanical placement/coverage |
| 10 | `level_location_mounting_placement` | placement | Level, elevation, mounting, roof, room, space, placement. | Lowest-priority catch-all; only wins when placement is the primary intent. | Model | Yes, when truly placement-only |
| 1 | `unknown_ambiguous` | unmatched | Requirements without a stable semantic family. | Avoids forcing ambiguous text into an unsafe deterministic rule. | Hybrid | No |

## Table 2 - Rule Triggers

| Requirement Type | Primary Trigger Keywords | Secondary Trigger Keywords | Example Requirement Text Pattern | False Positive Risk | Rule That It Must Outrank |
|---|---|---|---|---|---|
| `grounding_bonding_conductors` | grounding, bonding, ground bar, ground conductor | #6 ground, ground bus, bonding jumper | “Provide #6 ground conductor to building grounding system.” | “ground valve box” can look like grounding; current code narrows grounding patterns. | technology, mechanical, level |
| `plumbing_hose_bibb_rpz_valves` | hose bibb, RPZ, backflow, valve | roof zone, riser room, threaded connection | “Provide RPZ at roof hose bibbs per roof zone.” | Roof/elevation words can trigger placement if plumbing is missed. | level/location |
| `manufacturer_product_spec_submittal` | manufacturer, product data, submittal, catalog | approved equal, spec section, model | “Acceptable manufacturers include...” | “manufacturer’s condition” in protection text can look like product evidence; code suppresses this case. | category presence |
| `identification_labeling_nameplate` | identification, label, tag, nameplate | marker, marking, equipment ID | “Provide equipment identification labels.” | Mark/tag fields may exist for unrelated reasons. | category presence |
| `drawing_spec_manual_owner_approval` | drawings, specifications, owner approval | coordinate with owner, verify in field, as shown | “Provide as shown on drawings and approved by owner.” | Broad coordination language can appear with real model-checkable items. | model-only closure |
| `field_execution_demolition_protection` | demolition, remove, abandoned, salvage | protect, clean, contractor shall, do not use | “Disconnect abandoned outlets and provide blank covers.” | Outlet/device words can pull toward outlet assignment. | outlet/device, category presence |
| `panel_circuit_power` | panel, circuit, breaker, feeder | load, voltage, power, supply from | “Provide panel and circuit assignment.” | Power words can be broad; needs direct electrical parameters. | generic electrical category |
| `outlets_receptacles_devices` | outlet, receptacle, duplex | GFI/GFCI, 120V, 208V, quad | “Provide 120V duplex outlet connected to room circuit.” | Room/level terms can distract from outlet intent. | level/location |
| `technology_low_voltage_security_fire_alarm` | data, voice, security, fire alarm | CCTV, MATV, low voltage, MDF/IDF | “Provide data devices in IDF rooms.” | Grounding in technology text must move to grounding rule. | generic technology coverage |
| `mechanical_equipment_coverage` | HVAC, airflow, pump, fan | chiller, coil, RTU, AHU | “Mechanical equipment shall be placed on correct level.” | Mechanical words in grounding/support restrictions. | level, generic category |
| `level_location_mounting_placement` | level, elevation, mounted, roof | location, room, space, host | “Assign equipment to correct level.” | Can incorrectly absorb roof/hose bibb or grounding rows. | none; lowest catch-all |
| `conduit_raceway` | conduit, raceway, pathway | sleeve, underground connector, ductbank | “Provide conduit sleeves and raceway pathway.” | Conduit appears in grounding/demolition text. | mechanical/level |
| `controls_bms_bas_contactors_relays` | controls, BMS, BAS | contactor, relay, starter | “Provide BAS control wiring and relays.” | “Control” can be broad. | generic equipment |
| `commissioning_testing_om_training` | commissioning, testing, O&M | training, warranty, balancing | “Provide O&M manuals and owner training.” | “test” may appear in equipment text. | model/category evidence |
| `unknown_ambiguous` | no stable match | open-ended text | “Coordinate as required.” | N/A | deterministic closure |

## Table 3 - Candidate Scope by Rule

| Requirement Type | Allowed Categories | Excluded Categories | Required Family/Type Hints | Required Parameter Evidence | Category-Only Evidence Allowed? | Level-Only Evidence Allowed? | Full-Model Fallback Allowed? | If No Candidates Found |
|---|---|---|---|---|---|---|---|---|
| `grounding_bonding_conductors` | Electrical Equipment; Electrical Fixtures; Communication/Data/Security/Fire Alarm Devices | Mechanical Equipment | ground bar; ground conductor; bonding jumper; ground wire | Ground wire/conductor/grounding parameters | No | No | No | Needs Human Review or Insufficient Model Data |
| `plumbing_hose_bibb_rpz_valves` | Plumbing Fixtures; Pipe Accessories; Pipe Fittings; Pipes | Electrical/Lighting/Technology/Fire Alarm categories | Roof/Wall Hose Bibb; RPZ; Backflow Preventer; Ball Valve; Pressure Regulating Valve | RPZ; Valve; Backflow; System Type; Location | No | No | No | Needs Human Review or Insufficient Model Data |
| `manufacturer_product_spec_submittal` | Electrical Equipment; Electrical Fixtures; Lighting Fixtures; Mechanical Equipment; Plumbing Fixtures | Pipes; Pipe Fittings | manufacturer; model; catalog; product | Manufacturer; Model; Catalog Number; Product Data | No | No | No | Needs Human Review |
| `identification_labeling_nameplate` | Equipment/devices/fixtures across MEP and low voltage | Pipes; Pipe Fittings | nameplate; label; tag; marker | Mark; Type Mark; Tag; Label; Nameplate | No | No | No | Needs Human Review |
| `drawing_spec_manual_owner_approval` | None by default | None | None | Sheet Reference; Comments | No | No | No | Needs Human Review |
| `field_execution_demolition_protection` | Electrical/Lighting/Mechanical/Plumbing/Pipe categories | None | abandoned; demolition; blank cover; salvage; remove | Phase Created; Phase Demolished; Demolition Status; Status | No | No | No | Needs Human Review |
| `panel_circuit_power` | Electrical Equipment; Electrical Fixtures; Lighting Fixtures | Mechanical Equipment; Plumbing Fixtures | panel; receptacle; fixture; equipment | Panel; Panel Name; Circuit Number; Supply From; Voltage | No | No | No | Insufficient Model Data or Needs Human Review |
| `outlets_receptacles_devices` | Electrical Fixtures; Electrical Equipment | Mechanical Equipment; Plumbing Fixtures | duplex; receptacle; outlet; GFI/GFCI | Voltage; Panel; Circuit Number; Load Name; Room/Space | No | No | No | Insufficient Model Data or Needs Human Review |
| `technology_low_voltage_security_fire_alarm` | Communication; Data; Fire Alarm; Security; Nurse Call; Telephone Devices | Mechanical Equipment; Plumbing Fixtures | communication device; data device; fire alarm; security device; rack | Device ID; Address; System; Panel/Circuit where relevant | No | No | No | Needs Human Review |
| `mechanical_equipment_coverage` | Mechanical Equipment | Electrical Fixtures; Electrical Equipment | pump; fan; chiller; RTU; AHU | Level; Manufacturer; Model; Description | Sometimes, only when requirement is mechanical coverage | No for semantic/spec/manual rows | No | Insufficient Model Data |
| `level_location_mounting_placement` | Electrical Fixtures; Lighting Fixtures; Mechanical Equipment; Plumbing Fixtures; Electrical Equipment | Pipes; Pipe Fittings | level; elevation; mount; roof; location | Level; Offset; Elevation; Room; Space; Host | No | Yes, only for placement-primary rows | No | Insufficient Model Data |
| `conduit_raceway` | Conduits; Conduit Fittings; Cable Trays; Electrical Equipment; Electrical Fixtures | Mechanical Equipment; Plumbing Fixtures | conduit; raceway; connector; sleeve; ductbank | Conduit; Raceway; System Type; Size | No | No | No | Needs Human Review |
| `controls_bms_bas_contactors_relays` | Electrical Equipment; Mechanical Equipment; Communication Devices; Data Devices | Plumbing Fixtures | contactor; relay; starter; BMS; BAS; controls | Panel; Circuit Number; System; Controls | No | No | No | Needs Human Review |
| `commissioning_testing_om_training` | None by default | None | None | Comments; Status | No | No | No | Needs Human Review |
| `unknown_ambiguous` | None established | None | None | None established | No | No | No full-model model closure | Needs Human Review |

High-risk semantic rules cannot fall back to all model records. When candidate scope cannot be established, the result stays reviewable instead of using the 21,868-record full model as evidence.

## Table 4 - Expected Evidence and Parameters

| Requirement Type | Expected Evidence Sources | Expected Revit Categories | Expected Family/Type Hints | Expected Parameters | External Evidence Often Required | Example Parameter Values |
|---|---|---|---|---|---|---|
| `grounding_bonding_conductors` | Conductor/wire grounding parameters; drawings/specs; field verification | Electrical Equipment; Electrical Fixtures; low-voltage device categories | ground bar; ground conductor; bonding jumper | `DMET_Feeder_GroundWireSize`; `DMEN_Feeder_GroundWireArea`; `DMET_Instance_GroundWireSize`; `DMEN_Instance_GroundWireArea`; `DMET_Feeder_WireCallout`; Grounding; Bonding | Yes | `#6`, `EGC`, populated ground wire size |
| `plumbing_hose_bibb_rpz_valves` | Plumbing fixtures/fittings/accessories; drawings/specs; coordination notes | Plumbing Fixtures; Pipe Accessories; Pipe Fittings; Pipes | Roof Mount Hose Bibb; Wall Hose Bibb; RPZ; Backflow Preventer; Ball Valve | RPZ; Valve; Backflow; System Type; Level; Elevation; Location | Often | Domestic cold water, RPZ, valve type/location |
| `manufacturer_product_spec_submittal` | Specification text; submittals; product data; manufacturer metadata | Equipment and fixture categories | manufacturer; model; catalog; approved | Manufacturer; Model; Description; Catalog Number; Product Data; Comments | Yes | Brady, Seton, model/catalog number |
| `identification_labeling_nameplate` | Mark/tag/label parameters; specifications; submittals | Equipment/devices/fixtures | nameplate; label; tag; marker | Mark; Type Mark; Equipment ID; Tag; Label; Nameplate; Identification | Often | Equipment tag, label text, nameplate value |
| `drawing_spec_manual_owner_approval` | Drawings; specifications; owner direction; manual review | None by default | None | Sheet Reference; Comments | Yes | Sheet/detail reference, owner note |
| `field_execution_demolition_protection` | Drawings; specifications; field verification; phase data | Equipment/fixtures/pipes/accessories | abandoned; demolition; blank cover; remove | Phase Created; Phase Demolished; Demolition Status; Status; Comments | Yes | Existing, demolished phase, blank cover note |
| `panel_circuit_power` | Revit electrical parameters; panel schedules | Electrical Equipment; Electrical Fixtures; Lighting Fixtures | panel; receptacle; fixture | Panel; Panel Name; Circuit Number; Circuit; Supply From; Load Name; Voltage | Sometimes | `Panel=LP-1`, `Circuit Number=12`, `Voltage=120 V` |
| `outlets_receptacles_devices` | Electrical fixture/device parameters; panel schedules | Electrical Fixtures; Electrical Equipment | duplex; receptacle; outlet; GFI/GFCI | Voltage; Panel; Panel Name; Circuit Number; Circuit; Load Name; Room; Space; Level | Sometimes | `120 V`, panel/circuit values, room/space |
| `technology_low_voltage_security_fire_alarm` | Device elements; network/security/fire alarm schedules | Communication/Data/Fire Alarm/Security/Nurse Call/Telephone Devices | data device; fire alarm; rack | Level; Panel; Circuit Number; Device ID; Address; System | Often | device ID, address, system name |
| `mechanical_equipment_coverage` | Mechanical equipment elements | Mechanical Equipment | pump; fan; chiller; RTU; AHU | Level; Manufacturer; Model; Description | Sometimes | level, model, manufacturer |
| `level_location_mounting_placement` | Revit level/location assignment | Placement-relevant MEP categories | level; elevation; mount; roof | Level; Offset; Elevation; Room; Space; Host | Sometimes | Level 01, roof, room/space |
| `conduit_raceway` | Conduit/raceway model elements; drawings; specs; field verification | Conduits; Conduit Fittings; Cable Trays; Electrical Equipment/Fixtures | conduit; raceway; sleeve; ductbank | Conduit; Raceway; System Type; Size; Comments | Yes | conduit size, raceway system |
| `controls_bms_bas_contactors_relays` | Electrical/control equipment parameters; controls drawings; specs | Electrical Equipment; Mechanical Equipment; Communication/Data Devices | contactor; relay; starter; BMS/BAS | Panel; Circuit Number; System; Controls; Comments | Yes | BAS system, relay/contactor, panel/circuit |
| `commissioning_testing_om_training` | Commissioning records; test reports; O&M manuals; training logs; specs | None by default | None | Comments; Status | Yes | O&M status, test report reference |
| `unknown_ambiguous` | Manual review | None established | None | None established | Yes | N/A |

The Revit parameter inventory also includes project-specific names such as `DMET_Instance_ConduitSize`; those are useful evidence when exported, but the current rule table primarily checks the parameter names listed above.

## Table 5 - Status Decision Logic

| Status | Meaning | When Assigned | Evidence Requirement | Example | Allowed Evidence Alignment |
|---|---|---|---|---|---|
| Met | Available evidence appears sufficient for first-pass review. | Direct evidence and required parameters align with the rule. | Strong evidence alignment required. | Panel/circuit fields populated on relevant electrical candidates. | Strong only |
| Not Met | Relevant direct evidence exists, but required values/conditions/parameters are missing or incomplete. | Scoped candidates exist and missing direct parameters support a deterministic gap. | Direct relevant candidates plus missing/incomplete required values. | Electrical elements exist but required panel/circuit values are empty. | Strong when direct missing evidence is clear |
| Needs Human Review | Model data alone cannot close the requirement. | Manual/spec/drawing/field intent, weak/mismatch evidence, or guardrail escalation. | Drawing/specification/field/owner/project review. | Demolition/protection requirement with phase context not sufficient. | Weak; MismatchRisk; ManualOnly; sometimes Partial |
| Insufficient Model Data | Requirement appears model-checkable, but export lacks enough relevant evidence. | No relevant scoped model candidates or required evidence is absent from export. | More model/export evidence needed. | Model-checkable category absent from the export. | Weak |
| Not Applicable | Outside selected discipline/scope/filter. | Discipline filter excludes the row. | None for current view. | Plumbing row in Electrical-only view. | ManualOnly |

## Table 6 - Evidence Alignment Logic

| Evidence Alignment | Meaning | Typical Cause | Can Produce Met? | Typical Status | Example |
|---|---|---|---|---|---|
| Strong | Evidence categories and required direct parameters align. | Expected category and direct parameter evidence are present. | Yes | Met | Electrical candidate has Voltage, Panel, and Circuit Number. |
| Partial | Some evidence aligns but semantic closure is incomplete. | Candidate category is relevant but required context is incomplete. | No under current guardrail | Needs Human Review or Not Met | Level or category evidence exists but direct requirement proof is incomplete. |
| Weak | Evidence is broad or not direct enough. | Candidate pool is relevant but lacks required direct parameters. | No | Needs Human Review | Grounding row with electrical candidates but weak grounding closure. |
| MismatchRisk | Specific evidence requested but only generic/broad evidence exists. | Spec/manufacturer/labeling intent found without direct proof. | No | Needs Human Review | Manufacturer/spec row with equipment presence but no product data closure. |
| ManualOnly | No model-based evidence can close the requirement. | Manual/drawing/spec/field requirement or no deterministic rule. | No | Needs Human Review | Owner approval or commissioning/training deliverable. |

## Table 7 - Validation Type Logic

| Validation Type | Meaning | Used When | Model Evidence Role | Human/Document Review Role | Example Requirement Type |
|---|---|---|---|---|---|
| Model | Depends primarily on model elements and parameters. | Direct Revit categories/parameters can answer the requirement. | Primary evidence source. | Review may still confirm context. | `panel_circuit_power` |
| Hybrid | Combines model evidence with drawings/specs/manual review. | Some Revit evidence exists but external context matters. | Supporting evidence. | Required for closure when direct evidence is incomplete. | `grounding_bonding_conductors` |
| Drawing | References drawings/plans/sheets/details. | Requirement says drawings, plans, schedules, details, or as shown. | Cross-check/supporting. | Primary closure source. | drawing/spec coordination rows |
| Specification | Depends on specs, product data, manufacturer, standards. | Manufacturer, catalog, submittal, product data, acceptable manufacturer language. | Only closes if direct metadata exists. | Usually primary. | `manufacturer_product_spec_submittal` |
| Manual | Requires field verification, owner decision, closeout, or coordination. | Demolition/protection/commissioning/manual review language. | Supporting at best. | Primary closure source. | `field_execution_demolition_protection` |

## Table 8 - Guardrails

| Guardrail | Purpose | Applies To | What It Prevents | Result When Triggered |
|---|---|---|---|---|
| No full-model fallback for high-risk rules | Avoid candidate leakage from all 21,868 elements. | Grounding, plumbing hose bibb/RPZ/valve, manufacturer/spec, identification, manual/demolition, conduit, commissioning. | Inflated evidence and urgency from unrelated records. | Needs Human Review or Insufficient Model Data. |
| No category + level Met for semantic/spec/manual requirements | Prevent generic evidence from closing specific intent. | Spec/manual/high-risk semantic rows. | “Equipment exists on a level” becoming Met. | Status stays reviewable. |
| No Level-only closure for grounding/hose bibb/demolition/spec | Keep placement evidence in its lane. | Grounding, plumbing, demolition, spec/manual rows. | Roof/level/elevation words overriding true intent. | Semantic rule wins; often Needs Human Review. |
| No Met unless alignment is Strong | Enforce evidence sufficiency. | All results. | Weak, MismatchRisk, ManualOnly, or Partial evidence becoming Met. | Downgrade/escalate to Needs Human Review. |
| No Critical urgency from leaked candidate pools | Keep urgency tied to scoped evidence. | Key Issue ranking. | Full-model candidate count inflating ImpactScale. | ImpactScale becomes 0; Critical blocked where scope invalid. |
| No contradictory reasoning after status change | Keep visible explanation aligned with final status. | Guardrail status rewrites. | “Marked as Met” text under non-Met statuses. | Reasoning/action/limitations are regenerated. |
| No “No action required” for non-Met statuses | Preserve actionable follow-up. | Needs Human Review, Not Met, Insufficient Model Data. | Non-Met card saying no action is needed. | Next Best Action becomes review/update guidance. |

## Table 9 - Key Issue Score Formula

| Factor | Weight | What It Measures | How It Is Scored | Why It Matters |
|---|---:|---|---|---|
| StatusSeverity | 0.25 | Severity of final status. | Not Met highest; Insufficient Model Data medium/high; Needs Human Review depends on type risk and evidence gap. | Status is the first urgency signal. |
| DeliverableImpact | 0.20 | Likely effect on review, handoff, closeout, safety, coordination, or deliverable readiness. | Higher for high-risk types and direct blockers. | Prioritizes items likely to affect the next deliverable. |
| Actionability | 0.20 | Whether a discipline can act on specific elements/parameters. | High with element IDs and missing parameters; medium with clear review action; low when vague. | Helps teams choose work that can move. |
| EvidenceGap | 0.15 | How much required evidence is missing or weak. | High for missing direct evidence, weak/mismatch/manual evidence; medium for partial evidence. | Highlights evidence needed for closure. |
| RequirementTypeRisk | 0.10 | Semantic risk of the requirement family. | High for grounding, demolition, fire alarm/security/life-safety, panel/circuit, hose bibb/RPZ. | Prevents low-confidence high-risk rows from disappearing. |
| ImpactScale | 0.10 | Size of valid scoped affected element set. | `min(1.0, log10(affectedCount + 1) / 2.0)`. | Measures breadth only when scope is trustworthy. |

Formula:

`KeyIssueScore = 0.25*StatusSeverity + 0.20*DeliverableImpact + 0.20*Actionability + 0.15*EvidenceGap + 0.10*RequirementTypeRisk + 0.10*ImpactScale`

Confidence is not a core urgency factor. Confidence is reliability, not urgency. It is used only as a small modifier. ImpactScale only counts scoped affected elements; full-model fallback or invalid candidate scope sets ImpactScale to 0.

## Table 10 - Key Issue Factor Scoring

| Factor | High Score | Medium Score | Low Score | Zero / Excluded | Notes |
|---|---|---|---|---|---|
| StatusSeverity | Not Met; high-risk Needs Human Review. | Insufficient Model Data; moderate Needs Human Review. | Low-risk Needs Human Review. | Met and Not Applicable excluded. | Needs Human Review uses type risk and evidence gap. |
| DeliverableImpact | Safety, grounding, fire alarm/security, panel/circuit, demolition, hose bibb/RPZ blockers. | Spec/submittal, identification, technology coordination. | Generic level/location cleanup. | Met/Not Applicable. | Tied to deliverable and handoff risk. |
| Actionability | Direct elements plus missing parameters and element IDs. | Clear review action and discipline owner. | Vague action or no candidates. | No actionable path. | Specific next action increases score. |
| EvidenceGap | Direct required evidence missing; weak/mismatch/manual evidence. | Partial evidence or incomplete context. | Evidence mostly complete. | No gap. | Strong evidence lowers gap. |
| RequirementTypeRisk | Grounding, demolition, fire alarm/security, panel/circuit, hose bibb/RPZ. | Manufacturer/spec, identification, drawing/manual, technology, controls. | Generic level/location. | None. | Semantic risk prevents important rows from ranking too low. |
| ImpactScale | Many valid scoped affected elements. | Some scoped affected elements. | Few scoped elements. | Candidate scope invalid or full-model fallback used. | Does not count leaked 21,868-element pools. |

## Table 11 - Urgency Labels

| Urgency | Meaning | Typical Conditions | Example | Can Be Assigned From Score Alone? |
|---|---|---|---|---|
| Critical | Potential deliverable blocker or immediate coordination risk. | High score, high requirement type risk, valid candidate scope. | Grounding or panel/circuit issue with scoped evidence. | No |
| High | Likely to affect next review, deliverable, or discipline handoff. | High score but not Critical, or fallback/scope guard prevents Critical. | Missing circuit data on relevant electrical elements. | No |
| Medium | Needs action but is not first blocker. | Moderate score and clear follow-up. | Partial device metadata cleanup. | No |
| Low | Track through normal QA/QC. | Low-risk cleanup or info/unknown normalized downward. | Generic placement cleanup. | No |
| Needs Review | Priority depends on drawings, specifications, owner/project context, or human judgment. | ManualOnly/Weak/MismatchRisk and lower type risk, or review-driven closure. | Manufacturer/spec row without product metadata. | No |

Urgency is semantic/action priority, not only a numeric threshold.

## Table 12 - Requirement Card Display Rules

| Section | Visible by Default? | Collapsed in Dropdown? | Purpose | Data Source |
|---|---|---|---|---|
| Header | Yes | No | Shows status, urgency, discipline, confidence, row reference. | `RequirementCheckResult` summary fields |
| Requirement Text | Yes | No | Shows the source owner requirement. | Workbook row text |
| Decision Summary | Yes | No | Explains final status and confidence. | `StatusReason`, `ConfidenceReason`, `Reasoning` |
| Key Parameters Considered | Yes | No | Lists expected and missing parameters. | `ExpectedParameters`, `MissingExpectedParameters` |
| Evidence Summary | Yes | No | Summarizes evidence found. | `EvidenceSummary`, `Evidence` |
| Next Best Action | Yes | No | Gives the follow-up action. | `NextBestAction` |
| Rule & Decision Logic | Partly | Yes | Shows requirement type, rule, validation, alignment. | `RequirementType`, `RuleApplied`, `ValidationType`, `EvidenceAlignment` |
| Filtering Details | No | Yes | Shows candidate filtering stages and scope. | `FilterTrace.CandidateStages` |
| Parameter Checks | No | Yes | Shows expected parameter presence/missing state. | `ParameterChecks`, element parameter checks |
| Matched Elements | No | Yes | Shows matched element evidence. | `MatchedElements` |
| Element IDs | Preview visible | Details collapsed | Supports Revit follow-up/copy. | `MatchedElementIds`, `ElementIdCopyText` |
| Source & Traceability | Yes/partly | Some path details collapsed | Shows workbook/report/source references. | report metadata and source row fields |

## Table 13 - Hidden JSON Fields

| JSON Field | Level | Purpose | Populated From | Used By Ask EMA AI? |
|---|---|---|---|---|
| `requirement_type` | requirement result; key issue | Semantic family for the requirement. | `RequirementSemanticClassifier` and engine result. | Yes |
| `requirement_type_reason` | requirement result | Why the family was selected. | Semantic profile. | Yes |
| `validation_type` | requirement result | Model/Drawing/Specification/Manual/Hybrid classification. | Semantic profile and `ValidationTypeClassifier`. | Yes |
| `rule_applied` | requirement result/filter trace | Deterministic rule name used. | Rule context / semantic profile. | Yes |
| `candidate_scope` | requirement result | Human-readable scope reason. | `CandidateScopeReason`. | Yes |
| `allowed_categories` | requirement result/filter trace | Categories allowed for candidate search. | Semantic profile. | Yes |
| `excluded_categories` | requirement result/filter trace | Categories excluded from candidate search. | Semantic profile. | Yes |
| `fallback_used` | requirement result/filter trace | Whether fallback was used. | Engine result/filter trace. | Yes |
| `fallback_allowed` | requirement result/filter trace | Whether fallback is allowed for rule. | Semantic profile/rule context. | Yes |
| `candidate_scope_valid` | requirement result/filter trace/key issue | Whether scoped candidate pool is trusted for scoring. | Engine/ranker. | Yes |
| `full_model_fallback_used` | requirement result/filter trace/key issue | Whether full-model fallback occurred. | Engine/ranker. | Yes |
| `parameter_checks` | matched element; requirement context | Parameter presence/value status. | `ParameterCheckResult`. | Yes |
| `matched_element_ids` | requirement result | Revit element IDs for follow-up. | matched element records. | Yes |
| `element_id_copy_text` | requirement result | Semicolon-delimited copy text for Revit IDs. | matched element IDs. | Yes |
| `key_issue_score` | key issue; requirement result | Prioritized issue score. | `KeyIssueRanker`. | Yes |
| `urgency_reason` | key issue; requirement result | Explanation of urgency. | `KeyIssueRanker`. | Yes |
| `score_factors` | key issue; requirement result | Factor-level score breakdown. | `KeyIssueRanker`. | Yes |
| `ai_lookup_hints` | requirement result | Search terms, evidence location, likely owner, suggested question. | report generator helpers. | Yes |

## Table 14 - Canary Row Results

Parsed from the latest real-data report hidden JSON.

| Row | Requirement Intent | Expected Rule | Actual Rule | Status | Validation Type | Evidence Alignment | Candidate Scope Valid? | Full-Model Fallback Used? | Result OK? | Notes |
|---:|---|---|---|---|---|---|---|---|---|---|
| 22 | Outlet/circuit/power for commercial washer outlet. | `outlet_circuit_assignment` / `panel_circuit_power` | `outlet_circuit_assignment` | Needs Human Review | Hybrid | Weak | Yes | No | Yes | Uses electrical fixture/equipment scope and Voltage/Panel/Circuit evidence. |
| 100 | Identification/manufacturer/spec. | `manufacturer_product_spec_submittal` | `manufacturer_product_spec_submittal` | Needs Human Review | Specification | Mismatch Risk | Yes | No | Yes | Not Met from equipment/category presence. |
| 103 | Protection/manual field execution. | `field_execution_demolition_protection` | `field_execution_demolition_protection` | Needs Human Review | Manual | Mismatch Risk | No | No | Yes | Correctly not lighting fixture coverage; broad manual support scope is not used for model closure. |
| 112 | Demolition/existing/salvage. | `field_execution_demolition_protection` | `field_execution_demolition_protection` | Needs Human Review | Manual | Weak | No | No | Yes | Requires drawings/phase/manual evidence. |
| 113 | Abandoned outlets / blank covers. | `field_execution_demolition_protection` | `field_execution_demolition_protection` | Needs Human Review | Manual | Weak | No | No | Yes | Not simple outlet circuit assignment. |
| 133 | Insulated ground conductor in conduit systems. | `grounding_bonding_conductors` | `grounding_bonding_conductors` | Needs Human Review | Hybrid | Weak | Yes | No | Yes | Uses grounding/conductor semantic rule, not mechanical/level. |
| 142 | Data/voice closet ground bar. | `grounding_bonding_conductors` | `grounding_bonding_conductors` | Needs Human Review | Hybrid | Weak | Yes | No | Yes | Not closed from technology coverage. |
| 149 | Grounding and bonding / fastening/underground connector restriction. | `grounding_bonding_conductors` | `grounding_bonding_conductors` | Needs Human Review | Hybrid | Weak | Yes | No | Yes | Not mechanical equipment placement. |
| 150 | Technology/data/CATV/CCTV/MATV grounding. | `grounding_bonding_conductors` | `grounding_bonding_conductors` | Needs Human Review | Hybrid | Weak | Yes | No | Yes | Grounding outranks technology device coverage. |
| 600 | RPZ at roof hose bibbs per roof zone. | `plumbing_hose_bibb_rpz_valves` | `plumbing_hose_bibb_rpz_valves` | Needs Human Review | Model | Weak | Yes | No | Yes | Plumbing scope; no level-assignment fallback. |
| 601 | Shut off valve for hose bibbs. | `plumbing_hose_bibb_rpz_valves` | `plumbing_hose_bibb_rpz_valves` | Needs Human Review | Model | Weak | Yes | No | Yes | Plumbing fixture/accessory evidence; no full model pool. |
| 602 | Exterior hose bibbs, valve box, RPZ/threaded connections. | `plumbing_hose_bibb_rpz_valves` | `plumbing_hose_bibb_rpz_valves` | Needs Human Review | Model | Weak | Yes | No | Yes | “Ground valve box” no longer misclassified as grounding. |

## Table 15 - Real Report Summary

| Metric | Value |
|---|---|
| Report path | `C:\Users\Eliuth Chavero\AppData\Local\Temp\EMA_AI_Report_Tests\EMA_AI_Requirement_Check_MEP-NISD-MIDDLE SCHOOL 8_All Disciplines_20260608_055511.html` |
| Requirements parsed | 804 |
| Model elements parsed | 21,868 |
| Met | 142 |
| Not Met | 0 |
| Needs Human Review | 662 |
| Insufficient Model Data | 0 |
| Not Applicable | 0 |
| Validation type counts | Drawing=10; Hybrid=124; Manual=113; Model=399; Specification=158 |
| Evidence alignment counts | Manual Only=189; Mismatch Risk=151; Strong=142; Weak=322 |
| Key issue counts | 25 |
| Urgency counts | Critical=25 |
| Requirement type counts | unknown_ambiguous=190; panel_circuit_power=112; field_execution_demolition_protection=89; manufacturer_product_spec_submittal=83; level_location_mounting_placement=72; identification_labeling_nameplate=54; conduit_raceway=39; technology_low_voltage_security_fire_alarm=38; grounding_bonding_conductors=34; outlets_receptacles_devices=25; plumbing_hose_bibb_rpz_valves=22; mechanical_equipment_coverage=13; drawing_spec_manual_owner_approval=12; commissioning_testing_om_training=11; controls_bms_bas_contactors_relays=10 |
| Candidate leakage count detectable from hidden JSON | 0 |
| Contradiction count detectable from hidden JSON | 0 |

## Current Gaps to Track

| Gap | Current Behavior | Documentation Note |
|---|---|---|
| Broad manual support scopes | Some demolition/protection rows show large but non-full-model supporting candidate counts and `candidate_scope_valid=false`. | Safe for scoring because invalid scope prevents impact inflation, but the evidence card can still look broad. |
| Top 25 key issues all Critical | Latest report urgency counts are Critical=25 because the top key issue set is dominated by high-risk semantic families. | This reflects current ranker output, not a promise that only Critical issues exist. |
| Spec/manufacturer scopes are broad | Manufacturer/spec rows inspect broad equipment/product categories and remain Needs Human Review with Mismatch Risk. | Correctly not closed from category presence, but review load may be broad. |
