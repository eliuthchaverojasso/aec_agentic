# Appearance Settings

## Page
`Appearance` page controls local UI preferences and preview states.

## Configurable Settings
- Color scheme: light / dark / system
- Visual theme
- Accent profile
- Density
- Glass intensity
- Sidebar mode
- Chart style
- Motion preference
- Semantic label visibility toggles

## Export / Import
- Users can copy current appearance JSON.
- JSON import is validated and sanitized; malformed data falls back safely.

## Effective Mode Behavior
- Effective mode is derived from Color Scheme + Theme Variant and applied consistently to the whole app.
- Appearance now exposes a visible effective mode and a normalize action for incompatible combinations.
- Preview swatches are based on computed CSS tokens, not only static pack defaults.

## Safety Notes
- Appearance settings do not affect backend readiness.
- Appearance settings do not affect official evidence state.
- Appearance settings do not affect compliance outcomes.
- Corrupt or unsupported local appearance JSON is sanitized and falls back to safe visible defaults.
- The emergency recovery boundary includes a Reset Appearance Settings action that clears `appearance.settings.v1` and migrates legacy `ema-ai-appearance-settings` values when encountered.

## 2026-05-24 Premium Control Center Pass
- Appearance was rebuilt as a local UI control center with a Liquid Glass hero preview, theme configuration, glass material controls, layout/density, typography, motion, white-label, semantic-label, token inspector, and import/export sections.
- The page explicitly states that settings are stored locally in this browser and do not affect backend readiness, source data, or official compliance. The current storage contract uses `appearance.settings.v1` with legacy migration from `ema-ai-appearance-settings`.
- The Theme Preview now includes metric, badge, table, empty state, select/input/checkbox/slider, glass, warning, JSON/code, sidebar, and color token samples.

## 2026-05-24 Material QA Enhancements
- Appearance now includes Transparency, Gradient, Motion, and Material Blocks inspectors.
- The inspectors read live CSS variables, so they reflect the applied theme rather than static palette defaults.
- The preview includes explicit solid data, warning, table, and JSON/code surfaces to verify glass does not reduce operational readability.
