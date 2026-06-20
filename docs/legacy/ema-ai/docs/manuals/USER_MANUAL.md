# EMA AI User Manual

## What EMA AI Is
EMA AI is a local-first Engineering Intelligence and DD/CD readiness platform. Official readiness comes from deterministic backend logic and PostgreSQL records.

## Primary Navigation
- Executive Overview
- Projects Portfolio
- Deliverable Tracker
- Requirements
- Model Health
- Documents / Evidence
- Processing / Sync
- Debug / Logs
- System Health
- Appearance

## Core Workflow
1. Select a project in the global selector.
2. Use Project Setup to create/bind client/project/model and landing.
3. Run Processing / Sync: scan, rebuild manifest, dry-run ingest, ingest.
4. Review Deliverable Tracker, Requirements, Model Health, and Documents.

## Label Meanings
- Official: deterministic backend-backed state.
- Evidence Candidate: indexed artifact, not official evidence yet.
- Advisory: informational only, not authoritative.
- Fallback/Prototype: demo support only.
- Local Demo: local environment behavior, not production.

## Project Selector
- Use the global selector to choose active project context.
- Selected project is persisted locally (`ema-ai-selected-project-id`).
- If the project no longer exists, selection should safely fall back.

## Common Issues
- No projects: create/bootstrap from Project Setup.
- Backend offline: verify `GET /health`.
- Landing scan failure: check backend landing root mapping.
- Ingest failure: inspect Processing JSON + Debug / Logs.
- Path mismatch: Windows host path vs `/app/landing` container mapping.
- Wrong project context: verify selected project before running Processing / Sync.
- Theme not persisting: verify browser local storage for key `appearance.settings.v1` (legacy `ema-ai-appearance-settings` is migrated automatically).
