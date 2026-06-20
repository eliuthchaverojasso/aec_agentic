-- EMA AI -- Landing document ingestion tables
-- These tables were previously created lazily at runtime by
-- app/ingestion/document_service.py (Base.metadata / ensure-tables), which left a
-- fresh database missing `landing_document` until that service first ran. That gap
-- caused GET /dev/status (and test_dev_status_endpoint_available) to fail with
-- UndefinedTable on a clean DB (defect P1-1). DDL is IF NOT EXISTS and mirrors
-- document_service.py exactly, so applying it here is safe and idempotent.

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
);
CREATE INDEX IF NOT EXISTS idx_landing_document_project_category ON landing_document(project_id, document_category);
CREATE INDEX IF NOT EXISTS idx_landing_document_type ON landing_document(file_type);

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
);
CREATE INDEX IF NOT EXISTS idx_drawing_sheet_project ON drawing_sheet(project_id, sheet_number);

CREATE TABLE IF NOT EXISTS document_text_snippet (
    id BIGSERIAL PRIMARY KEY,
    document_id BIGINT NOT NULL REFERENCES landing_document(id) ON DELETE CASCADE,
    page_number INT,
    text_preview TEXT NOT NULL,
    extraction_method VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_document_text_snippet_page UNIQUE(document_id, page_number)
);
CREATE INDEX IF NOT EXISTS idx_document_text_snippet_document ON document_text_snippet(document_id);
