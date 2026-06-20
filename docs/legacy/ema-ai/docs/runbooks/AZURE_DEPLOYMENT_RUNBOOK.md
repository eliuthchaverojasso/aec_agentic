# Azure Deployment Runbook

**Last updated:** 2026-05-26  
**Status:** Planned for P1 (post-demo deployment)  
**Owner:** Infrastructure team  
**Duration:** ~2–4 hours (first deployment)  

---

## Overview

This runbook walks you through deploying EMA AI from local Docker to Azure Container Apps, Static Web Apps, and PostgreSQL Flexible Server for a controlled Shokworks pilot.

---

## Architecture Target

```
Frontend (React/Vite)
  └─ Azure Static Web Apps (dist/ files)
     └─ Served over HTTPS

Backend API (FastAPI)
  └─ Azure Container Apps (Docker image)
     └─ Exposed via HTTPS ingress
     └─ Connects to PostgreSQL

Database (PostgreSQL)
  └─ Azure Database for PostgreSQL Flexible Server
     └─ Behind private network

Storage (Landing/Processing)
  └─ Azure Storage Account (Data Lake Gen2)
     └─ Landing, processed, archive, rejected containers

Secrets & Config
  └─ Azure Key Vault
  └─ Environment variables in Container Apps settings

Monitoring
  └─ Application Insights
  └─ Log Analytics Workspace
```

---

## Pre-Deployment Checklist

- [ ] **Azure subscription access:** Owner or Contributor role on target subscription.
- [ ] **Azure CLI installed:** `az --version` returns a version.
- [ ] **Docker Desktop running:** `docker ps` works.
- [ ] **Git branch clean:** `git status` shows no uncommitted changes (docs-only is OK).
- [ ] **Backend tests passing:** `cd Pipeline/pipeline && python -m pytest tests -v` (see `.ai/TEST_MATRIX.md` for current count — requires running Docker stack).
- [ ] **Frontend build passing:** `npm run build` in `Pipeline/pipeline/frontend/`.
- [ ] **Local Docker smoke passing:** `docker compose up -d --build && curl http://localhost:8010/health`.
- [ ] **Resource group name decided:** e.g., `rg-ema-ai-pilot` (must be unique globally).
- [ ] **Naming convention decided:** e.g., `ema-{env}-{component}` (`ema-pilot-api`, `ema-pilot-db`, etc.).

---

## Step 1: Prepare Azure Resources

### 1.1 Create Resource Group

```powershell
$resourceGroup = "rg-ema-ai-pilot"
$location = "eastus"  # or your preferred region

az group create \
  --name $resourceGroup \
  --location $location

Write-Output "Resource group created: $resourceGroup in $location"
```

### 1.2 Create Storage Account (Data Lake Gen2)

```powershell
$storageAccount = "emaailandings"  # Must be globally unique, lowercase
$resourceGroup = "rg-ema-ai-pilot"

az storage account create \
  --name $storageAccount \
  --resource-group $resourceGroup \
  --location eastus \
  --kind StorageV2 \
  --enable-hierarchical-namespace true \
  --tier Standard \
  --access-tier Hot

Write-Output "Storage account created: $storageAccount"

# Create containers
foreach ($container in @("landing", "processed", "archive", "rejected")) {
  az storage container create \
    --name $container \
    --account-name $storageAccount \
    --auth-mode login
  Write-Output "Container created: $container"
}
```

### 1.3 Create Key Vault

```powershell
$keyVault = "ema-pilot-kv"
$resourceGroup = "rg-ema-ai-pilot"

az keyvault create \
  --name $keyVault \
  --resource-group $resourceGroup \
  --location eastus \
  --enable-purge-protection false

Write-Output "Key Vault created: $keyVault"

# Add secrets (example values)
az keyvault secret set \
  --vault-name $keyVault \
  --name "postgres-password" \
  --value "ChangeMe_SecurePassword123!"

az keyvault secret set \
  --vault-name $keyVault \
  --name "appinsights-connection-string" \
  --value "InstrumentationKey=..."

Write-Output "Secrets added to Key Vault"
```

### 1.4 Create PostgreSQL Flexible Server

```powershell
$dbServer = "ema-pilot-db"
$resourceGroup = "rg-ema-ai-pilot"
$adminUser = "ema_admin"
$adminPassword = "ChangeMe_SecurePassword123!"  # From Key Vault above
$dbName = "ema_ai"

az postgres flexible-server create \
  --name $dbServer \
  --resource-group $resourceGroup \
  --location eastus \
  --admin-user $adminUser \
  --admin-password $adminPassword \
  --database-name $dbName \
  --tier Burstable \
  --sku-name Standard_B1ms \
  --storage-size 32 \
  --version 16

Write-Output "PostgreSQL Flexible Server created: $dbServer"
Write-Output "Database created: $dbName"

# Enable public access temporarily (for first data load)
# Later, restrict to VNET/private endpoints
az postgres flexible-server firewall-rule create \
  --resource-group $resourceGroup \
  --name $dbServer \
  --rule-name "AllowAzureServices" \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 255.255.255.255

Write-Output "Firewall rule added (temporary for setup)"
```

### 1.5 Load Database Schema

```powershell
$dbServer = "ema-pilot-db.postgres.database.azure.com"
$adminUser = "ema_admin"
$dbName = "ema_ai"

# Get password from Key Vault
$adminPassword = az keyvault secret show \
  --vault-name "ema-pilot-kv" \
  --name "postgres-password" \
  --query "value" \
  --output tsv

# Load schema from init.sql
$schemaFile = "Pipeline\pipeline\db\init.sql"

psql -h $dbServer \
  -U "$adminUser@$dbServer" \
  -d $dbName \
  -f $schemaFile

Write-Output "Database schema loaded"
```

### 1.6 Create Container Registry (ACR)

```powershell
$acrName = "emaairegistry"  # Must be globally unique
$resourceGroup = "rg-ema-ai-pilot"

az acr create \
  --resource-group $resourceGroup \
  --name $acrName \
  --sku Basic

Write-Output "Azure Container Registry created: $acrName"

# Get login server
$loginServer = az acr show \
  --name $acrName \
  --query "loginServer" \
  --output tsv

Write-Output "ACR login server: $loginServer"
```

### 1.7 Create Application Insights

```powershell
$appInsights = "ema-pilot-insights"
$resourceGroup = "rg-ema-ai-pilot"

az monitor app-insights component create \
  --app $appInsights \
  --resource-group $resourceGroup \
  --location eastus \
  --application-type web

$connectionString = az monitor app-insights component show \
  --app $appInsights \
  --resource-group $resourceGroup \
  --query "connectionString" \
  --output tsv

az keyvault secret set \
  --vault-name "ema-pilot-kv" \
  --name "appinsights-connection-string" \
  --value $connectionString

Write-Output "Application Insights created: $appInsights"
```

---

## Step 2: Build and Push Container Images

### 2.1 Build Backend Image

```powershell
cd Pipeline\pipeline

# Build the image locally
docker build -t ema-api:latest .

# Tag for ACR
$acrName = "emaairegistry"
$loginServer = az acr show \
  --name $acrName \
  --query "loginServer" \
  --output tsv

docker tag ema-api:latest "$loginServer/ema-api:latest"
docker tag ema-api:latest "$loginServer/ema-api:$(date +%Y%m%d.%H%M%S)"

# Login to ACR
az acr login --name $acrName

# Push image
docker push "$loginServer/ema-api:latest"

Write-Output "Backend image pushed to ACR"
```

### 2.2 Build Frontend

```powershell
cd Pipeline\pipeline\frontend

# Build the static files
npm install
npm run build

# Output will be in dist/
Write-Output "Frontend built. Output in dist/"

# Verify dist/ exists
ls -la dist/
```

---

## Step 3: Deploy Backend (Container Apps)

### 3.1 Create Container Apps Environment

```powershell
$env_name = "ema-pilot-env"
$resourceGroup = "rg-ema-ai-pilot"

az containerapp env create \
  --name $env_name \
  --resource-group $resourceGroup \
  --location eastus

Write-Output "Container Apps environment created: $env_name"
```

### 3.2 Create Container App (API)

```powershell
$acrName = "emaairegistry"
$containerAppName = "ema-pilot-api"
$resourceGroup = "rg-ema-ai-pilot"
$envName = "ema-pilot-env"
$loginServer = az acr show \
  --name $acrName \
  --query "loginServer" \
  --output tsv

# Database URL (from Key Vault reference)
$databaseUrl = "postgresql+psycopg2://ema_admin:@ema-pilot-db.postgres.database.azure.com:5432/ema_ai"

# Create the container app
az containerapp create \
  --name $containerAppName \
  --resource-group $resourceGroup \
  --environment $envName \
  --image "$loginServer/ema-api:latest" \
  --target-port 8000 \
  --ingress external \
  --query properties.configuration.ingress.fqdn

# Set environment variables
az containerapp update \
  --name $containerAppName \
  --resource-group $resourceGroup \
  --set-env-vars \
    DATABASE_URL="$databaseUrl" \
    POSTGRES_USER="ema_admin" \
    POSTGRES_DB="ema_ai" \
    LOG_LEVEL="INFO" \
    CORS_ORIGINS="https://ema-dashboard.azurestaticwebapps.net" \
    LANDING_DIR="/app/landing" \
    APPINSIGHTS_CONNECTION_STRING="@Microsoft.KeyVault(VaultName=ema-pilot-kv,SecretName=appinsights-connection-string)"

# Get the app URL
$appFqdn = az containerapp show \
  --name $containerAppName \
  --resource-group $resourceGroup \
  --query "properties.configuration.ingress.fqdn" \
  --output tsv

Write-Output "Container App created. API URL: https://$appFqdn"

# Verify health
Start-Sleep -Seconds 30
$health = Invoke-WebRequest -Uri "https://$appFqdn/health" -ErrorAction SilentlyContinue
if ($health.StatusCode -eq 200) {
  Write-Output "✅ Health check passed"
} else {
  Write-Output "❌ Health check failed. Check logs:"
  az containerapp logs show \
    --name $containerAppName \
    --resource-group $resourceGroup \
    --follow
}
```

---

## Step 4: Deploy Frontend (Static Web Apps)

### 4.1 Create Static Web App

```powershell
$staticAppName = "ema-pilot-dashboard"
$resourceGroup = "rg-ema-ai-pilot"

az staticwebapp create \
  --name $staticAppName \
  --resource-group $resourceGroup \
  --location westus2 \
  --sku Free

$staticAppUrl = az staticwebapp show \
  --name $staticAppName \
  --resource-group $resourceGroup \
  --query "defaultHostname" \
  --output tsv

Write-Output "Static Web App created: $staticAppName"
Write-Output "URL: https://$staticAppUrl"
```

### 4.2 Upload Frontend Files

```powershell
$staticAppName = "ema-pilot-dashboard"
$resourceGroup = "rg-ema-ai-pilot"
$distPath = "Pipeline\pipeline\frontend\dist"

# Create a deployment token (if not already created)
$token = az staticwebapp secrets list \
  --name $staticAppName \
  --resource-group $resourceGroup \
  --query "properties.apiKey" \
  --output tsv

# Option A: Using az CLI (simple)
az staticwebapp upload \
  --name $staticAppName \
  --resource-group $resourceGroup \
  --source-path $distPath

# Option B: Using GitHub Actions (more complex, for next iteration)
# See: https://learn.microsoft.com/en-us/azure/static-web-apps/github-actions-workflow

Write-Output "Frontend deployed to Static Web App"
```

### 4.3 Configure API Base URL

The frontend needs to know where the backend is. Configure via environment variables at build or deployment time.

**Option A: Rebuild with environment variable**

```powershell
$apiUrl = "https://$appFqdn"  # From Step 3.2

cd Pipeline\pipeline\frontend
$env:VITE_API_BASE_URL = $apiUrl
npm run build
# Then re-upload dist/ to Static Web App
```

**Option B: Configure in Static Web App settings (if using build integration)**

In Azure Portal:
1. Go to Static Web App > Settings > Configuration.
2. Add environment variable: `VITE_API_BASE_URL = https://ema-pilot-api.{region}.containerapp.azurecontainers.io`

---

## Step 5: Verify End-to-End

### 5.1 Test Backend Health

```powershell
$apiUrl = "https://$appFqdn"  # From Step 3.2

curl "$apiUrl/health"
# Expected: {"status":"ok","database":"ok","version":"0.1.0"}

Write-Output "✅ Backend health check passed"
```

### 5.2 Test Frontend Loading

```powershell
# Open in browser
start "https://$staticAppUrl"

# Or test with curl
curl "https://$staticAppUrl/"
# Expected: HTML document with EMA AI dashboard
```

### 5.3 Test Frontend-to-Backend Communication

In the browser, open Developer Tools > Network tab and:
1. Navigate to the dashboard.
2. Open the Requirements page.
3. Look for API calls to `GET /api/v1/projects` or similar.
4. Verify they return 200 OK and have JSON data.

If CORS errors appear:
- Check backend `CORS_ORIGINS` setting in Container App environment variables.
- Should include the Static Web App URL.

### 5.4 Load Demo Data

If data is needed:
```powershell
# Option A: Load from local PostgreSQL dump
pg_dump -h ema-pilot-db.postgres.database.azure.com \
  -U ema_admin \
  -d ema_ai \
  > backup.sql

# Option B: Use landing folder workflow
# Upload test data to Storage Account > landing container
# Then trigger ingest via API or UI
```

---

## Step 6: Monitoring & Troubleshooting

### 6.1 Check Logs

```powershell
# Container App logs
az containerapp logs show \
  --name ema-pilot-api \
  --resource-group rg-ema-ai-pilot \
  --follow

# Application Insights query (in Azure Portal)
# Traces | where message contains "error" | limit 100
```

### 6.2 Scale (Optional)

```powershell
# Increase CPU/memory
az containerapp update \
  --name ema-pilot-api \
  --resource-group rg-ema-ai-pilot \
  --cpu "0.5" \
  --memory "1Gi"
```

### 6.3 Common Errors

| Error | Cause | Fix |
|---|---|---|
| Container fails to start | Image not in ACR | Re-push: `docker push $loginServer/ema-api:latest` |
| 502 Bad Gateway | Backend not responding | Check Container App logs: `az containerapp logs show ...` |
| CORS error in browser | `CORS_ORIGINS` doesn't include frontend URL | Update Container App env vars |
| Database connection failed | PostgreSQL firewall rule | Temporarily allow Azure Services in PostgreSQL firewall |
| Health check returns 500 | Database unreachable | Verify `DATABASE_URL` and PostgreSQL is running |

---

## Step 7: Clean Up & Next Steps

### 7.1 (Optional) Restrict Database Access

Once everything is working, restrict PostgreSQL access to Container Apps via VNET and private endpoints (enterprise hardening, deferred for now).

### 7.2 Plan Data Refresh

Set up a schedule for:
- Daily backup of PostgreSQL.
- Archive old landing files.
- Refresh demo data if needed.

### 7.3 Document Runbook

Save this runbook with final values:
- Resource group name: `rg-ema-ai-pilot`
- Storage account: `emaailandings`
- Container App: `ema-pilot-api`
- Static Web App: `ema-pilot-dashboard`
- Database: `ema-pilot-db.postgres.database.azure.com`
- API URL: `https://ema-pilot-api.{region}.containerapp.azurecontainers.io`
- Dashboard URL: `https://ema-pilot-dashboard.azurestaticwebapps.net`

---

## Rollback Plan

If deployment fails:

1. **Restore PostgreSQL from backup:**
   ```powershell
   # Point-in-time restore
   az postgres flexible-server restore \
     --name ema-pilot-db-restored \
     --resource-group rg-ema-ai-pilot \
     --restore-time "2026-05-26T10:00:00Z" \
     --source-server ema-pilot-db
   ```

2. **Revert to previous image:**
   ```powershell
   # Tag a known-good image
   docker tag emaairegistry.azurecr.io/ema-api:20260525.140000 \
     emaairegistry.azurecr.io/ema-api:stable
   
   # Update Container App to use stable tag
   az containerapp update \
     --name ema-pilot-api \
     --resource-group rg-ema-ai-pilot \
     --image emaairegistry.azurecr.io/ema-api:stable
   ```

3. **Revert frontend:**
   - Upload previous `dist/` files to Static Web App.
   - Or trigger a rebuild from the previous git commit.

---

## Related Files

- `docs/deployment/ENVIRONMENT_VARIABLES.md` — Environment variable reference
- `docs/api/API_CONSUMPTION_GUIDE.md` — Frontend-backend integration guide
- `docs/architecture/AZURE_PILOT_ARCHITECTURE.md` — Architecture overview
- `Pipeline/pipeline/Dockerfile` — Backend image definition
- `Pipeline/pipeline/docker-compose.yml` — Local Docker reference

