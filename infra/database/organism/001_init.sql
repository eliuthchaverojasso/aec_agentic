CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS age;

CREATE SCHEMA IF NOT EXISTS organism;

CREATE TABLE IF NOT EXISTS organism.missions (
    mission_id text PRIMARY KEY,
    objective text NOT NULL,
    status text NOT NULL,
    current_phase text NOT NULL,
    active_plan_version integer NOT NULL DEFAULT 1,
    iteration integer NOT NULL DEFAULT 0,
    budget jsonb NOT NULL DEFAULT '{}'::jsonb,
    working_set jsonb NOT NULL DEFAULT '{}'::jsonb,
    pending_approvals jsonb NOT NULL DEFAULT '[]'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS organism.tasks (
    task_id text PRIMARY KEY,
    mission_id text NOT NULL REFERENCES organism.missions(mission_id),
    title text NOT NULL,
    status text NOT NULL DEFAULT 'created',
    dependencies jsonb NOT NULL DEFAULT '[]'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS organism.task_leases (
    lease_id text PRIMARY KEY,
    task_id text NOT NULL REFERENCES organism.tasks(task_id),
    worker_id text NOT NULL,
    acquired_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS organism.tool_calls (
    event_id text PRIMARY KEY,
    mission_id text NOT NULL REFERENCES organism.missions(mission_id),
    actor text NOT NULL,
    tool text NOT NULL,
    risk_level integer NOT NULL CHECK (risk_level BETWEEN 0 AND 4),
    input_summary text NOT NULL,
    output_summary text NOT NULL DEFAULT '',
    result text NOT NULL,
    correlation_id text,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS organism.artifacts (
    artifact_id text PRIMARY KEY,
    source text NOT NULL,
    mime_type text NOT NULL,
    content_hash text NOT NULL,
    project_id text,
    sensitivity text NOT NULL,
    parser_version text NOT NULL,
    provenance text NOT NULL DEFAULT '',
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS organism.memory_items (
    memory_id text PRIMARY KEY,
    mission_id text REFERENCES organism.missions(mission_id),
    memory_type text NOT NULL,
    content text NOT NULL,
    provenance jsonb NOT NULL DEFAULT '[]'::jsonb,
    embedding vector(1024),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS organism.evolution_proposals (
    proposal_id text PRIMARY KEY,
    target_path text NOT NULL,
    reason text NOT NULL,
    evidence jsonb NOT NULL DEFAULT '[]'::jsonb,
    status text NOT NULL DEFAULT 'candidate',
    created_at timestamptz NOT NULL DEFAULT now(),
    decided_at timestamptz
);

CREATE INDEX IF NOT EXISTS idx_organism_tasks_mission_id ON organism.tasks(mission_id);
CREATE INDEX IF NOT EXISTS idx_organism_tool_calls_mission_id ON organism.tool_calls(mission_id);
CREATE INDEX IF NOT EXISTS idx_organism_memory_items_mission_id ON organism.memory_items(mission_id);

