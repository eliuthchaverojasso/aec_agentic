# AEC Agentic Control Plane

AEC Agentic Control Plane is the next architecture for EMA AI: a cross-system control layer that connects obligations, executable work, evidence, approvals, and economic value without trying to replace Revit, Procore, Primavera, ERP, or document systems.

The first migrated vertical is EMA AI Owner Requirements Readiness:

```text
Owner requirement
-> deliverable/model applicability
-> deterministic evaluation
-> evidence claim
-> human review
-> milestone readiness
```

The next vertical is specialty contractor execution control:

```text
Contract scope
-> work package
-> assigned actor
-> required evidence
-> approved progress
-> earned value
-> billing readiness
```

## Repository Shape

```text
standard/    Versioned vocabulary, schemas, events, policies, mappings, examples.
packages/    Reusable core, policy, evidence, reporting, agent, and connector packages.
apps/        Deployable API, worker, web console, Revit add-in, connector runner, CLI.
connectors/  External-system adapters.
agents/      Governed agent capabilities and evals.
infra/       Compose, containers, database, environments, monitoring, deployment.
tests/       Architecture, contract, conformance, integration, and golden project tests.
docs/        Architecture decisions, domain docs, product docs, and legacy EMA context.
.organism/   Local persistent cognitive doctrine, governance, mission memory, and supervision packets.
```

## Migrated EMA Components

- `apps/control-plane-api/` contains the previous FastAPI backend and tests.
- `apps/web-console/` contains the previous React/Vite dashboard.
- `apps/revit-addin/` contains the previous EMAExtractor Revit add-in and C# tests.
- `infra/database/ema-db/` contains the previous database initialization and migrations.
- `data/taxonomies/ema-ai/` contains the previous requirement taxonomy.
- `docs/legacy/ema-ai/` preserves previous EMA AI documentation as historical context.

## Current Development Status

This workspace is a newly created monorepo scaffold with migrated EMA source code. The code has not yet been fully refactored into the final package boundaries. Transitional packages and manifests are present so the next work can proceed safely by vertical slice.

## Useful Commands

```powershell
# Inspect repo
pwsh .\scripts\bootstrap.ps1

# Run available validation
pwsh .\scripts\test.ps1

# Start legacy EMA API stack from migrated compose file
docker compose -f .\infra\compose\ema-local.compose.yml up -d --build
```

## Design Rule

AI may extract, classify, propose, explain, and draft. Approval, authoritative state changes, economic recognition, and billing readiness must pass through deterministic policy, explicit authority, and audit trail.

## ORGANISM

The repo includes an ORGANISM layer for local persistent cognition. It records durable doctrine, governance, agent contracts, mission-cycle methodology, model routes for the installed Ollama models, GPT Web supervision inbox/outbox folders, and database/runtime scaffolding.

```powershell
# Show local model capability routes
pwsh .\scripts\organism.ps1 routes

# Start ORGANISM persistence services when Docker is available
pwsh .\scripts\organism.ps1 up
```
