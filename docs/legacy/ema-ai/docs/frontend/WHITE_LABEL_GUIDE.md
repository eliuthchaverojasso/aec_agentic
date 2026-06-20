# White Label Guide

## Purpose

This guide explains how to rebrand the EMA AI frontend shell without changing deterministic backend truth.

## Brand Source

- `Pipeline/pipeline/frontend/src/brand/brandConfig.ts`

Edit this file to change:
- app/product/company names
- tagline
- nav labels
- support labels
- core color tokens
- feature flags

## Appearance Source

- `Pipeline/pipeline/frontend/src/lib/appearance.ts`
- `Pipeline/pipeline/frontend/src/index.css`

Local display preferences are stored in browser `localStorage` and applied through `data-theme`, `data-density`, and `data-accent`.

## Do Not Change

- Backend readiness semantics
- Official/evidence/advisory boundaries
- API contracts in `app/schemas.py`
- Deterministic readiness formula

## Safety Boundaries

- Official readiness remains backend deterministic output.
- Indexed PDFs/specs/drawings remain evidence candidates unless official evidence records exist.
- SEION suggestions remain advisory only.

## Rebrand Checklist

1. Update `brandConfig.ts`.
2. Verify top nav, sidebar labels, footer labels.
3. Verify theme controls in Appearance page.
4. Build frontend and run smoke validation.
