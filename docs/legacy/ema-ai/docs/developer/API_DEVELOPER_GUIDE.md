# API Developer Guide

Use [../api/API_INDEX.md](../api/API_INDEX.md) as the canonical endpoint map.

## Design Notes
- Prefer project-scoped processing endpoints.
- Return structured operation responses (`warnings`, `errors`, counts).
- Include diagnostics IDs where available (`request_id`, `run_id`, `operation_log_id`).
- Never accept arbitrary absolute file paths from clients.
