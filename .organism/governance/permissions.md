# Permissions

Risk levels:

```text
Level 0: Read memory, source files, docs, schemas, and non-sensitive metadata.
Level 1: Create new files or generated proposals.
Level 2: Modify existing source-controlled files.
Level 3: Execute commands, install dependencies, run services, or mutate local databases.
Level 4: Delete, publish, deploy, push, send messages, or alter doctrine/governance/skills.
```

Default policy:

- Level 0 is allowed for normal missions.
- Level 1 is allowed when the mission objective implies creation.
- Level 2 requires mission scope.
- Level 3 requires sandbox awareness and logging.
- Level 4 requires explicit human approval.

