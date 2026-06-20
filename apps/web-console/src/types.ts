export type Severity = "critical" | "high" | "medium" | "low";

export type ProjectSummary = {
  id: number;
  organization_id: number;
  client_id?: number | null;
  project_title: string;
  project_code?: string | null;
  project_name?: string | null;
  job_number?: string | null;
  revit_version?: string | null;
  phase?: string | null;
  client_name?: string | null;
  active_models: number;
  open_issues: number;
  critical_issues: number;
  high_issues: number;
  medium_issues: number;
  low_issues: number;
  model_health_score?: number | null;
  last_sync_at?: string | null;
};

export type ExportRecord = {
  id: number;
  project_id: number;
  model_id: number;
  export_type: string;
  file_name?: string | null;
  element_count?: number | null;
  status: string;
  started_at: string;
  completed_at?: string | null;
  duration_seconds?: number | null;
};

export type SyncLog = {
  id: number;
  export_id: number;
  step: string;
  status: string;
  started_at: string;
  completed_at?: string | null;
  duration_seconds?: number | null;
  message?: string | null;
};

export type Issue = {
  id: number;
  project_id: number;
  model_id: number;
  export_id: number;
  element_db_id?: number | null;
  element_unique_id?: string | null;
  rule_code?: string | null;
  issue_type?: string | null;
  severity: Severity;
  status: string;
  source: string;
  message?: string | null;
  traceability?: {
    observed_values?: Record<string, unknown>;
    rule_version?: string;
    check_timestamp?: string;
  } | null;
  created_at: string;
};

export type IssueListResponse = {
  total: number;
  page: number;
  page_size: number;
  items: Issue[];
};

export type IssueDetail = Issue & {
  project_title?: string | null;
  project_code?: string | null;
  element_category?: string | null;
  element_name?: string | null;
  element_family?: string | null;
  element_type?: string | null;
  element_level?: string | null;
  rule_name?: string | null;
  rule_description?: string | null;
};

export type ProjectRequirementCounts = {
  total: number;
  actionable: number;
  non_actionable: number;
  covered: number;
  missing: number;
  needs_review: number;
  not_applicable: number;
  evaluated: number;
  rejected: number;
  candidate_evidence_count: number;
  accepted_evidence_count: number;
  rejected_evidence_count: number;
  no_evidence_count: number;
  requirement_coverage_ratio: number;
  requirement_coverage_percent: number;
};

export type ProjectRequirementRow = {
  requirement_id: number;
  source: string;
  source_type: string;
  discipline: string;
  category?: string | null;
  milestone?: string | null;
  requirement_text: string;
  owner_status?: string | null;
  is_actionable: boolean;
  readiness_status: string;
  normalized_status: "no_evidence" | "candidate" | "needs_review" | "accepted" | "rejected" | "not_applicable";
  evidence_status?: string | null;
  evidence_review_status?: "candidate" | "accepted" | "rejected" | "needs_review" | "none" | string | null;
  evidence_source_label?: string | null;
  related_issue_id?: number | null;
  related_sheet?: string | null;
  updated_at?: string | null;
};

export type ProjectComplianceRow = {
  requirement_id: number;
  discipline: string;
  category?: string | null;
  milestone?: string | null;
  requirement_text: string;
  compliance_id?: number | null;
  status: "compliant" | "non_compliant" | "not_evaluated" | "not_applicable" | "needs_review";
  evaluated_by?: string | null;
  evaluated_at?: string | null;
  notes?: string | null;
  evidence_type?: string | null;
  evidence_status?: string | null;
  evidence_source?: string | null;
};

export type ProjectComplianceMatrix = {
  project_id: number;
  client_id?: number | null;
  total_requirements: number;
  by_status: Record<string, number>;
  items: ProjectComplianceRow[];
};

export type ProjectRequirementsResponse = {
  project_id: number;
  project_name?: string | null;
  client_id?: number | null;
  client_name?: string | null;
  state:
    | "no_client_linked"
    | "client_linked_no_requirements"
    | "requirements_loaded_no_evidence"
    | "evidence_candidates_pending"
    | "readiness_available"
    | "requirements_loaded"
    | "filtered_empty"
    | "backend_error";
  counts: ProjectRequirementCounts;
  by_discipline: Record<string, ProjectRequirementCounts>;
  by_source_type: Record<string, number>;
  by_owner_status: Record<string, number>;
  items: ProjectRequirementRow[];
  page?: number;
  page_size?: number;
  total?: number;
};

export type Client = {
  id: number;
  code: string;
  display_name: string;
};

export type Requirement = {
  id: number;
  client_id: number;
  discipline: string;
  category?: string | null;
  requirement_text: string;
  owner_status?: string | null;
  resource?: string | null;
  links?: string | null;
  modified_by?: string | null;
  date_updated?: string | null;
  is_active: boolean;
};

export type EvidenceReviewStatus = "candidate" | "accepted" | "rejected" | "needs_review" | "none";
export type EvidenceType = "model" | "sheet" | "spec" | "manual" | "hybrid";

export type RequirementEvidence = {
  id: number;
  project_id: number;
  requirement_id: number;
  evidence_type: EvidenceType;
  evidence_status: "covered" | "missing" | "needs_review" | "blocked" | "not_applicable";
  review_status: EvidenceReviewStatus;
  source_ref?: string | null;
  source_label?: string | null;
  element_unique_id?: string | null;
  model_element_id?: string | null;
  sheet_number?: string | null;
  sheet_id?: number | null;
  spec_section?: string | null;
  confidence?: number | null;
  review_note?: string | null;
  reviewed_by?: string | null;
  reviewed_at?: string | null;
  document_id?: number | null;
  metadata_json?: Record<string, unknown> | null;
  created_at: string;
  updated_at: string;
};

export type RequirementEvidenceCreateInput = {
  evidence_type?: EvidenceType;
  review_status?: EvidenceReviewStatus;
  source_ref?: string | null;
  source_label?: string | null;
  confidence?: number | null;
  review_note?: string | null;
  reviewed_by?: string | null;
  document_id?: number | null;
  sheet_id?: number | null;
  model_element_id?: string | null;
  sheet_number?: string | null;
  spec_section?: string | null;
  metadata?: Record<string, unknown>;
};

export type RequirementEvidenceUpdateInput = Partial<RequirementEvidenceCreateInput>;

export type RequirementComplianceUpdateInput = {
  status: "compliant" | "non_compliant" | "not_evaluated" | "not_applicable" | "needs_review";
  model_id?: number | null;
  evidence?: Record<string, unknown> | null;
  notes?: string | null;
  evaluated_by?: string | null;
};

export type RequirementMappingUpdateInput = {
  milestone?: string | null;
  discipline?: string | null;
  is_actionable?: boolean | null;
  notes?: string | null;
};

export type RequirementListResponse = {
  client_id: number;
  total: number;
  page: number;
  page_size: number;
  items: Requirement[];
};

export type ReadinessComponent = {
  score: number;
  label: string;
  detail: string;
};

export type TradeReadinessRow = {
  discipline: string;
  readiness: number;
  label: string;
  requirements_total: number;
  requirements_evaluated: number;
  missing_requirements: number;
  needs_review: number;
  critical_issues: number;
  high_issues: number;
  source?: string;
  official?: boolean;
  elements?: number;
  open_issues?: number;
  medium_issues?: number;
  low_issues?: number;
};

export type ReadinessGap = {
  rule_code: string;
  severity: Severity;
  status: string;
  message: string;
  requirement_id?: number | null;
  discipline?: string | null;
  milestone?: string | null;
  evidence_type?: string | null;
  readiness_impact: number;
  action_type?: string | null;
  evidence?: Record<string, unknown> | null;
};

export type ReadinessRecommendedAction = {
  action_type: string;
  label: string;
  detail: string;
  severity: Severity;
  rule_code: string;
  requirement_id?: number | null;
  discipline?: string | null;
};

export type ProjectReadiness = {
  project_id: number;
  project_title: string;
  client_id?: number | null;
  client_name?: string | null;
  overall_readiness: number;
  label: string;
  requirement_coverage: ReadinessComponent;
  qaqc_health: ReadinessComponent;
  sync_freshness: ReadinessComponent;
  open_issues: Record<Severity, number>;
  latest_export_id?: number | null;
  latest_sync_at?: string | null;
  trade_readiness: TradeReadinessRow[];
  gap_summary?: Partial<Record<Severity, number>>;
  top_gaps?: ReadinessGap[];
  recommended_actions?: ReadinessRecommendedAction[];
};

export type TradeReadinessSnapshot = {
  id: number;
  snapshot_id: number;
  discipline: string;
  score: number;
  requirements_total: number;
  requirements_covered: number;
  missing_requirements: number;
  needs_review: number;
  open_issues: number;
  critical_gaps: number;
};

export type ReadinessSnapshot = {
  id: number;
  project_id: number;
  export_id?: number | null;
  overall_score: number;
  label: string;
  requirement_coverage_score: number;
  qaqc_health_score: number;
  sync_freshness_score: number;
  gap_summary?: Record<string, number> | null;
  created_at: string;
  trade_readiness: TradeReadinessSnapshot[];
};

export type ReadinessAction = {
  id: number;
  project_id: number;
  requirement_id?: number | null;
  issue_id?: number | null;
  rule_code?: string | null;
  action_type: string;
  title: string;
  description?: string | null;
  status: "open" | "in_review" | "done" | "dismissed";
  priority: "low" | "medium" | "high" | "critical";
  owner?: string | null;
  created_at: string;
  updated_at: string;
  persisted?: boolean;
  source?: string;
};

export type ModelHealth = {
  model_id: number;
  total_elements: number;
  elements_by_category: Record<string, number>;
  elements_by_level: Record<string, number>;
  open_issues: number;
  critical_issues: number;
  high_issues: number;
  medium_issues: number;
  low_issues: number;
  model_health_score: number;
  last_export_id?: number | null;
  last_sync_at?: string | null;
};

export type ProjectCreateInput = {
  name: string;
  project_code?: string;
  project_number?: string;
  project_type?: string;
  client_id?: number | null;
  client_code?: string;
  client_name?: string;
  location?: string;
  current_milestone?: string;
  enabled_disciplines?: string[];
  landing_project_folder?: string;
  environment?: string;
  storage_mode?: string;
};

export type ProjectModelCreateInput = {
  model_name: string;
  model_type?: string;
  discipline?: string;
  revit_document_title?: string;
  revit_version?: string;
  source_system?: string;
};

export type ProjectModelSummary = {
  id: number;
  project_id: number;
  model_name: string;
  model_type?: string | null;
  discipline?: string | null;
  source_system?: string | null;
  created_at: string;
};

export type LandingConfigureInput = {
  landing_root: string;
  project_folder_name: string;
  create_folders?: boolean;
};

export type ProjectOperationResult = {
  ok: boolean;
  operation: string;
  project_id: number;
  project_name?: string | null;
  project_folder_name?: string | null;
  endpoint: string;
  dry_run?: boolean | null;
  counts: Record<string, number>;
  warnings: string[];
  errors: string[];
  next_actions: string[];
};

export type LandingStatus = ProjectOperationResult & {
  project_landing_path?: string | null;
  folder_status?: Record<string, boolean>;
  folder_found?: boolean;
  landing_root?: string | null;
  requested_folder?: string | null;
  available_folders?: string[];
  suggested_folder?: string | null;
};

export type LandingProjectDiscovery = {
  project_folder_name: string;
  has_manifest: boolean;
  latest_revit_export?: string | null;
  counts: Record<string, number>;
  warnings: string[];
};

export type LandingDiscoverResult = {
  ok: boolean;
  operation: string;
  endpoint: string;
  landing_root: string;
  projects: LandingProjectDiscovery[];
  warnings: string[];
  errors: string[];
};

export type LandingProjectBootstrapInput = {
  landing_root: string;
  project_folder_name: string;
  project_display_name?: string;
  project_code?: string;
  client_code?: string;
  client_name?: string;
  environment?: string;
};

export type LandingProjectBootstrapResult = {
  ok: boolean;
  operation: string;
  endpoint: string;
  project_id: number;
  client_id?: number | null;
  project_name?: string | null;
  project_folder_name: string;
  project_landing_path: string;
  discovered_files: number;
  landing_status: LandingStatus;
  warnings: string[];
  errors: string[];
  next_actions: string[];
};

export type LandingProjectStatus =
  | "ready"
  | "needs_manifest"
  | "needs_client_binding"
  | "partial"
  | "has_errors"
  | "empty";

export type LandingProjectCounts = {
  revit_exports: number;
  revit_meta: number;
  drawings: number;
  owner_requirements: number;
  specifications: number;
  docx: number;
  manifests: number;
  unknown: number;
};

export type LandingClientSuggestion = {
  client_name?: string | null;
  client_code?: string | null;
  source?: string | null;
  confidence: string;
};

export type LandingProjectSummary = {
  project_folder: string;
  project_name: string;
  project_id?: number | null;
  client_id?: number | null;
  client_name?: string | null;
  client_code?: string | null;
  manifest_exists: boolean;
  manifest_path?: string | null;
  counts: LandingProjectCounts;
  documents: Record<string, string[]>;
  client_suggestion?: LandingClientSuggestion | null;
  status: LandingProjectStatus;
  warnings: string[];
  errors: string[];
  next_action?: string | null;
};

export type LandingProjectDiscoveryResponse = {
  landing_root: string;
  project_count: number;
  totals: LandingProjectCounts;
  projects: LandingProjectSummary[];
};

export type LandingManifestBatchRequest = {
  dry_run?: boolean;
  preserve_existing?: boolean;
  infer_client_from_owner_requirements?: boolean;
  project_folders?: string[] | null;
};

export type LandingManifestBatchProjectResult = {
  project_folder: string;
  manifest_path?: string | null;
  would_write: boolean;
  file_count: number;
  counts: LandingProjectCounts;
  client_suggestion?: LandingClientSuggestion | null;
  warnings: string[];
  errors: string[];
};

export type LandingManifestBatchResponse = {
  dry_run: boolean;
  landing_root: string;
  project_count: number;
  updated: number;
  skipped: number;
  projects: LandingManifestBatchProjectResult[];
};

export type LandingIngestAllRequest = {
  dry_run?: boolean;
  project_folders?: string[] | null;
  require_client_for_owner_requirements?: boolean;
  create_snapshot?: boolean;
  preserve_existing?: boolean;
};

export type LandingBatchProjectResult = {
  project_folder: string;
  project_id?: number | null;
  status: string;
  counts: Record<string, number>;
  readiness: Record<string, unknown>;
  warnings: string[];
  errors: string[];
  next_action?: string | null;
};

export type LandingIngestAllResponse = {
  dry_run: boolean;
  project_count: number;
  processed: number;
  success: number;
  partial: number;
  failed: number;
  projects: LandingBatchProjectResult[];
};

export type LandingProjectBindRequest = {
  project_id?: number;
  client_id?: number;
  client_code?: string;
  client_name?: string;
  milestone?: string;
  project_name?: string;
  create_project?: boolean;
};

export type LandingProjectBindResponse = {
  project_folder: string;
  project_id: number;
  project_name: string;
  client_id?: number | null;
  client_name?: string | null;
  client_code?: string | null;
  status: LandingProjectStatus;
  warnings: string[];
  errors: string[];
  next_actions: string[];
};

export type ProjectFileRegisterInput = {
  files: Array<{
    relative_path: string;
    category?: string;
    discipline?: string;
  }>;
};

export type ProjectFileUploadResult = {
  ok: boolean;
  operation: string;
  project_id: number;
  project_name: string;
  project_folder_name: string;
  endpoint: string;
  intake_type: "owner_requirements" | "drawing" | "specification";
  target_folder: string;
  counts: {
    requested: number;
    uploaded: number;
    created: number;
    updated: number;
    failed: number;
  };
  warnings: string[];
  errors: string[];
  next_actions: string[];
};

export type ProjectBindingJson = {
  environment: string;
  api_base_url: string;
  dashboard_url: string;
  landing_root: string;
  project_folder_name: string;
  project_display_name: string;
  project_code?: string;
  client_code?: string;
  client_name?: string;
  project_id: number;
  client_id?: number | null;
  model_id?: number | null;
  current_milestone: string;
  use_landing_structure: boolean;
};

export type LandingDocument = {
  id: number;
  project_id?: number | null;
  client_id?: number | null;
  project_folder?: string | null;
  relative_path: string;
  file_name: string;
  file_ext: string;
  file_type: string;
  document_category?: string | null;
  discipline?: string | null;
  sheet_number?: string | null;
  sheet_title?: string | null;
  spec_section?: string | null;
  spec_title?: string | null;
  page_count?: number | null;
  file_size_bytes?: number | null;
  checksum_sha256?: string | null;
  source_system: string;
  ingestion_status: string;
  indexed_at: string;
  evidence_status: "candidate" | "official" | string;
  metadata_json?: Record<string, unknown> | null;
};

export type DocumentPreview = {
  document_id: number;
  available: boolean;
  category?: string | null;
  parser_status?: string | null;
  message?: string | null;
  metadata: Record<string, unknown>;
};

export type ComplianceGate = {
  name: string;
  passed: boolean;
  detail?: string | null;
};

export type ComplianceCorpus = {
  id: number;
  name: string;
  code_family: string;
  edition?: string | null;
  jurisdiction?: string | null;
  source_type: string;
  loader_status: string;
  health_score?: number | null;
  gate_status: string;
  notes?: string | null;
  created_at: string;
};

export type ComplianceRule = {
  id: number;
  corpus_id: number;
  reference: string;
  title?: string | null;
  requirement_text: string;
  discipline?: string | null;
  validation_type: "deterministic" | "semantic_review" | "manual_review" | "hybrid";
  status: "candidate" | "active" | "superseded" | "rejected";
  review_status: string;
  source_document?: string | null;
  created_at: string;
};

export type ComplianceLoaderPreview = {
  status: string;
  code_family: string;
  blocks_count: number;
  edges_count: number;
  health_score?: number | null;
  gate_status: string;
  failed_gates: ComplianceGate[];
  sample_references: string[];
  warnings: string[];
};

export type ComplianceImportResult = {
  status: string;
  corpus: ComplianceCorpus;
  rules_created: number;
  references_created: number;
  review_required: boolean;
};

export type ComplianceStatus = {
  status: string;
  corpora_count: number;
  candidate_rules: number;
  active_rules: number;
  findings_count: number;
  latest_loader_run_at?: string | null;
};

export type SeionPrediction = {
  id: number;
  project_id?: number | null;
  head_uid: string;
  relation: string;
  tail_uid: string;
  score: number;
  rank?: number | null;
  model_version: string;
  status: "suggested" | "accepted" | "rejected" | "stale" | "superseded";
  source: string;
  reviewer_note?: string | null;
  accepted_by?: string | null;
  accepted_at?: string | null;
  metadata?: Record<string, unknown>;
  created_at: string;
  updated_at?: string | null;
  advisory: boolean;
};

export type DevStatus = {
  status: string;
  backend_health?: string;
  database_health?: string;
  database?: string;
  version: string;
  app_version?: string | null;
  api_contract_version?: string | null;
  selected_project_id?: number | null;
  selected_project_name?: string | null;
  selected_project_folder?: string | null;
  last_sync_at?: string | null;
  requirements_state?: string | null;
  readiness_available: boolean;
  counts: {
    projects: number;
    exports: number;
    issues: number;
    high_issues: number;
    critical_issues?: number;
    documents: number;
    specifications: number;
    drawings: number;
    actions?: number;
    snapshots?: number;
  };
  warnings?: string[];
  notes?: string[];
  endpoint_availability?: Record<string, boolean>;
  landing_root_configured?: boolean;
  default_project_folder?: string | null;
};

export type DevSmokeTest = {
  status: string;
  checks: Array<{
    endpoint: string;
    ok: boolean;
    detail: string;
  }>;
};

export type DebugLog = {
  id: number;
  run_id?: string | null;
  request_id?: string | null;
  project_id?: number | null;
  project_name?: string | null;
  operation_type: string;
  operation_label?: string | null;
  source: string;
  endpoint?: string | null;
  method?: string | null;
  status: string;
  severity: string;
  started_at?: string | null;
  finished_at?: string | null;
  duration_ms?: number | null;
  counts_json: Record<string, unknown>;
  request_summary_json: Record<string, unknown>;
  response_summary_json: Record<string, unknown>;
  warnings_json: unknown[];
  errors_json: unknown[];
  environment_json: Record<string, unknown>;
  metadata_json: Record<string, unknown>;
};

export type DebugLogSummary = {
  total: number;
  errors: number;
  warnings: number;
  last_operation?: DebugLog | null;
  last_failed_operation?: DebugLog | null;
};

export type ExecutiveProjectStatus =
  | "historical"
  | "in_execution"
  | "on_track"
  | "behind"
  | "blocked"
  | "demo";

export type ProjectLocationSource =
  | "manual"
  | "client_provided"
  | "geocoded"
  | "synthetic_demo"
  | "unknown";

export type ProjectLocation = {
  city?: string;
  state?: string;
  country?: string;
  lat?: number;
  lng?: number;
  source: ProjectLocationSource;
  isSynthetic: boolean;
  label?: string;
};

export type ExecutiveProject = {
  id: number | string;
  name: string;
  clientName?: string;
  projectCode?: string;
  owner?: string;
  status: ExecutiveProjectStatus;
  currentMilestone?: string;
  nextMilestone?: string;
  readinessScore?: number;
  requirementCoverage?: number;
  evidenceCoverage?: number;
  modelHealth?: number;
  openIssues?: number;
  criticalIssues?: number;
  documentsIndexed?: number;
  lastSync?: string;
  location?: ProjectLocation;
  tags?: string[];
};

export type User = {
    id: number;
    name: string;
    email: string;
    role: string
    auth_provider: string;
    is_active: boolean;
    is_locked: boolean;
    failed_login_attempts: number;
    last_login_at: string;
    password_changed_at: string; 
    must_change_password: boolean;
    created_at: string;
    updated_at: string;
  }

export interface LoginResponse {
  message: string;
  access_token: string;
  token_type: string;
  expires_in: number;
  user: User;
}

// ---------------------------------------------------------------------------
// Requirement Audit & Evaluation Bundle v1
// ---------------------------------------------------------------------------

export interface RequirementAuditRun {
  id: number;
  project_id: number;
  export_id: number | null;
  source_file_id: number | null;
  run_uid: string;
  run_status: string;
  as_of: string;
  schema_version: string;
  engine_version: string | null;
  ruleset_version: string | null;
  taxonomy_version: string | null;
  score_policy_version: string | null;
  input_hash: string | null;
  output_hash: string | null;
  project_name: string | null;
  model_name: string | null;
  requirements_file: string | null;
  requirements_total: number;
  status_counts: Record<string, number>;
  coherence_grade: string | null;
  coherence_findings_total: number;
  ingested_at: string;
}

export interface RequirementReviewDecision {
  id: number;
  audit_record_id: number;
  reviewer_user_id: number | null;
  reviewer_name: string | null;
  action: string;
  previous_status: string | null;
  resulting_status: string | null;
  reason: string;
  created_at: string;
}

export interface RequirementAuditRecord {
  id: number;
  run_id: number;
  requirement_id: number | null;
  requirement_uid: string | null;
  requirement_content_hash: string | null;
  decision_status: string;
  lifecycle_status: string;
  requirement_type: string | null;
  validation_type: string | null;
  applies: boolean;
  rule_applied: string | null;
  decision_reason: string | null;
  confidence: number | null;
  direct_evidence_count: number;
  supporting_evidence_count: number;
  source_provenance: Record<string, unknown>;
  semantic_ir: Record<string, unknown>;
  evidence_policy: Record<string, unknown>;
  candidate_funnel: Record<string, unknown>;
  coherence_finding_ids: string[];
  next_best_action: string | null;
  record_hash: string | null;
  created_at: string;
  review_decisions: RequirementReviewDecision[];
}

export interface RequirementCoherenceFinding {
  id: number;
  run_id: number;
  finding_uid: string;
  finding_type: string;
  severity: string;
  requirement_type: string | null;
  status: string;
  rationale: string | null;
  primary_requirement: Record<string, unknown>;
  related_requirement: Record<string, unknown> | null;
  normalized_values: Record<string, unknown>;
  created_at: string;
}
