# EMA AI — Environment Variables Reference

**Last updated:** 2026-06-08

---

## AI Provider Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `AI_PROVIDER` | No | `ollama` | AI provider: `ollama`, `openrouter`, `opencode` |
| `AI_MODEL` | No | `qwen3.6:35b` | Main LLM model |
| `AI_SMALL_MODEL` | No | `granite4.1:30b` | Fallback / smaller model |
| `AI_EMBED_MODEL` | No | `bge-m3:latest` | Embeddings model (future RAG) |
| `OLLAMA_BASE_URL` | No | `http://localhost:11434/v1` | Ollama API endpoint |
| `OPENROUTER_API_KEY` | No | — | OpenRouter API key (from env only) |
| `OPENCODE_API_KEY` | No | — | OpenCode Zen API key (from env only) |

## Backend Variables

See `Pipeline/pipeline/.env.example` for full backend configuration.

| Variable | Required | Description |
|----------|----------|-------------|
| `DATABASE_URL` | Yes | PostgreSQL connection string |
| `LANDING_ROOT` | Yes | Windows path to landing zone |
| `SECRET_KEY` | Yes | JWT / session secret |
| `ALLOWED_ORIGINS` | No | CORS origins |

## Frontend Variables

See `Pipeline/pipeline/frontend/.env.example`.

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `VITE_API_BASE_URL` | No | `http://localhost:8010` | Backend API URL |

## No Secrets Rule

- Never commit `.env` files
- Never hardcode API keys in source code
- Never log secrets or connection strings
- Always use environment variables or secure config

## Local Development Defaults

```ini
# AI (local)
AI_PROVIDER=ollama
AI_MODEL=qwen3.6:35b
AI_SMALL_MODEL=granite4.1:30b
AI_EMBED_MODEL=bge-m3:latest
OLLAMA_BASE_URL=http://localhost:11434/v1

# Backend (Docker defaults)
DATABASE_URL=postgresql://ema:ema@localhost:5432/ema
LANDING_ROOT=C:\EMA-AI\landing

# Frontend
VITE_API_BASE_URL=http://localhost:8010
```

## Cloud (Optional) Configuration

```ini
# OpenRouter
AI_PROVIDER=openrouter
OPENROUTER_API_KEY=sk-or-v1-...

# OpenCode Zen
AI_PROVIDER=opencode
OPENCODE_API_KEY=oc-...
```
