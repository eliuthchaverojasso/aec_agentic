# Backend Architecture

## Purpose
FastAPI services for project setup, landing processing, readiness, documents, and diagnostics.

## Data Ownership
- Official state: PostgreSQL.
- Deterministic readiness: `app/readiness`.
- Diagnostics: pipeline operation logs.

## Status
- Implemented Local: project/landing/processing/readiness/debug endpoints.
- Partial: compliance persistence depth and milestone criteria lifecycle.
- Planned: production auth, governance, and audit controls.
