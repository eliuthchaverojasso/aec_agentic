# ADR 0005 - Backend Sync Is Optional

## Status

Accepted

## Context

Management history and cross-project intelligence can be useful later, but they should not block the local designer workflow.

## Decision

Any backend submission of requirement check runs must be opt-in.

## Consequences

- No project setup is required for the local checker.
- Dashboard sync becomes an optional later step.
