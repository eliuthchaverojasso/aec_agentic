<!-- STATUS: HISTORICAL | Original date: 2026-05-26 | This document describes a demo target that has passed. -->
<!-- Current product focus: Revit-first Owner Requirements Readiness — see .ai/CURRENT_STATE.md -->
<!-- For current demo guidance use: docs/demo/NISD_LOCAL_DEMO_SCRIPT.md or docs/demo/EMA_AI_MONDAY_DEMO_FLOW.md -->

# Thursday Client Demo Plan (HISTORICAL — 2026-05-26)

**Original date:** 2026-05-26
**Audience:** EMA AI engineering team, client stakeholders  
**Duration:** 20–25 minutes  
**Environment:** Local (Docker) or Azure (not yet deployed)  

---

## Demo Purpose & Context

**One-Sentence Value Proposition:**  
*EMA AI helps identify whether a project is ready against Owner Requirements by connecting project data, requirements, evidence, gaps, and readiness status.*

**What the Client Wants to See:**
- Can the system compare Owner Requirements against project data?
- Can it show what evidence was found?
- Can it show where evidence came from?
- Can it separate automatic findings from items requiring human review?
- Can they access the system and try a workflow?

**What the Client Does NOT Care About Yet:**
- Model viewer, full RBAC, SSO, email integration.
- Compliance approval or official certification.
- Production deployment or enterprise hardening.
- AI/LLM magic — deterministic logic is better.

---

## Happy Path Walkthrough (20 minutes)

### 1. Login (1 minute)
**What to show:** Basic login screen with demo credentials.

**Demo account:**
- Username: `demo`
- Password: (use configured demo password)

**Talking point:**  
*"This is a controlled pilot, so we're using demo credentials today. In production, this would be SSO or enterprise auth, but for now we're focused on the workflow."*

**Fallback:**  
If login fails, show a screenshot of the dashboard.

---

### 2. Project Portfolio (2 minutes)
**Route:** `/` or `/portfolio`  
**What to show:** List of projects with status badges (ready, at-risk, in-progress).

**Talking point:**  
*"Here's the project portfolio. Each project has a readiness status. At-risk projects are flagged so the team can prioritize. Let's click into one to see the details."*

**User action:** Click on a project (e.g., "NISD — Renovation Package 1").

**Fallback:**  
If the project list doesn't load, show a screenshot.

---

### 3. Project Overview (2 minutes)
**Route:** `/projects/{project_id}`  
**What to show:** Readiness score, gap summary, evidence snapshot.

**Key elements:**
- Readiness score (e.g., 78% ready, 12% gaps, 10% needs review).
- Count of Owner Requirements (e.g., 25 total).
- Count of evidence items (candidate, accepted, rejected).
- Highlighted gaps (e.g., "3 missing", "2 needs human review").

**Talking point:**  
*"This is the project overview. The score of 78% means 78% of the Owner Requirements are supported by accepted evidence. The 12% gap shows requirements with no evidence yet. The 10% 'needs review' are items that need human judgment — this isn't automated."*

**User action:** Scroll to see the full summary, then click "View Requirements."

**Fallback:**  
Show screenshot of overview page.

---

### 4. Owner Requirements & Evidence (8 minutes)
**Route:** `/projects/{project_id}/requirements`  
**What to show:** Table of requirements with evidence status for each.

**Key columns:**
- Requirement ID (e.g., "REQ-001")
- Requirement text (e.g., "Building envelope insulation R-value ≥ R-40")
- Evidence status (candidate, accepted, missing, needs review)
- Evidence count (how many pieces support this requirement)
- Evidence type (Revit data, drawing, specification, manual)

**Talking point:**  
*"Each requirement is linked to the evidence that supports it. Green means 'accepted' — the team reviewed the evidence and confirmed it meets the requirement. Yellow means 'candidate' — we found something that might work, but it needs review. Red means 'missing' — we haven't found evidence yet."*

**Demo interaction (this is the key moment):**

1. **Click on a requirement with accepted evidence (green):**
   - Show the evidence list drawer/modal.
   - Point out the source: "This comes from the Revit model" or "This comes from the specifications."
   - Show the reviewer name and timestamp: "Reviewed by Alice on 2026-05-25."
   - Explain: "Alice reviewed the evidence and confirmed it meets the requirement."

2. **Click on a requirement with candidate evidence (yellow):**
   - Show the candidate evidence.
   - Explain: "This is evidence we found automatically, but it's not accepted yet. A reviewer needs to decide if it counts."
   - Demonstrate accepting the evidence: Enter reviewer name, click "Accept."
   - Show the evidence move to green.

3. **Click on a requirement with missing evidence (red):**
   - Show "No evidence found."
   - Explain: "We scanned the Revit model and the project documents, but we didn't find anything that supports this requirement. The team will need to follow up with the design team or provide manual evidence."

**Talking point (tying it together):**  
*"This is what EMA AI does: it connects requirements to evidence, shows you what's covered and what's missing, and lets your team make the judgment calls. It's not about automating compliance — it's about making the evidence visible so you can make decisions faster."*

**Fallback:**  
Show screenshots of three example requirements (one accepted, one candidate, one missing).

---

### 5. Readiness Score & Gaps (2 minutes)
**Route:** `/projects/{project_id}/readiness`  
**What to show:** Readiness score breakdown, gap list, recommended actions.

**Key elements:**
- Overall score: "Ready" / "At Risk" / "In Progress"
- Breakdown: "X% covered, Y% missing, Z% needs review"
- List of gaps: requirements that don't have accepted evidence
- Recommended actions: "Follow up with design team", "Review specifications", "Resolve manual evidence"

**Talking point:**  
*"This is the readiness summary. We're 78% ready. The 12% gap are specific requirements we need to address before delivery. Here are the recommended next steps."*

**User action:** Scroll to see all gaps and actions.

**Fallback:**  
Show screenshot of readiness page.

---

### 6. Processing / Data Sync (2 minutes)
**Route:** `/processing` (optional, only if time permits)  
**What to show:** How data gets into the system.

**Key steps shown:**
1. **Scan landing folder** — discovers project files, Revit exports, specifications.
2. **Rebuild manifest** — creates a list of what was found.
3. **Ingest (dry-run)** — shows what would happen without committing.
4. **Ingest (real)** — actually loads the data.

**Talking point:**  
*"Behind the scenes, the system scans a landing folder, discovers project files, and ingests them into the database. This process is operator-controlled — we don't auto-ingest everything. The team decides what to load and when."*

**Fallback:**  
Skip this section if running short on time. Show a screenshot if the client asks.

---

## Expected Client Questions & Answers

### Q: How does the system know if evidence is correct?
**A:** It doesn't decide automatically. The team reviews the evidence and marks it as accepted or rejected. The system shows what it found, but humans make the judgment calls. This is a tool to make the evidence visible, not to automate compliance decisions.

### Q: What if we have evidence the system doesn't recognize?
**A:** You can manually add evidence. Right now the MVP is limited to what the system can discover (Revit data, file indexes, manual entry). In the next phase, we'll add richer parsing for PDFs and specifications.

### Q: How do we integrate this with our current workflow?
**A:** The landing folder is your connection point. You put project files there, and the system scans them. As we move to Azure, you'll be able to upload files through the dashboard too.

### Q: Is this officially compliant?
**A:** No. This is a pilot tool to make evidence and readiness visible internally. It's not a compliance system. All official compliance still goes through your normal processes.

### Q: Can we see a live model viewer?
**A:** Not yet. We're focusing on the readiness and evidence workflow first. Model viewer is on the roadmap for the next phase. For now, we show evidence sources and descriptions.

### Q: What about AI/ChatGPT? Are you using that to approve compliance?
**A:** No. The readiness score is deterministic — it's based on the evidence your team accepts. AI might help us extract text from documents in the future, but it won't approve readiness or compliance. Those decisions are always yours.

### Q: Where is this deployed?
**A:** Currently, we're running this locally for demos. We're planning to deploy to Azure in the next sprint so you can test it in your own environment with real project data.

### Q: What data do you need from us?
**A:** Ideally, we want:
- A Revit model (or export JSON).
- Owner Requirements (spreadsheet or document).
- Specifications or standards documents.
- Any existing QA/QC notes or issues.

We'll walk through loading that data and showing you the readiness summary.

### Q: How long will this take to implement for our projects?
**A:** The pilot MVP is ready now. We can start with one test project next week. Full rollout depends on your team's capacity and data availability.

---

## Fallback Plan

**If the live demo fails:**

1. **Before the demo:** Prepare screenshots of all key pages (Portfolio, Overview, Requirements with evidence, Readiness, Processing).

2. **During the demo:** Walk through the screenshots in the same order as the live walkthrough.

3. **Record a video backup:** If there's time before Thursday, record a 5-minute screencast of the happy path. Play this if the live demo crashes.

4. **Have a test account ready:** If the environment is unstable, have a second test instance running (e.g., another Docker container or screenshot library).

---

## Pre-Demo Checklist (Thursday Morning)

- [ ] **Environment is up:** Docker containers running, PostgreSQL healthy, API responding.
- [ ] **Demo user account exists:** Login works with demo credentials.
- [ ] **Demo project is seeded:** At least one project in the system with requirements and evidence.
- [ ] **All happy-path pages load:** Portfolio, Overview, Requirements, Readiness.
- [ ] **Evidence workflow works:** Can accept/reject evidence on Requirements page.
- [ ] **No runtime errors:** Browser console clean, no AppErrorBoundary shown.
- [ ] **Demo script printed:** Have the walkthrough steps written on paper or on a second screen.
- [ ] **Fallback materials ready:** Screenshots, video, or second environment prepared.
- [ ] **Client names & context:** Know who the attendees are and what they care about.
- [ ] **Talking points reviewed:** Practice the one-sentence value prop and key transitions.
- [ ] **Time check:** Run through the walkthrough in 20 minutes without pausing.

---

## Post-Demo Follow-Up

**After the demo, send the client:**

1. **Deck or summary document** explaining:
   - What EMA AI is (readiness + evidence + visibility).
   - What you showed (happy path, evidence linking, readiness summary).
   - What's coming next (Azure deployment, web uploader, PDF parsing).
   - What's not in scope for MVP (model viewer, compliance approval, live integrations).

2. **Roadmap** with P0 (demo), P1 (post-demo hardening), P2 (scaling).

3. **Next steps:** How they can provide data, when they can test, what feedback you need.

4. **Contact info** for technical and product questions.

---

## Success Metrics

**The demo is successful if:**

- ✅ Client sees a working happy path (login → portfolio → requirements → readiness).
- ✅ Client understands the evidence-to-readiness connection.
- ✅ Client understands this is a pilot, not production compliance.
- ✅ Client understands they make the judgment calls, not the AI.
- ✅ Client wants to test with their own data.
- ✅ Client agrees to move forward to Azure pilot phase.

**The demo is not successful if:**

- ❌ Client thinks this is official compliance software.
- ❌ Client thinks the AI automatically approves compliance.
- ❌ Client wants the full model viewer before MVP closes.
- ❌ Client sees confusing UI or runtime errors.
- ❌ Client doesn't understand how to provide their own data.
