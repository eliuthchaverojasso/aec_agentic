# ORGANISM Architecture

ORGANISM is the local, persistent, evolvable cognitive operating layer for this repository.

Its continuity does not depend on keeping a process alive forever. It depends on durable state:

- mission records and task leases in PostgreSQL
- checkpoints from the orchestration graph
- event log for actions and transitions
- source and result artifacts
- approved doctrine, governance, skills, and methodologies in Git
- rebuildable semantic and graph indexes through pgvector and Apache AGE

## Planes

Control plane:

- control center
- governance
- LangGraph orchestration
- scheduler and watchdog

Cognitive plane:

- Planner
- Researcher
- Executor / Coder
- Critic / Evaluator
- Memory Curator

Inference plane:

- Model Gateway
- Qwen for production and coding
- Gemma for critique
- Granite for extraction and consolidation
- bge-m3 for embeddings
- Ollama as the local runtime

Action plane:

- file system
- isolated shell
- Git
- authorized web navigation
- tests and validators

Persistence plane:

- PostgreSQL for operational state
- pgvector for associative memory
- Apache AGE for world-model relationships
- Git for identity and approved methods
- artifacts for observable results
- event log for immutable history

## GPT Web

GPT Web is not an automatic API dependency. ORGANISM writes supervision packets to `.organism/outbox/gpt/`; the human operator pastes them into GPT Web and stores responses in `.organism/inbox/gpt/`.

Those responses are external advice, not privileged commands.

