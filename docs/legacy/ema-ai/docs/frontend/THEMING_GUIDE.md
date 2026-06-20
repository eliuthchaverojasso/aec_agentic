# EMA AI Theming Guide

## Scope
EMA AI supports light, dark, and system color scheme with four visual themes:
- Minimal Corporate
- Liquid Glass Corporate
- Material Engineering
- High Contrast / Control Room
- Theme packs and variants are applied through CSS variables, not component-level hardcoded colors.

## Local Persistence
- Key: `appearance.settings.v1`
- Legacy migration: `ema-ai-appearance-settings`
- Stored client-side only
- No secrets, tokens, credentials, or API keys are stored

## Implementation
- Settings model: `Pipeline/pipeline/frontend/src/lib/appearance.ts`
- Hook: `Pipeline/pipeline/frontend/src/hooks/useAppearanceSettings.ts`
- UI: `Pipeline/pipeline/frontend/src/pages/AppearancePage.tsx`

## Safe Component Rules
- Use token-backed classes (`ema-card`, `ema-input`, `ema-btn-*`) for new UI.
- Prefer semantic badges that include text labels.
- Keep warning/advisory/evidence labels visible in all themes.

## System Mode
When `colorScheme=system`, EMA AI resolves from `prefers-color-scheme` and listens to runtime changes.

## Theme Variants
- Light
- Dark
- Bold
- Matte
- LiquidGlassLight
- LiquidGlassDark

## 2026-05-24 Normalization Pass
- Added effective theme mode resolution so Color Scheme and Theme Variant cannot produce mixed light/dark control states.
- Theme application now writes a complete control/token set (inputs, segmented controls, checkbox/slider, chart, glass) per pack/variant.
- Appearance page now shows effective mode and uses shared tokenized control styles for selects/inputs/segments.

## 2026-05-24 Liquid Glass Material Pass
- Liquid Glass is now treated as a material layer with base fill, optical border, highlight, depth shadow, inner shadow, saturation, and solid fallback tokens.
- Generic page cards no longer receive blur automatically; glass is scoped to `ema-glass-*` surfaces and disabled/reduced in high-contrast, no-glass, and dense data contexts.
- Data-heavy regions such as JSON, logs, warning blocks, and tables should continue to use solid or tinted surfaces.

## 2026-05-24 Transparency and Gradient Hardening
- Added alpha, glass opacity, background gradient, glass block gradient, specular, refraction, and motion token families.
- Background gradients are theme-aware: minimal/material stay restrained, Liquid Glass uses subtle radial depth, and High Contrast remains solid.
- Selected segmented controls and glass capsules use tokenized gradients instead of hardcoded color utilities.

## 2026-05-24 App-Wide Materialization Pass
- Liquid Glass is now applied through shared shell/card/KPI/nav/badge/table-wrapper classes instead of one-off page styling.
- Legacy `bg-white` card wrappers receive high-opacity Liquid Glass treatment in Liquid Glass variants unless marked `data-no-glass` or used on table/code/log elements.
- AppShell sidebar, topbar, footer, selected nav items, KPI cards, milestone cards, status badges, Executive Overview, Documents, and System Health now use the material system more consistently.
