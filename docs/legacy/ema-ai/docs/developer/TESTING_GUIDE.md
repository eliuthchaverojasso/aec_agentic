# Testing Guide

## Backend
```powershell
cd "C:\Documents\Hyperghaps EMA\EMA-AI"
$env:PYTHONPATH = (Resolve-Path .\Pipeline\pipeline).Path
py -3.12 -m compileall .\Pipeline\pipeline\app
py -3.12 -m pytest .\Pipeline\pipeline\tests -v
```

## Frontend
```powershell
cd "C:\Documents\Hyperghaps EMA\EMA-AI\Pipeline\pipeline\frontend"
npm.cmd run build
```

## Revit (Scoped)
```powershell
dotnet msbuild EMAExtractor\EMAExtractor.csproj /p:Configuration=Debug /p:Platform=x64
```

## Manual Smoke
- Project setup + bootstrap.
- Processing / Sync operations.
- Debug / Logs timeline and environment diagnostics.
