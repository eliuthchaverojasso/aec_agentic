# EMA AI Local-to-Azure Deployment Foundation

Status: Planned deployment foundation. The local MVP exists; the Azure pilot is documented but not deployed.

EMA AI is an Engineering Intelligence / Deliverable Readiness platform. PostgreSQL remains the official source of truth, and deterministic Rule, Evidence, and Readiness engines produce official readiness state. LLMs may explain, summarize, search, and draft suggestions, but they do not approve readiness, decide compliance, close issues, or mutate official source-of-truth data automatically.

## Deployment Stages

| Stage | Purpose | Runtime Shape | Status |
| --- | --- | --- | --- |
| Stage 1: Local developer laptop | Local development, demo validation, deterministic backend testing | Docker Compose PostgreSQL + FastAPI API, React/Vite dashboard, local landing folder | Active MVP |
| Stage 2: Shokworks Azure pilot | Controlled cloud pilot and stakeholder review | Azure Static Web Apps, Azure Container Apps, Azure Database for PostgreSQL Flexible Server, ADLS Gen2, Key Vault, Application Insights | Planned, not deployed |
| Stage 3: EMA / enterprise Azure environment | Enterprise-owned production-ready deployment | Hardened Azure landing zone with VNET integration, private endpoints, RBAC, Managed Identity, environment separation, CI/CD | Target architecture |

## Architecture Overview

| Capability | Local Developer Laptop | Shokworks Azure Pilot | EMA / Enterprise Azure |
| --- | --- | --- | --- |
| Frontend / dashboard | React/Vite dev server on `http://localhost:5173` | Azure Static Web Apps recommended; App Service acceptable if full-stack hosting is needed | Static Web Apps or App Service with custom domain, approved origins, enterprise SSO path when scoped |
| Backend API / processing | FastAPI container `ema_api` exposed on `http://localhost:8010` | Azure Container Apps recommended; App Service acceptable for simple deployment | Azure Container Apps with VNET integration, private dependencies, scaling rules, health checks |
| Database | Docker Compose PostgreSQL container `ema_postgres` | Azure Database for PostgreSQL Flexible Server | Azure Database for PostgreSQL Flexible Server with no public database access, backups, firewall/private endpoints |
| Storage / landing | Local `Pipeline/pipeline/landing` folder | Azure Storage Account with ADLS Gen2 containers | ADLS Gen2 with RBAC, lifecycle policies, private endpoints, audit logging |
| Secrets | Local `.env.example` templates; local `.env` never committed | Azure Key Vault plus app settings | Key Vault references, Managed Identity, no production secrets in repo |
| Observability | Docker logs, local API health, Swagger | Application Insights + Log Analytics | Central Log Analytics, alerts, audit retention, dashboards |
| Container registry | Local Docker build | Azure Container Registry optional but recommended for Container Apps | Azure Container Registry with private access and CI/CD integration |
| Networking | Public localhost | Public HTTPS endpoints may be acceptable with restricted CORS/IP rules | VNET integration, private endpoints, firewall rules, locked CORS, least privilege |

## Local Runtime Map

| Service | Local Name / URL | Notes |
| --- | --- | --- |
| PostgreSQL | `ema_postgres`, `localhost:5432` | Database `ema_ai`, user `ema`, demo password only |
| FastAPI backend | `ema_api`, `http://localhost:8010` | Container maps host `8010` to container `8000` |
| Swagger | `http://localhost:8010/docs` | Local API inspection |
| React dashboard | `http://localhost:5173` | Same dashboard intended for initial Azure hosting |
| Landing folder | `Pipeline/pipeline/landing` | Local file landing zone for demo manifests and exports |

Local startup:

```powershell
cd Pipeline\pipeline
docker compose up -d --build
curl.exe http://localhost:8010/health

cd frontend
npm.cmd install
npm.cmd run dev
```

## Azure Pilot Target Map

Recommended Shokworks pilot default:

| Capability | Azure Resource |
| --- | --- |
| Frontend / dashboard | Azure Static Web Apps |
| Backend API / processing | Azure Container Apps |
| Database | Azure Database for PostgreSQL Flexible Server |
| Storage / landing | Azure Storage Account with Data Lake Gen2 capability |
| Storage containers | `landing`, `processed`, `archive`, `rejected`; optional `raw-exports` |
| Secrets | Azure Key Vault |
| Monitoring | Application Insights + Log Analytics |
| Registry | Azure Container Registry recommended for Container Apps |

The dashboard is the existing React frontend initially. Do not split the dashboard into a separate backend or service unless future requirements justify it.

AI Query and GraphRAG are deferred. Future AI Query is an evidence assistant only; it does not approve readiness or compliance.

## Enterprise Hardening Map

Enterprise deployment should support:

- EMA-owned subscription or resource group structure.
- Separate `dev`, `test`, and `prod` environments.
- VNET integration for backend services.
- Private endpoints for PostgreSQL, Storage, Key Vault, and registry where required.
- No public database access.
- Locked CORS to approved dashboard domains.
- Managed Identity for service-to-service access.
- Key Vault references instead of plain production secrets.
- Least-privilege RBAC groups for operators, developers, and deployment identities.
- Backup/restore policies for PostgreSQL and Storage.
- Application Insights, Log Analytics, alert rules, and audit retention.
- CI/CD pipeline with approvals and environment-specific configuration.

## Deferred Capabilities

These are not part of the current deployment foundation:

- Production AI Query.
- GraphRAG.
- Live ACC integration.
- UNANET integration.
- Full PDF/OCR Drawing Reel.
- Separate dashboard backend service.

## Deployment Rule

Do not create real Azure resources from this repository without an explicitly scoped infrastructure branch, reviewed parameters, approved environment naming, and approved identity/secrets handling. This document defines the deployment path; it does not claim the Azure pilot is deployed.
