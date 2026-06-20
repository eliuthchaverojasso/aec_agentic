# ADR 0002: Start With a Modular Monolith Core

Status: Accepted

## Context

The domain model is still being discovered across obligations, work packages, evidence, approvals, value, and billing.

## Decision

Keep the transactional domain in a modular Python monolith first. Use workers for async processing and isolated connector runners for edge workloads.

## Consequences

This avoids premature distributed boundaries while preserving a future path to extract services.

