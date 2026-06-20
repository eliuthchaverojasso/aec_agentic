# Docker Compose Quickstart

**Last Updated:** 2026-05-28  
**Time to Running:** ~5 minutes

This guide runs EMA AI via Docker Compose (no local Python/Node install required beyond Docker & browser).

## Prerequisites

- **Docker Desktop** — [Download](https://www.docker.com/products/docker-desktop)
- **Web browser** (Chrome, Firefox, Edge, Safari)
- **PowerShell 7+** or bash

**Verify Docker:**
```powershell
docker --version
docker compose version
```

---

## Step 1: Navigate to Project

```powershell
cd "C:\Documents\Hyperghaps EMA\EMA-AI\Pipeline\pipeline"
```

---

## Step 2: Start Services

```powershell
# Start backend + database (in background)
docker compose up -d --build

# Check service status
docker compose ps

# Expected output:
# NAME       STATUS              PORTS
# postgres   Up (healthy)        5432/tcp
# api        Up (healthy)        8000/tcp, 0.0.0.0:8010->8000/tcp
```

**Wait for "healthy"** (usually 10-15 seconds):
```powershell
# Poll until ready
docker compose ps | grep api | grep healthy

# Or just check API directly
curl http://localhost:8010/health

# Should return JSON with status
```

---

## Step 3: Open Frontend

**Frontend is NOT included in docker-compose.yml** (frontend is dev-only via npm).

**Option A: Run frontend locally (recommended for demo)**
```powershell
# New terminal
cd "C:\Documents\Hyperghaps EMA\EMA-AI\Pipeline\pipeline\frontend"
npm install
npm run dev

# Then open: http://localhost:5173
```

**Option B: View API docs (for debugging)**
```
http://localhost:8010/docs  # Swagger UI
http://localhost:8010/openapi.json  # OpenAPI spec
```

---

## Step 4: Log In & Demo

1. **Open** http://localhost:5173 (if Option A)
2. **Login** with:
   - Email: `demo@ema.local`
   - Password: `demo` (any password works locally)
3. **See** seed projects (Denton ISD, Northwest ISD, Rockwall ISD)

---

## Common Docker Commands

### View Logs
```powershell
# API logs (last 50 lines)
docker compose logs api --tail 50

# API logs (follow, live stream)
docker compose logs api -f

# Database logs
docker compose logs postgres

# All services
docker compose logs --tail 30
```

### Access Database
```powershell
# Enter PostgreSQL CLI
docker compose exec postgres psql -U ema -d ema_ai

# View projects
SELECT id, name FROM projects;

# Exit
\q
```

### Stop Services
```powershell
# Stop all (keeps data)
docker compose stop

# Start again
docker compose start

# Stop and remove (keeps data)
docker compose down

# Stop and remove everything (wipes database)
docker compose down -v
```

### Rebuild After Code Changes
```powershell
# Rebuild images
docker compose up -d --build

# Force recreate containers
docker compose up -d --force-recreate --build
```

### View Container Resource Usage
```powershell
docker stats
```

---

## Troubleshooting

### "Cannot connect to API on port 8010"

**Check service status:**
```powershell
docker compose ps
# Should show "api" status as "Up"
```

**Check logs:**
```powershell
docker compose logs api
# Look for errors
```

**If not healthy:**
```powershell
# Restart
docker compose restart

# Or fresh start
docker compose down
docker compose up -d --build
```

### "Port 8010 already in use"

**Find what's using it:**
```powershell
netstat -ano | findstr :8010
```

**Stop it or use different port:**
```powershell
# Edit docker-compose.yml, change:
# ports:
#   - "8010:8000"  # Change first number to free port, e.g. 8011
```

### "Database connection refused"

**Check if postgres container is healthy:**
```powershell
docker compose logs postgres
```

**If corrupted, wipe and restart:**
```powershell
docker compose down -v
docker compose up -d --build
```

### "API returning 500 errors"

**Check logs:**
```powershell
docker compose logs api -f

# Common issues:
# - Database migration failed
# - PYTHONPATH not set (should be automatic in container)
# - Missing environment variables
```

---

## Environment Variables

Docker Compose loads from `.env` file (if it exists).

**Default values (built-in, work without .env):**
- `POSTGRES_USER=ema`
- `POSTGRES_PASSWORD=ema_dev_pw`
- `POSTGRES_DB=ema_ai`
- `API_PORT=8000` (exposed as 8010 via docker-compose.yml)
- `LOG_LEVEL=INFO`
- `CORS_ORIGINS=http://localhost:5173,http://127.0.0.1:5173`

**To override, create `.env` in `Pipeline/pipeline/`:**
```
POSTGRES_PASSWORD=my_custom_pw
LOG_LEVEL=DEBUG
API_PORT=8000
```

Then restart:
```powershell
docker compose down
docker compose up -d --build
```

---

## Health Check

### API Health
```powershell
curl http://localhost:8010/health

# Expected (healthy):
# {
#   "status": "ok",
#   "database": "connected",
#   "version": "0.1.0"
# }
```

### Database Health
```powershell
docker compose exec postgres pg_isready -U ema

# Expected output:
# accepting connections
```

### Full Stack Check
```powershell
Write-Host "=== Docker Compose Health Check ===" -ForegroundColor Green

$ps = docker compose ps
Write-Host $ps

$api_health = curl -s http://localhost:8010/health | jq '.status' 2>/dev/null
Write-Host "`nAPI Status: $api_health" -ForegroundColor Cyan

Write-Host "`nDashboard: http://localhost:5173" -ForegroundColor Green
Write-Host "API Docs: http://localhost:8010/docs" -ForegroundColor Green
```

---

## Data Persistence

**PostgreSQL data is stored in a Docker volume** (`postgres_data`):
- Persists between restarts
- Removed only by `docker compose down -v`

**To back up database:**
```powershell
docker compose exec postgres pg_dump -U ema ema_ai > backup.sql
```

**To restore from backup:**
```powershell
docker compose exec -T postgres psql -U ema ema_ai < backup.sql
```

---

## Running Tests in Docker

**Backend tests (inside container):**
```powershell
# Run all tests
docker compose exec api pytest /app/tests -v

# Run specific test file
docker compose exec api pytest /app/tests/test_projects.py -v

# With coverage
docker compose exec api pytest /app/tests --cov=/app --cov-report=term-missing
```

---

## Performance Tips

- **Keep Docker memory high:** Docker Desktop → Settings → Resources → Memory = 4GB+
- **Watch resource usage:** `docker stats`
- **Use Docker volumes:** Faster than file mounts on Windows
- **Rebuild infrequently:** Only after Python or system package changes

---

## Next Steps

- **Run demo:** [Demo Runbook](demo/DEMO_RUNBOOK.md)
- **View API docs:** http://localhost:8010/docs (Swagger)
- **Understand architecture:** [03_ARCHITECTURE.md](03_ARCHITECTURE.md)
- **Debug issues:** [Troubleshooting Guide](developer/TROUBLESHOOTING_GUIDE.md)
- **Deploy to Azure:** [Azure Deployment Runbook](runbooks/AZURE_DEPLOYMENT_RUNBOOK.md) (planned; not yet deployed)

---

**Quick Start Complete!** Your EMA AI stack is running at http://localhost:8010 (API) and http://localhost:5173 (frontend, if you run npm dev).
