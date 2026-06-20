# Rule Taxonomy Recommendations

## Priority Order
1. Grounding, bonding, and conductors
2. Hose bibb, RPZ, backflow, and valve coordination
3. Manufacturer, product, spec, and submittal requirements
4. Identification, labels, tags, and nameplates
5. Demolition, abandoned, salvage, and protection requirements
6. Drawing/spec/manual/owner approval requirements
7. Panel, circuit, and power assignment
8. Outlets/receptacles/devices
9. Technology, security, fire alarm, data, and low-voltage coverage
10. Mechanical equipment coverage
11. Level/location/mounting/placement

## Candidate Scoping Rules
- Specific semantic family must outrank generic category/level intent.
- Grounding/bonding/conductor rules must never fall back to technology or mechanical pools.
- Hose bibb/RPZ/valve rules must never fall back to all model records.
- Identification/manufacturer/spec rules must require parameter or document evidence, not category presence alone.
- Demolition/abandoned/salvage rules must require phase/drawing/spec/manual context.
- If no relevant candidates are found, return Insufficient Model Data or Needs Human Review.

## Fallback Behavior
- Full-model fallback should be forbidden for high-risk semantic families.
- Generic level assignment should be used only when the requirement is truly about level/location/placement.
- If the model has no direct evidence, keep the result human-reviewable instead of forcing a Met/Not Met closure.

## Recommended Wording Changes
- Replace vague marked as Met phrases when the evidence is weak or manual.
- Say model evidence is not sufficient instead of implying the check succeeded.
- Show the rule family and why category/level alone is or is not sufficient.
- Explicitly note when evidence comes from drawings, specifications, or owner coordination rather than Revit.
