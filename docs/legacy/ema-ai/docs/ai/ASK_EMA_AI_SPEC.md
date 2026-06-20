# Ask EMA AI — Specification

**Last updated:** 2026-06-08

---

## 1. Purpose

Ask EMA AI is an AI-powered assistant that helps users understand the Owner Requirements Report. It answers questions about requirement status, evidence, validation type, and next actions using the report context.

---

## 2. What It Can Answer

- "Why is requirement ORS-042 marked Not Met?"
- "What is the overall readiness score?"
- "What are the top issues in Electrical?"
- "Show me all Element IDs for Mechanical requirements with Critical urgency"
- "What evidence was found for requirement ORS-101?"
- "Explain the validation type for Lighting requirements"
- "What is the next best action for issue #3?"
- "Copy all Element IDs for Plumbing requirements"

---

## 3. What It Must NOT Do

- Certify or approve compliance status
- Change requirement statuses
- Invent evidence not present in the report context
- Replace engineering review
- Provide legal or contractual opinions
- Claim the report is official compliance documentation
- Suggest changes to the deterministic engine

---

## 4. Report Context Loading

When a user opens a report, the hidden machine-readable JSON (`#ema-ai-report-context`) is loaded as context:

```
1. Report rendered in browser
2. JS extracts <script id="ema-ai-report-context"> Content
3. JSON parsed → stored as context object
4. User asks question → question + context sent to LLM
5. LLM response includes citations and Element IDs
```

---

## 5. Hidden JSON Schema

The machine-readable context block is embedded in every report:

```html
<script type="application/json" id="ema-ai-report-context">
{...}
</script>
```

Full schema in [docs/reference/DATA_SCHEMA.md](../reference/DATA_SCHEMA.md) and [docs/reporting/OWNER_REQUIREMENTS_REPORT_SPEC.md](../reporting/OWNER_REQUIREMENTS_REPORT_SPEC.md).

Key fields for AI context:
- `summary` — aggregate counts and scores
- `key_issues` — ranked key issues with full detail
- `requirement_results` — per-requirement results
- `ai_lookup_hints` — discipline colors, anchors, status/urgency order

---

## 6. Context Builder

The context builder (`RequirementCheckWorkflowService.cs`) constructs the report context at generation time:

- Suggested questions per discipline
- Data hash for context integrity
- Full requirement results array
- Summary aggregates

---

## 7. Provider Architecture

```
User Question
    │
    ▼
┌─────────────────────────────┐
│  Provider Router            │
│  (try in order)             │
└─────────────────────────────┘
    │
    ├─ Level 1: Deterministic Fallback (always available)
    │   └─ Rule-based response from context
    │
    ├─ Level 2: Ollama Local (preferred, default)
    │   ├─ Model: qwen3.6:35b
    │   ├─ Fallback: granite4.1:30b
    │   └─ Base URL: http://localhost:11434/v1
    │
    ├─ Level 3: OpenRouter (optional)
    │   └─ API key from OPENROUTER_API_KEY env var
    │
    └─ Level 4: RAG/Qdrant (future, not implemented)
        └─ Semantic retrieval over report context
```

---

## 8. Local Model Configuration

```ini
AI_PROVIDER=ollama
AI_MODEL=qwen3.6:35b
AI_SMALL_MODEL=granite4.1:30b
AI_EMBED_MODEL=bge-m3:latest
OLLAMA_BASE_URL=http://localhost:11434/v1
```

### Available Models
| Model | Size | Role |
|-------|------|------|
| qwen3.6:35b | 23 GB | Default report assistant |
| granite4.1:30b | 17 GB | Fallback / faster responses |
| gemma4:31b | 19 GB | Alternative / long audits |
| bge-m3 | 1.2 GB | Future embeddings / RAG |

---

## 9. OpenRouter / OpenCode (Optional)

If configured via environment variables:
- `OPENROUTER_API_KEY` — enables cloud LLM
- `OPENCODE_API_KEY` — enables OpenCode Zen provider

No API keys are committed. Provider must fall back gracefully if unavailable.

---

## 10. Deterministic Fallback

When no LLM is available, Ask EMA AI provides rule-based responses:

- "I can only answer from the report context."
- "Based on the report, requirement ORS-042 is Not Met because..."
- "I cannot certify compliance — please consult a qualified professional."

The fallback never invents evidence or changes statuses.

---

## 11. Guardrails

System prompt (applied to all LLM calls):

```
You are Ask EMA AI, an assistant for the EMA AI Owner Requirements Report. 
You have access to the report context JSON which contains requirement results, 
evidence, Element IDs, and summary data.

Rules:
1. Answer ONLY from the provided report context. Do not invent evidence.
2. Cite specific requirement IDs and Element IDs when relevant.
3. Explain the report's findings, reasoning, and next actions.
4. If the context does not include the answer, say so clearly.
5. Do not certify, approve, or change any compliance status.
6. Do not claim the report is official compliance documentation.
7. If asked about a specific Element ID, provide all available context.
8. Keep answers concise and actionable.
```

---

## 12. Revit UI Concept

- "Ask About Report" button in the EMA AI Panel
- Opens modeless tool window with question input
- Takes the report context from the last generated report
- Response shown inline with citations
- Element IDs in responses are clickable (trigger Revit `SelectElementById`)

---

## 13. Citations / Anchors

All AI responses include:
- **Requirement ID** (e.g., "ORS-042") linked to report section
- **Element IDs** (e.g., "Element 12345") for Revit selection
- **Report section** (e.g., "Electrical — Equipment Verification")

---

## 14. Element ID Support

- Ask EMA AI can list, filter, and explain Element IDs
- Response includes "Copy Element IDs" for multi-ID results
- In Revit, Element IDs from AI response trigger Select Element by ID

---

## 15. Tests

Test coverage in `OwnerRequirementReportTests.cs`:
- `Generate_AskEmaAiQuestionsChangeByDiscipline`
- Suggested question generation per discipline

---

## 16. Manual Validation

- [ ] Open report with local Ollama running
- [ ] Ask a question about a requirement status
- [ ] Verify response cites specific requirement ID
- [ ] Verify response includes Element IDs
- [ ] Ask a question not in the report context
- [ ] Verify response says context is insufficient
- [ ] Ask to certify compliance
- [ ] Verify response declines
- [ ] Test with no LLM available (deterministic fallback)
- [ ] Verify fallback provides informational response
