# Readiness API Contract

## Purpose

Readiness APIs expose deterministic scoring, actions, and snapshots.

## Core Endpoints

- `GET /api/v1/projects/{project_id}/readiness`
- `GET /api/v1/projects/{project_id}/readiness/actions`
- `GET /api/v1/projects/{project_id}/readiness/snapshots`
- `POST /api/v1/projects/{project_id}/readiness/snapshots`
- `PATCH /api/v1/readiness/actions/{action_id}`

## Semantics

- Evaluated is not covered.
- Not applicable is excluded.
- Missing remains visible and counts against readiness where applicable.
- Model Health fallback is not official owner requirement readiness.

