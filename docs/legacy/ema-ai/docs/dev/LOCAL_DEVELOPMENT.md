# EMA AI — Local Development Guide

**Last updated:** 2026-06-08

---

## Prerequisites

| Tool | Required | Version |
|------|----------|---------|
| Revit | For add-in work | 2023 / 2024 / 2025 / 2026 / 2027 |
| .NET Framework | Revit add-in | 4.8 |
| MSBuild | Revit add-in build | Via Visual Studio or Build Tools |
| Python | Backend | 3.12 |
| Node.js | Frontend | 18+ |
| Docker | Backend/DB | Desktop |
| Ollama | Local AI | Latest |
| Git | Version control | Latest |

---

## Repo Structure

```
EMA-AI/
├── EMAExtractor/         # Revit add-in (C# .NET 4.8)
├── EMAExtractor.Tests/   # Revit add-in tests (xUnit)
├── Pipeline/
│   └── pipeline/
│       ├── app/          # FastAPI backend (Python)
│       ├── frontend/     # React dashboard (TypeScript)
│       ├── db/           # PostgreSQL schema
│       └── tests/        # Backend tests
├── docs/                 # Documentation
├── .ai/                  # Agent context
├── scripts/              # Build/installer scripts
├── artifacts/            # Build output (gitignored)
├── installer/            # Installer scripts
└── README.md
```

---

## Revit Add-in Development

### Build
```powershell
# Revit 2023
msbuild EMAExtractor/EMAExtractor.csproj /p:RevitYear=2023 /p:Platform=x64

# Revit 2024
msbuild EMAExtractor/EMAExtractor.csproj /p:RevitYear=2024 /p:Platform=x64
```

### Important
- Old-style `.csproj` — new `.cs` files must be explicitly added to the project file
- External Revit API DLLs: `C:\Program Files\Autodesk\Revit {Year}\`
- Output to `EMAExtractor/bin/x64/{Debug|Release}/`

### Run Tests
```powershell
cd EMAExtractor.Tests
dotnet test
```

---

## Backend Development

### Setup
```powershell
cd Pipeline\pipeline
python -m venv venv
.\venv\Scripts\Activate
pip install -r requirements.txt
```

### Start
```powershell
docker compose up -d --build
curl http://localhost:8010/health
```

### Tests
```powershell
py -3.12 -m pytest tests -v
```

---

## Frontend Development

### Setup
```powershell
cd Pipeline\pipeline\frontend
npm install
```

### Start
```powershell
npm run dev
# Opens http://localhost:5173
```

### Build / Check
```powershell
npx tsc -b --noEmit
npm run build
```

---

## Local AI / Ollama

### Check Models
```powershell
ollama list
```

Expected:
```
gemma4:31b        19 GB
qwen3.6:35b       23 GB
granite4.1:30b    17 GB
bge-m3:latest     1.2 GB
```

### Start Ollama
```powershell
ollama serve
# Runs at http://localhost:11434
```

### Configuration
```ini
AI_PROVIDER=ollama
AI_MODEL=qwen3.6:35b
AI_SMALL_MODEL=granite4.1:30b
AI_EMBED_MODEL=bge-m3:latest
OLLAMA_BASE_URL=http://localhost:11434/v1
```

---

## Common Commands

```powershell
# Git status
git status --short

# Check branch
git branch --show-current

# Run all backend tests
cd Pipeline\pipeline && py -3.12 -m pytest tests -v

# Build Revit add-in
msbuild EMAExtractor/EMAExtractor.csproj /p:RevitYear=2024 /p:Platform=x64

# Frontend typecheck + build
cd Pipeline\pipeline\frontend && npx tsc -b --noEmit && npm run build

# Ollama list
ollama list
```

---

## Troubleshooting

| Problem | Likely Cause | Fix |
|---------|-------------|-----|
| MSBuild can't find Revit DLLs | Wrong Revit year | Set `/p:RevitYear=2024` correctly |
| Docker containers won't start | Docker daemon not running | Start Docker Desktop |
| Backend tests fail on Windows | Python version mismatch | Use `py -3.12` |
| Ollama not responding | Service not running | `ollama serve` |
| Report shows all NA | Excel parser failed | Check workbook format |
| Element IDs not shown | Model sync failed | Re-run "Sync Model Data" |

---

## What NOT to Do

- Do not commit `artifacts/`, `bin/`, `obj/`, `dist/`, `node_modules/`
- Do not commit `*.exe`, `*.zip`, `*.log`
- Do not commit real client files (`.xlsx`, `.rvt`)
- Do not commit `.env` files
- Do not use `git add .`
