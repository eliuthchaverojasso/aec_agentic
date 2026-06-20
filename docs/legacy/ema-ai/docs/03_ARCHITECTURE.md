# System Architecture

**Last Updated:** 2026-05-28

High-level system design and component overview.

## System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    EMA AI System (MVP)                           │
└─────────────────────────────────────────────────────────────────┘

Client Layer:
  Revit (C#/.NET)              Web Browser (React/TS)
         │                              │
         └──────────────────┬───────────┘
                            │
                    ┌───────▼──────────┐
                    │   FastAPI        │
                    │   Backend        │
                    │   (Python)       │
                    └───────┬──────────┘
                            │
                    ┌───────▼──────────────┐
                    │  PostgreSQL          │
                    │  Database            │
                    │  (localhost:5432)    │
                    └──────────────────────┘
```

### Components

#### 1. Frontend (React/TypeScript)
- **Location:** `Pipeline/pipeline/frontend/`
- **Framework:** React 19 + TypeScript 5.7 + Vite 6
- **Port:** http://localhost:5173
- **Pages:** 21 pages (login, portfolio, project, requirements, evidence, readiness, etc.)
- **Style:** Tailwind CSS + Liquid Glass design system
- **API:** Centralized `src/api/client.ts` (100+ methods)
- **Build:** `npm run build` → `dist/` folder

#### 2. Backend (FastAPI)
- **Location:** `Pipeline/pipeline/app/`
- **Framework:** FastAPI 0.115 + SQLAlchemy 2.0 ORM
- **Language:** Python 3.12
- **Port:** http://localhost:8010 (mapped from 8000 in Docker)
- **Routes:** 14 routers covering projects, exports, requirements, evidence, readiness, documents, etc.
- **Documentation:** Swagger UI at `/docs`
- **Health:** `/health` endpoint

#### 3. Database (PostgreSQL)
- **Location:** `Pipeline/pipeline/db/init.sql`
- **Version:** PostgreSQL 16
- **Port:** localhost:5432
- **Schema:** 20+ tables (projects, requirements, evidence, readiness, issues, etc.)
- **Credentials:** User `ema`, password `ema_dev_pw` (local dev only)
- **Persistence:** Docker volume `postgres_data`

#### 4. Revit Add-in (C#/.NET)
- **Location:** `EMAExtractor/`
- **Language:** C# / .NET Framework 4.8
- **Target:** Autodesk Revit 2023–2026 (x64)
- **Function:** Export model data → JSON → Landing folder or API
- **Ribbon:** 40+ commands organized by workflow
- **UI:** WPF (ModelessToolWindow, ProgressWindow)

#### 5. Landing Zone
- **Location:** `Pipeline/pipeline/landing/`
- **Purpose:** File staging area for documents (drawings, specs, requirements)
- **Format:** Folder structure with project binding manifest
- **Function:** Local FS → Scanner → Ingestion pipeline

---

## Technology Stack

| Layer | Technology | Version | Purpose |
|-------|-----------|---------|---------|
| **Frontend** | React | 19.0.0 | Component framework |
| | TypeScript | 5.7 | Type safety |
| | Vite | 6.0.1 | Build tool & dev server |
| | Tailwind CSS | 3.4 | Styling |
| **Backend** | FastAPI | 0.115 | REST API framework |
| | SQLAlchemy | 2.0 | ORM |
| | Pydantic | 2.10 | Validation |
| | Python | 3.12 | Runtime |
| **Database** | PostgreSQL | 16 | Relational data store |
| | psycopg | - | Python driver |
| **Infrastructure** | Docker | Latest | Containerization |
| | Docker Compose | Latest | Orchestration |
| **Testing** | pytest | - | Python test runner |
| **Revit** | .NET Framework | 4.8 | Windows runtime |

---

## Data Model (ERD)

### Core Entities

```
Organization
  ├─→ Client (ISD/district)
  ├─→ Project (Revit project)
  │     ├─→ Model (Discipline model)
  │     │     └─→ Export (Revit JSON ingest)
  │     │           └─→ Element (BIM element)
  │     │                 ├─→ Issue (QA/QC finding)
  │     │                 └─→ RequirementEvidence (Link to requirement)
  │     └─→ Requirement (Owner req text)
  │           └─→ RequirementEvidence (Evidence link)
  │                 ├─ evidence_type: model|sheet|spec|manual|hybrid
  │                 ├─ status: candidate|accepted|rejected
  │                 └─ accepted_at, accepted_by (audit trail)
  └─→ AppUser (User accounts)
```

### Key Tables

| Table | Purpose | Key Fields |
|-------|---------|-----------|
| `projects` | Revit projects | id, name, client_id, binding |
| `requirements` | Owner requirements | id, text, discipline, category |
| `requirement_evidence` | Evidence links | id, requirement_id, evidence_type, status, accepted_at, accepted_by |
| `exports` | Revit export runs | id, project_id, filename, element_count, sync_status |
| `elements` | BIM elements | id, export_id, unique_id, category, parameters (JSON) |
| `issues` | QA/QC findings | id, element_id, rule_id, severity |
| `readiness_snapshot` | Scored readiness | id, project_id, overall_score, requirement_coverage, qaqc_health, freshness, timestamp |
| `operation_log` | Audit trail | id, endpoint, actor, duration, counts, errors |

---

## API Architecture

### Router Structure

```
FastAPI app (main.py)
  ├─ /health                      (health checks)
  ├─ /api/v1/
  │   ├─ projects/                (CRUD, binding, models, readiness)
  │   ├─ exports/                 (upload, sync logs, ingest)
  │   ├─ models/                  (model metadata)
  │   ├─ issues/                  (QA/QC issues, filtering)
  │   ├─ clients/                 (client management)
  │   ├─ documents/               (landing documents)
  │   ├─ requirements/            (requirement catalog)
  │   ├─ evidence/                (evidence state tracking)
  │   ├─ readiness/               (scores, snapshots, actions)
  │   ├─ landing/                 (zone operations)
  │   ├─ auth/                    (login, tokens)
  │   ├─ seion/                   (KGE predictions)
  │   ├─ compliance/              (NEC rules)
  │   ├─ viewpoints/              (stakeholder views)
  │   ├─ debug/                   (operation logs)
  │   ├─ ai_query/                (LLM integration)
  │   └─ dev/                     (development utilities)
  └─ /docs, /openapi.json        (Swagger, OpenAPI)
```

### Request/Response Pattern

All endpoints follow Pydantic schema validation:

```python
# Request
class ProjectCreateRequest(BaseModel):
    name: str
    client_id: str
    
# Response (201 Created)
class ProjectOut(BaseModel):
    id: str
    name: str
    client_id: str
    created_at: datetime
    status: str  # "active" | "archived"
    
    class Config:
        from_attributes = True  # SQLAlchemy compatibility
```

---

## Request Flow (Example: Upload & Ingest)

```
1. User uploads Revit JSON
   POST /api/v1/exports/sync
   ↓
2. Backend receives, validates JSON structure
   ↓
3. ijson streams elements (supports 100MB+ files)
   ↓
4. Element extraction: normalize, infer discipline
   ↓
5. QA/QC rules evaluated (R001-R004)
   ↓
6. Elements + Issues + Sync logs persisted to PostgreSQL
   ↓
7. Readiness recalculated (requirement coverage + QA/QC health + freshness)
   ↓
8. ReadinessSnapshot stored (audit trail)
   ↓
9. Frontend polls /api/v1/projects/{id}/readiness
   ↓
10. Dashboard updates with new score
```

---

## Deployment Architecture

### Local Development

```
Docker Host (Your Machine)
  ├─ Container: PostgreSQL 16
  │   └─ Volume: postgres_data (persistent)
  │
  └─ Container: FastAPI (Python)
      └─ Mounts: ./app, ./landing (code volumes)

Frontend: npm run dev (local Node.js process)
  └─ http://localhost:5173
```

### Azure Target (Post-MVP)

```
Azure Subscription
  ├─ Static Web Apps (Frontend)
  │   └─ Deployed from dist/
  │
  ├─ Container Apps (Backend)
  │   └─ Runs FastAPI container
  │
  ├─ PostgreSQL Flexible Server
  │   └─ Managed DB (production)
  │
  ├─ Storage Account (Data Lake Gen2)
  │   ├─ landing container (source files)
  │   ├─ processed container (ingested)
  │   ├─ archive container (exports)
  │   └─ rejected container (QC failures)
  │
  ├─ Key Vault
  │   └─ Secrets, connection strings
  │
  └─ Application Insights
      └─ Logs, metrics, traces
```

---

## Key Services

### Ingestion Pipeline (`app/ingestion/`)

1. **File Upload** → Validation → Stream parsing
2. **Element Extraction** → Parameter normalization
3. **QA/QC Evaluation** → Issue logging
4. **Persistence** → Batch inserts to PostgreSQL
5. **Readiness Recalc** → Score update

**Files:**
- `parser.py` — JSON streaming (ijson)
- `loader.py` — Orchestration
- `rules.py` — Legacy rule integration
- `requirements_loader.py` — Excel ingestion
- `landing_service.py` — Landing zone scan

### Readiness Engine (`app/readiness/`)

1. **Requirement Coverage** (50%) — % with accepted evidence
2. **QA/QC Health** (30%) — Penalty from issue severity
3. **Sync Freshness** (20%) — Decay based on export age
4. **Overall Score** = Weighted sum, clamped 0–100

**Formula:**
```
coverage_score = accepted_evidence / total_requirements * 100
qaqc_score = 100 - (critical×5.0 + high×2.0 + medium×0.75 + low×0.25)
freshness_score = decay(export_age)  # 100% ≤1d, 50% ≤14d, 25% >14d

overall = (coverage×0.50 + qaqc×0.30 + freshness×0.20)
```

**Files:**
- `scoring.py` — Score calculation
- `service.py` — Readiness orchestration
- `rules.py` — Gap detection rules
- `persistence.py` — Snapshot storage

### Rules Engine (`app/rules/`)

QA/QC rule evaluation framework:

```python
class RuleEngine:
    def evaluate(element) -> RuleFinding[]
        # Evaluate all registered rules
        # Return findings (violations)
```

**Current rules (R001–R004):**
- R001: Element Without Level
- R002: Unconnected Fixture (electrical)
- R003: Fixture Missing Circuit (electrical)
- R004: Panel Without Source (electrical)

---

## Security & Data Handling

### Authentication

- **Local Dev:** Hardcoded demo users (email/password)
- **Production:** JWT tokens (not yet implemented)
- **Future:** OAuth2, RBAC (P1–P2)

### Data Isolation

- One PostgreSQL database per environment
- No cross-tenant data leakage
- Landing folder per project (local FS isolation)

### Secrets Management

- `.env` files (never committed)
- Environment variables for sensitive config
- Future: Azure Key Vault (post-deploy)

### Audit Trail

- `operation_log` table: all API calls logged (endpoint, actor, duration, counts)
- `requirement_evidence.accepted_at`, `accepted_by`: who approved what, when
- `export.sync_log`: ingest progress and errors

---

## Testing Architecture

### Backend (`pytest`)

- **126+ tests** covering:
  - API endpoints (happy path, errors)
  - Landing workflows
  - Readiness calculation
  - Evidence state transitions
  - Document ingestion
  - Compliance rules
  - SEION predictions
  - Debug logging

**Run:** `pytest Pipeline/pipeline/tests -v`

### Frontend (TypeScript)

- **Type checking:** `npm run build` (TypeScript strict mode)
- **No unit tests yet** (future: Jest + React Testing Library)

**Run:** `npm run build` in `Pipeline/pipeline/frontend`

### Docker & Smoke Tests

- Health checks for all services
- Database connectivity
- Browser smoke tests (core routes)

**Run:** `docker compose ps` (all should be healthy)

---

## Development Workflow

```
1. Create branch
   git checkout -b feat/my-feature

2. Make changes (backend, frontend, or both)
   - Update API endpoint → Update Frontend Client
   - Update ORM model → Update schemas.py
   - Add test → Run pytest

3. Validate
   pytest (backend)
   npm run build (frontend)
   docker compose ps (services healthy)

4. Commit with clear message
   git commit -m "feat: add evidence bulk acceptance"

5. Push to GitHub
   git push origin feat/my-feature

6. Create PR with description
   - What changed
   - Why
   - Tests added
   - Breaking changes (if any)

7. Get approval, merge
   git merge --no-ff (preserve branch history)
```

---

## Known Limitations

- **No model viewer** (images/descriptions only, no 3D)
- **No RBAC** (everyone can see everything)
- **Local auth only** (hardcoded demo users)
- **No GraphRAG** (deferred, advisory only)
- **No real-time collaboration** (manual refresh required)

---

## Next Steps

- **Post-Demo (P1):** Azure deployment, web uploader, evidence detail page
- **Production (P2):** RBAC, SSO, load testing, backup/recovery
- **Advanced (P3+):** GraphRAG, model viewer, advanced compliance rules

---

See also:
- [Data Flow](architecture/DATA_FLOW.md) — Detailed data flow
- [docs/api/API_INDEX.md](api/API_INDEX.md) — Endpoint reference
- [docs/architecture/BACKEND_ARCHITECTURE.md](architecture/BACKEND_ARCHITECTURE.md) — Deep dive
