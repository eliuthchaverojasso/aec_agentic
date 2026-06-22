"""Pydantic schemas for API request/response contracts."""

from datetime import date, datetime
from typing import Any, Literal

from pydantic import BaseModel, ConfigDict, Field, model_validator


class ORMModel(BaseModel):
    model_config = ConfigDict(from_attributes=True)


# ---------------------------------------------------------------------------
# Organization / Project / Model
# ---------------------------------------------------------------------------


class ProjectOut(ORMModel):
    id: int
    organization_id: int
    client_id: int | None = None
    project_title: str
    project_code: str | None = None
    project_name: str | None = None
    job_number: str | None = None
    revit_version: str | None = None
    client_name: str | None = None
    location: str | None = None
    jurisdiction: str | None = None
    phase: str | None = None
    created_at: datetime
    updated_at: datetime


class ProjectSummary(ProjectOut):
    """Project with aggregate health metrics for the portfolio table."""

    active_models: int = 0
    open_issues: int = 0
    critical_issues: int = 0
    high_issues: int = 0
    medium_issues: int = 0
    low_issues: int = 0
    model_health_score: float | None = None
    last_sync_at: datetime | None = None


class ProjectCreateRequest(BaseModel):
    name: str = Field(..., min_length=2, max_length=500)
    project_code: str | None = Field(default=None, max_length=100)
    project_number: str | None = Field(default=None, max_length=100)
    project_type: str | None = Field(default=None, max_length=100)
    client_id: int | None = None
    client_code: str | None = Field(default=None, max_length=100)
    client_name: str | None = Field(default=None, max_length=255)
    location: str | None = Field(default=None, max_length=255)
    current_milestone: str | None = Field(default=None, max_length=100)
    enabled_disciplines: list[str] = Field(default_factory=list)
    landing_project_folder: str | None = Field(default=None, max_length=255)
    environment: str | None = Field(default=None, max_length=50)
    storage_mode: str | None = Field(default=None, max_length=100)


class ProjectUpdateRequest(BaseModel):
    project_title: str | None = Field(default=None, min_length=2, max_length=500)
    project_code: str | None = Field(default=None, max_length=100)
    project_name: str | None = Field(default=None, max_length=255)
    job_number: str | None = Field(default=None, max_length=100)
    location: str | None = Field(default=None, max_length=255)
    phase: str | None = Field(default=None, max_length=100)
    revit_version: str | None = Field(default=None, max_length=20)


class ProjectClientBindRequest(BaseModel):
    client_id: int | None = None
    client_code: str | None = Field(default=None, min_length=2, max_length=100)
    client_name: str | None = Field(default=None, min_length=2, max_length=255)
    owner_name: str | None = Field(default=None, max_length=255)
    current_milestone: str | None = Field(default=None, max_length=100)
    project_stage: str | None = Field(default=None, max_length=100)


class ProjectClientBindResponse(BaseModel):
    project: ProjectSummary
    client: "ClientOut"
    created_client: bool = False
    message: str


class ModelOut(ORMModel):
    id: int
    project_id: int
    revit_file_name: str | None = None
    revit_version: str | None = None
    discipline: str | None = None
    model_type: str | None = None
    last_sync_at: datetime | None = None
    exported_by: str | None = None
    created_at: datetime


class ProjectModelCreateRequest(BaseModel):
    model_name: str = Field(..., min_length=1, max_length=500)
    model_type: str = Field(default="Revit", max_length=100)
    discipline: str | None = Field(default=None, max_length=100)
    revit_document_title: str | None = Field(default=None, max_length=500)
    revit_version: str | None = Field(default=None, max_length=20)
    source_system: str = Field(default="Revit", max_length=100)


class ProjectModelSummary(BaseModel):
    id: int
    project_id: int
    model_name: str
    model_type: str | None = None
    discipline: str | None = None
    source_system: str | None = None
    created_at: datetime


class ModelHealth(BaseModel):
    model_id: int
    total_elements: int
    elements_by_category: dict[str, int]
    elements_by_level: dict[str, int]
    open_issues: int
    critical_issues: int
    high_issues: int
    medium_issues: int
    low_issues: int
    model_health_score: float
    last_export_id: int | None = None
    last_sync_at: datetime | None = None


# ---------------------------------------------------------------------------
# Export / Sync
# ---------------------------------------------------------------------------


class ExportOut(ORMModel):
    id: int
    project_id: int
    model_id: int
    export_type: str
    file_name: str | None = None
    file_size_bytes: int | None = None
    element_count: int | None = None
    status: str
    started_at: datetime
    completed_at: datetime | None = None
    duration_seconds: float | None = None
    error_message: str | None = None


class ExportCreateResponse(BaseModel):
    export_id: int
    status: str
    file_name: str
    message: str


class SyncLogOut(ORMModel):
    id: int
    export_id: int
    step: str
    status: str
    started_at: datetime
    completed_at: datetime | None = None
    duration_seconds: float | None = None
    message: str | None = None


# ---------------------------------------------------------------------------
# Landing ingestion
# ---------------------------------------------------------------------------


LandingFileType = Literal[
    "revit_export",
    "owner_requirements",
    "drawing_pdf",
    "specification_pdf",
    "pdf_document",
    "dwfx_export",
    "viewpoint_json",
    "timeline_excel",
    "project_extract",
    "binding",
    "unknown",
]


class LandingIngestRequest(BaseModel):
    manifest_path: str = "landing_manifest.json"
    dry_run: bool = False
    recalculate_readiness: bool = True


class LandingFileReport(BaseModel):
    path: str
    type: LandingFileType
    status: str
    message: str
    required: bool = False
    counts: dict[str, Any] = Field(default_factory=dict)
    warnings: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)


class LandingIngestReport(BaseModel):
    status: str
    dry_run: bool
    manifest_path: str
    processed: dict[str, int] = Field(default_factory=dict)
    created: dict[str, int] = Field(default_factory=dict)
    updated: dict[str, int] = Field(default_factory=dict)
    skipped: list[str] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)
    files: list[LandingFileReport] = Field(default_factory=list)


class LandingScanRequest(BaseModel):
    project_folder: str | None = None
    update_manifest: bool = False
    include_pdf_metadata: bool = True
    dry_run: bool = True
    preserve_existing: bool = True


class LandingScanDocument(BaseModel):
    path: str
    type: LandingFileType
    document_category: str | None = None
    discipline: str | None = None
    sheet_number: str | None = None
    sheet_title: str | None = None
    spec_section: str | None = None
    spec_title: str | None = None
    checksum_sha256: str | None = None
    file_size_bytes: int | None = None
    page_count: int | None = None
    indexed_at: datetime | None = None
    metadata: dict[str, Any] = Field(default_factory=dict)


class LandingScanReport(BaseModel):
    status: str
    project_folder: str | None = None
    files_found: int = 0
    manifest_updated: bool = False
    manifest_path: str | None = None
    documents: list[LandingScanDocument] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)


class LandingRebuildManifestRequest(BaseModel):
    project_folder: str
    preserve_existing: bool = True
    include_pdf_metadata: bool = True
    dry_run: bool = False


class ProjectLandingConfigureRequest(BaseModel):
    landing_root: str
    project_folder_name: str
    create_folders: bool = True


class ProjectLandingStatusOut(BaseModel):
    ok: bool = True
    operation: str
    project_id: int
    project_name: str | None = None
    project_folder_name: str | None = None
    endpoint: str
    project_landing_path: str | None = None
    folder_status: dict[str, bool] = Field(default_factory=dict)
    counts: dict[str, int] = Field(default_factory=dict)
    warnings: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)
    next_actions: list[str] = Field(default_factory=list)
    folder_found: bool = True
    landing_root: str | None = None
    requested_folder: str | None = None
    available_folders: list[str] = Field(default_factory=list)
    suggested_folder: str | None = None


class LandingDiscoverRequest(BaseModel):
    landing_root: str


class LandingDiscoveredProject(BaseModel):
    project_folder_name: str
    has_manifest: bool
    latest_revit_export: str | None = None
    counts: dict[str, int] = Field(default_factory=dict)
    warnings: list[str] = Field(default_factory=list)


class LandingDiscoverResponse(BaseModel):
    ok: bool = True
    operation: str = "discover"
    endpoint: str = "/api/v1/landing/projects/discover"
    landing_root: str
    projects: list[LandingDiscoveredProject] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)


LandingProjectStatus = Literal[
    "ready",
    "needs_manifest",
    "needs_client_binding",
    "partial",
    "has_errors",
    "empty",
]


class LandingProjectCounts(BaseModel):
    revit_exports: int = 0
    revit_meta: int = 0
    drawings: int = 0
    owner_requirements: int = 0
    specifications: int = 0
    docx: int = 0
    manifests: int = 0
    unknown: int = 0


class LandingClientSuggestion(BaseModel):
    client_name: str | None = None
    client_code: str | None = None
    source: str | None = None
    confidence: str = "filename"


class LandingProjectSummary(BaseModel):
    project_folder: str
    project_name: str
    project_id: int | None = None
    client_id: int | None = None
    client_name: str | None = None
    client_code: str | None = None
    manifest_exists: bool = False
    manifest_path: str | None = None
    counts: LandingProjectCounts = Field(default_factory=LandingProjectCounts)
    documents: dict[str, list[str]] = Field(default_factory=dict)
    client_suggestion: LandingClientSuggestion | None = None
    status: LandingProjectStatus = "empty"
    warnings: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)
    next_action: str | None = None


class LandingProjectsDiscoveryResponse(BaseModel):
    landing_root: str
    project_count: int
    totals: LandingProjectCounts = Field(default_factory=LandingProjectCounts)
    projects: list[LandingProjectSummary] = Field(default_factory=list)


class LandingManifestBatchRequest(BaseModel):
    dry_run: bool = True
    preserve_existing: bool = True
    infer_client_from_owner_requirements: bool = True
    project_folders: list[str] | None = None


class LandingManifestBatchProjectResult(BaseModel):
    project_folder: str
    manifest_path: str | None = None
    would_write: bool = False
    file_count: int = 0
    counts: LandingProjectCounts = Field(default_factory=LandingProjectCounts)
    client_suggestion: LandingClientSuggestion | None = None
    warnings: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)


class LandingManifestBatchResponse(BaseModel):
    dry_run: bool
    landing_root: str
    project_count: int
    updated: int
    skipped: int
    projects: list[LandingManifestBatchProjectResult] = Field(default_factory=list)


class LandingIngestAllRequest(BaseModel):
    dry_run: bool = True
    project_folders: list[str] | None = None
    require_client_for_owner_requirements: bool = True
    create_snapshot: bool = False
    preserve_existing: bool = True


class LandingBatchProjectResult(BaseModel):
    project_folder: str
    project_id: int | None = None
    status: str
    counts: dict[str, int] = Field(default_factory=dict)
    readiness: dict[str, Any] = Field(default_factory=dict)
    warnings: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)
    next_action: str | None = None


class LandingIngestAllResponse(BaseModel):
    dry_run: bool
    project_count: int
    processed: int
    success: int
    partial: int
    failed: int
    projects: list[LandingBatchProjectResult] = Field(default_factory=list)


class LandingProjectBindRequest(BaseModel):
    project_id: int | None = None
    client_id: int | None = None
    client_code: str | None = None
    client_name: str | None = None
    milestone: str | None = None
    project_name: str | None = None
    create_project: bool = False


class LandingProjectBindResponse(BaseModel):
    project_folder: str
    project_id: int
    project_name: str
    client_id: int | None = None
    client_name: str | None = None
    client_code: str | None = None
    status: LandingProjectStatus
    warnings: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)
    next_actions: list[str] = Field(default_factory=list)


class LandingBootstrapRequest(BaseModel):
    landing_root: str
    project_folder_name: str
    project_display_name: str | None = None
    project_code: str | None = None
    client_code: str | None = None
    client_name: str | None = None
    environment: str = "Local"


class ProjectOperationOut(BaseModel):
    ok: bool = True
    operation: str
    project_id: int
    project_name: str | None = None
    project_folder_name: str | None = None
    endpoint: str
    dry_run: bool | None = None
    counts: dict[str, int] = Field(default_factory=dict)
    warnings: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)
    next_actions: list[str] = Field(default_factory=list)
    request_id: str | None = None
    run_id: str | None = None
    operation_log_id: int | None = None


class DebugLogOut(BaseModel):
    id: int
    run_id: str | None = None
    request_id: str | None = None
    project_id: int | None = None
    project_name: str | None = None
    operation_type: str
    operation_label: str | None = None
    source: str
    endpoint: str | None = None
    method: str | None = None
    status: str
    severity: str
    started_at: datetime | None = None
    finished_at: datetime | None = None
    duration_ms: int | None = None
    counts_json: dict[str, Any] = Field(default_factory=dict)
    request_summary_json: dict[str, Any] = Field(default_factory=dict)
    response_summary_json: dict[str, Any] = Field(default_factory=dict)
    warnings_json: list[Any] = Field(default_factory=list)
    errors_json: list[Any] = Field(default_factory=list)
    environment_json: dict[str, Any] = Field(default_factory=dict)
    metadata_json: dict[str, Any] = Field(default_factory=dict)


class LandingBootstrapResponse(BaseModel):
    ok: bool = True
    operation: str = "bootstrap-from-folder"
    endpoint: str = "/api/v1/landing/projects/bootstrap-from-folder"
    project_id: int
    client_id: int | None = None
    project_name: str | None = None
    project_folder_name: str
    project_landing_path: str
    discovered_files: int = 0
    landing_status: ProjectLandingStatusOut
    warnings: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)
    next_actions: list[str] = Field(default_factory=list)


class ProjectFileRegisterItem(BaseModel):
    relative_path: str
    category: str | None = None
    discipline: str | None = None


class ProjectFileRegisterRequest(BaseModel):
    files: list[ProjectFileRegisterItem]


class LandingDocumentOut(ORMModel):
    id: int
    project_id: int | None = None
    client_id: int | None = None
    project_folder: str | None = None
    relative_path: str
    file_name: str
    file_ext: str
    file_type: str
    document_category: str | None = None
    discipline: str | None = None
    sheet_number: str | None = None
    sheet_title: str | None = None
    spec_section: str | None = None
    spec_title: str | None = None
    page_count: int | None = None
    file_size_bytes: int | None = None
    checksum_sha256: str | None = None
    manifest_path: str | None = None
    source_system: str = "landing"
    ingestion_status: str = "indexed"
    indexed_at: datetime
    processed_at: datetime | None = None
    evidence_status: str = "candidate"
    metadata_json: dict[str, Any] | None = None


class DocumentTextPreviewOut(BaseModel):
    document_id: int
    page_number: int | None = None
    text_preview: str | None = None
    extraction_method: str | None = None
    available: bool = False
    message: str | None = None


# ---------------------------------------------------------------------------
# Issue
# ---------------------------------------------------------------------------


class IssueOut(ORMModel):
    id: int
    organization_id: int
    project_id: int
    model_id: int
    export_id: int
    element_unique_id: str | None = None
    element_db_id: int | None = None
    rule_id: int | None = None
    rule_code: str | None = None
    issue_type: str | None = None
    severity: str
    status: str
    source: str
    message: str | None = None
    traceability: dict[str, Any] | None = None
    assigned_to_user_id: int | None = None
    created_at: datetime
    due_date: datetime | None = None
    reviewed_by_user_id: int | None = None
    reviewed_at: datetime | None = None
    resolution_notes: str | None = None


class IssueDetail(IssueOut):
    """Enriched issue detail for the drawer view."""

    project_title: str | None = None
    project_code: str | None = None
    element_category: str | None = None
    element_name: str | None = None
    element_family: str | None = None
    element_type: str | None = None
    element_level: str | None = None
    rule_name: str | None = None
    rule_description: str | None = None


class IssueUpdate(BaseModel):
    status: str | None = None
    assigned_to_user_id: int | None = None
    assigned_to: str | None = None
    resolution_notes: str | None = None
    reviewed_by_user_id: int | None = None
    reviewed_by: str | None = None
    reviewed_at: datetime | None = None


class IssueListResponse(BaseModel):
    total: int
    page: int
    page_size: int
    items: list[IssueOut]


# ---------------------------------------------------------------------------
# Auth
# ---------------------------------------------------------------------------


class AuthRegisterRequest(BaseModel):
    name: str = Field(..., min_length=2, max_length=255)
    email: str = Field(..., min_length=5, max_length=255)
    password: str = Field(..., min_length=8, max_length=128)


class AuthUserOut(ORMModel):
    id: int
    name: str
    email: str | None = None
    role: str
    auth_provider: str
    is_active: bool
    is_locked: bool
    failed_login_attempts: int
    last_login_at: datetime | None = None
    password_changed_at: datetime | None = None
    must_change_password: bool
    created_at: datetime
    updated_at: datetime


class AuthRegisterResponse(BaseModel):
    message: str
    user: AuthUserOut


class AuthLoginRequest(BaseModel):
    email: str = Field(..., min_length=5, max_length=255)
    password: str = Field(..., min_length=8, max_length=128)


class AuthLoginResponse(BaseModel):
    message: str
    access_token: str
    token_type: str = "bearer"
    expires_in: int
    user: AuthUserOut


class AuthProfileResponse(BaseModel):
    user: AuthUserOut


# ---------------------------------------------------------------------------
# AI Query
# ---------------------------------------------------------------------------


class AIQueryRequest(BaseModel):
    query: str = Field(..., min_length=2, max_length=500)
    project_id: int | None = None


class AIQueryResponse(BaseModel):
    query: str
    answer: str
    matched_template: str | None = None
    table: list[dict[str, Any]] | None = None
    source: str = "postgres"
    filters: dict[str, Any] = Field(default_factory=dict)
    timestamp: datetime


# ---------------------------------------------------------------------------
# Client / Owner Requirements
# ---------------------------------------------------------------------------


class ClientOut(ORMModel):
    id: int
    organization_id: int
    code: str
    display_name: str
    sharepoint_path: str | None = None
    created_at: datetime


class ClientCreate(BaseModel):
    code: str = Field(..., min_length=2, max_length=100)
    display_name: str = Field(..., min_length=2, max_length=255)
    sharepoint_path: str | None = None


class RequirementSourceFileOut(ORMModel):
    id: int
    client_id: int
    original_filename: str
    file_hash: str
    row_count_raw: int | None = None
    row_count_loaded: int | None = None
    row_count_skipped: int | None = None
    export_date: date | None = None
    ingested_at: datetime
    sheet_names: str | None = None
    parser_version: str | None = None


class RequirementOut(ORMModel):
    id: int
    client_id: int
    source_file_id: int | None = None
    discipline: str
    category: str | None = None
    requirement_text: str
    content_hash: str
    owner_status: str | None = None
    resource: str | None = None
    links: str | None = None
    modified_by: str | None = None
    date_updated: datetime | None = None
    sharepoint_path: str | None = None
    is_actionable: bool
    is_active: bool
    first_seen_at: datetime
    last_seen_at: datetime
    source_sheet: str | None = None
    source_row: int | None = None
    source_cell_range: str | None = None
    original_columns_json: dict[str, Any] | None = None
    parser_version: str | None = None
    import_id: str | None = None


class RequirementListResponse(BaseModel):
    client_id: int
    total: int
    page: int
    page_size: int
    items: list[RequirementOut]


# Import modes for requirements ingestion.
ImportMode = Literal["full_snapshot", "partial_update", "append_only"]


class ImportDiffReport(BaseModel):
    import_mode: ImportMode = "full_snapshot"
    new_requirements: list[dict[str, Any]] = Field(default_factory=list)
    updated_requirements: list[dict[str, Any]] = Field(default_factory=list)
    deactivated_requirements: list[dict[str, Any]] = Field(default_factory=list)
    unchanged_requirements: list[dict[str, Any]] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)
    per_discipline: dict[str, int] = Field(default_factory=dict)
    per_sheet: dict[str, int] = Field(default_factory=dict)


class RequirementIngestResponse(BaseModel):
    client_id: int
    source_file_id: int
    original_filename: str
    file_hash: str
    row_count_raw: int
    row_count_loaded: int
    row_count_skipped: int
    row_count_new: int
    row_count_updated: int
    row_count_deactivated: int
    export_date: date | None = None
    reused_existing_file: bool = False
    per_discipline: dict[str, int] = Field(default_factory=dict)
    per_sheet: dict[str, int] = Field(default_factory=dict)
    import_mode: str = "full_snapshot"
    import_id: str | None = None
    dry_run: bool = False
    diff_report: ImportDiffReport | None = None
    sheet_names: list[str] = Field(default_factory=list)
    parser_version: str | None = None


ComplianceStatus = Literal[
    "compliant", "non_compliant", "not_evaluated", "not_applicable", "needs_review"
]


class RequirementComplianceOut(ORMModel):
    id: int
    requirement_id: int
    project_id: int
    model_id: int | None = None
    status: ComplianceStatus
    evidence: dict[str, Any] | None = None
    evaluated_by: str | None = None
    evaluated_at: datetime
    notes: str | None = None


class RequirementComplianceUpdate(BaseModel):
    status: ComplianceStatus
    model_id: int | None = None
    evidence: dict[str, Any] | None = None
    notes: str | None = None
    evaluated_by: str | None = "manual"


class RequirementMappingUpdate(BaseModel):
    milestone: str | None = Field(default=None, max_length=100)
    discipline: str | None = Field(default=None, max_length=50)
    is_actionable: bool | None = None
    notes: str | None = None


class ProjectComplianceRow(BaseModel):
    requirement_id: int
    discipline: str
    category: str | None = None
    milestone: str | None = None
    requirement_text: str
    compliance_id: int | None = None
    status: ComplianceStatus = "not_evaluated"
    evaluated_by: str | None = None
    evaluated_at: datetime | None = None
    notes: str | None = None
    evidence_type: str | None = None
    evidence_status: str | None = None
    evidence_source: str | None = None


class ProjectComplianceMatrix(BaseModel):
    project_id: int
    client_id: int | None = None
    total_requirements: int
    by_status: dict[str, int]
    items: list[ProjectComplianceRow]


class ProjectRequirementCounts(BaseModel):
    total: int = 0
    actionable: int = 0
    non_actionable: int = 0
    covered: int = 0
    missing: int = 0
    needs_review: int = 0
    not_applicable: int = 0
    evaluated: int = 0
    rejected: int = 0
    candidate_evidence_count: int = 0
    accepted_evidence_count: int = 0
    rejected_evidence_count: int = 0
    no_evidence_count: int = 0
    requirement_coverage_ratio: float = 0.0
    requirement_coverage_percent: float = 0.0


class ProjectRequirementRow(BaseModel):
    requirement_id: int
    source: str = "Owner Requirements"
    source_type: str = "owner_requirements"
    discipline: str
    category: str | None = None
    milestone: str | None = None
    requirement_text: str
    owner_status: str | None = None
    is_actionable: bool
    readiness_status: str
    normalized_status: str = "no_evidence"
    evidence_status: str | None = None
    evidence_review_status: str | None = None
    evidence_source_label: str | None = None
    related_issue_id: int | None = None
    related_sheet: str | None = None
    updated_at: datetime | None = None


class ProjectRequirementsResponse(BaseModel):
    project_id: int
    project_name: str | None = None
    client_id: int | None = None
    client_name: str | None = None
    state: Literal[
        "no_client_linked",
        "client_linked_no_requirements",
        "requirements_loaded_no_evidence",
        "evidence_candidates_pending",
        "readiness_available",
        "requirements_loaded",
        "filtered_empty",
        "backend_error",
    ]
    counts: ProjectRequirementCounts
    by_discipline: dict[str, ProjectRequirementCounts] = Field(default_factory=dict)
    by_source_type: dict[str, int] = Field(default_factory=dict)
    by_owner_status: dict[str, int] = Field(default_factory=dict)
    items: list[ProjectRequirementRow] = Field(default_factory=list)
    page: int = 1
    page_size: int = 100
    total: int = 0


# ---------------------------------------------------------------------------
# Readiness
# ---------------------------------------------------------------------------


class ReadinessComponent(BaseModel):
    score: float
    label: str
    detail: str


class TradeReadinessRow(BaseModel):
    discipline: str
    readiness: float
    label: str
    requirements_total: int
    requirements_evaluated: int
    missing_requirements: int
    needs_review: int
    critical_issues: int
    high_issues: int
    source: str = "requirements"
    official: bool = True
    elements: int = 0
    open_issues: int = 0
    medium_issues: int = 0
    low_issues: int = 0


class ReadinessGap(BaseModel):
    rule_code: str
    severity: str
    status: str
    message: str
    requirement_id: int | None = None
    discipline: str | None = None
    milestone: str | None = None
    evidence_type: str | None = None
    readiness_impact: float = 0.0
    action_type: str | None = None
    evidence: dict[str, Any] | None = None


class ReadinessRecommendedAction(BaseModel):
    action_type: str
    label: str
    detail: str
    severity: str
    rule_code: str
    requirement_id: int | None = None
    discipline: str | None = None


class ProjectReadinessOut(BaseModel):
    project_id: int
    project_title: str
    client_id: int | None = None
    client_name: str | None = None
    overall_readiness: float
    label: str
    requirement_coverage: ReadinessComponent
    qaqc_health: ReadinessComponent
    sync_freshness: ReadinessComponent
    open_issues: dict[str, int]
    latest_export_id: int | None = None
    latest_sync_at: datetime | None = None
    trade_readiness: list[TradeReadinessRow]
    gap_summary: dict[str, int] = Field(default_factory=dict)
    top_gaps: list[ReadinessGap] = Field(default_factory=list)
    recommended_actions: list[ReadinessRecommendedAction] = Field(default_factory=list)


EvidenceType = Literal["model", "sheet", "spec", "manual", "hybrid"]
EvidenceStatus = Literal["covered", "missing", "needs_review", "blocked", "not_applicable"]
EvidenceReviewStatus = Literal["candidate", "accepted", "rejected", "needs_review", "none"]
ReadinessActionStatus = Literal["open", "in_review", "done", "dismissed"]
ReadinessActionPriority = Literal["low", "medium", "high", "critical"]
SeionPredictionStatus = Literal["suggested", "accepted", "rejected", "stale", "superseded"]


class RequirementEvidenceOut(ORMModel):
    id: int
    project_id: int
    requirement_id: int
    evidence_type: EvidenceType
    evidence_status: EvidenceStatus
    review_status: EvidenceReviewStatus = "none"
    source_ref: str | None = None
    source_label: str | None = None
    element_unique_id: str | None = None
    model_element_id: str | None = None
    sheet_number: str | None = None
    sheet_id: int | None = None
    spec_section: str | None = None
    confidence: float | None = None
    review_note: str | None = None
    reviewed_by: str | None = None
    reviewed_at: datetime | None = None
    document_id: int | None = None
    metadata_json: dict[str, Any] | None = None
    created_at: datetime
    updated_at: datetime


class RequirementEvidenceCreate(BaseModel):
    evidence_type: EvidenceType = "manual"
    review_status: EvidenceReviewStatus = "candidate"
    source_ref: str | None = None
    source_label: str | None = None
    confidence: float | None = None
    review_note: str | None = None
    document_id: int | None = None
    sheet_id: int | None = None
    model_element_id: str | None = None
    sheet_number: str | None = None
    spec_section: str | None = None
    metadata: dict[str, Any] = Field(default_factory=dict)


class RequirementEvidenceUpdate(BaseModel):
    review_status: EvidenceReviewStatus | None = None
    source_ref: str | None = None
    source_label: str | None = None
    confidence: float | None = None
    review_note: str | None = None
    document_id: int | None = None
    sheet_id: int | None = None
    model_element_id: str | None = None
    sheet_number: str | None = None
    spec_section: str | None = None
    metadata: dict[str, Any] = Field(default_factory=dict)


class TradeReadinessSnapshotOut(ORMModel):
    id: int
    snapshot_id: int
    discipline: str
    score: float
    requirements_total: int
    requirements_covered: int
    missing_requirements: int
    needs_review: int
    open_issues: int
    critical_gaps: int


class ReadinessSnapshotOut(ORMModel):
    id: int
    project_id: int
    export_id: int | None = None
    overall_score: float
    label: str
    requirement_coverage_score: float
    qaqc_health_score: float
    sync_freshness_score: float
    gap_summary: dict[str, Any] | None = None
    created_at: datetime
    trade_readiness: list[TradeReadinessSnapshotOut] = Field(default_factory=list)


class ReadinessActionOut(ORMModel):
    id: int
    project_id: int
    requirement_id: int | None = None
    issue_id: int | None = None
    rule_code: str | None = None
    action_type: str
    title: str
    description: str | None = None
    status: ReadinessActionStatus
    priority: ReadinessActionPriority
    owner: str | None = None
    created_at: datetime
    updated_at: datetime
    persisted: bool = True
    source: str = "readiness_action"


class ReadinessActionUpdate(BaseModel):
    status: ReadinessActionStatus | None = None
    priority: ReadinessActionPriority | None = None
    owner: str | None = None
    description: str | None = None


# ---------------------------------------------------------------------------
# SEION-KGE advisory suggestions
# ---------------------------------------------------------------------------


class SeionGraphExportOut(BaseModel):
    advisory: bool = True
    entity_count: int
    triple_count: int
    entities_path: str
    triples_path: str
    warnings: list[str] = Field(default_factory=list)


class SeionPredictionCreate(BaseModel):
    project_id: int | None = None
    head_uid: str = Field(..., min_length=1)
    relation: str = Field(..., min_length=1)
    tail_uid: str = Field(..., min_length=1)
    score: float = Field(..., ge=0)
    rank: int | None = Field(default=None, ge=1)
    model_version: str = Field(..., min_length=1)
    source: str = "seion_kge"
    metadata: dict[str, Any] = Field(default_factory=dict)


class SeionPredictionReviewUpdate(BaseModel):
    reviewer_note: str | None = None
    accepted_by: str | None = None


class SeionPredictionOut(ORMModel):
    id: int
    project_id: int | None = None
    head_uid: str
    relation: str
    tail_uid: str
    score: float
    rank: int | None = None
    model_version: str
    status: SeionPredictionStatus
    source: str
    reviewer_note: str | None = None
    accepted_by: str | None = None
    accepted_at: datetime | None = None
    metadata_json: dict[str, Any] = Field(default_factory=dict, serialization_alias="metadata")
    created_at: datetime
    updated_at: datetime | None = None
    advisory: bool = True


class SeionPredictionImportRequest(BaseModel):
    path: str = Field(..., min_length=1)
    project_id: int | None = None


class SeionPredictionImportOut(BaseModel):
    inserted_count: int
    skipped_count: int
    warnings: list[str] = Field(default_factory=list)
    advisory: bool = True


# ---------------------------------------------------------------------------
# Dev mode
# ---------------------------------------------------------------------------


class DevStatusCounts(BaseModel):
    projects: int = 0
    exports: int = 0
    issues: int = 0
    high_issues: int = 0
    critical_issues: int = 0
    documents: int = 0
    specifications: int = 0
    drawings: int = 0
    actions: int = 0
    snapshots: int = 0


class DevStatusOut(BaseModel):
    status: str
    backend_health: str = "ok"
    database_health: str = "ok"
    version: str
    app_version: str | None = None
    api_contract_version: str | None = None
    selected_project_id: int | None = None
    selected_project_name: str | None = None
    selected_project_folder: str | None = None
    last_sync_at: datetime | None = None
    requirements_state: str | None = None
    readiness_available: bool = False
    counts: DevStatusCounts = Field(default_factory=DevStatusCounts)
    warnings: list[str] = Field(default_factory=list)
    endpoint_availability: dict[str, bool] = Field(default_factory=dict)
    landing_root_configured: bool = False
    default_project_folder: str | None = None


class DevSmokeEndpointResult(BaseModel):
    endpoint: str
    ok: bool
    detail: str


class DevSmokeTestOut(BaseModel):
    status: str
    checks: list[DevSmokeEndpointResult] = Field(default_factory=list)


class DocumentPreviewOut(BaseModel):
    document_id: int
    available: bool
    category: str | None = None
    parser_status: str | None = None
    message: str | None = None
    metadata: dict[str, Any] = Field(default_factory=dict)


class ComplianceGateOut(BaseModel):
    name: str
    passed: bool
    detail: str | None = None


class ComplianceCorpusOut(BaseModel):
    id: int
    name: str
    code_family: str
    edition: str | None = None
    jurisdiction: str | None = None
    source_type: str
    loader_status: str
    health_score: float | None = None
    gate_status: str = "unknown"
    notes: str | None = None
    created_at: datetime


class ComplianceRuleOut(BaseModel):
    id: int
    corpus_id: int
    reference: str
    title: str | None = None
    requirement_text: str
    discipline: str | None = None
    validation_type: Literal["deterministic", "semantic_review", "manual_review", "hybrid"] = "semantic_review"
    status: Literal["candidate", "active", "superseded", "rejected"] = "candidate"
    review_status: str = "pending"
    source_document: str | None = None
    created_at: datetime


class ComplianceLoaderPreviewRequest(BaseModel):
    name: str = "Local Compliance Corpus"
    code_family: str = "GENERIC"
    edition: str | None = None
    jurisdiction: str | None = None
    source_type: str = "local"
    blocks_path: str | None = None
    edges_path: str | None = None
    structure_audit_path: str | None = None
    gates_path: str | None = None
    override_review_required: bool = False


class ComplianceLoaderPreviewOut(BaseModel):
    status: str
    code_family: str
    blocks_count: int = 0
    edges_count: int = 0
    health_score: float | None = None
    gate_status: str = "unknown"
    failed_gates: list[ComplianceGateOut] = Field(default_factory=list)
    sample_references: list[str] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)


class ComplianceImportOut(BaseModel):
    status: str
    corpus: ComplianceCorpusOut
    rules_created: int
    references_created: int
    review_required: bool


class ComplianceStatusOut(BaseModel):
    status: str
    corpora_count: int = 0
    candidate_rules: int = 0
    active_rules: int = 0
    findings_count: int = 0
    latest_loader_run_at: datetime | None = None


# ---------------------------------------------------------------------------
# Requirement Audit & Evaluation Bundle v1
# ---------------------------------------------------------------------------


ReviewAction = Literal["accept", "reject", "override", "request_changes", "lock", "supersede"]


class EvaluationBundleIngestIn(BaseModel):
    """The Evaluation Bundle produced by the C# engine, posted for ingest.

    Accepts the engine's camelCase object ({manifest, auditRecords, coherence}).
    Records and coherence are kept loosely typed so the ingest boundary tolerates
    forward-compatible additions to the bundle contract.
    """

    model_config = ConfigDict(populate_by_name=True, extra="ignore")

    manifest: dict[str, Any]
    audit_records: list[dict[str, Any]] = Field(default_factory=list)
    coherence: dict[str, Any] = Field(default_factory=dict)
    export_id: int | None = None
    source_file_id: int | None = None

    @model_validator(mode="before")
    @classmethod
    def _accept_engine_camelcase(cls, data: Any) -> Any:
        # The C# engine emits camelCase "auditRecords". A Field(validation_alias=...)
        # is silently dropped in the FastAPI request-body schema-build context
        # (pydantic UnsupportedFieldAttributeWarning), so normalize here instead —
        # otherwise the real bundle's records are ignored and ingest stores nothing.
        if isinstance(data, dict) and "auditRecords" in data and "audit_records" not in data:
            data = {**data, "audit_records": data["auditRecords"]}
        return data


class RequirementAuditRunOut(ORMModel):
    id: int
    project_id: int
    export_id: int | None = None
    source_file_id: int | None = None
    run_uid: str
    run_status: str
    as_of: datetime
    schema_version: str
    engine_version: str | None = None
    ruleset_version: str | None = None
    taxonomy_version: str | None = None
    score_policy_version: str | None = None
    input_hash: str | None = None
    output_hash: str | None = None
    project_name: str | None = None
    model_name: str | None = None
    requirements_file: str | None = None
    requirements_total: int
    status_counts: dict[str, Any] = Field(default_factory=dict)
    coherence_grade: str | None = None
    coherence_findings_total: int
    ingested_at: datetime


class RequirementReviewDecisionOut(ORMModel):
    id: int
    audit_record_id: int
    reviewer_user_id: int | None = None
    reviewer_name: str | None = None
    action: ReviewAction
    previous_status: str | None = None
    resulting_status: str | None = None
    reason: str
    created_at: datetime


class RequirementReviewDecisionCreate(BaseModel):
    action: ReviewAction
    reason: str = Field(..., min_length=1)
    reviewer_name: str | None = None
    reviewer_user_id: int | None = None
    resulting_status: str | None = None


class RequirementAuditRecordOut(ORMModel):
    id: int
    run_id: int
    requirement_id: int | None = None
    requirement_uid: str | None = None
    requirement_content_hash: str | None = None
    decision_status: str
    lifecycle_status: str
    requirement_type: str | None = None
    validation_type: str | None = None
    applies: bool
    rule_applied: str | None = None
    decision_reason: str | None = None
    confidence: float | None = None
    direct_evidence_count: int
    supporting_evidence_count: int
    source_provenance: dict[str, Any] = Field(default_factory=dict)
    semantic_ir: dict[str, Any] = Field(default_factory=dict)
    evidence_policy: dict[str, Any] = Field(default_factory=dict)
    candidate_funnel: dict[str, Any] = Field(default_factory=dict)
    coherence_finding_ids: list[Any] = Field(default_factory=list)
    next_best_action: str | None = None
    record_hash: str | None = None
    created_at: datetime
    review_decisions: list[RequirementReviewDecisionOut] = Field(default_factory=list)


class RequirementCoherenceFindingOut(ORMModel):
    id: int
    run_id: int
    finding_uid: str
    finding_type: str
    severity: str
    requirement_type: str | None = None
    status: str
    rationale: str | None = None
    primary_requirement: dict[str, Any] = Field(default_factory=dict)
    related_requirement: dict[str, Any] | None = None
    normalized_values: dict[str, Any] = Field(default_factory=dict)
    created_at: datetime


class RequirementAuditIngestResult(BaseModel):
    run: RequirementAuditRunOut
    records_ingested: int
    coherence_findings_ingested: int
    requirements_linked: int
    reused_existing: bool = False
