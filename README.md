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
- `infra/database/ema-db/` holds the historical EMA schema SQL. The schema is now created and versioned by **Alembic** (`apps/control-plane-api/alembic`) as the single schema-authoring mechanism; these files are retained for reference only.
- `data/taxonomies/ema-ai/` contains the previous requirement taxonomy.
- `docs/legacy/ema-ai/` preserves previous EMA AI documentation as historical context.

## Current Development Status

This workspace is a newly created monorepo scaffold with migrated EMA source code. The code has not yet been fully refactored into the final package boundaries. Transitional packages and manifests are present so the next work can proceed safely by vertical slice.

## Useful Commands

```powershell
# Bootstrap local env: prereq checks, .env, Postgres, then `alembic upgrade head`
pwsh .\scripts\bootstrap.ps1

# Run available validation
pwsh .\scripts\test.ps1

# Apply database migrations (Alembic; single schema-authoring mechanism)
pwsh .\scripts\migrate.ps1            # docker `migrate` service -> upgrade head
pwsh .\scripts\migrate.ps1 -Local     # host `alembic` against DATABASE_URL

# Start the full local stack (Postgres + migrate + API). Same file scripts/dev.ps1 uses.
docker compose up -d --build
# API on http://localhost:8010 (health: /health, reports schema_revision). Web console runs separately (Vite).
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
