# Frontend Developer Guide

## Key Structure
- Routing and shell: `App.tsx`, `components/Layout.tsx`
- API client: `api/client.ts`
- Types: `types.ts`
- Workflows: `pages/ProjectSetupPage.tsx`, `pages/ProcessingPage.tsx`, `pages/DebugLogsPage.tsx`

## State Rules
- Selected project persisted at `ema-ai-selected-project-id`.
- Project-scoped operations must use selected project ID.
- Fallback/demo values must be clearly labeled.
- Appearance settings persisted at `appearance.settings.v1` with legacy migration from `ema-ai-appearance-settings`.
- Appearance settings are local-only and must not alter backend semantics.

## Error Handling
- Never hide endpoint failures.
- Render unavailable/partial states explicitly.

## Theming
- Theme model and migration: `src/lib/appearance.ts`
- Appearance hook: `src/hooks/useAppearanceSettings.ts`
- Theme UI: `src/pages/AppearancePage.tsx`
