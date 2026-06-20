# Backend Developer Guide

## Core Modules
- Routers: `app/api`
- Schemas: `app/schemas.py`
- Models: `app/models.py`
- Ingestion/processing: `app/ingestion`, `app/processing`
- Readiness: `app/readiness`
- Debug logs: `app/services/operation_log_service.py`, `app/api/debug.py`

## Priority Endpoints
- Projects/models
- Landing configure/discover/bootstrap
- Processing/Sync ingest aliases
- Readiness snapshots/actions
- Debug diagnostics

## Test Focus
- Endpoint contracts
- Path safety and traversal rejection
- Deterministic readiness boundaries
