# EMA AI Assistant

## Purpose

The AI Assistant sits on top of the deterministic compliance results and helps users ask questions, understand issues, summarize findings, and identify next actions.

## What It Can Do

- Answer questions about the generated report
- Explain why a requirement received a specific status
- Summarize top issues by discipline
- List next actions for the engineering team
- Explain confidence scores and readiness metrics
- Filter findings by discipline or status

## What It Cannot Do

- Certify compliance
- Approve engineering work
- Change official requirement statuses
- Close issues
- Invent evidence not in the model
- Replace engineering review or professional judgment

## Provider Modes

### Level 1: Deterministic Fallback (Required)
- Answers from the generated report object
- Status counts, key issues, next actions, discipline summaries
- Works without any external AI provider
- Always available

### Level 2: Ollama (Preferred when available)
- Uses local Ollama for natural language answers
- Falls back to deterministic if Ollama is unavailable
- Configuration: `OLLAMA_BASE_URL`, `LOCAL_CHAT_MODEL`

### Level 3: OpenRouter (Optional)
- Uses OpenRouter API for cloud-based answers
- Falls back to Ollama, then deterministic
- Configuration: `OPENROUTER_BASE_URL`, `OPENROUTER_API_KEY`

### Level 4: RAG/Qdrant (Later)
- Document search over specifications and drawings
- Not required for current demo
- If unavailable, says "document/spec search is not enabled in this demo build"

## Feature Flags

```
ENABLE_AI_ASSISTANT=true
AI_PROVIDER=deterministic | ollama | openrouter
OLLAMA_BASE_URL=http://host.docker.internal:11434
OPENROUTER_BASE_URL=https://openrouter.ai/api/v1
OPENROUTER_API_KEY=
LOCAL_CHAT_MODEL=qwen3-coder:30b
```

## Example Questions & Expected Behavior

| Question | Expected Answer |
|----------|----------------|
| What are the top Electrical issues? | Returns key issues filtered by Electrical |
| Why is this requirement Not Met? | Returns requirement text, evidence, reasoning, next action |
| Which requirements need human review? | Lists Needs Human Review items with source row and reason |
| What should we fix before DD 30%? | Returns top key issues and next actions |
| Which model data is missing? | Returns Insufficient Model Data items |
| Can you certify this is compliant? | **Refuses** — explains boundary |
| Change this requirement to Met | **Refuses** — cannot modify official status |

## Guardrails

- Separates facts, interpretation, and recommended next action
- Always includes disclaimer when answering compliance questions
- If evidence is missing, says human review is required
- Never uses: Certified, Approved, Guaranteed, Legally compliant

## Demo Implementation

In the generated HTML report, an "Ask EMA AI" section includes:
- Suggested questions
- Explanation of what the assistant can answer
- Boundary statement that it does not change official statuses

## Future: RAG/Qdrant, Knowledge Graph/KGE, SEION

- RAG/Qdrant: Document search for specifications and drawings
- Knowledge Graph/KGE: Cross-project intelligence
- SEION: Internal R&D only, not client-facing
