-- =====================================================================
-- EMA AI — Requirement Audit & Evaluation Bundle v1  (migration 20260615_001)
--
-- Adds the system-of-record tables that *ingest* the deterministic
-- Evaluation Bundle produced by the C# engine. These tables RECORD how the
-- engine reached each decision and the coherence of the requirement set;
-- they never recompute a status. The C# engine remains the single authority.
--
-- Existing tables remain untouched:
--   requirement / requirement_compliance / requirement_evidence /
--   readiness_snapshot / readiness_action  (operational state)
--
-- This file is applied by scripts/apply_migrations.py (NOT by init.sql, which
-- only runs on first volume creation). It is safe to re-run: every object uses
-- IF NOT EXISTS and the migrations runner records what has already applied.
-- =====================================================================

-- ---------------------------------------------------------------------
-- requirement_audit_run — one ingested evaluation run (a closed bundle)
-- ---------------------------------------------------------------------
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
);

CREATE INDEX IF NOT EXISTS idx_requirement_audit_run_project
    ON requirement_audit_run (project_id, ingested_at DESC);

-- ---------------------------------------------------------------------
-- requirement_audit_record — the auditable dossier for one requirement
-- ---------------------------------------------------------------------
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
);

CREATE INDEX IF NOT EXISTS idx_requirement_audit_record_run
    ON requirement_audit_record (run_id);
CREATE INDEX IF NOT EXISTS idx_requirement_audit_record_requirement
    ON requirement_audit_record (requirement_id);

-- ---------------------------------------------------------------------
-- requirement_coherence_finding — duplicates / conflicts across requirements
-- ---------------------------------------------------------------------
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
);

CREATE INDEX IF NOT EXISTS idx_requirement_coherence_finding_run
    ON requirement_coherence_finding (run_id, severity);

-- ---------------------------------------------------------------------
-- requirement_review_decision — append-only immutable human review history
-- ---------------------------------------------------------------------
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
);

CREATE INDEX IF NOT EXISTS idx_requirement_review_decision_record
    ON requirement_review_decision (audit_record_id, created_at);
