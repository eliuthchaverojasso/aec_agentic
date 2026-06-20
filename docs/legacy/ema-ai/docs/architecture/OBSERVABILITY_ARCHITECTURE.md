# Observability Architecture

## Purpose
Local diagnostics for API operations, ingestion pipeline state, and environment mismatch detection.

## Implemented
- Backend pipeline operation log model/service.
- Debug endpoints (`/api/v1/debug/*`).
- Frontend Debug / Logs page and integrations.

## Limits
- Diagnostic layer is not audit-grade production observability.
- Redaction rules apply; no secret values or raw proprietary file content.
