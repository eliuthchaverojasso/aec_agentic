# Login Page (Local Demo)

## Purpose
`/login` provides a local-demo welcome/session entry page for EMA AI. It is not production authentication.

## Key Behavior
- Local-only session record is stored under `ema-ai-demo-session`.
- Session includes demo role, project selection, and environment label.
- Entering local demo routes users into the dashboard shell.
- Resetting session from footer returns to `/login`.

## Explicit Constraints
- No production auth or authorization claims.
- No secrets, tokens, credentials, or backend auth material stored in local storage.
- Page keeps Local Demo / Not Production / Not Official Compliance language visible.
