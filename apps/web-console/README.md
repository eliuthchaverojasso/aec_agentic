# EMA AI Dashboard

React + Vite + TypeScript frontend for the EMA AI pilot dashboard.

## Local run

```powershell
cd "D:\Documents\Shokworks\Hyperghaps EMA\Framework\Pipeline\pipeline\frontend"
npm install
npm run dev
```

The app expects the FastAPI backend at `http://localhost:8010`.

Override with:

```powershell
$env:VITE_API_BASE_URL="http://localhost:8000"
npm run dev
```

## Pages

- Projects portfolio
- Project overview
- Trade readiness
- Owner requirements
- Issues and gaps

AI Query is intentionally not surfaced in this frontend MVP.
