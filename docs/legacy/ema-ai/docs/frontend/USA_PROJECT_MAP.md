# USA Project Map

EMA AI uses a local-first USA Project Map on Executive Overview for portfolio visualization.

## Implementation
- **Real USA geographic map** with contiguous state boundaries — no Google Maps, Mapbox, ArcGIS, MapLibre, external tiles, geocoding, or API keys.
- **Dependency-free local projection** — focuses on contiguous USA only and avoids runtime blank screens from missing map packages.
- **Configurable padding**: 30px viewport padding keeps the USA outline centered with balanced margins, using a taller 900×580 viewBox for a more natural aspect ratio.
- **Simplified USA states GeoJSON** (~48 contiguous states + DC) rendered as SVG polygon paths.
- **9 source files** in `src/components/maps/`:
  - `UsaProjectMap.tsx` — Real geographic SVG viewport with zoom/pan/reset, state boundaries, status-aware markers, collision-resolved marker layout, demo toggle, accessible project list
  - `ProjectMapMarker.tsx` — SVG circles with status colors, keyboard support, synthetic demo ring indicator
  - `ProjectMapPopover.tsx` — Sidebar with project details, location source, and epoch-aware action buttons
  - `ProjectMapLegend.tsx` — Color-coded status legend including Demo Location
  - `projectMapUtils.ts` — Deterministic hash coordinates, local projection-based marker placement, marker sizing, collision resolution, labels, action registry
  - `demoProjectLocations.ts` — 16 well-distributed deterministic demo location entries (WA, CA×2, AZ, CO, NM, TX×3, MN, IL, MO, TN, GA, FL, NC)
  - `usaMapRenderer.tsx` — Dependency-free SVG state polygon renderer with shared local projection, `aria-hidden` on geometry
  - `data/usaStateOutlines.ts` — Simplified GeoJSON boundaries for 48 contiguous states + DC
- **Dependencies**: no map runtime dependency.
- Build-verified: ✅ `tsc -b && vite build` passes clean (no TS errors, no CSS warnings)

## Map Data
- Local simplified state boundary polygons stored in `data/usaStateOutlines.ts` (TypeScript module).
- ~8–20 coordinate points per state for recognizable shapes.
- All 48 contiguous states + DC included.
- **Alaska and Hawaii excluded** — viewport focuses on contiguous USA only, no inset artifacts.
- Data source: simplified public-domain USA state geometry for demo map rendering.
- No map data is fetched at runtime — entirely local to the repo.

## Projection & Viewport
- Local lon/lat projection bounded to the contiguous USA viewport.
- 30px internal padding.
- SVG viewBox: `0 0 900 580` (taller to match natural USA aspect ratio)
- Zoom range: 0.6–2.8× with clamped pan (±300px)
- State paths and markers use the same projection instance for perfect alignment
- Markers that would overlap (<28px apart) receive deterministic jitter based on project ID hash, fanning out at a stable angle

## Coordinate Policy
- **All coordinates without persisted project metadata are deterministic synthetic demo locations** derived from a stable hash of the project ID/name (`getStableHash()` → `DEMO_PROJECT_LOCATIONS[hash % 16]`).
- Synthetic markers are **always labeled `Demo Location`** and do not represent official project addresses.
- Marker positions are computed with the same local projection as state polygons for stable alignment within the USA outline.
- Multiple truth disclosures: map subtitle, pill tags ("Local Demo", "Demo Coordinates", "No External Map API"), data-surface disclaimer, per-marker labels, popover source field.

## Interaction
- Markers are keyboard focusable (`tabIndex={0}`, `onKeyDown` for Enter/Space, `aria-label` with status and source).
- Popovers show project name, client, milestone, status, readiness score, open issues, documents indexed, last sync, location source, and route actions (Open Project, Model/Viewer, Processing/Sync, Debug/Logs).
- Route actions call `onSelectProject()` before navigation to prevent broken links.
- Zoom (0.6–2.8×), pan (clamped ±300px), reset, demo marker toggle, and N/S/E/W pan controls are local UI state only.
- State polygons have hover effect, subtle drop-shadow, and smooth transition.
- Empty state shows helpful message when no project coordinates are available.
- Keyboard-accessible project list fallback below the map viewport.

## Styling
- State fill: `color-mix()` with `--ema-accent-soft` 20% + `--ema-surface-2` 80% for subtle theme-adaptive coloring.
- State stroke: `color-mix()` with `--ema-accent` 52% + `--ema-border` for stronger visible boundaries.
- State drop-shadow: subtle 1px shadow via `filter`.
- Map background: solid `--ema-bg-elevated` with subtle radial accent/info glow gradients.
- `.ema-map-bg` rect fills behind state layer for clean appearance.
- Marker rings have `drop-shadow` for elevation.
- All tokens adapt to light/dark/liquid-glass/high-contrast theme modes.

## Accessibility
- `aria-label` on SVG, markers, and controls.
- State geometry paths have `aria-hidden` (decorative).
- `focus-visible` ring on markers, controls, and project rows.
- `aria-live="polite"` on popover region.
- Marker `aria-label` includes project name, status, and Demo Location.
- State boundaries visible with sufficient stroke contrast in all themes.
- Reduced-motion respected via global `[data-motion="reduced"]` and `@media (prefers-reduced-motion: reduce)` CSS rules.
- High-contrast compatible via theme-safe CSS variables.

## Deferred
- Project Setup location fields (city/state/country/lat/lng/source).
- Backend project location DTO for persisted real coordinates.
- State abbreviation labels on map.
- Alaska and Hawaii state geometry data.
- Optional geocoding integration for real addresses.
- Optional MapLibre, local tile server, or WebGL globe implementation.
