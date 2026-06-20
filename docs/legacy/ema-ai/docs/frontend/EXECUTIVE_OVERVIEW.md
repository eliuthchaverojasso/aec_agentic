# Executive Overview

## Purpose
Executive Overview is the portfolio command screen for local demo operations.

## Included Surfaces
- Portfolio KPI strip (6 cards: Portfolio Readiness, Active Projects, Behind, Blocked, On Track, Documents Indexed)
- Status/date filtering with localStorage persistence (`ema-ai-executive-overview-filters`)
- Real USA geographic map (build-verified) with state boundaries, dependency-free local projection, project markers aligned to local geometry, popovers, zoom/pan/reset, demo toggle, and keyboard-accessible marker list
- Executive Action Queue listing known pipeline blockers
- Top at-risk projects table (up to 8 projects, with Open and Viewer actions)
- Truth boundary pill indicators: Local Demo, Operator Controlled, Not Production, Not Official Compliance
- Executive action queue

## Data Semantics
- Uses backend project/readiness/documents data when available.
- Uses clearly labeled demo/synthetic coordinates when location data is missing.
- The map does not call Google Maps, Mapbox, ArcGIS, external tile servers, or geocoding APIs.
- Does not imply official compliance or production readiness.

## Filters
- Status: all, historical, in execution, on track, behind, blocked, demo
- Date range: 7d, 30d, 90d, ytd, all
- Persisted in `ema-ai-executive-overview-filters`.

## USA Project Map
- Implementation: real USA geographic map with state boundaries via local simplified GeoJSON and dependency-free local projection.
- No external map APIs, keys, tiles, or geocoding — all data is local to the repo.
- Synthetic fallback coordinates are deterministic from project id/name/code and remain stable across reloads.
- Marker positions use the same local projection as state boundaries, keeping placement stable without external map packages.
- Synthetic points are labeled `Demo Location` and must not be treated as official project addresses.
- Marker actions route to Project, Model / Viewer, Processing / Sync, and Debug / Logs while preserving selected project state.
- Future real coordinates should come from Project Setup or imported client/project metadata.
