# API Consumption Guide

**Last updated:** 2026-05-26  
**Audience:** Frontend engineers, infrastructure engineers, integration partners  
**Reference:** Backend API lives at `Pipeline/pipeline/app/main.py`  

---

## Overview

EMA AI frontend consumes the backend API through a centralized client (`src/api/client.ts`). This guide explains how the frontend is structured, how to add new endpoints, and how to deploy the frontend/backend pair to Azure.

---

## Architecture

```
Frontend (React/Vite/TS)
  └─ src/api/client.ts (centralized HTTP client)
  └─ src/pages/ (route-level components)
  └─ src/components/ (reusable UI components)

Backend (FastAPI/Python)
  └─ app/main.py (entry point, router setup)
  └─ app/api/ (endpoint modules)
  └─ app/services/ (business logic)
  └─ app/models.py (database models)
  └─ app/schemas.py (request/response schemas)

PostgreSQL (source of truth)
```

---

## Frontend API Client

### Location
```
Pipeline/pipeline/frontend/src/api/client.ts
```

### Pattern

All API calls go through a centralized client, NOT scattered throughout components:

```typescript
// ❌ DON'T do this:
fetch('http://localhost:8010/api/v1/projects')

// ✅ DO this:
import { client } from '../api/client';
const projects = await client.listProjects();
```

### Base URL Resolution

The frontend determines the API base URL in this order:

1. **Environment variable** `VITE_API_BASE_URL` (set at build time or deploy time).
2. **Fallback** `http://localhost:8010` (for local development).

```typescript
// src/api/client.ts
const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") || "http://localhost:8010";
```

### Available Client Methods

See `src/api/client.ts` for the full list. Common methods include:

```typescript
// Projects
client.listProjects()
client.getProject(projectId)
client.createProject(input)

// Requirements
client.listRequirements(projectId)
client.getRequirement(projectId, requirementId)

// Evidence
client.listDocuments(projectId)
client.acceptEvidence(projectId, evidenceId, reviewer)
client.rejectEvidence(projectId, evidenceId, reviewer)

// Readiness
client.getReadiness(projectId)
client.recalculateReadiness(projectId)

// Landing / Processing
client.scanLanding(projectId)
client.rebuildManifests()
client.ingestAllDryRun()
client.ingestAll()
```

---

## Backend API Endpoints

### Health & Status

```
GET /health
```
Response:
```json
{
  "status": "ok",
  "database": "ok",
  "version": "0.1.0"
}
```

### Projects

```
GET /api/v1/projects
```
Response:
```json
{
  "projects": [
    {
      "id": "proj-001",
      "name": "NISD Renovation",
      "status": "active",
      "readiness_score": 78
    }
  ]
}
```

```
GET /api/v1/projects/{project_id}
POST /api/v1/projects (create)
PUT /api/v1/projects/{project_id} (update)
DELETE /api/v1/projects/{project_id}
```

### Owner Requirements

```
GET /api/v1/projects/{project_id}/requirements
```
Response:
```json
{
  "requirements": [
    {
      "id": "req-001",
      "text": "Building envelope insulation R-value ≥ R-40",
      "status": "completed",
      "evidence_count": 3,
      "evidence_state": "accepted"
    }
  ]
}
```

### Evidence / Documents

```
GET /api/v1/projects/{project_id}/documents
```
Response:
```json
{
  "documents": [
    {
      "id": "doc-001",
      "title": "Specification Section 07-21-00",
      "source": "specification",
      "state": "candidate",
      "linked_requirements": ["req-001"]
    }
  ]
}
```

```
POST /api/v1/evidence (create)
PATCH /api/v1/evidence/{evidence_id} (accept/reject)
```

### Readiness

```
GET /api/v1/projects/{project_id}/readiness
```
Response:
```json
{
  "project_id": "proj-001",
  "overall_score": 78,
  "covered_percent": 78,
  "missing_percent": 12,
  "needs_review_percent": 10,
  "recommendation": "Ready with minor gaps"
}
```

```
POST /api/v1/projects/{project_id}/readiness/recalculate
```

### Landing / Processing

```
GET /api/v1/landing/projects (discover projects in landing folder)
POST /api/v1/landing/rebuild-all-manifests (dry-run)
POST /api/v1/landing/ingest-all (dry-run)
POST /api/v1/landing/ingest-all (real)
```

For full endpoint list, see:
- `docs/api/PROJECTS_API.md`
- `docs/api/READINESS_API.md`
- `docs/api/DOCUMENTS_API.md`
- `docs/api/LANDING_API.md`

---

## Local Development Setup

### 1. Start the Backend

```powershell
cd Pipeline\pipeline

# Option A: Docker Compose (recommended)
docker compose up -d --build
curl http://localhost:8010/health

# Option B: Local Python (if Docker not available)
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
export DATABASE_URL=postgresql://ema:ema_dev_pw@localhost:5432/ema_ai
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

API is now available at `http://localhost:8010`.

Swagger docs: `http://localhost:8010/docs`

### 2. Start the Frontend

```powershell
cd Pipeline\pipeline\frontend

npm install
npm run dev
```

Frontend is now available at `http://localhost:5173`.

The frontend will automatically connect to `http://localhost:8010` via the fallback in `client.ts`.

### 3. Verify Connection

```powershell
# Backend is running
curl http://localhost:8010/health

# Frontend can reach backend
curl http://localhost:8010/api/v1/projects

# Open browser
start http://localhost:5173
```

---

## CORS Configuration

The backend restricts which origins can call it. For development and deployment:

### Local Development
Backend CORS setting (default in `docker-compose.yml`):
```
CORS_ORIGINS=http://localhost:5173,http://127.0.0.1:5173
```

### Azure Pilot Deployment
Backend CORS setting (to be set in Container Apps environment):
```
CORS_ORIGINS=https://ema-ai-dashboard.azurestaticwebapps.net
```

The exact URL depends on where you deploy the Static Web App. Set this in Azure via:
1. Container Apps environment variables.
2. Or in Key Vault if using secret references.

**Important:** Always match the frontend URL to the CORS setting. If they don't match, the frontend will see CORS errors in the browser console.

---

## Adding a New Endpoint

### Backend (FastAPI)

1. **Create a new router** or add to an existing one in `app/api/`:

```python
# app/api/example.py
from fastapi import APIRouter, HTTPException
from app.database import SessionLocal

router = APIRouter(prefix="/api/v1", tags=["example"])

@router.get("/example/{item_id}")
def get_example(item_id: str):
    db = SessionLocal()
    # Query logic here
    return {"item_id": item_id, "data": "..."}
```

2. **Register the router** in `app/main.py`:

```python
from app.api import example
app.include_router(example.router)
```

3. **Test with pytest**:

```python
# tests/test_api_example.py
def test_get_example(client):
    response = client.get("/api/v1/example/test-id")
    assert response.status_code == 200
    assert response.json()["item_id"] == "test-id"
```

### Frontend (React/TypeScript)

1. **Add a method to `client.ts`**:

```typescript
export async function getExample(itemId: string): Promise<ExampleResponse> {
  return request<ExampleResponse>(`/api/v1/example/${itemId}`);
}
```

2. **Use in a component**:

```typescript
import { client } from "../api/client";

function ExamplePage() {
  const [data, setData] = useState(null);

  useEffect(() => {
    client.getExample("test-id").then(setData);
  }, []);

  return <div>{data?.data}</div>;
}
```

3. **Add to `src/types.ts`**:

```typescript
export interface ExampleResponse {
  item_id: string;
  data: string;
}
```

---

## Environment Variables

### Frontend (`VITE_API_BASE_URL`)

**Local Development:**
- Not set. Frontend falls back to `http://localhost:8010`.

**Azure Deployment:**
- Set to the backend Container App URL:
  ```
  VITE_API_BASE_URL=https://ema-api.containerapp.azurecontainers.io
  ```

**How to set (Vite):**
- Create a `.env` file in `Pipeline/pipeline/frontend/`:
  ```
  VITE_API_BASE_URL=http://localhost:8010
  ```
- Or set at build time:
  ```bash
  VITE_API_BASE_URL=https://ema-api.prod.example.com npm run build
  ```

### Backend Environment Variables

See `docs/deployment/ENVIRONMENT_VARIABLES.md` for the full list.

Key ones:
- `DATABASE_URL` — PostgreSQL connection string.
- `CORS_ORIGINS` — Comma-separated list of allowed frontend origins.
- `LANDING_DIR` — Path to landing folder (local) or storage config (Azure).
- `LOG_LEVEL` — `DEBUG`, `INFO`, `WARNING`, `ERROR`.

---

## Deployment

### Local to Docker

```powershell
cd Pipeline\pipeline
docker compose up -d --build
```

Accesses via:
- Backend: `http://localhost:8010`
- Frontend: `http://localhost:5173`
- Postgres: `localhost:5432`

### Local to Azure (Shokworks Pilot)

For detailed instructions, see `docs/runbooks/AZURE_DEPLOYMENT_RUNBOOK.md`.

**Quick summary:**

1. **Build frontend:**
   ```powershell
   cd Pipeline\pipeline\frontend
   npm install && npm run build
   # Output: dist/
   ```

2. **Build backend image:**
   ```powershell
   cd Pipeline\pipeline
   docker build -t ema-api:latest .
   docker tag ema-api:latest <acr>.azurecr.io/ema-api:latest
   docker push <acr>.azurecr.io/ema-api:latest
   ```

3. **Deploy frontend to Static Web Apps:**
   - Upload `dist/` contents to Azure Static Web Apps.
   - Static Web Apps will host at `https://ema-dashboard.azurestaticwebapps.net`.

4. **Deploy backend to Container Apps:**
   - Create Container App from `ema-api:latest` image.
   - Set environment variables (DATABASE_URL, CORS_ORIGINS, etc.).
   - Container App exposes at `https://ema-api.containerapp.azurecontainers.io`.

5. **Link frontend to backend:**
   - Set Static Web App config to pass `VITE_API_BASE_URL=https://ema-api.containerapp.azurecontainers.io` at build time.
   - Or set it in the frontend's environment after deployment.

---

## Troubleshooting

### CORS Error in Browser Console
```
Access to XMLHttpRequest at 'http://localhost:8010/api/v1/projects' 
from origin 'http://localhost:5173' has been blocked by CORS policy
```

**Fix:** Verify backend `CORS_ORIGINS` includes the frontend URL.

```bash
# In docker-compose.yml or Container Apps settings
CORS_ORIGINS=http://localhost:5173,http://127.0.0.1:5173
```

### 404 on API Endpoint

**Check:** Does the endpoint exist in `app/api/`?

```bash
curl -v http://localhost:8010/api/v1/projects
```

Expected: Status 200, JSON response.

If 404: The endpoint is not registered. Check `app/main.py` to ensure the router is included.

### Frontend Can't Reach Backend

**Check:** Is `VITE_API_BASE_URL` set correctly?

```typescript
// Open browser console and check:
console.log(import.meta.env.VITE_API_BASE_URL);
// Should print the backend URL, not "undefined"
```

If undefined: Set `VITE_API_BASE_URL` in `.env` or at build time.

### Database Connection Error

```
sqlalchemy.exc.OperationalError: could not connect to server
```

**Check:** Is PostgreSQL running?

```bash
docker compose ps
# Look for "postgres" container with state "healthy"

# If not healthy, check logs:
docker compose logs postgres
```

---

## API Documentation

**OpenAPI / Swagger:**
- Local: `http://localhost:8010/docs`
- Azure: `https://ema-api.containerapp.azurecontainers.io/docs`

**ReDoc (alternative format):**
- Local: `http://localhost:8010/redoc`

---

## Code Standards

### Backend (Python/FastAPI)

- Use type hints: `def list_projects() -> List[ProjectSummary]:`
- Return schemas, not ORM models: `return ProjectSchema.from_orm(db_project)`
- Document with docstrings:
  ```python
  def get_project(project_id: str) -> ProjectDetail:
      """Retrieve a single project with all metadata."""
  ```

### Frontend (TypeScript/React)

- Use the `client.ts` module, never call endpoints directly.
- Define all types in `types.ts`, not inline.
- Use `useQuery` or similar (if implementing caching later).

---

## Related Files

- `Pipeline/pipeline/app/main.py` — Backend entry point
- `Pipeline/pipeline/frontend/src/api/client.ts` — Frontend client
- `Pipeline/pipeline/frontend/src/types.ts` — Frontend types
- `docs/api/API_INDEX.md` — Full API endpoint reference
- `docs/deployment/ENVIRONMENT_VARIABLES.md` — Env var reference

