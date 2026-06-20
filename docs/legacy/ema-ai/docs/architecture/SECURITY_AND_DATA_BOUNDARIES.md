# Security and Data Boundaries

## Purpose

This document states the local MVP data-handling rules that keep EMA-AI honest and reviewable.

## Rules

- PostgreSQL is the source of truth.
- No secrets in docs, logs, or debug bundles.
- No path traversal in landing operations.
- No raw file content is exposed beyond capped previews or metadata.
- Evidence candidates are not official evidence.
- Local demo users and roles are not production auth.

## Caveats

- Azure security design is a recommendation path, not a deployment claim.
- Revit runtime validation remains pending unless explicitly tested in host Revit.

