-- EMA AI MVP -- Serving layer schema
-- Aligned with Dashboard Guidelines section 11 ("Data Model")

BEGIN;

-- =========================================================================
-- Core hierarchy
-- =========================================================================

CREATE TABLE organization (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Cliente / owner (ISD, distrito, cliente final). A project belongs to a client,
-- and a client owns a catalog of requirements (see below).
CREATE TABLE client (
    id SERIAL PRIMARY KEY,
    organization_id INT NOT NULL REFERENCES organization(id) ON DELETE CASCADE,
    code VARCHAR(100) NOT NULL,                 -- e.g. DENTON_ISD
    display_name VARCHAR(255) NOT NULL,         -- e.g. Denton ISD
    sharepoint_path TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (organization_id, code)
);
CREATE INDEX idx_client_org ON client(organization_id);

CREATE TABLE project (
    id SERIAL PRIMARY KEY,
    organization_id INT NOT NULL REFERENCES organization(id) ON DELETE CASCADE,
    client_id INT REFERENCES client(id) ON DELETE SET NULL,
    project_title VARCHAR(500) NOT NULL,
    project_code VARCHAR(100),
    project_name VARCHAR(255),
    job_number VARCHAR(100),
    revit_version VARCHAR(20),
    client_name VARCHAR(255),                   -- free-text client name from ProjectTitle
    location VARCHAR(255),
    jurisdiction VARCHAR(255),
    phase VARCHAR(100),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (organization_id, project_title)
);
CREATE INDEX idx_project_code ON project(project_code);
CREATE INDEX idx_project_client ON project(client_id);

CREATE TABLE model (
    id SERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    revit_file_name VARCHAR(500),
    revit_version VARCHAR(20),
    discipline VARCHAR(100),
    model_type VARCHAR(100),
    last_sync_at TIMESTAMPTZ,
    exported_by VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (project_id, revit_file_name, discipline)
);
CREATE INDEX idx_model_project ON model(project_id);

-- =========================================================================
-- Exports and sync
-- =========================================================================

CREATE TABLE export (
    id SERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    model_id INT NOT NULL REFERENCES model(id) ON DELETE CASCADE,
    export_type VARCHAR(50) NOT NULL,           -- all, electrical, mechanical, lighting, plumbing
    file_name VARCHAR(500),
    file_size_bytes BIGINT,
    element_count INT,
    status VARCHAR(50) NOT NULL DEFAULT 'pending',
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    duration_seconds NUMERIC(10, 2),
    error_message TEXT,
    CONSTRAINT chk_export_status CHECK (status IN ('pending','in_progress','completed','failed','warning'))
);
CREATE INDEX idx_export_model_completed ON export(model_id, completed_at DESC);
CREATE INDEX idx_export_status ON export(status);

CREATE TABLE sync_log (
    id SERIAL PRIMARY KEY,
    export_id INT NOT NULL REFERENCES export(id) ON DELETE CASCADE,
    step VARCHAR(100) NOT NULL,                 -- received, validation, parsing, element_extraction, param_normalization, qa_qc_checks, dashboard_update, query_index_update
    status VARCHAR(50) NOT NULL,                -- pending, in_progress, completed, failed, warning
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    duration_seconds NUMERIC(10, 2),
    message TEXT,
    CONSTRAINT chk_sync_status CHECK (status IN ('pending','in_progress','completed','failed','warning'))
);
CREATE INDEX idx_sync_log_export ON sync_log(export_id, started_at);

-- =========================================================================
-- Elements and parameters
-- =========================================================================

CREATE TABLE element (
    id BIGSERIAL PRIMARY KEY,
    unique_id VARCHAR(100) NOT NULL,            -- Revit UniqueId (stable GUID)
    element_id BIGINT NOT NULL,                 -- Revit ElementId (per-model integer)
    model_id INT NOT NULL REFERENCES model(id) ON DELETE CASCADE,
    export_id INT NOT NULL REFERENCES export(id) ON DELETE CASCADE,
    category VARCHAR(100),
    name VARCHAR(500),
    family VARCHAR(500),
    type VARCHAR(500),
    level VARCHAR(100),
    instance_parameters JSONB,
    type_parameters JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (unique_id, export_id)
);
CREATE INDEX idx_element_export ON element(export_id);
CREATE INDEX idx_element_model ON element(model_id);
CREATE INDEX idx_element_category ON element(category);
CREATE INDEX idx_element_level ON element(level);
CREATE INDEX idx_element_family ON element(family);
CREATE INDEX idx_element_instance_params ON element USING gin(instance_parameters);
CREATE INDEX idx_element_type_params ON element USING gin(type_parameters);

-- =========================================================================
-- Rules and issues
-- =========================================================================

CREATE TABLE rule (
    id SERIAL PRIMARY KEY,
    rule_code VARCHAR(20) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    discipline VARCHAR(100),
    severity VARCHAR(20) NOT NULL,
    check_type VARCHAR(50),                     -- parameter_missing, connection_missing, value_out_of_range, etc.
    active BOOLEAN NOT NULL DEFAULT TRUE,
    version VARCHAR(20) NOT NULL DEFAULT '1.0',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_rule_severity CHECK (severity IN ('low','medium','high','critical'))
);

CREATE TABLE issue (
    id BIGSERIAL PRIMARY KEY,
    organization_id INT NOT NULL REFERENCES organization(id) ON DELETE CASCADE,
    project_id INT NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    model_id INT NOT NULL REFERENCES model(id) ON DELETE CASCADE,
    export_id INT NOT NULL REFERENCES export(id) ON DELETE CASCADE,
    element_unique_id VARCHAR(100),
    element_db_id BIGINT REFERENCES element(id) ON DELETE SET NULL,
    rule_id INT REFERENCES rule(id),
    rule_code VARCHAR(20),
    issue_type VARCHAR(50),
    severity VARCHAR(20) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'open',
    source VARCHAR(50) NOT NULL DEFAULT 'automated',
    message TEXT,
    traceability JSONB,                         -- {rule_version, observed_values, check_timestamp, ...}
    assigned_to_user_id INT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    due_date TIMESTAMPTZ,
    reviewed_by_user_id INT,
    reviewed_at TIMESTAMPTZ,
    resolution_notes TEXT,
    CONSTRAINT chk_issue_severity CHECK (severity IN ('low','medium','high','critical')),
    CONSTRAINT chk_issue_status CHECK (status IN ('open','in_review','reviewed','closed','reopened')),
    CONSTRAINT chk_issue_source CHECK (source IN ('automated','manual'))
);
CREATE INDEX idx_issue_project ON issue(project_id);
CREATE INDEX idx_issue_model ON issue(model_id);
CREATE INDEX idx_issue_export ON issue(export_id);
CREATE INDEX idx_issue_rule ON issue(rule_code);
CREATE INDEX idx_issue_status_severity ON issue(status, severity);
CREATE INDEX idx_issue_created ON issue(created_at DESC);

-- =========================================================================
-- Owner / client requirements (ingested from ISD/district xlsx exports)
-- =========================================================================

CREATE TABLE requirement_source_file (
    id SERIAL PRIMARY KEY,
    client_id INT NOT NULL REFERENCES client(id) ON DELETE CASCADE,
    original_filename VARCHAR(500) NOT NULL,
    file_hash CHAR(64) NOT NULL,                -- sha256 of raw bytes
    row_count_raw INT,                          -- rows read from sheet (excluding header)
    row_count_loaded INT,                       -- rows that produced upserts
    row_count_skipped INT,                      -- sentinel/placeholder rows filtered
    export_date DATE,                           -- parsed from filename when possible
    ingested_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (client_id, file_hash)
);
CREATE INDEX idx_req_source_client ON requirement_source_file(client_id);

CREATE TABLE requirement (
    id BIGSERIAL PRIMARY KEY,
    client_id INT NOT NULL REFERENCES client(id) ON DELETE CASCADE,
    source_file_id INT REFERENCES requirement_source_file(id) ON DELETE SET NULL,
    discipline VARCHAR(50) NOT NULL,            -- ELECTRICAL / LIGHTING / PLUMBING / MECHANICAL / TECHNOLOGY / OTHER
    category VARCHAR(255),                      -- SharePoint CATEGORY LIST value
    requirement_text TEXT NOT NULL,
    content_hash CHAR(64) NOT NULL,             -- sha256(discipline + '|' + normalized_text)
    owner_status VARCHAR(50),                   -- NOT_STARTED / DONE (owner-side tracking)
    resource VARCHAR(500),                      -- provenance quoted by the owner
    links TEXT,
    modified_by VARCHAR(255),
    date_updated TIMESTAMPTZ,
    sharepoint_path TEXT,
    is_actionable BOOLEAN NOT NULL DEFAULT TRUE, -- false for placeholder/sentinel rows
    is_active BOOLEAN NOT NULL DEFAULT TRUE,     -- soft delete when absent in latest re-ingest
    first_seen_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_seen_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (client_id, content_hash)
);
CREATE INDEX idx_req_client_discipline ON requirement(client_id, discipline);
CREATE INDEX idx_req_active ON requirement(is_active);
CREATE INDEX idx_req_category ON requirement(category);

-- Per-project compliance state (populated manually for MVP; hooks for automation later).
CREATE TABLE requirement_compliance (
    id BIGSERIAL PRIMARY KEY,
    requirement_id BIGINT NOT NULL REFERENCES requirement(id) ON DELETE CASCADE,
    project_id INT NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    model_id INT REFERENCES model(id) ON DELETE CASCADE,
    status VARCHAR(30) NOT NULL DEFAULT 'not_evaluated',
    evidence JSONB,                             -- optional references to elements/issues
    evaluated_by VARCHAR(50),                   -- 'manual' | 'rule:<code>' | 'llm' | NULL
    evaluated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    notes TEXT,
    UNIQUE (requirement_id, project_id, model_id),
    CONSTRAINT chk_compliance_status CHECK (
        status IN ('compliant','non_compliant','not_evaluated','not_applicable','needs_review')
    )
);
CREATE INDEX idx_compliance_project_status ON requirement_compliance(project_id, status);
CREATE INDEX idx_compliance_requirement ON requirement_compliance(requirement_id);

-- Minimal evidence layer for the pilot. Sheet/spec evidence remains future
-- scope; the MVP records missing/manual/model evidence without pretending PDF
-- parsing exists.
CREATE TABLE requirement_evidence (
    id BIGSERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    requirement_id BIGINT NOT NULL REFERENCES requirement(id) ON DELETE CASCADE,
    evidence_type VARCHAR(30) NOT NULL,
    evidence_status VARCHAR(30) NOT NULL,
    source_ref TEXT,
    element_unique_id VARCHAR(100),
    sheet_number VARCHAR(100),
    spec_section VARCHAR(100),
    confidence NUMERIC(5, 2),
    metadata_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_requirement_evidence_type CHECK (
        evidence_type IN ('model','sheet','spec','manual','hybrid')
    ),
    CONSTRAINT chk_requirement_evidence_status CHECK (
        evidence_status IN ('covered','missing','needs_review','blocked','not_applicable')
    )
);
CREATE INDEX idx_req_evidence_project ON requirement_evidence(project_id, evidence_status);
CREATE INDEX idx_req_evidence_requirement ON requirement_evidence(requirement_id);
CREATE UNIQUE INDEX uq_req_evidence_project_requirement_source
    ON requirement_evidence(project_id, requirement_id, COALESCE(source_ref, ''));

-- Persisted readiness snapshots/actions for traceability and trend views.
CREATE TABLE readiness_snapshot (
    id BIGSERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    export_id INT REFERENCES export(id) ON DELETE SET NULL,
    overall_score NUMERIC(6, 2) NOT NULL,
    label VARCHAR(50) NOT NULL,
    requirement_coverage_score NUMERIC(6, 2) NOT NULL,
    qaqc_health_score NUMERIC(6, 2) NOT NULL,
    sync_freshness_score NUMERIC(6, 2) NOT NULL,
    gap_summary JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_readiness_snapshot_project_created
    ON readiness_snapshot(project_id, created_at DESC);

CREATE TABLE trade_readiness_snapshot (
    id BIGSERIAL PRIMARY KEY,
    snapshot_id BIGINT NOT NULL REFERENCES readiness_snapshot(id) ON DELETE CASCADE,
    discipline VARCHAR(100) NOT NULL,
    score NUMERIC(6, 2) NOT NULL,
    requirements_total INT NOT NULL DEFAULT 0,
    requirements_covered INT NOT NULL DEFAULT 0,
    missing_requirements INT NOT NULL DEFAULT 0,
    needs_review INT NOT NULL DEFAULT 0,
    open_issues INT NOT NULL DEFAULT 0,
    critical_gaps INT NOT NULL DEFAULT 0
);
CREATE INDEX idx_trade_readiness_snapshot ON trade_readiness_snapshot(snapshot_id);

CREATE TABLE readiness_action (
    id BIGSERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES project(id) ON DELETE CASCADE,
    requirement_id BIGINT REFERENCES requirement(id) ON DELETE SET NULL,
    issue_id BIGINT REFERENCES issue(id) ON DELETE SET NULL,
    rule_code VARCHAR(30),
    action_type VARCHAR(100) NOT NULL,
    title VARCHAR(255) NOT NULL,
    description TEXT,
    status VARCHAR(30) NOT NULL DEFAULT 'open',
    priority VARCHAR(30) NOT NULL DEFAULT 'medium',
    owner VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_readiness_action_status CHECK (
        status IN ('open','in_review','done','dismissed')
    ),
    CONSTRAINT chk_readiness_action_priority CHECK (
        priority IN ('low','medium','high','critical')
    )
);
CREATE INDEX idx_readiness_action_project_status
    ON readiness_action(project_id, status, priority);

CREATE TABLE rule_execution_log (
    id BIGSERIAL PRIMARY KEY,
    project_id INT REFERENCES project(id) ON DELETE CASCADE,
    export_id INT REFERENCES export(id) ON DELETE SET NULL,
    rule_code VARCHAR(30) NOT NULL,
    status VARCHAR(30) NOT NULL,
    findings_count INT NOT NULL DEFAULT 0,
    duration_ms INT,
    error_message TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- =========================================================================
-- User table extension for auth-ready MVP
-- =========================================================================

DO $$
BEGIN
    IF to_regclass('public.app_user') IS NULL THEN
        CREATE TABLE public.app_user (
            id SERIAL PRIMARY KEY,
            name VARCHAR(255) NOT NULL,
            email VARCHAR(255),
            role VARCHAR(100) NOT NULL DEFAULT 'engineer',
            password_hash TEXT,
            auth_provider VARCHAR(30) NOT NULL DEFAULT 'local',
            is_active BOOLEAN NOT NULL DEFAULT TRUE,
            is_locked BOOLEAN NOT NULL DEFAULT FALSE,
            failed_login_attempts INTEGER NOT NULL DEFAULT 0,
            last_login_at TIMESTAMPTZ,
            password_changed_at TIMESTAMPTZ,
            must_change_password BOOLEAN NOT NULL DEFAULT FALSE,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
    ELSE
        ALTER TABLE public.app_user
            ADD COLUMN IF NOT EXISTS password_hash TEXT,
            ADD COLUMN IF NOT EXISTS auth_provider VARCHAR(30) NOT NULL DEFAULT 'local',
            ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT TRUE,
            ADD COLUMN IF NOT EXISTS is_locked BOOLEAN NOT NULL DEFAULT FALSE,
            ADD COLUMN IF NOT EXISTS failed_login_attempts INTEGER NOT NULL DEFAULT 0,
            ADD COLUMN IF NOT EXISTS last_login_at TIMESTAMPTZ,
            ADD COLUMN IF NOT EXISTS password_changed_at TIMESTAMPTZ,
            ADD COLUMN IF NOT EXISTS must_change_password BOOLEAN NOT NULL DEFAULT FALSE,
            ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW();

        UPDATE public.app_user
        SET role = COALESCE(role, 'engineer')
        WHERE role IS NULL;

        ALTER TABLE public.app_user
            ALTER COLUMN role SET DEFAULT 'engineer',
            ALTER COLUMN role SET NOT NULL;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_app_user_email_lower
    ON public.app_user ((LOWER(email)))
    WHERE email IS NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'chk_app_user_auth_provider'
          AND conrelid = 'public.app_user'::regclass
    ) THEN
        ALTER TABLE public.app_user
            ADD CONSTRAINT chk_app_user_auth_provider
            CHECK (auth_provider IN ('local', 'azure_ad', 'sso'));
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'chk_app_user_failed_attempts_non_negative'
          AND conrelid = 'public.app_user'::regclass
    ) THEN
        ALTER TABLE public.app_user
            ADD CONSTRAINT chk_app_user_failed_attempts_non_negative
            CHECK (failed_login_attempts >= 0);
    END IF;
END $$;

CREATE OR REPLACE FUNCTION public.set_app_user_updated_at()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_trigger
        WHERE tgname = 'trg_app_user_set_updated_at'
          AND tgrelid = 'public.app_user'::regclass
    ) THEN
        CREATE TRIGGER trg_app_user_set_updated_at
        BEFORE UPDATE ON public.app_user
        FOR EACH ROW
        EXECUTE FUNCTION public.set_app_user_updated_at();
    END IF;
END $$;

-- =========================================================================
-- Seed data: default organization and rules
-- =========================================================================

INSERT INTO organization (name) VALUES ('EMA Engineering');

-- Seed the 3 known clients observed in the Owner Requirements SharePoint export.
-- code is the canonical key, display_name is shown in UI.
INSERT INTO client (organization_id, code, display_name, sharepoint_path) VALUES
    (1, 'DENTON_ISD',     'Denton ISD',     'sites/OwnerRequirements/MASTER DISTRICT OWNER REQ LIST/Lists/DENTON ISD'),
    (1, 'NORTHWEST_ISD',  'Northwest ISD',  'sites/OwnerRequirements/MASTER DISTRICT OWNER REQ LIST/Lists/NORTHWEST ISD'),
    (1, 'ROCKWALL_ISD',   'Rockwall ISD',   'sites/OwnerRequirements/MASTER DISTRICT OWNER REQ LIST/Lists/ROCKWALL ISD');

INSERT INTO rule (rule_code, name, description, discipline, severity, check_type, version) VALUES
    ('R001',
     'Element Without Level',
     'Element has no Level assigned. Required for drawing generation, space analysis, and coordination.',
     'all',
     'low',
     'field_missing',
     '1.0'),
    ('R002',
     'Unconnected Fixture',
     'Electrical or lighting fixture has no Panel assigned in its instance parameters. Indicates missing electrical connection.',
     'electrical',
     'high',
     'connection_missing',
     '1.0'),
    ('R003',
     'Fixture Missing Circuit',
     'Fixture is assigned to a Panel but has no Circuit Number. Electrical design is incomplete.',
     'electrical',
     'medium',
     'parameter_missing',
     '1.0'),
    ('R004',
     'Panel Without Source',
     'Electrical panel has no Supply From value. Panel may be orphaned or main distribution (requires manual confirmation).',
     'electrical',
     'high',
     'connection_missing',
     '1.0');

COMMIT;
