# EMA AI Admin Manual

## Scope
Local admin guidance for project setup, diagnostics, and release hygiene.

## Responsibilities
- Maintain local environment health.
- Validate landing-root configuration and path mappings.
- Run deterministic Processing / Sync operations.
- Keep users/roles labeled as local planning only.
- Export redacted debug bundles for issue triage.

## Safety
- Do not store or expose secrets in logs.
- Do not treat evidence candidates as official evidence.
- Keep ingest operator-controlled.
- Keep real project files out of git.

## Pre-Release Checks
- Backend tests/build green.
- Frontend build green.
- Revit build/install flow validated when in scope.
- Protected files unstaged (`opencode.json`, `.env*`, generated artifacts).
