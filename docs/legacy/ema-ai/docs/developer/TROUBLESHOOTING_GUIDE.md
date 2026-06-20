# Developer Troubleshooting Guide

## Docker / Health
- If `/health` fails, check container status and ports.

## Run Ingest 404
- Ensure frontend uses `/api/v1/projects/{project_id}/landing/ingest`.

## Landing Path Mismatch
- Backend may run with `/app/landing`; host Windows path must be mounted.

## Revit 2026 Build
- Ensure ElementId compatibility helper is present.

## Protected File Drift
- Keep `opencode.json` and secrets unstaged.
