# Project Viewer (Local Demo)

## Purpose
Project Viewer provides a project-level model package surface for registered `DWFx/DWF/RVT/NWD/NWC/IFC/SVF/SVF2/GLTF/GLB` files.

## Current Behavior
- Viewer packages are derived from indexed project documents by file extension.
- The viewer is **APS-ready architecture only** in this build.
- If APS is not configured, UI shows:
  - `APS Not Configured`
  - `Registered Package`
  - `Preview Unavailable` or future-local modes for IFC/glTF.

## Truth Boundaries
- Viewer packages are **Evidence Candidates** by default.
- Viewer package presence is not official evidence or compliance.
- Browser-native DWFx rendering is not claimed in this local demo.
- APS token broker/upload/translation/URN flow is not active here.

## Linked Surfaces
- Processing / Sync
- Documents / Evidence
- Debug / Logs
- Requirements and issue traceability panel
