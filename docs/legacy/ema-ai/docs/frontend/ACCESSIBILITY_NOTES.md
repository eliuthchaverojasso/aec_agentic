# Accessibility Notes

## Current Improvements
- Visible focus rings across controls
- Theme-aware contrast tokens for light/dark/high-contrast
- Reduced-motion support
- Text-visible semantic labels (not color-only)
- Readable table/forms in dark mode

## USA Project Map Accessibility
- SVG viewport has `role="img"` and `aria-label="Local demo USA project map viewport"`
- All marker groups have `role="button"`, `tabIndex={0}`, `aria-label` with project name, status, and location source
- Keyboard Enter/Space triggers `onSelect` on markers
- Focus-visible ring on markers (`ema-map-marker:focus-visible .ema-map-marker-ring`)
- Popover region has `aria-live="polite"` for dynamic content announcements
- Popover close button has `aria-label="Close project map details"`
- Zoom/pan/reset/demo-toggle buttons have `aria-label` descriptions
- Pan controls have `aria-label="Map pan controls"`
- Accessible project list fallback renders as keyboard-focusable `<button>` elements with project name and status/location
- Reduced-motion respected via global `[data-motion="reduced"]` and `@media (prefers-reduced-motion: reduce)` CSS rules

## USA State Boundaries (Real Geography)
- State polygons rendered as SVG `<path>` elements with `aria-hidden` (decorative, not interactive).
- State boundary stroke contrast maintained across all themes via CSS variable `--ema-border` and `--ema-accent` at 52% opacity.
- State fill uses `color-mix()` for theme-safe coloring that adapts to light/dark/contrast modes.
- Real geographic outlines help spatially-situated users understand project distribution.
- Map uses a local dependency-free projection — contiguous USA only for focused cartography.

## Ongoing Checks
- Keyboard-only navigation for major forms and controls
- Icon buttons should include accessible labels
- Maintain contrast in chart and badge palettes
- Emergency recovery screen must remain solid, high contrast, keyboard accessible, and independent of Liquid Glass settings.

## 2026-05-24 Color QA
- Placeholder, label, and control text tokens were strengthened to avoid faint/low-contrast states.
- Select/input/textarea/segment/checkbox/slider controls now share theme-aware tokens.
- High-contrast and reduced-motion dataset modes remain honored by the token system.

## 2026-05-24 Liquid Glass QA
- Generic cards no longer receive automatic backdrop blur in Liquid Glass mode, reducing fog/disabled-overlay risk.
- The Appearance preview includes solid warning and JSON/code panels to verify that dense information remains readable.
- Glass material pseudo-elements use `pointer-events: none`, so optical highlights cannot block controls.

## MVP Boundaries
This pass improves practical accessibility but is not a full WCAG certification pass.
