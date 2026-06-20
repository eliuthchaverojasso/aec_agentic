# EMA Frontend Design System

## Purpose
EMA AI uses a tokenized, theme-aware design system for engineering dashboard workflows. Appearance changes are local-browser preferences and never change backend readiness or source-of-truth behavior.

## Token Groups
Defined in `Pipeline/pipeline/frontend/src/index.css`:
- Background, surface, text, borders, focus ring
- Accent and semantic status colors
- Chart colors and tooltip colors
- Shadow and glass variables

## Theme Model
- Color scheme preference: `light | dark | system`
- Visual theme: `minimalCorporate | liquidGlass | materialEngineering | highContrast`
- Accent profile: `emaTeal | gold | slate | blue | graphite | customLocal`
- Density: `comfortable | compact | dense`
- Glass intensity: `none | subtle | medium | strong`

## Root Attributes
Appearance is applied through `document.documentElement.dataset.*`:
- `data-color-scheme`
- `data-visual-theme`
- `data-accent`
- `data-density`
- `data-glass`
- `data-sidebar`
- `data-chart`
- `data-motion`

## Semantic Label Rules
The following labels remain explicit text, never color-only:
- Official
- Evidence Candidate
- Advisory
- Prototype
- Fallback
- Local Demo

## Product Integrity Rules
- Never style prototype/advisory outputs like official deterministic results.
- Never hide evidence-candidate distinctions.
- Never imply production auth/governance from local UI polish.
- Liquid Glass is scoped to cards/panels/sidebar/topbar surfaces only.
- Do not apply blur overlays to `html`, `body`, `#root`, or main page containers.

## Liquid Glass Material Rules
- Use `ema-glass-card`, `ema-glass-panel`, `ema-glass-hero`, `ema-glass-toolbar`, `ema-glass-chip`, or `ema-glass-capsule` only for approved decorative or navigation surfaces.
- Do not apply Liquid Glass to dense data grids, raw response panels, log panels, JSON/code panels, warnings, or error blocks.
- High Contrast and `glassIntensity=none` must remain solid and readable.
- Controls must use tokenized classes (`ema-input`, `ema-select`, `ema-segmented`, `ema-checkbox`, `ema-slider`, `ema-btn-*`) rather than browser defaults.
- Shared app surfaces use the app-wide material classes: `ema-liquid-kpi`, `ema-liquid-metric`, `ema-liquid-section`, `ema-liquid-panel`, `ema-liquid-sidebar`, `ema-liquid-topbar`, `ema-liquid-nav-item`, and high-opacity legacy `bg-white` card fallback rules.

## Transparency and Motion Rules
- Data surfaces use solid or near-solid opacity; control surfaces stay readable at medium/strong glass settings.
- `ema-solid-data-surface`, `ema-solid-json-surface`, `ema-solid-table-surface`, and `ema-solid-warning-surface` explicitly disable backdrop filtering.
- Motion tokens keep hover lift under 2px and restrict persistent animation to heartbeat/data-flow contexts.
- Reduced motion disables pulses, shimmer, and transform-heavy effects.

## App-Wide Materialization
- AppShell sidebar/topbar/footer, KPI cards, metric cards, milestone cards, status badges, Executive Overview map/filter/action panels, Documents surfaces, and System Health cards now use the Liquid Glass material language.
- Tables, JSON, logs, warning/error surfaces, and dense payloads remain protected by solid data classes or `data-no-glass`.
