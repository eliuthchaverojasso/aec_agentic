# EMA AI

**EMA AI** is a pilot MVP for deliverable readiness assessment. It connects project data (Revit exports, Owner Requirements, QA/QC issues) into a web dashboard that shows what evidence exists, what's missing, and whether a project is ready against defined requirements.

**Status:** Local MVP. Azure deployment planned for P1.
**Last updated:** 2026-06-15

---

## What EMA AI Does

```
Revit model → Owner Requirements → Evidence gathering → Readiness score → Dashboard review
```

1. **Compare** Owner Requirements against project data.
2. **Find** evidence from Revit exports, drawings, specifications, and manual review.
3. **Track** what evidence exists (candidate, accepted, rejected, missing).
4. **Score** readiness: what % of requirements have accepted evidence.
5. **Visualize** in a web dashboard: portfolio, requirements, gaps, actions.

**One-sentence value prop:**
*EMA AI helps identify whether a project is ready against Owner Requirements by connecting project data, requirements, evidence, gaps, and readiness status.*

---

## What EMA AI Is NOT

- **Not production compliance software.** This is a pilot tool, not official certification.
- **Not AI approval.** Humans review all evidence and make acceptance decisions. AI is advisory only.
- **Not automatic.** Every acceptance requires explicit reviewer action and audit trail.
- **Not enterprise-ready yet.** Local MVP now. Azure and RBAC planned for P1–P2.

---

## Quick Start

### Local Development

**Prerequisites:** Docker Desktop, Node.js, Python 3.11+

```powershell
# Start backend + database
cd Pipeline\pipeline
docker compose up -d --build
curl http://localhost:8010/health  # Should return {"status":"ok",...}

# Start frontend (new terminal)
cd Pipeline\pipeline\frontend
npm install
npm run dev
# Open http://localhost:5173
```

**API Docs:** http://localhost:8010/docs (Swagger)

### Demo Happy Path

1. **Login:** Demo account (credentials in docs)
2. **Portfolio:** View projects and readiness status
3. **Requirements:** See owner requirements + evidence for each
4. **Readiness:** View overall score and gaps
5. **Evidence:** Review and accept/reject evidence

See [Demo Runbook](docs/demo/DEMO_RUNBOOK.md) for a step-by-step walkthrough.

---

## Documentation

**Complete documentation index:** [docs/DOCUMENTATION_INDEX.md](docs/DOCUMENTATION_INDEX.md)

### Quick Start (Choose Your Path)

**New to the project?** Start here:
1. [Project Overview](docs/00_OVERVIEW.md) — What EMA AI is and does
2. [Local Dev Setup](docs/01_QUICKSTART_LOCAL.md) — Get running in 15 minutes
3. [Architecture Overview](docs/03_ARCHITECTURE.md) — How the system works

**Running a demo?**
→ [Demo Runbook](docs/demo/DEMO_RUNBOOK.md) (step-by-step script)

**For your role:**

### For Everyone
- [**Current State**](.ai/CURRENT_STATE.md) — What works, what doesn't, priorities
- [**Next Steps**](.ai/NEXT_STEPS.md) — P0 (Revit smoke), P1 (hardening), P2+ (scaling)

### For Developers
- [**Agentic Development Guide**](docs/05_AGENTIC_DEVELOPMENT_GUIDE.md) — Critical rules for AI coding agents
- [**Backend Architecture**](docs/architecture/BACKEND_ARCHITECTURE.md) — Python/FastAPI structure
- [**Frontend Architecture**](docs/architecture/FRONTEND_ARCHITECTURE.md) — React/TypeScript structure
- [**API Reference**](docs/api/API_INDEX.md) — All endpoints documented

### For Demo/Operations
- [**Demo Runbook**](docs/demo/DEMO_RUNBOOK.md) — Script + talking points
- [**Local Dev Setup**](docs/01_QUICKSTART_LOCAL.md) — How to start services
- [**Docker Quickstart**](docs/02_QUICKSTART_DOCKER.md) — Docker Compose quickstart

### For Deployment/Infrastructure
- [**Azure Deployment Runbook**](docs/runbooks/AZURE_DEPLOYMENT_RUNBOOK.md) — Post-MVP deployment guide
- [**Docker Quickstart**](docs/02_QUICKSTART_DOCKER.md) — Docker Compose quickstart
- [**Environment Variables**](docs/deployment/ENVIRONMENT_VARIABLES.md) — Configuration reference

### For Revit Users/Developers
- [**Revit Add-in Installation**](docs/revit/ADDIN_INSTALLATION.md) — Install, commands, export workflow
- [**Revit Command Map**](docs/revit/COMMAND_MAP.md) — Available commands
- [**Revit Runtime Smoke Checklist**](docs/revit/REVIT_RUNTIME_SMOKE_CHECKLIST.md) — Host validation checklist

### For Testing
- [**Testing Strategy**](docs/dev/TESTING_STRATEGY.md) — Backend, frontend, Docker tests
- [**Test Matrix**](.ai/TEST_MATRIX.md) — All tests and current status

### For Product/Stakeholders
- [**Roadmap**](.ai/ROADMAP_P1_P3.md) — MVP goals, post-demo, production
- [**Known Limitations**](docs/release/KNOWN_LIMITATIONS.md) — What's not implemented
- [**Production Readiness Gap**](docs/release/PRODUCTION_READINESS_GAP.md) — What's required before production

---

## Architecture

### Local Stack
```
Frontend (React/Vite) @ http://localhost:5173
  ↓ API calls via centralized client
Backend (FastAPI) @ http://localhost:8010
  ↓ SQL queries
PostgreSQL @ localhost:5432
  ↓ (stores)
Projects, requirements, evidence, readiness state
```

### Azure Pilot Target (Planned — Not Deployed)
```
Static Web Apps (Frontend)
  ↓
Container Apps (Backend)
  ↓
PostgreSQL Flexible Server
Storage (Data Lake Gen2) → landing, processed, archive, rejected containers
Key Vault → secrets & config
Application Insights → observability
```

See [Azure Pilot Architecture](docs/architecture/AZURE_PILOT_ARCHITECTURE.md) for full details.

---

## Modules

- **`EMAExtractor/`** — Revit add-in (C# / .NET)
  - Exports model → JSON → landing folder
  - Build: `dotnet build EMAExtractor/EMAExtractor.csproj /p:Platform=x64`

- **`Pipeline/pipeline/app/`** — Backend API (Python / FastAPI)
  - Ingestion, readiness, evidence, landing workflows
  - Start: `docker compose up` (includes PostgreSQL)
  - Endpoints: `/health`, `/api/v1/projects`, `/api/v1/requirements`, `/api/v1/readiness`, etc.

- **`Pipeline/pipeline/frontend/`** — Dashboard (React / TypeScript / Vite)
  - Client-side app, consumed via `VITE_API_BASE_URL`
  - Start: `npm run dev` (@ http://localhost:5173)
  - Build: `npm run build` (outputs `dist/`)

- **`Pipeline/pipeline/db/`** — PostgreSQL schema & seed data
  - `init.sql` — loaded automatically by Docker

- **`docs/`** — Documentation
  - Organized by role: `demo/`, `api/`, `architecture/`, `deployment/`, `runbooks/`, `product/`

---

## What Works Now

> For current validated test counts and status, see [Test Matrix](.ai/TEST_MATRIX.md)
> and [Project Reference Manifest](.ai/PROJECT_REFERENCE_MANIFEST.yaml).

### Revit Add-in
- Owner Requirements workflow: Load workbook → Sync model → Run check → Generate report
- 265 C# tests passing (verified 2026-06-15 at `b0cb42b`; was 246 at the audited baseline `ae6ded2`)
- HTML report with Executive Summary, discipline sections, evidence, Element IDs
- Build validated; **runtime smoke pending** (requires host Revit session)

### Backend
- Landing discovery, manifest, ingest batch operations
- Project CRUD and read endpoints
- Owner Requirements listing and linking
- Evidence candidate/accepted/rejected state tracking
- Deterministic readiness scoring
- PostgreSQL persistence
- Docker Compose orchestration
- Python tests: full-stack run on 2026-06-15 with PostgreSQL up — 172 of 184 pass; the 12 failures are real defects (see Test Matrix), not DB-availability

### Frontend
- Login (basic demo auth)
- Project Portfolio view
- Project Overview (readiness summary)
- Requirements page with evidence states
- Documents/Evidence page with acceptance workflow
- Readiness page (score, gaps, actions)
- Appearance page (Liquid Glass theming)
- TypeScript typecheck + Vite production build (verified 2026-06-15)

### Testing & Validation (verified 2026-06-15 at `b0cb42b`; see [Test Matrix](.ai/TEST_MATRIX.md))
- C# add-in tests: 265 passed, 0 failed
- Python backend full suite (PostgreSQL up): 172 of 184 pass; 12 real failures (auth 404, evidence semantics, qaqc empty-model score, Windows upload)
- Frontend typecheck: PASS (tsc 5.9.3)
- Frontend production build: PASS (vite 6.4.2; main bundle 960 kB — perf-budget item)
- Docker/PostgreSQL smoke: PASS (both containers healthy; `/health` → database:ok)
- Browser smoke (core routes): last verified 2026-05-26 (manual; not re-run)
- Reference drift validator: PASS (0 errors)

### Pending
- Revit runtime validation (build passes, host Revit smoke needed)
- Azure deployment
- CI Python test configuration (47 DB-dependent tests currently fail in CI without database service)

### Not Implemented (Not P0)
- Model viewer, full RBAC, SSO, GraphRAG, SEION approval, compliance claims

---

## Validation Commands

```powershell
# C# add-in tests (no Revit required)
dotnet test EMAExtractor.Tests\EMAExtractor.Tests.csproj --verbosity minimal

# Backend (requires running Docker stack)
cd Pipeline\pipeline
docker compose up -d --build
python -m pytest tests -v

# Backend (unit tests only, no DB required)
cd Pipeline\pipeline
python -m pytest tests -v -m "not requires_db"

# Frontend
cd Pipeline\pipeline\frontend
npx tsc -b --noEmit  # TypeScript check
npm run build         # Vite build

# Docker health check
curl http://localhost:8010/health

# Browser
start http://localhost:5173
```

---

## Known Limitations

- Revit runtime not yet validated in host Revit (build only).
- Local demo auth (hardcoded users, not production).
- No PDF/DOCX parsing yet (manual indexing only).
- No model viewer (images/descriptions only).
- Docker smoke is local; Azure deployment pending.
- Not official compliance software.
- CI Python tests currently fail (database service not configured in workflow).

---

## Getting Help

- **Current state & priorities?** Read [Current State](.ai/CURRENT_STATE.md)
- **How to deploy to Azure?** See [Azure Deployment Runbook](docs/runbooks/AZURE_DEPLOYMENT_RUNBOOK.md)
- **How to consume the API?** See [API Consumption Guide](docs/api/API_CONSUMPTION_GUIDE.md)
- **What readiness means?** See [Readiness Engine Notes](docs/product/READINESS_ENGINE_NOTES.md)
- **Full documentation index?** See [Documentation Index](docs/DOCUMENTATION_INDEX.md)

---

## Repository

- **GitHub:** [echavero-shock/EMA-AI](https://github.com/echavero-shock/EMA-AI)
- **Current branch:** `docs/project-reference-reconciliation`
- **Validated product branch:** `feat/revit-first-owner-requirement-checker` (audited commit `ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e`)
- **Default branch:** `main`
- **Canonical manifest:** [.ai/PROJECT_REFERENCE_MANIFEST.yaml](.ai/PROJECT_REFERENCE_MANIFEST.yaml)

---

**This is a pilot MVP, not production software. All feedback is welcome.**
