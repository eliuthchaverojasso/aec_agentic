# ADR 0003: Agents Propose Commands

Status: Accepted

## Context

Agents can extract, classify, plan, and explain, but they must not bypass authority or audit.

## Decision

Agents produce proposed commands. Policy evaluates those commands. A human or deterministic rule authorizes them. Application services execute authorized commands and emit audit events.

## Consequences

Agent work becomes governable, replayable, and measurable.

