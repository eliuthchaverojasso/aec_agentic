# EMA AI Appearance Memory

## Purpose

Appearance Memory defines the local visual design language for the EMA AI frontend. It exists so future frontend propagation work can reuse the same style language across pages, dashboards, tables, maps, reports, and UI states without drifting into ad hoc styling.

## Non-functional Boundary

> Appearance settings are stored locally in this browser and do not affect backend readiness, source data, or official compliance status.

Appearance is local.
Appearance is visual only.
Appearance does not modify backend state.
Appearance does not modify source data.
Appearance does not modify compliance status.
Appearance does not modify readiness status.
Appearance does not certify anything.
Appearance does not alter analytical results.

## Design Personality

The interface should feel:

- serious
- investigative
- institutional
- data-driven
- sober
- modern
- high-legibility
- appropriate for research, maps, reports, evidence, governance, compliance, and national-scale analysis

Avoid:

- playful styling
- excessive gradients
- chaotic visual noise
- decorative UI that reduces readability
- gamer aesthetics
- over-animated interfaces

## Global Design Tokens

The frontend should maintain a consistent token layer for:

- background
- foreground
- muted background
- muted foreground
- card background
- card foreground
- border
- input
- ring / focus
- primary
- primary foreground
- secondary
- secondary foreground
- accent
- accent foreground
- destructive
- warning
- success
- info
- map colors
- chart colors
- table row states
- badge states

Tokens should keep official / source-grounded surfaces distinct from processed, analytical, hypothesis, warning, and purely visual settings surfaces.

## Typography

Use a restrained and legible typography system:

- font family strategy: UI sans for interface text, monospace for code and numeric diagnostics
- heading scale: compact and institutional, not poster-like
- body text: clear and readable at dense dashboard sizes
- small text: reserved for metadata and labels
- metadata text: muted but still readable
- numeric/stat display: strong contrast and stable alignment
- table text: dense but high-legibility
- report text: sober and information-first
- line heights: tight enough for scanning, loose enough to avoid collisions
- letter spacing: minimal and consistent
- font weights: use weight to establish hierarchy, not decoration

## Layout System

Use a predictable layout system:

- AppShell for global navigation and route framing
- page width constrained for readability
- content max-width tuned for dashboards and reports
- grid spacing consistent across cards and sections
- section spacing generous enough to separate meaning
- card spacing compact but not cramped
- page header pattern with title, subtitle, and local state notes
- dashboard layout optimized for scanability
- table layout readable without horizontal fog
- map/report layout should preserve provenance and legend clarity
- responsive behavior should reflow, not hide meaning

## Component Style Rules

Reusable component patterns should remain consistent:

- buttons: clear hierarchy, visible focus, readable labels
- cards: distinct from page background, never ambiguous
- metric cards: strong numeric emphasis
- tables: solid, readable, and data-safe
- filters: compact and consistent
- search inputs: obvious affordance and visible focus
- selects: readable text and stable spacing
- tabs: active state must be obvious
- dialogs: readable, modal, and dismissible
- drawers: clear layering and no accidental data ambiguity
- badges: semantic, compact, and readable
- alerts: clearly tied to status or limitation
- banners: used for local-only notices and limitations
- empty states: honest about missing data
- loading states: explicit and non-fake
- error states: direct and legible
- tooltips: support dense UI without crowding
- navigation: active item must stay obvious
- breadcrumbs: concise and informative
- map panels: readable legend and restrained color
- chart panels: informative, not decorative
- report panels: dense but truth-grounded

## Data Hierarchy Rules

Visual separation matters because the product handles evidence, analysis, official data, and hypotheses.

- official source data: strongest truth signal, clearly labeled
- processed data: visibly downstream from source data
- analytical indicators: informative, not official by default
- hypotheses: clearly advisory or candidate-only
- warnings: visually distinct and impossible to miss
- limitations: explicit and readable
- compliance / readiness state: deterministic and clearly marked
- purely visual Appearance settings: local-only UI preferences with no backend effect

## Appearance Settings Schema

Supported local Appearance settings:

- theme: light | dark | system
- density: comfortable | compact
- accentColor: default | blue | green | amber | red | purple | neutral
- radius: sharp | soft | rounded
- motion: normal | reduced
- fontScale: small | normal | large
- dataDisplay: simple | detailed
- mapStyle: standard | contrast | muted
- dashboardStyle: standard | compact | executive

These settings are local-only preferences. They do not modify official data, official readiness, or compliance state.

## Local Storage Contract

- localStorage key: `appearance.settings.v1`
- legacy migration key: `ema-ai-appearance-settings`
- default values: use the shipped defaults from the frontend appearance module
- migration behavior: legacy settings should hydrate into the current key and then be treated as migrated
- reset behavior: clear local appearance settings and restore shipped defaults
- SSR-safe hydration: read from `window.localStorage` only in browser-safe code paths; fall back to defaults when storage is unavailable

## Instructions for Agent Style Propagation

- Read this file before modifying any page styling.
- Use existing tokens before creating new styles.
- Prefer shared components over page-specific styling.
- Keep all pages visually consistent.
- Do not modify backend, APIs, source data, compliance, readiness, ingestion, auth, or calculations.
- Do not make Appearance imply official status.
- Keep data and evidence UI sober and readable.
- Preserve the local-only Appearance notice.
- Apply the same AppShell, PageHeader, SectionCard, DataTableShell, MetricCard, StatusBadge, EmptyState, and NoticeBanner patterns where relevant.
- When unsure, add a TODO comment instead of inventing logic.
- Run lint, typecheck, and build after propagation.

## Implementation Notes for Future Propagation

- Use the active theme palette consistently across ambient fields, shell chrome, cards, tables, badges, charts, and report surfaces.
- Treat data-safe surfaces as solid or semi-solid when readability is at risk.
- Treat glass as a shell material for high-level surfaces, not a default treatment for dense rows.
- Preserve selected, warning, error, and evidence-state semantics in both light and dark modes.
