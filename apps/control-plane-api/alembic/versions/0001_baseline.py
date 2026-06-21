"""baseline: adopt the full current EMA schema (27 tables + seed)

Revision ID: 0001_baseline
Revises:
Create Date: 2026-06-21

This baseline reproduces the *current live schema* exactly, so a fresh database
brought up with ``alembic upgrade head`` is byte-for-byte equivalent to the
legacy bootstrap (``infra/database/ema-db/init.sql`` + the two SQL migrations)
PLUS the two tables that previously only existed because the application created
them lazily at request time:

  * ``pipeline_operation_log`` — was created by ``ensure_operation_log_table``
  * ``seion_prediction``       — was created by ``ensure_seion_tables``

From this revision on, Alembic is the single schema-authoring mechanism. The
SQL files under ``infra/database/ema-db`` are retained only as the historical
baseline that ``tests/test_migrations.py`` diffs against for parity; they are no
longer auto-loaded by Compose or bootstrap.

Statements are executed individually through the raw DBAPI cursor
(``exec_driver_sql``) — the same approach the retired ``ensure_*`` helpers used —
so PostgreSQL-specific DDL (DO blocks, ``$$`` bodies, GIN/partial/expression
indexes, ``::jsonb`` casts) passes through untouched.
"""

from __future__ import annotations

from typing import Sequence, Union

from alembic import op

# revision identifiers, used by Alembic.
revision: str = "0001_baseline"
down_revision: Union[str, None] = None
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


# ---------------------------------------------------------------------------
# Upgrade DDL, in dependency order. Each list element is exactly one SQL
# statement (a CREATE TABLE / CREATE INDEX / DO block / CREATE FUNCTION / INSERT).
# ---------------------------------------------------------------------------

_CORE = [
    # ---- organization / client / project / model ----
    """
    CREATE TABLE organization (
        id SERIAL PRIMARY KEY,
        name VARCHAR(255) NOT NULL UNIQUE,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
    )
    """,
    """
    CREATE TABLE client (
        id SERIAL PRIMARY KEY,
        organization_id INT NOT NULL REFERENCES organization(id) ON DELETE CASCADE,
        code VARCHAR(100) NOT NULL,
        display_name VARCHAR(255) NOT NULL,
        sharepoint_path TEXT,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE (organization_id, code)
    )
    """,
    "CREATE INDEX idx_client_org ON client(organization_id)",
    """
    CREATE TABLE project (
        id SERIAL PRIMARY KEY,
        organization_id INT NOT NULL REFERENCES organization(id) ON DELETE CASCADE,
        client_id INT REFERENCES client(id) ON DELETE SET NULL,
        project_title VARCHAR(500) NOT NULL,
        project_code VARCHAR(100),
        project_name VARCHAR(255),
        job_number VARCHAR(100),
        revit_version VARCHAR(20),
        client_name VARCHAR(255),
        location VARCHAR(255),
        jurisdiction VARCHAR(255),
        phase VARCHAR(100),
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE (organization_id, project_title)
    )
    """,
    "CREATE INDEX idx_project_code ON project(project_code)",
    "CREATE INDEX idx_project_client ON project(client_id)",
    """
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
    )
    """,
    "CREATE INDEX idx_model_project ON model(project_id)",
    # ---- export / sync_log ----
    """
    CREATE TABLE export (
        id SERIAL PRIMARY KEY,
        project_id INT NOT NULL REFERENCES project(id) ON DELETE CASCADE,
        model_id INT NOT NULL REFERENCES model(id) ON DELETE CASCADE,
        export_type VARCHAR(50) NOT NULL,
        file_name VARCHAR(500),
        file_size_bytes BIGINT,
        element_count INT,
        status VARCHAR(50) NOT NULL DEFAULT 'pending',
        started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        completed_at TIMESTAMPTZ,
        duration_seconds NUMERIC(10, 2),
        error_message TEXT,
        CONSTRAINT chk_export_status CHECK (status IN ('pending','in_progress','completed','failed','warning'))
    )
    """,
    "CREATE INDEX idx_export_model_completed ON export(model_id, completed_at DESC)",
    "CREATE INDEX idx_export_status ON export(status)",
    """
    CREATE TABLE sync_log (
        id SERIAL PRIMARY KEY,
        export_id INT NOT NULL REFERENCES export(id) ON DELETE CASCADE,
        step VARCHAR(100) NOT NULL,
        status VARCHAR(50) NOT NULL,
        started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        completed_at TIMESTAMPTZ,
        duration_seconds NUMERIC(10, 2),
        message TEXT,
        CONSTRAINT chk_sync_status CHECK (status IN ('pending','in_progress','completed','failed','warning'))
    )
    """,
    "CREATE INDEX idx_sync_log_export ON sync_log(export_id, started_at)",
    # ---- element ----
    """
    CREATE TABLE element (
        id BIGSERIAL PRIMARY KEY,
        unique_id VARCHAR(100) NOT NULL,
        element_id BIGINT NOT NULL,
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
    )
    """,
    "CREATE INDEX idx_element_export ON element(export_id)",
    "CREATE INDEX idx_element_model ON element(model_id)",
    "CREATE INDEX idx_element_category ON element(category)",
    "CREATE INDEX idx_element_level ON element(level)",
    "CREATE INDEX idx_element_family ON element(family)",
    "CREATE INDEX idx_element_instance_params ON element USING gin(instance_parameters)",
    "CREATE INDEX idx_element_type_params ON element USING gin(type_parameters)",
    # ---- rule / issue ----
    """
    CREATE TABLE rule (
        id SERIAL PRIMARY KEY,
        rule_code VARCHAR(20) NOT NULL UNIQUE,
        name VARCHAR(255) NOT NULL,
        description TEXT,
        discipline VARCHAR(100),
        severity VARCHAR(20) NOT NULL,
        check_type VARCHAR(50),
        active BOOLEAN NOT NULL DEFAULT TRUE,
        version VARCHAR(20) NOT NULL DEFAULT '1.0',
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        CONSTRAINT chk_rule_severity CHECK (severity IN ('low','medium','high','critical'))
    )
    """,
    """
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
        traceability JSONB,
        assigned_to_user_id INT,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        due_date TIMESTAMPTZ,
        reviewed_by_user_id INT,
        reviewed_at TIMESTAMPTZ,
        resolution_notes TEXT,
        CONSTRAINT chk_issue_severity CHECK (severity IN ('low','medium','high','critical')),
        CONSTRAINT chk_issue_status CHECK (status IN ('open','in_review','reviewed','closed','reopened')),
        CONSTRAINT chk_issue_source CHECK (source IN ('automated','manual'))
    )
    """,
    "CREATE INDEX idx_issue_project ON issue(project_id)",
    "CREATE INDEX idx_issue_model ON issue(model_id)",
    "CREATE INDEX idx_issue_export ON issue(export_id)",
    "CREATE INDEX idx_issue_rule ON issue(rule_code)",
    "CREATE INDEX idx_issue_status_severity ON issue(status, severity)",
    "CREATE INDEX idx_issue_created ON issue(created_at DESC)",
]

_REQUIREMENTS = [
    """
    CREATE TABLE requirement_source_file (
        id SERIAL PRIMARY KEY,
        client_id INT NOT NULL REFERENCES client(id) ON DELETE CASCADE,
        original_filename VARCHAR(500) NOT NULL,
        file_hash CHAR(64) NOT NULL,
        row_count_raw INT,
        row_count_loaded INT,
        row_count_skipped INT,
        export_date DATE,
        ingested_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE (client_id, file_hash)
    )
    """,
    "CREATE INDEX idx_req_source_client ON requirement_source_file(client_id)",
    """
    CREATE TABLE requirement (
        id BIGSERIAL PRIMARY KEY,
        client_id INT NOT NULL REFERENCES client(id) ON DELETE CASCADE,
        source_file_id INT REFERENCES requirement_source_file(id) ON DELETE SET NULL,
        discipline VARCHAR(50) NOT NULL,
        category VARCHAR(255),
        requirement_text TEXT NOT NULL,
        content_hash CHAR(64) NOT NULL,
        owner_status VARCHAR(50),
        resource VARCHAR(500),
        links TEXT,
        modified_by VARCHAR(255),
        date_updated TIMESTAMPTZ,
        sharepoint_path TEXT,
        is_actionable BOOLEAN NOT NULL DEFAULT TRUE,
        is_active BOOLEAN NOT NULL DEFAULT TRUE,
        first_seen_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        last_seen_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE (client_id, content_hash)
    )
    """,
    "CREATE INDEX idx_req_client_discipline ON requirement(client_id, discipline)",
    "CREATE INDEX idx_req_active ON requirement(is_active)",
    "CREATE INDEX idx_req_category ON requirement(category)",
    """
    CREATE TABLE requirement_compliance (
        id BIGSERIAL PRIMARY KEY,
        requirement_id BIGINT NOT NULL REFERENCES requirement(id) ON DELETE CASCADE,
        project_id INT NOT NULL REFERENCES project(id) ON DELETE CASCADE,
        model_id INT REFERENCES model(id) ON DELETE CASCADE,
        status VARCHAR(30) NOT NULL DEFAULT 'not_evaluated',
        evidence JSONB,
        evaluated_by VARCHAR(50),
        evaluated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        notes TEXT,
        UNIQUE (requirement_id, project_id, model_id),
        CONSTRAINT chk_compliance_status CHECK (
            status IN ('compliant','non_compliant','not_evaluated','not_applicable','needs_review')
        )
    )
    """,
    "CREATE INDEX idx_compliance_project_status ON requirement_compliance(project_id, status)",
    "CREATE INDEX idx_compliance_requirement ON requirement_compliance(requirement_id)",
    """
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
    )
    """,
    "CREATE INDEX idx_req_evidence_project ON requirement_evidence(project_id, evidence_status)",
    "CREATE INDEX idx_req_evidence_requirement ON requirement_evidence(requirement_id)",
    """
    CREATE UNIQUE INDEX uq_req_evidence_project_requirement_source
        ON requirement_evidence(project_id, requirement_id, COALESCE(source_ref, ''))
    """,
]

_READINESS = [
    """
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
    )
    """,
    "CREATE INDEX idx_readiness_snapshot_project_created ON readiness_snapshot(project_id, created_at DESC)",
    """
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
    )
    """,
    "CREATE INDEX idx_trade_readiness_snapshot ON trade_readiness_snapshot(snapshot_id)",
    """
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
    )
    """,
    "CREATE INDEX idx_readiness_action_project_status ON readiness_action(project_id, status, priority)",
    """
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
    )
    """,
]

# app_user is created via the original idempotent DO block (verbatim) so a fresh
# DB matches a DB that was migrated through the legacy create-or-alter path.
_APP_USER = [
    """
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
    """,
    """
    CREATE UNIQUE INDEX IF NOT EXISTS ux_app_user_email_lower
        ON public.app_user ((LOWER(email)))
        WHERE email IS NOT NULL
    """,
    """
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
    """,
    """
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
    """,
    """
    CREATE OR REPLACE FUNCTION public.set_app_user_updated_at()
    RETURNS TRIGGER
    LANGUAGE plpgsql
    AS $$
    BEGIN
        NEW.updated_at = NOW();
        RETURN NEW;
    END;
    $$;
    """,
    """
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
    """,
]

# Requirement Audit & Evaluation Bundle v1 (legacy migration 20260615_001).
_AUDIT = [
    """
    CREATE TABLE IF NOT EXISTS requirement_audit_run (
        id                      BIGSERIAL PRIMARY KEY,
        project_id              INT NOT NULL REFERENCES project(id) ON DELETE CASCADE,
        export_id               INT REFERENCES export(id) ON DELETE SET NULL,
        source_file_id          INT REFERENCES requirement_source_file(id) ON DELETE SET NULL,
        run_uid                 VARCHAR(64) NOT NULL,
        run_status              VARCHAR(30) NOT NULL DEFAULT 'completed',
        as_of                   TIMESTAMPTZ NOT NULL,
        schema_version          VARCHAR(50) NOT NULL DEFAULT '1.0',
        engine_version          VARCHAR(50),
        ruleset_version         VARCHAR(50),
        taxonomy_version        VARCHAR(50),
        score_policy_version    VARCHAR(50),
        input_hash              CHAR(64),
        output_hash             CHAR(64),
        project_name            VARCHAR(500),
        model_name              VARCHAR(500),
        requirements_file       VARCHAR(500),
        requirements_total      INT NOT NULL DEFAULT 0,
        status_counts           JSONB NOT NULL DEFAULT '{}'::jsonb,
        coherence_grade         VARCHAR(40),
        coherence_findings_total INT NOT NULL DEFAULT 0,
        ingested_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
        CONSTRAINT uq_requirement_audit_run UNIQUE (project_id, run_uid),
        CONSTRAINT chk_requirement_audit_run_status CHECK (
            run_status IN ('pending','running','completed','completed_with_warnings','failed')
        )
    )
    """,
    "CREATE INDEX IF NOT EXISTS idx_requirement_audit_run_project ON requirement_audit_run (project_id, ingested_at DESC)",
    """
    CREATE TABLE IF NOT EXISTS requirement_audit_record (
        id                      BIGSERIAL PRIMARY KEY,
        run_id                  BIGINT NOT NULL REFERENCES requirement_audit_run(id) ON DELETE CASCADE,
        requirement_id          BIGINT REFERENCES requirement(id) ON DELETE SET NULL,
        requirement_uid         VARCHAR(255),
        requirement_content_hash CHAR(64),
        decision_status         VARCHAR(40) NOT NULL,
        lifecycle_status        VARCHAR(40) NOT NULL DEFAULT 'CoherenceChecked',
        requirement_type        VARCHAR(120),
        validation_type         VARCHAR(40),
        applies                 BOOLEAN NOT NULL DEFAULT TRUE,
        rule_applied            VARCHAR(120),
        decision_reason         TEXT,
        confidence              NUMERIC(6,5),
        direct_evidence_count   INT NOT NULL DEFAULT 0,
        supporting_evidence_count INT NOT NULL DEFAULT 0,
        source_provenance       JSONB NOT NULL DEFAULT '{}'::jsonb,
        semantic_ir             JSONB NOT NULL DEFAULT '{}'::jsonb,
        evidence_policy         JSONB NOT NULL DEFAULT '{}'::jsonb,
        candidate_funnel        JSONB NOT NULL DEFAULT '{}'::jsonb,
        coherence_finding_ids   JSONB NOT NULL DEFAULT '[]'::jsonb,
        next_best_action        TEXT,
        record_hash             CHAR(64),
        created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
        CONSTRAINT uq_requirement_audit_record UNIQUE (run_id, requirement_uid),
        CONSTRAINT chk_requirement_audit_decision_status CHECK (
            decision_status IN ('Compliant','NonCompliant','NeedsReview','InsufficientData','NotApplicable','Indeterminate')
        )
    )
    """,
    "CREATE INDEX IF NOT EXISTS idx_requirement_audit_record_run ON requirement_audit_record (run_id)",
    "CREATE INDEX IF NOT EXISTS idx_requirement_audit_record_requirement ON requirement_audit_record (requirement_id)",
    """
    CREATE TABLE IF NOT EXISTS requirement_coherence_finding (
        id                      BIGSERIAL PRIMARY KEY,
        run_id                  BIGINT NOT NULL REFERENCES requirement_audit_run(id) ON DELETE CASCADE,
        finding_uid             VARCHAR(255) NOT NULL,
        finding_type            VARCHAR(50) NOT NULL,
        severity                VARCHAR(20) NOT NULL,
        requirement_type        VARCHAR(120),
        status                  VARCHAR(30) NOT NULL DEFAULT 'open',
        rationale               TEXT,
        primary_requirement     JSONB NOT NULL DEFAULT '{}'::jsonb,
        related_requirement     JSONB,
        normalized_values       JSONB NOT NULL DEFAULT '{}'::jsonb,
        created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
        CONSTRAINT uq_requirement_coherence_finding UNIQUE (run_id, finding_uid)
    )
    """,
    "CREATE INDEX IF NOT EXISTS idx_requirement_coherence_finding_run ON requirement_coherence_finding (run_id, severity)",
    """
    CREATE TABLE IF NOT EXISTS requirement_review_decision (
        id                      BIGSERIAL PRIMARY KEY,
        audit_record_id         BIGINT NOT NULL REFERENCES requirement_audit_record(id) ON DELETE CASCADE,
        reviewer_user_id        INT REFERENCES app_user(id) ON DELETE SET NULL,
        reviewer_name           VARCHAR(255),
        action                  VARCHAR(30) NOT NULL,
        previous_status         VARCHAR(40),
        resulting_status        VARCHAR(40),
        reason                  TEXT NOT NULL,
        created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
        CONSTRAINT chk_requirement_review_action CHECK (
            action IN ('accept','reject','override','request_changes','lock','supersede')
        )
    )
    """,
    "CREATE INDEX IF NOT EXISTS idx_requirement_review_decision_record ON requirement_review_decision (audit_record_id, created_at)",
]

# Landing documents (legacy migration 20260620_002).
_DOCUMENTS = [
    """
    CREATE TABLE IF NOT EXISTS landing_document (
        id BIGSERIAL PRIMARY KEY,
        project_id INT REFERENCES project(id) ON DELETE SET NULL,
        client_id INT REFERENCES client(id) ON DELETE SET NULL,
        project_folder VARCHAR(500),
        relative_path TEXT NOT NULL,
        file_name VARCHAR(500) NOT NULL,
        file_ext VARCHAR(20) NOT NULL,
        file_type VARCHAR(50) NOT NULL,
        document_category VARCHAR(50),
        discipline VARCHAR(100),
        sheet_number VARCHAR(100),
        sheet_title VARCHAR(500),
        spec_section VARCHAR(100),
        spec_title VARCHAR(500),
        page_count INT,
        file_size_bytes BIGINT,
        checksum_sha256 VARCHAR(64),
        manifest_path TEXT,
        source_system VARCHAR(100) NOT NULL DEFAULT 'landing',
        ingestion_status VARCHAR(50) NOT NULL DEFAULT 'indexed',
        indexed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        processed_at TIMESTAMPTZ,
        evidence_status VARCHAR(50) NOT NULL DEFAULT 'candidate',
        metadata_json JSONB,
        CONSTRAINT uq_landing_document_path_hash UNIQUE(relative_path, checksum_sha256)
    )
    """,
    "CREATE INDEX IF NOT EXISTS idx_landing_document_project_category ON landing_document(project_id, document_category)",
    "CREATE INDEX IF NOT EXISTS idx_landing_document_type ON landing_document(file_type)",
    """
    CREATE TABLE IF NOT EXISTS drawing_sheet (
        id BIGSERIAL PRIMARY KEY,
        document_id BIGINT NOT NULL REFERENCES landing_document(id) ON DELETE CASCADE,
        project_id INT REFERENCES project(id) ON DELETE SET NULL,
        sheet_number VARCHAR(100) NOT NULL,
        sheet_title VARCHAR(500),
        discipline VARCHAR(100),
        page_number INT,
        metadata_json JSONB,
        CONSTRAINT uq_drawing_sheet_document_sheet UNIQUE(document_id, sheet_number)
    )
    """,
    "CREATE INDEX IF NOT EXISTS idx_drawing_sheet_project ON drawing_sheet(project_id, sheet_number)",
    """
    CREATE TABLE IF NOT EXISTS document_text_snippet (
        id BIGSERIAL PRIMARY KEY,
        document_id BIGINT NOT NULL REFERENCES landing_document(id) ON DELETE CASCADE,
        page_number INT,
        text_preview TEXT NOT NULL,
        extraction_method VARCHAR(100) NOT NULL,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        CONSTRAINT uq_document_text_snippet_page UNIQUE(document_id, page_number)
    )
    """,
    "CREATE INDEX IF NOT EXISTS idx_document_text_snippet_document ON document_text_snippet(document_id)",
]

# pipeline_operation_log — previously created lazily by ensure_operation_log_table.
_OPERATION_LOG = [
    """
    CREATE TABLE IF NOT EXISTS pipeline_operation_log (
        id BIGSERIAL PRIMARY KEY,
        run_id VARCHAR(64),
        request_id VARCHAR(64),
        project_id INTEGER NULL,
        project_name VARCHAR(500),
        operation_type VARCHAR(100) NOT NULL,
        operation_label VARCHAR(255),
        source VARCHAR(100) NOT NULL DEFAULT 'backend',
        endpoint VARCHAR(255),
        method VARCHAR(20),
        status VARCHAR(30) NOT NULL DEFAULT 'started',
        severity VARCHAR(20) NOT NULL DEFAULT 'info',
        started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
        finished_at TIMESTAMPTZ NULL,
        duration_ms INTEGER NULL,
        actor_type VARCHAR(100),
        actor_label VARCHAR(255),
        landing_root TEXT,
        project_folder_name VARCHAR(255),
        file_path_relative TEXT,
        file_name VARCHAR(500),
        file_hash VARCHAR(128),
        counts_json JSONB,
        request_summary_json JSONB,
        response_summary_json JSONB,
        warnings_json JSONB,
        errors_json JSONB,
        environment_json JSONB,
        metadata_json JSONB
    )
    """,
    "CREATE INDEX IF NOT EXISTS idx_pipeline_operation_project_started ON pipeline_operation_log(project_id, started_at)",
    "CREATE INDEX IF NOT EXISTS idx_pipeline_operation_type_status ON pipeline_operation_log(operation_type, status)",
    "CREATE INDEX IF NOT EXISTS idx_pipeline_operation_run_request ON pipeline_operation_log(run_id, request_id)",
]

# seion_prediction — previously created lazily by ensure_seion_tables.
_SEION = [
    """
    CREATE TABLE IF NOT EXISTS seion_prediction (
        id BIGSERIAL PRIMARY KEY,
        project_id INT REFERENCES project(id) ON DELETE CASCADE,
        head_uid TEXT NOT NULL,
        relation TEXT NOT NULL,
        tail_uid TEXT NOT NULL,
        score DOUBLE PRECISION NOT NULL,
        rank INT,
        model_version TEXT NOT NULL,
        status VARCHAR(30) NOT NULL DEFAULT 'suggested',
        source TEXT NOT NULL DEFAULT 'seion_kge',
        reviewer_note TEXT,
        accepted_by TEXT,
        accepted_at TIMESTAMPTZ,
        metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        updated_at TIMESTAMPTZ,
        CONSTRAINT chk_seion_prediction_status CHECK (
            status IN ('suggested','accepted','rejected','stale','superseded')
        )
    )
    """,
    "CREATE INDEX IF NOT EXISTS idx_seion_prediction_project_status ON seion_prediction(project_id, status)",
    "CREATE INDEX IF NOT EXISTS idx_seion_prediction_relation ON seion_prediction(relation)",
]

# Baseline seed (reference data) — default org, the 3 known ISD clients, and the
# 4 built-in QA/QC rules. Matches infra/database/ema-db/init.sql.
_SEED = [
    "INSERT INTO organization (name) VALUES ('EMA Engineering')",
    """
    INSERT INTO client (organization_id, code, display_name, sharepoint_path) VALUES
        (1, 'DENTON_ISD',     'Denton ISD',     'sites/OwnerRequirements/MASTER DISTRICT OWNER REQ LIST/Lists/DENTON ISD'),
        (1, 'NORTHWEST_ISD',  'Northwest ISD',  'sites/OwnerRequirements/MASTER DISTRICT OWNER REQ LIST/Lists/NORTHWEST ISD'),
        (1, 'ROCKWALL_ISD',   'Rockwall ISD',   'sites/OwnerRequirements/MASTER DISTRICT OWNER REQ LIST/Lists/ROCKWALL ISD')
    """,
    """
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
         '1.0')
    """,
]

# Every table this baseline creates, in reverse-safe drop order (CASCADE handles
# FKs regardless, but listing children first keeps the intent clear).
_ALL_TABLES = [
    "seion_prediction",
    "pipeline_operation_log",
    "document_text_snippet",
    "drawing_sheet",
    "landing_document",
    "requirement_review_decision",
    "requirement_coherence_finding",
    "requirement_audit_record",
    "requirement_audit_run",
    "rule_execution_log",
    "readiness_action",
    "trade_readiness_snapshot",
    "readiness_snapshot",
    "requirement_evidence",
    "requirement_compliance",
    "requirement",
    "requirement_source_file",
    "issue",
    "rule",
    "element",
    "sync_log",
    "export",
    "model",
    "project",
    "client",
    "app_user",
    "organization",
]


def _run(statements: list[str]) -> None:
    bind = op.get_bind()
    for statement in statements:
        bind.exec_driver_sql(statement)


def upgrade() -> None:
    _run(_CORE)
    _run(_REQUIREMENTS)
    _run(_READINESS)
    _run(_APP_USER)
    _run(_AUDIT)
    _run(_DOCUMENTS)
    _run(_OPERATION_LOG)
    _run(_SEION)
    _run(_SEED)


def downgrade() -> None:
    bind = op.get_bind()
    for table in _ALL_TABLES:
        bind.exec_driver_sql(f"DROP TABLE IF EXISTS {table} CASCADE")
    bind.exec_driver_sql("DROP FUNCTION IF EXISTS public.set_app_user_updated_at() CASCADE")
