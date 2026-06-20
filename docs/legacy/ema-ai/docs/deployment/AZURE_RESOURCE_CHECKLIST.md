# Azure Resource Checklist

Status: Planning checklist. No Azure resources are created by this document.

## Minimum Shokworks Pilot Resources

- Resource Group.
- Azure Storage Account with Data Lake Gen2 / hierarchical namespace enabled.
- Storage containers:
  - `landing`
  - `processed`
  - `archive`
  - `rejected`
  - optional `raw-exports`
- Azure Database for PostgreSQL Flexible Server.
- PostgreSQL database name: `ema_ai`.
- Azure Container Apps Environment.
- Container App for the FastAPI API.
- Azure Static Web Apps for the React dashboard, or Azure App Service if traditional app hosting is required.
- Azure Key Vault.
- Application Insights.
- Log Analytics Workspace.
- Azure Container Registry, optional for a very simple pilot and recommended for Container Apps.

## Enterprise Resources

- EMA-owned Azure subscription or resource group structure.
- Separate environments:
  - `dev`
  - `test`
  - `prod`
- Azure Container Registry with RBAC and private access where required.
- Container App for the FastAPI API.
- Optional Container Apps Job for background processing.
- Azure Static Web Apps or Azure App Service for dashboard hosting.
- Azure Database for PostgreSQL Flexible Server with backup/restore policy.
- Storage Account with ADLS Gen2, lifecycle policy, RBAC, and private endpoints.
- Key Vault with Managed Identity access.
- Application Insights and Log Analytics Workspace with alert rules.
- Optional API Management for enterprise API gateway policies.
- Optional VNET integration and private endpoints.
- Firewall rules and no public database access.
- RBAC groups and managed identities for:
  - deployment automation
  - backend runtime
  - operations
  - support/read-only review
- CI/CD pipeline with environment approvals.
- Audit logging and retention policy.

## Deferred Resources

- Production AI Query infrastructure.
- GraphRAG / vector retrieval production infrastructure.
- Live ACC integration resources.
- UNANET integration resources.
- Full Drawing Reel / PDF OCR processing services.
- Separate dashboard backend.

## Readiness Review Questions

- Has the Azure pilot been approved as deployed scope, not only documented scope?
- Are CORS origins restricted to the approved dashboard URL?
- Is the database accessible only from approved services and networks?
- Are all secrets in Key Vault or app settings, with no real secrets committed?
- Is PostgreSQL still the official source of truth?
- Are LLM/AI features excluded from official readiness approval?
- Are backup and restore responsibilities assigned?
