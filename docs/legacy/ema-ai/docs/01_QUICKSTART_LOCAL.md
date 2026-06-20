# Local Development Quickstart

**Last Updated:** 2026-05-28  
**Time to Running:** ~10 minutes

This guide gets you running EMA AI locally with Docker Compose.

## Prerequisites

- **Docker Desktop** (with Compose) — [Download](https://www.docker.com/products/docker-desktop)
- **Node.js 18+** — [Download](https://nodejs.org/)
- **Python 3.12** (for backend testing) — [Download](https://www.python.org/)
- **PowerShell 7** (Windows) or bash (macOS/Linux)
- **Git**

**Verify installed:**
```powershell
docker --version
docker compose version
node --version
npm --version
python --version
git --version
```

---

## Step 1: Clone & Navigate

```powershell
# Clone the repo (if you haven't)
git clone https://github.com/shokworks/ema-ai.git
cd "C:\Documents\Hyperghaps EMA\EMA-AI"

# Or if already cloned, pull latest
git pull origin main
```

---

## Step 2: Start Backend & Database

**Terminal 1:**
```powershell
cd "C:\Documents\Hyperghaps EMA\EMA-AI\Pipeline\pipeline"

# Start Docker stack (postgres + api)
docker compose up -d --build

# Verify services are healthy
docker compose ps

# Expected output:
# NAME       STATUS              PORTS
# postgres   Up (healthy)        5432/tcp
# api        Up (healthy)        8010/tcp
```

**Verify backend is running:**
```powershell
# Should return {"status":"ok",...}
curl http://localhost:8010/health

# Or in browser:
# http://localhost:8010/docs (Swagger API docs)
```

---

## Step 3: Start Frontend

**Terminal 2:**
```powershell
cd "C:\Documents\Hyperghaps EMA\EMA-AI\Pipeline\pipeline\frontend"

# Install dependencies (first time only)
npm install

# Start dev server
npm run dev

# Expected: "Local: http://localhost:5173"
```

**Open in browser:**
```
http://localhost:5173
```

---

## Step 4: Log In

1. **Navigate** to http://localhost:5173
2. **Login** with demo account:
   - Email: `demo@ema.local`
   - Password: `demo` (any password works in local mode)

You should see the **Portfolio** page with seed projects (Denton ISD, Northwest ISD, Rockwall ISD).

---

## Quick Demo Flow

Once logged in:

1. **Click** a project (e.g., "Denton ISD")
2. **Go to** "Processing" tab → "Owner Requirements"
3. **Upload** a sample requirements Excel (see below)
4. **Go to** "Processing" tab → "Revit Exports"
5. **Upload** a Revit JSON export (see below)
6. **Go to** "Readiness" → See your readiness score

### Sample Data

**Owner Requirements (Excel):**
Use any Excel file with columns: `Discipline`, `Requirement`, `Category`
Example: See `Pipeline/pipeline/landing/Specifications/` for sample structure

**Revit Export (JSON):**
Export from Revit add-in, or use sample:
```json
{
  "ProjectTitle": "Denton ISD - MS Elementary",
  "ExportDate": "2026-05-28T10:00:00",
  "ExportVersion": "1.0",
  "Elements": [
    {
      "UniqueId": "elem-1",
      "ElementId": 123,
      "Category": "Electrical Equipment",
      "FamilyAndType": "Panel - Main",
      "Level": "Level 01",
      "Parameters": {
        "CircuitNumber": "A01",
        "Capacity": "200A"
      }
    }
  ]
}
```

---

## Useful Commands

### View Database
```powershell
# Enter PostgreSQL CLI
docker compose exec postgres psql -U ema -d ema_ai

# View projects
SELECT id, name, created_at FROM projects;

# View requirements
SELECT id, text, discipline FROM requirements LIMIT 5;

# Exit
\q
```

### View Backend Logs
```powershell
docker compose logs api -f  # Follow logs
docker compose logs api --tail 50  # Last 50 lines
```

### Run Backend Tests
```powershell
cd "C:\Documents\Hyperghaps EMA\EMA-AI"
$env:PYTHONPATH = (Resolve-Path .\Pipeline\pipeline).Path
py -3.12 -m pytest .\Pipeline\pipeline\tests -v
```

### Stop Services
```powershell
cd "C:\Documents\Hyperghaps EMA\EMA-AI\Pipeline\pipeline"
docker compose down

# To remove database (clean slate):
docker compose down -v
```

### Rebuild (After Code Changes)
```powershell
docker compose up -d --build
```

---

## Troubleshooting

### "Connection refused on port 8010"
**Issue:** Backend not running or not healthy
```powershell
docker compose ps
# Should show "api" with status "Up (healthy)"

docker compose logs api
# Check for errors in logs
```

**Fix:** Restart services
```powershell
docker compose down
docker compose up -d --build
```

### "Port 5173 already in use"
**Issue:** Another process is using port 5173
```powershell
# Find what's using port 5173
netstat -ano | findstr :5173

# Or just use a different port
npm run dev -- --port 5174
```

### "PostgreSQL connection failed"
**Issue:** Database not initialized or corrupt
```powershell
docker compose down -v  # Remove volume
docker compose up -d --build  # Fresh start
```

### "npm install fails"
**Issue:** Old dependencies or lockfile issues
```powershell
rm -r node_modules package-lock.json
npm install
```

### "TypeScript errors in frontend"
**Issue:** Type mismatches (expected for development)
```powershell
cd Pipeline\pipeline\frontend
npx tsc --noEmit  # Check types without building
```

### "Can't log in"
**Issue:** Cookie/session issue
```powershell
# Clear browser localStorage
# Open DevTools (F12) → Console → type:
localStorage.clear()
# Refresh page
```

---

## File Locations

| Component | Location |
|-----------|----------|
| Backend API | `Pipeline/pipeline/app/` |
| Frontend | `Pipeline/pipeline/frontend/src/` |
| Tests | `Pipeline/pipeline/tests/` |
| Database schema | `Pipeline/pipeline/db/init.sql` |
| Docker Compose | `Pipeline/pipeline/docker-compose.yml` |
| Environment template | `.env.example` |

---

## Environment Variables (Optional)

Backend uses defaults that work for local dev. To customize:

1. **Copy template:**
   ```powershell
   copy .env.example Pipeline\pipeline\.env
   ```

2. **Edit** `Pipeline\pipeline\.env`:
   ```
   POSTGRES_USER=ema
   POSTGRES_PASSWORD=ema_dev_pw
   POSTGRES_DB=ema_ai
   API_PORT=8010
   VITE_API_BASE_URL=http://localhost:8010
   ```

3. **Restart services:**
   ```powershell
   docker compose down
   docker compose up -d --build
   ```

---

## Next Steps

- **Run demo walkthrough:** See [Demo Runbook](demo/DEMO_RUNBOOK.md)
- **Understand architecture:** See [03_ARCHITECTURE.md](03_ARCHITECTURE.md)
- **Run tests:** See [Testing Strategy](dev/TESTING_STRATEGY.md) and [Test Matrix](../.ai/TEST_MATRIX.md)
- **Learn API:** See [API Index](api/API_INDEX.md)
- **Use Revit add-in:** See [Revit Add-in Installation](revit/ADDIN_INSTALLATION.md)

---

## Quick Health Check

Paste this into PowerShell to verify everything is running:

```powershell
Write-Host "=== EMA AI Health Check ===" -ForegroundColor Green

# Backend health
$backend = curl -s http://localhost:8010/health | jq '.status' 2>/dev/null
Write-Host "Backend: $backend"

# Database
$postgres = docker compose ps 2>/dev/null | grep -E "postgres.*healthy"
if ($postgres) { Write-Host "Database: Healthy" } else { Write-Host "Database: Not Healthy" -ForegroundColor Red }

# Frontend
$frontend = curl -s http://localhost:5173 2>/dev/null
if ($frontend) { Write-Host "Frontend: Running" } else { Write-Host "Frontend: Not Running" -ForegroundColor Red }

Write-Host "`nDashboard: http://localhost:5173" -ForegroundColor Cyan
```

---

**Done!** You're running EMA AI locally. Next: [docs/demo/THURSDAY_DEMO_PLAN.md](demo/THURSDAY_DEMO_PLAN.md) for walkthrough.
