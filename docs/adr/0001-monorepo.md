# ADR 0001: Use a Polyglot Monorepo

Status: Accepted

## Context

The product spans Python backend/domain code, React/TypeScript UI, C# Revit integration, connectors, agents, schemas, and infrastructure.

## Decision

Use a monorepo with explicit top-level boundaries: `standard`, `packages`, `apps`, `connectors`, `agents`, `infra`, `tests`, and `docs`.

## Consequences

This keeps contracts and implementation close while the domain boundaries are still changing. Microservices may be extracted later when a module has stable ownership, scale, and deployment needs.

