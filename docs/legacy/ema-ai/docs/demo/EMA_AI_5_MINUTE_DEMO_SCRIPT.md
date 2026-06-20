# EMA AI 5-Minute Demo Script

## Demo Boundaries (Say This Up Front)

- Local Demo
- Not Production
- Not Official Compliance
- Evidence Candidate data is not official evidence
- Revit runtime smoke is pending unless explicitly demonstrated in Revit

## 1) Open Dashboard (30s)

1. Open `http://127.0.0.1:5173`.
2. Enter Local Demo session from `/login`.
3. Point out project context and local-only labels.

## 2) Executive Overview (45s)

1. Show KPI cards and filter controls.
2. Show USA Project Map:
   - Demo Location markers are synthetic when real coordinates are unavailable.
   - No external map API keys.
3. Open one project from map/table actions.

## 3) Deliverable Tracker / Project Surface (45s)

1. Show readiness summary.
2. Explain deterministic readiness vs advisory/AI surfaces.
3. Show milestone/action context.

## 4) Requirements (40s)

1. Open Requirements page.
2. If client is missing: show actionable blocker state.
3. If client is linked and requirements empty: show honest empty state.

## 5) Documents / Evidence (40s)

1. Open Documents / Evidence.
2. Show drawings/specifications/documents counts.
3. Emphasize Evidence Candidate and metadata-only warning where applicable.

## 6) Processing / Sync (60s)

1. Show operator workflow: Health -> Scan -> Rebuild -> Dry Run -> Ingest -> Snapshot.
2. Confirm heartbeat is read-only and does not auto-ingest.
3. Mention write-guard and explicit manual control.

## 7) Debug / Logs or Dev Mode (40s)

1. Open Debug / Logs:
   - operation timeline
   - request/run IDs
   - warnings/errors
2. Or open Dev Mode to show endpoint checks and local diagnostics.

## 8) Snapshot and Close (40s)

1. Show readiness snapshots/actions state.
2. Close with caveats:
   - Local Demo
   - Not Production
   - Not Official Compliance
   - Revit runtime smoke pending (unless actually shown in Revit)

