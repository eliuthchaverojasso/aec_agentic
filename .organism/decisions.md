# Decisions

- Use a monorepo.
- Keep the first core as a modular monolith.
- Preserve EMA AI as the first requirement-to-evidence vertical.
- Treat agents as command proposers, not authority holders.
- Keep external system payloads immutable.
- Treat ORGANISM as continuously recoverable, not unbounded.
- Route model calls by capability, not by hard-coded model name.
- Use GPT Web only through manual supervision packets in `.organism/outbox/gpt` and `.organism/inbox/gpt`.
- Do not let memory promote doctrine, governance, or skills without human approval.
