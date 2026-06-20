# Troubleshooting Manual

## Backend Offline
- Check Docker/services.
- Call `http://localhost:8010/health`.

## Landing Discover/Bootstrap Fails
- Validate landing root exists.
- Check Windows host path vs container `/app/landing` mapping.

## Run Ingest Fails
- Verify project-scoped ingest endpoint.
- Review Processing response JSON and Debug / Logs entries.

## Selected Project Mismatch
- Clear stale `ema-ai-selected-project-id` if project deleted.

## Revit Add-in Not Visible
- Re-run installer dry-run and install commands.
- Validate `%APPDATA%\\Autodesk\\Revit\\Addins\\<year>`.

## Revit 2026 ElementId Build Error
- Ensure branch includes `ElementId` compatibility helper updates.
