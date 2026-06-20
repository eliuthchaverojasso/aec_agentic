# Component Style Guide

## Core Tokens
Use design tokens through CSS variables in `src/index.css`.

## Preferred Utility Classes
- `ema-card`
- `ema-card-muted`
- `ema-input`
- `ema-btn-primary`
- `ema-btn-secondary`
- `ema-pill`, `ema-pill-success`, `ema-pill-warning`, `ema-pill-danger`

## States
Every data-heavy surface should expose:
- Loading
- Empty
- Error
- Partial / fallback

## Tables and Forms
- Keep headers readable in dark mode.
- Keep cell badges legible in dense mode.
- Keep focus rings visible on keyboard navigation.

## Liquid Glass Components
- Use `ema-glass-card`, `ema-glass-panel`, `ema-glass-hero`, `ema-glass-toolbar`, `ema-glass-chip`, and `ema-glass-capsule` for premium shell and preview surfaces.
- Keep raw logs, JSON, warnings, errors, and tables on `ema-card` or `bg-surface-solid` surfaces with `data-no-glass` where appropriate.
- New controls should use `ema-input`, `ema-select`, `ema-textarea`, `ema-segmented`, `ema-checkbox`, `ema-slider`, and `ema-btn-*` so theme packs do not leak browser-default colors.

## App-Wide Material Classes
- Use `ema-liquid-kpi` / `ema-liquid-metric` for numeric cards.
- Use `ema-liquid-section` / `ema-liquid-panel` for page and secondary panels.
- Use `ema-solid-table-surface`, `ema-solid-json-surface`, `ema-solid-log-surface`, `ema-solid-warning-surface`, and `ema-solid-error-surface` for dense or sensitive operational content.
- Use `ema-anim-hover-lift` sparingly on cards; persistent animation is reserved for heartbeat/data-flow contexts.
