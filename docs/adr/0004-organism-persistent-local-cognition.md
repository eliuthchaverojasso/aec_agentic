# ADR 0004: Add ORGANISM Persistent Local Cognition

Status: Accepted

## Context

The AEC Control Plane will be built through long-running, restartable, multi-step missions. Conversation history alone is not a reliable substrate for continuity.

## Decision

Create ORGANISM as a local persistent cognitive layer. Git owns doctrine and approved methods. PostgreSQL owns mission state. pgvector and Apache AGE provide rebuildable semantic and relational memory. Local models are routed through capability names, not hard-coded model references.

## Consequences

Mission execution can pause, resume, be audited, and evolve. Self-improvement becomes a governed proposal flow rather than uncontrolled mutation.

