# Local Demo Manual (NISD-Style)

## Demo Flow
1. Start backend/frontend locally.
2. In Project Setup, bootstrap `NISD-MIDDLE SCHOOL` from landing folder.
3. Select NISD project.
4. Run Processing / Sync operations:
   - scan
   - rebuild manifest
   - dry-run ingest
   - run ingest
5. Review Deliverable Tracker, Requirements, Documents, Model Health.
6. Open Debug / Logs to show operation timeline and path diagnostics.

## Expected Outcomes
- Project appears in selector and persists.
- Ingest endpoint returns non-404 mapped response.
- Documents are evidence candidates by default.
- Readiness is deterministic and traceable.
