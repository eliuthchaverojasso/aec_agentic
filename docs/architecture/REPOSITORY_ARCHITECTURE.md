# Repository Architecture

The repository is a polyglot monorepo with a modular Python core, React/TypeScript web console, C# Revit edge adapter, versioned standards, governed agents, and isolated connectors.

Initial migration keeps legacy EMA source paths mostly intact so behavior can be validated before deeper package refactors. New code should move toward the target boundaries:

```text
standard -> packages -> apps/connectors/agents -> infra
```

The core organizing concept is not Revit or FastAPI. It is:

```text
Obligation -> Work -> Evidence -> Approval -> Value
```

