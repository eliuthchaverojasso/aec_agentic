# Revit Add-in Architecture

## Purpose
Provide local export/binding workflow into project landing structure.

## Components
- Ribbon commands.
- Settings and project binding.
- Export runner and metadata sidecars.
- Installer/update scripts.

## Boundaries
- Add-in does not compute official readiness.
- Ingest remains operator-controlled in web app.
- Local-first settings with no secrets embedded.
