# Data Flow

## Purpose

This document describes how EMA-AI moves data through the local MVP pipeline.

## Canonical Flow

Revit add-in -> local landing root -> manifest/sidecar metadata -> FastAPI landing discovery -> PostgreSQL -> deterministic readiness -> dashboard.

## Important Boundaries

- Revit organizes and exports local files.
- Backend scans, classifies, ingests, and computes readiness.
- Dashboard visualizes state and exposes operator actions.
- Evidence candidates are not official evidence.

## Notes

- PDFs/specifications/drawings are indexed for operator review.
- The system remains deterministic at the official readiness layer.
- LLM/SEION helpers are advisory only.

