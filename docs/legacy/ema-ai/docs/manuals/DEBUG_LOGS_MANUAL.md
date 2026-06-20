# Debug / Logs Manual

## Purpose
Operational diagnostics for project setup, landing sync, and ingest workflows.

## Use
- Filter by project, operation type, severity, status, run ID, request ID.
- Open log details (request/response/warnings/errors/environment).
- Generate debug bundle for local triage.

## Important Boundaries
- Diagnostic logs do not alter readiness.
- Logs are not audit-compliant production telemetry.
- Secrets and raw file contents must be redacted/excluded.

## Path Mismatch Diagnosis
If user path is Windows and backend `landing_dir` is `/app/landing`, map volumes correctly or use container-reachable paths.
