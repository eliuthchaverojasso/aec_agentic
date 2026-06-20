# Local Development

## Start Stack
```powershell
cd "C:\Documents\Hyperghaps EMA\EMA-AI\Pipeline\pipeline"
docker compose up -d --build
curl.exe http://localhost:8010/health
cd ".\frontend"
npm.cmd install
npm.cmd run dev
```

## Frontend Build
```powershell
npm.cmd run build
```

## Backend Compile + Tests
```powershell
cd "C:\Documents\Hyperghaps EMA\EMA-AI"
$env:PYTHONPATH = (Resolve-Path .\Pipeline\pipeline).Path
py -3.12 -m compileall .\Pipeline\pipeline\app
py -3.12 -m pytest .\Pipeline\pipeline\tests -v
```

## Revit Build / Install Dry-Run
```powershell
dotnet msbuild EMAExtractor\EMAExtractor.csproj /p:Configuration=Debug /p:Platform=x64
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 -Scope User -RevitYears 2026 -BuildFirst -DryRun
```
