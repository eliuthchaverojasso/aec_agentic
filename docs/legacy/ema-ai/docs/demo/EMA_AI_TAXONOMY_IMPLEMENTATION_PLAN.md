# EMA AI Taxonomy Implementation Plan

Generated: 2026-06-08

## Objective

Use the universal taxonomy and evidence model from the latest report as the source of truth for future classification changes, without changing engine behavior yet.

## Plan

### Phase 1 - Freeze the taxonomy

- Treat `docs/demo/EMA_AI_UNIVERSAL_REQUIREMENT_TAXONOMY.md` and `docs/demo/EMA_AI_UNIVERSAL_REQUIREMENT_TAXONOMY.json` as the canonical taxonomy reference for the next implementation pass.
- Treat `docs/demo/EMA_AI_REQUIREMENT_TYPE_PRIORITY_ORDER.json` as the canonical priority order for future rule dispatch.
- Treat `docs/demo/EMA_AI_UNIVERSAL_EVIDENCE_PROFILES.json` as the evidence boundary contract.

### Phase 2 - Introduce modifier-first routing

- Add a first-pass modifier layer for hard constraints and prohibitions.
- Make `no`, `only`, `minimum`, `maximum`, `without`, and similar phrases outrank presence-based families.
- Route manufacturer, controls, installation-method, and closeout language before generic equipment or device presence.

### Phase 3 - Tighten candidate scoping

- Remove broad fallback paths for high-risk families.
- Forbid full-model fallback when the requirement belongs to manufacturer/spec, controls, conduit size, installation method, demolition, O&M, or attic-stock families.
- Require direct parameter or document evidence before allowing `Met` on the model-checkable types.

### Phase 4 - Align validation and report wording

- Add hard overrides in the validation classifier for manufacturer/spec, controls, field execution, and closeout language.
- Surface the direct-close vs supporting-context boundary in the hidden JSON and visible report copy.
- Keep `Met` language unavailable when the evidence is only contextual.

### Phase 5 - Add regression tests

- Add row-level regression tests for rows 155, 478, 479, 480, 485, and 491.
- Add tests for the current canary rows so grounding and hose bibb / RPZ stay guarded.
- Add a report-context test that asserts the hidden JSON continues to expose rule, validation, expected evidence, and candidate-scope fields.

### Phase 6 - Run real-data verification

- Re-run the latest workbook against the latest Revit export.
- Confirm the taxonomy reduces false positives without converting them into false Not Met results.
- Confirm the report still remains deterministic and no-overclaim.

## Implementation Notes

- Do not change engine behavior in the documentation-only pass that produced this plan.
- Use the deterministic engine as the source of truth.
- Keep AI advisory-only.
- Preserve the distinction between supporting context and direct closing evidence.

## Success Criteria

- Row 155 no longer closes on generic lighting or technology presence.
- Row 478 no longer closes on mechanical presence.
- Row 479 no longer closes on plumbing or mechanical presence.
- Row 480 no longer closes on mechanical presence.
- Row 485 no longer closes on plumbing presence.
- Row 491 no longer closes on mechanical presence.
- Manufacturer, controls, installation, and closeout requirements stay reviewable unless direct evidence exists.

