# ADR 0006 - EMA AI Installer and Release Architecture

## Status

Accepted

## Context

EMA AI already has partial release tooling:

- Inno Setup scripts for the Revit add-in
- PowerShell install and uninstall helpers
- A multi-year Revit build/package script
- Docker-based backend and PostgreSQL local development
- A Vite frontend that already builds to static assets

That foundation is useful, but it does not yet provide a single, evidence-driven
release architecture for pilot delivery. The installer layer needs to support:

- selectable install profiles
- dependency and prerequisite detection
- reversible installation
- data preservation by default
- static frontend delivery without a Node runtime on the target machine
- optional local AI capability
- deterministic release-build artifacts and provenance

## Decision

Use a manifest-driven release model built around these pieces:

1. **Inno Setup** as the Windows installer/bootstrapper shell
2. **PowerShell release orchestration** as the deterministic build and packaging layer
3. **Component manifests** in machine-readable JSON for dependency resolution
4. **Docker Compose** for backend, PostgreSQL, and optional local-service hosting
5. **Static frontend assets** built once and served without a Node dev server on the target machine
6. **PowerShell lifecycle scripts** for start, stop, health, repair, uninstall, and rollback staging
7. **Signed manifest design** for future update validation and anti-downgrade checks

## Why This Architecture

### Inno Setup over MSI or WiX/Burn

- The repository already contains Inno Setup packaging work.
- A single EXE is easy for pilots and IT to distribute.
- The product needs custom prerequisite checks and conditional file layout more than deep MSI servicing.
- Inno Setup is simpler to audit and extend than a first-pass Burn authoring effort.

### PowerShell release orchestration over a custom bootstrapper only

- The repo already uses PowerShell for install, uninstall, and local startup logic.
- PowerShell is easier to compose with existing build tools and Git metadata.
- The release process must build multiple payloads, compute hashes, and emit inventory.
- A pure custom bootstrapper would add maintenance burden without improving traceability.

### Docker Compose for backend and PostgreSQL

- The repo already uses Docker Compose for the local backend stack.
- PostgreSQL state can live in a named Docker volume, which supports reversible uninstall and data preservation.
- Backend images remain reproducible from source.

### Static frontend assets

- The frontend already produces a production build with Vite.
- Shipping static assets avoids a Node.js dependency on the target machine.
- The dashboard can be served from a lightweight web host or container without a dev server.

## Consequences

- The first release foundation can be delivered without rewriting the deterministic Revit engine.
- The installer can offer explicit profiles instead of forcing every dependency on every machine.
- Backend and frontend lifecycle become part of the release bundle instead of repo-only developer workflows.
- The installer still depends on external prerequisites for some profiles, especially Docker Desktop and optional Ollama.
- The architecture remains compatible with future MSI/WiX migration if enterprise servicing later demands it.

## Non-Goals

- Do not claim production readiness or universal Windows/Revit compatibility.
- Do not make AI authoritative for compliance or readiness.
- Do not treat optional local AI as required for deterministic operation.
- Do not replace the deterministic engine or PostgreSQL as sources of truth.

