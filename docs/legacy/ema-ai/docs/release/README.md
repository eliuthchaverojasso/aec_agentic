# Release Documentation

This folder now holds the installer and release foundation for EMA AI.

## Key Docs

- [Installer Release Architecture](./INSTALLER_RELEASE_ARCHITECTURE.md)
- [Component Matrix](./COMPONENT_MATRIX.md)
- [Signed Manifest Design](./SIGNED_MANIFEST_DESIGN.md)
- [Installer Implementation Audit](./INSTALLER_IMPLEMENTATION_AUDIT.md)

## Scope

- Local pilot packaging
- Installer/bootstrapper architecture
- Component dependency mapping
- Update and rollback foundation
- Signed-manifest direction for update validation

## Current Boundary

- Deterministic engines still decide official status
- PostgreSQL remains official source of truth for the dashboard stack
- AI remains advisory only
- No production-readiness claim is made here
