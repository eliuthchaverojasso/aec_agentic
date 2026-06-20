# Readiness Engine Notes

**Last updated:** 2026-05-26  
**Audience:** Product managers, architects, reviewers  
**Purpose:** Explain what "readiness" means in EMA AI and what it does NOT mean.  

---

## One-Sentence Summary

**Readiness is a deterministic, auditable score that shows what percentage of Owner Requirements are supported by accepted evidence.**

---

## What Readiness IS

### Deterministic
- Readiness is calculated from database rows, not AI opinions.
- Given the same data, the same project produces the same score.
- The calculation is testable and auditable.

### Auditable
- Every piece of evidence that supports a requirement is traceable.
- Every acceptance decision is recorded with: who, when, and why.
- The score can be explained: "This project is 78% ready because 78% of requirements have accepted evidence."

### Based on Accepted Evidence
- Readiness counts only evidence that has been **accepted** by a reviewer.
- **Candidate evidence** (automatically found but not yet reviewed) does NOT count.
- **Rejected evidence** (reviewed and deemed not applicable) does NOT count.

### Clear About Gaps
- Missing evidence: "No candidate evidence found for this requirement."
- Needs review: "Candidate evidence exists but hasn't been accepted yet."
- Approved: "Evidence has been accepted by a reviewer."

---

## What Readiness IS NOT

### NOT Official Compliance
- Readiness is **not** a compliance certification.
- It cannot be submitted to regulators or used as proof of compliance.
- It is a **pilot tool** for internal project visibility.

### NOT AI Approval
- AI cannot approve readiness or compliance.
- AI may suggest evidence ("This drawing looks relevant") but humans decide.
- Approval is always human, always traceable.

### NOT Automatic
- No requirements are auto-approved.
- Every piece of accepted evidence requires human review.
- The workflow is explicit: candidate → reviewed → accepted/rejected.

### NOT a Percentage Guarantee
- 78% ready does NOT mean the project is 78% complete.
- It means 78% of the defined requirements have accepted evidence.
- The remaining 22% may be genuinely missing or may need different evidence.

### NOT Final or Immutable
- Readiness can change if evidence is rejected or accepted.
- New evidence can be added anytime.
- Reviewers can change their decisions with an audit trail.

---

## Readiness Calculation

### Formula (Simplified)

```
Readiness Score = (Requirements with accepted evidence / Total requirements) × 100
```

### Example

| Requirement | Evidence State | Counts Toward Readiness? |
|---|---|---|
| REQ-001: Insulation R-value | Accepted (Revit model) | ✅ YES |
| REQ-002: Window U-value | Candidate (spec found) | ❌ NO |
| REQ-003: Air tightness | Rejected (doesn't apply) | ❌ NO |
| REQ-004: HVAC efficiency | Missing (no evidence) | ❌ NO |
| REQ-005: Fire rating | Accepted (manual review) | ✅ YES |

**Calculation:** 2 accepted out of 5 = 40% ready.

---

## Readiness States

### Requirement States

| State | Meaning | Next Step |
|---|---|---|
| **Completed** | Accepted evidence covers this requirement | Confirm in review; move to next requirement |
| **Missing** | No candidate evidence found anywhere | Find or generate evidence, or mark not applicable |
| **In Progress** | Candidate evidence exists but not yet reviewed | Review and accept/reject |
| **Needs Human Review** | Evidence found but auto-detection is uncertain | Reviewer must decide |
| **Not Applicable** | Requirement doesn't apply to this project | Mark as not applicable (doesn't count against readiness) |
| **Approved with Carryover** | Approved now but will need updated evidence later (e.g., at design stage) | Plan for follow-up review |

### Evidence States

| State | Meaning | Who Sets? | Impact on Readiness |
|---|---|---|---|
| **Candidate** | System found it automatically; not yet reviewed | System | ❌ Does NOT count |
| **Accepted** | Reviewer confirmed it meets the requirement | Reviewer | ✅ COUNTS |
| **Rejected** | Reviewer confirmed it doesn't meet the requirement | Reviewer | ❌ Does NOT count |
| **Archived** | Old/superseded evidence (not active) | Reviewer | ❌ Does NOT count |

---

## Evidence Sources & Confidence

Readiness can be supported by evidence from many sources. Each source has different properties:

### Revit Model Data
- **What:** Geometry, properties, parameters extracted from Revit.
- **Example:** "Wall assembly is Insulated Masonry @ R-40 per Revit parameter."
- **Confidence:** High (comes directly from model).
- **Acceptance:** Usually accepted with minimal review.
- **Audit trail:** Source file, export timestamp, parameter path.

### Drawings
- **What:** Extracted text, dimensions, notes from PDF or DWG files.
- **Example:** "Detail A shows 8\" insulation layer per drawing."
- **Confidence:** Medium (depends on OCR or manual extraction).
- **Acceptance:** Requires human review of source image.
- **Audit trail:** Source document, page, section, extraction method.

### Specifications
- **What:** Extracted text from specification documents.
- **Example:** "Section 07-21-00 requires minimum R-40 cavity insulation per model audit."
- **Confidence:** Medium (extracted text, not fully parsed).
- **Acceptance:** Requires review to confirm relevance.
- **Audit trail:** Source document, section, extraction method.

### Manual Entry
- **What:** Evidence added by a team member without automatic extraction.
- **Example:** "REQ-001 approved by Alice on 2026-05-25 based on site visit observations."
- **Confidence:** Depends on reviewer expertise.
- **Acceptance:** By definition, manual entries are accepted (require reviewer signature).
- **Audit trail:** Reviewer name, timestamp, evidence text.

### Internal EMA Standards
- **What:** Reference requirements or known-good standards applied to the project.
- **Example:** "Project matches EMA Standard Detail DEV-34 for insulation (R-40 minimum)."
- **Confidence:** High (if standard is well-defined).
- **Acceptance:** Usually accepted with minimal review if standard is applicable.
- **Audit trail:** Reference to standard, version, applicability notes.

---

## Acceptance Workflow

### Step 1: Discovery
System scans project data and finds candidate evidence.

```
Revit model → Extract R-value parameter → "40 (R-value)" found
OR
Specification PDF → Extract text → "R-40 insulation" found
```

**Result:** Evidence marked as **candidate**.

### Step 2: Review
A reviewer looks at the candidate evidence and decides:
- Is it relevant to the requirement?
- Does it meet the requirement's criteria?
- Is it credible and traceable?

### Step 3: Accept or Reject
- **Accept:** Reviewer confirms evidence supports requirement.
  - Sets state to **accepted**.
  - Records reviewer name, timestamp, reason.
  - Evidence now counts toward readiness.

- **Reject:** Reviewer confirms evidence does NOT support requirement.
  - Sets state to **rejected**.
  - Records reviewer name, timestamp, reason.
  - Evidence does NOT count toward readiness.

### Step 4: Audit Trail
All decisions are logged:

```json
{
  "evidence_id": "ev-001",
  "requirement_id": "req-001",
  "state_transition": "candidate → accepted",
  "reviewer": "alice@example.com",
  "reviewed_at": "2026-05-25T14:30:00Z",
  "reason": "Confirmed R-value in Revit model matches spec requirement",
  "source": "Revit model export",
  "source_details": {
    "file": "NISD_Main_Building_20260520.rvt",
    "parameter": "R-value",
    "value": "40"
  }
}
```

---

## Readiness Interpretation Examples

### Example 1: Simple Pass

**Project:** NISD Renovation, Building A

| Requirement | Status | Evidence | Readiness Impact |
|---|---|---|---|
| Insulation R-value | Completed | Revit model (accepted) | ✅ +1 |
| Window U-value | Completed | Specification (accepted) | ✅ +1 |
| Air tightness | Completed | Manual review (accepted) | ✅ +1 |

**Readiness:** 3/3 = **100% Ready**

**Interpretation:**  
*"This project meets all defined requirements. All evidence has been reviewed and accepted. The project is ready for delivery."*

### Example 2: Partial Completion

**Project:** NISD Renovation, Building B

| Requirement | Status | Evidence | Readiness Impact |
|---|---|---|---|
| Insulation R-value | Completed | Revit model (accepted) | ✅ +1 |
| Window U-value | In Progress | Candidate evidence in spec (not yet reviewed) | ❌ +0 |
| Air tightness | Missing | No evidence found | ❌ +0 |
| Moisture barrier | Completed | Manual evidence (accepted) | ✅ +1 |

**Readiness:** 2/4 = **50% Ready**

**Interpretation:**  
*"This project has 2 out of 4 requirements covered. Window U-value has candidate evidence but needs reviewer approval. Air tightness has no evidence yet and needs investigation. Moisture barrier is confirmed ready. Next steps: review window spec, find or generate air tightness evidence."*

### Example 3: At Risk

**Project:** NISD Renovation, Building C

| Requirement | Status | Evidence | Readiness Impact |
|---|---|---|---|
| Insulation R-value | Completed | Revit model (accepted) | ✅ +1 |
| Window U-value | Missing | No evidence found | ❌ +0 |
| Air tightness | Missing | No evidence found | ❌ +0 |
| Fire rating | Completed | Spec section (accepted) | ✅ +1 |
| Accessibility | Needs Human Review | Multiple candidates, conflicting info | ❌ +0 |

**Readiness:** 2/5 = **40% Ready**

**Interpretation:**  
*"This project is at risk. Only 2 out of 5 requirements are confirmed ready. 2 requirements have no evidence and need urgent investigation. 1 requirement has conflicting evidence and needs expert review. Recommended action: assign team members to find/generate missing evidence for windows and air tightness; schedule expert review for accessibility requirement."*

---

## NOT Readiness: What It Doesn't Measure

### Design Completeness
Readiness does NOT measure how far along design is (30%, 50%, 95%).

- A project can be 100% ready against requirements but still only 40% designed.
- Or 90% designed but only 20% of requirements have evidence yet.

**These are separate metrics.**

### Compliance Status
Readiness does NOT prove compliance with codes, standards, or regulations.

- Readiness shows evidence exists for requirements.
- Compliance requires official review by engineers, auditors, or authorities.
- EMA AI supports the readiness discussion but cannot certify compliance.

### Budget or Schedule
Readiness does NOT predict cost or timeline.

- A requirement with no evidence might be quick to close or expensive to fix.
- Readiness is about evidence visibility, not project cost.

### Quality or Constructability
Readiness does NOT guarantee the design will build.

- Evidence might support a requirement, but the design might still have conflicts or construction challenges.
- Readiness is a document checklist, not a design quality review.

---

## Using Readiness in Practice

### For Project Managers
*"Readiness tells me which requirements still need evidence. I can use it to assign work and track progress."*

### For Designers
*"When I finalize a design decision, I can add evidence here so the project manager sees it's ready."*

### For QA/Compliance Reviewers
*"I use readiness to see which requirements have been claimed to be ready. I then verify the evidence is valid."*

### For Executives
*"Readiness gives me a scorecard of project readiness at a glance. 78% ready means clear visibility into what's done and what's missing."*

### For Clients
*"I can see what evidence supports each requirement and what still needs work. It's transparent, auditable, and traceable to source documents."*

---

## Limitations & Future Work

### Current MVP Limitations
- Evidence is limited to what the system can automatically discover (Revit exports, indexed documents, manual entry).
- No intelligent PDF/DOCX parsing yet (candidate extraction only).
- No integration with live project management or design tools.
- No scheduled re-checks or auto-invalidation of old evidence.

### Future Enhancements
- **PDF/DOCX parsing:** Extract evidence from more document types without manual indexing.
- **Source linking:** Deep links to evidence in original files (Revit elements, drawing pages, spec sections).
- **Evidence expiration:** Flag evidence as stale if not updated in N days.
- **Requirement evolution:** Track requirement changes across design phases and migrate accepted evidence.
- **Integration with live systems:** Auto-ingest evidence from ACC, UNANET, BIM360 as projects progress.
- **Predictive analytics:** Suggest which requirements are at risk based on project timeline.

---

## Decision Tree: Is X "Readiness"?

```
Does it measure whether requirements have accepted evidence?
  ├─ YES → It's readiness ✅
  └─ NO → It's not readiness ❌
    ├─ Does it measure design completeness (%)? → It's design progress, not readiness
    ├─ Does it predict schedule/budget? → It's a project management metric
    ├─ Does it guarantee compliance? → It's a compliance review, not readiness
    └─ Does it measure quality or constructability? → It's a design quality metric
```

---

## Related Files

- `Pipeline/pipeline/app/services/readiness_service.py` — Readiness calculation code
- `Pipeline/pipeline/app/api/readiness.py` — Readiness API endpoints
- `Pipeline/pipeline/app/models.py` — Requirement and evidence database models
- `docs/product/READINESS_SEMANTICS.md` — Detailed readiness data model and states
- `docs/api/READINESS_API.md` — API reference for readiness endpoints

