# Architecture Overview

## Purpose
Describe component boundaries across Revit add-in, landing zone, backend, database, and dashboard.

## Components
- Revit Add-in (export + metadata).
- Landing Zone (project folders + manifest).
- FastAPI services (setup, ingestion, readiness, debug).
- PostgreSQL source of truth.
- React dashboard (setup, processing, readiness, diagnostics).

## Status
- Implemented Local: core workflow and debug layer.
- Partial: milestone criteria lifecycle and evidence promotion governance.
- Planned: production auth, Azure managed deployment, audit-grade observability.
