import type {
  Client,
  ComplianceCorpus,
  ComplianceImportResult,
  ComplianceLoaderPreview,
  ComplianceRule,
  ComplianceStatus,
  DevSmokeTest,
  DevStatus,
  DebugLog,
  DebugLogSummary,
  DocumentPreview,
  ExportRecord,
  IssueDetail,
  IssueListResponse,
  LandingDocument,
  LandingDiscoverResult,
  LandingIngestAllRequest,
  LandingIngestAllResponse,
  LandingManifestBatchRequest,
  LandingManifestBatchResponse,
  LandingProjectBindRequest,
  LandingProjectBindResponse,
  LandingProjectDiscoveryResponse,
  LandingProjectBootstrapInput,
  LandingProjectBootstrapResult,
  LandingStatus,
  ModelHealth,
  ProjectCreateInput,
  ProjectFileRegisterInput,
  ProjectFileUploadResult,
  ProjectModelCreateInput,
  ProjectModelSummary,
  ProjectOperationResult,
  ProjectReadiness,
  ProjectComplianceMatrix,
  ProjectRequirementsResponse,
  ProjectSummary,
  ReadinessAction,
  ReadinessSnapshot,
  RequirementEvidence,
  RequirementEvidenceCreateInput,
  RequirementComplianceUpdateInput,
  RequirementMappingUpdateInput,
  RequirementEvidenceUpdateInput,
  RequirementListResponse,
  RequirementAuditRun,
  RequirementAuditRecord,
  RequirementCoherenceFinding,
  RequirementReviewDecision,
  SeionPrediction,
  SyncLog,
  LoginResponse,
} from "../types";

// Production/staging MUST set VITE_API_BASE_URL to an HTTPS endpoint: the bearer
// token attached below must never travel over plain HTTP to a public host. The
// fallback is a safe local-dev default only (was a public http:// demo host).
export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") ||
  "http://localhost:8010";

// Keys shared with AuthContext, which owns writing/clearing the session.
const AUTH_TOKEN_KEY = "ema_token";
const AUTH_USER_KEY = "ema_user";

function readToken(): string | null {
  if (typeof window === "undefined") return null;
  return (
    window.localStorage.getItem(AUTH_TOKEN_KEY) ||
    window.sessionStorage.getItem(AUTH_TOKEN_KEY)
  );
}

function clearStoredSession(): void {
  if (typeof window === "undefined") return;
  for (const store of [window.localStorage, window.sessionStorage]) {
    store.removeItem(AUTH_TOKEN_KEY);
    store.removeItem(AUTH_USER_KEY);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const isFormData =
    typeof FormData !== "undefined" && init?.body instanceof FormData;

  const token = readToken();
  const headers: Record<string, string> = {
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    // Let the browser set the multipart Content-Type (with boundary) for FormData.
    ...(!isFormData && init?.body ? { "Content-Type": "application/json" } : {}),
    ...((init?.headers as Record<string, string> | undefined) ?? {}),
  };

  const response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers });
  if (!response.ok) {
    // Session expired/invalid -> clear stored auth so the app routes back to
    // login. Skip the login call itself, where 401 just means bad credentials.
    if (response.status === 401 && path !== "/api/v1/auth/login") {
      clearStoredSession();
    }
    throw new Error(`${response.status} ${response.statusText}`);
  }
  return response.json() as Promise<T>;
}

export const api = {
  login: (payload: Record<string, string>) =>
    request<LoginResponse>("/api/v1/auth/login", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  health: () =>
    request<{ status: string; database: string; version: string }>("/health"),
  listProjects: () => request<ProjectSummary[]>("/api/v1/projects"),
  createProject: (payload: ProjectCreateInput) =>
    request<ProjectSummary>("/api/v1/projects", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  projects: () => request<ProjectSummary[]>("/api/v1/projects"),
  getProject: (projectId: number) =>
    request<ProjectSummary>(`/api/v1/projects/${projectId}`),
  updateProject: (projectId: number, payload: Record<string, unknown>) =>
    request<ProjectSummary>(`/api/v1/projects/${projectId}`, {
      method: "PATCH",
      body: JSON.stringify(payload),
    }),
  listExports: () => request<ExportRecord[]>("/api/v1/exports"),
  exports: () => request<ExportRecord[]>("/api/v1/exports"),
  listIssues: (params?: URLSearchParams) =>
    request<IssueListResponse>(
      `/api/v1/issues${params ? `?${params.toString()}` : "?page_size=500"}`,
    ),
  issues: (projectId?: number) => {
    const query = projectId
      ? `?project_id=${projectId}&page_size=500`
      : "?page_size=500";
    return request<IssueListResponse>(`/api/v1/issues${query}`);
  },
  issuePage: (params: URLSearchParams) =>
    request<IssueListResponse>(`/api/v1/issues?${params.toString()}`),
  getIssue: (issueId: number) =>
    request<IssueDetail>(`/api/v1/issues/${issueId}`),
  updateIssue: (
    issueId: number,
    payload: { status?: string; resolution_notes?: string },
  ) =>
    request(`/api/v1/issues/${issueId}`, {
      method: "PATCH",
      body: JSON.stringify(payload),
    }),
  listClients: () => request<Client[]>("/api/v1/clients"),
  clients: () => request<Client[]>("/api/v1/clients"),
  getClientRequirements: (clientId: number, filters?: URLSearchParams) =>
    request<RequirementListResponse>(
      `/api/v1/clients/${clientId}/requirements${filters ? `?${filters.toString()}` : "?page_size=100"}`,
    ),
  requirements: (clientId: number) =>
    request<RequirementListResponse>(
      `/api/v1/clients/${clientId}/requirements?page_size=100`,
    ),
  getProjectReadiness: (projectId: number) =>
    request<ProjectReadiness>(`/api/v1/projects/${projectId}/readiness`),
  readiness: (projectId: number) =>
    request<ProjectReadiness>(`/api/v1/projects/${projectId}/readiness`),
  recalculateReadiness: (projectId: number) =>
    request<ReadinessSnapshot>(
      `/api/v1/projects/${projectId}/readiness/recalculate`,
      { method: "POST" },
    ),
  listReadinessSnapshots: (projectId: number) =>
    request<ReadinessSnapshot[]>(
      `/api/v1/projects/${projectId}/readiness/snapshots`,
    ),
  readinessSnapshots: (projectId: number) =>
    request<ReadinessSnapshot[]>(
      `/api/v1/projects/${projectId}/readiness/snapshots`,
    ),
  listReadinessActions: (projectId: number) =>
    request<ReadinessAction[]>(
      `/api/v1/projects/${projectId}/readiness/actions`,
    ),
  readinessActions: (projectId: number) =>
    request<ReadinessAction[]>(
      `/api/v1/projects/${projectId}/readiness/actions`,
    ),
  updateReadinessAction: (actionId: number, payload: Record<string, unknown>) =>
    request<ReadinessAction>(`/api/v1/readiness/actions/${actionId}`, {
      method: "PATCH",
      body: JSON.stringify(payload),
    }),
  createReadinessSnapshot: (projectId: number) =>
    request<ReadinessSnapshot>(
      `/api/v1/projects/${projectId}/readiness/snapshots`,
      { method: "POST" },
    ),
  getProjectRequirements: (projectId: number, filters?: URLSearchParams) =>
    request<ProjectRequirementsResponse>(
      `/api/v1/projects/${projectId}/requirements${filters ? `?${filters.toString()}` : ""}`,
    ),
  projectRequirements: (projectId: number) =>
    request<ProjectRequirementsResponse>(
      `/api/v1/projects/${projectId}/requirements`,
    ),
  getProjectCompliance: (projectId: number, discipline?: string) =>
    request<ProjectComplianceMatrix>(
      `/api/v1/projects/${projectId}/compliance${discipline ? `?discipline=${encodeURIComponent(discipline)}` : ""}`,
    ),
  projectCompliance: (projectId: number) =>
    request<ProjectComplianceMatrix>(`/api/v1/projects/${projectId}/compliance`),
  updateRequirementCompliance: (
    projectId: number,
    requirementId: number,
    payload: RequirementComplianceUpdateInput,
  ) =>
    request(`/api/v1/projects/${projectId}/requirements/${requirementId}/compliance`, {
      method: "PUT",
      body: JSON.stringify(payload),
    }),
  updateRequirementMapping: (
    projectId: number,
    requirementId: number,
    payload: RequirementMappingUpdateInput,
  ) =>
    request(`/api/v1/projects/${projectId}/requirements/${requirementId}/mapping`, {
      method: "PATCH",
      body: JSON.stringify(payload),
    }),
  listProjectEvidence: (projectId: number, requirementId?: number) =>
    request<RequirementEvidence[]>(
      `/api/v1/projects/${projectId}/evidence${requirementId ? `?requirement_id=${requirementId}` : ""}`,
    ),
  listRequirementEvidence: (projectId: number, requirementId: number) =>
    request<RequirementEvidence[]>(
      `/api/v1/projects/${projectId}/requirements/${requirementId}/evidence`,
    ),
  createRequirementEvidence: (
    projectId: number,
    requirementId: number,
    payload: RequirementEvidenceCreateInput,
  ) =>
    request<RequirementEvidence>(
      `/api/v1/projects/${projectId}/requirements/${requirementId}/evidence`,
      {
        method: "POST",
        body: JSON.stringify(payload),
      },
    ),
  updateRequirementEvidence: (
    projectId: number,
    evidenceId: number,
    payload: RequirementEvidenceUpdateInput,
  ) =>
    request<RequirementEvidence>(
      `/api/v1/projects/${projectId}/evidence/${evidenceId}`,
      {
        method: "PATCH",
        body: JSON.stringify(payload),
      },
    ),
  bindProjectClient: (
    projectId: number,
    payload: {
      client_id?: number;
      client_code?: string;
      client_name?: string;
      current_milestone?: string;
    },
  ) =>
    request<{
      project: ProjectSummary;
      client: Client;
      created_client: boolean;
      message: string;
    }>(`/api/v1/projects/${projectId}/client`, {
      method: "PATCH",
      body: JSON.stringify(payload),
    }),
  createProjectModel: (projectId: number, payload: ProjectModelCreateInput) =>
    request<ProjectModelSummary>(`/api/v1/projects/${projectId}/models`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  configureProjectLanding: (
    projectId: number,
    payload: {
      landing_root: string;
      project_folder_name: string;
      create_folders?: boolean;
    },
  ) =>
    request<LandingStatus>(`/api/v1/projects/${projectId}/landing/configure`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  getProjectLandingStatus: (projectId: number) =>
    request<LandingStatus>(`/api/v1/projects/${projectId}/landing/status`),
  dedupeProjectLanding: (
    projectId: number,
    payload: {
      category?: string;
      dry_run?: boolean;
      delete_files?: boolean;
      rebuild_manifest?: boolean;
      prefer_clean_filename?: boolean;
    },
  ) =>
    request<{
      ok: boolean;
      operation: string;
      project_id: number;
      project_folder: string;
      category: string;
      dry_run: boolean;
      delete_files: boolean;
      kept: unknown[];
      deleted_documents: unknown[];
      deleted_files: unknown[];
      counts: { kept: number; deleted_documents: number; deleted_files: number };
      manifest_updated: boolean;
      manifest?: unknown;
    }>(`/api/v1/projects/${projectId}/landing/dedupe`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  deleteProjectLandingFile: (
    projectId: number,
    relativePath: string,
    options?: { delete_index?: boolean; rebuild_manifest?: boolean },
  ) => {
    const params = new URLSearchParams({
      relative_path: relativePath,
      delete_index: String(options?.delete_index ?? true),
      rebuild_manifest: String(options?.rebuild_manifest ?? true),
    });
    return request<{
      ok: boolean;
      operation: string;
      project_id: number;
      relative_path: string;
      deleted_file: boolean;
      deleted_index_rows: number;
      manifest_updated: boolean;
      manifest?: unknown;
    }>(`/api/v1/projects/${projectId}/landing/file?${params.toString()}`, {
      method: "DELETE",
    });
  },
  discoverLandingProjects: (landing_root: string) =>
    request<LandingDiscoverResult>("/api/v1/landing/projects/discover", {
      method: "POST",
      body: JSON.stringify({ landing_root }),
    }),
  getLandingProjects: () =>
    request<LandingProjectDiscoveryResponse>("/api/v1/landing/projects"),
  rebuildAllLandingManifests: (payload: LandingManifestBatchRequest) =>
    request<LandingManifestBatchResponse>(
      "/api/v1/landing/rebuild-all-manifests",
      {
        method: "POST",
        body: JSON.stringify(payload),
      },
    ),
  ingestAllLandingProjects: (payload: LandingIngestAllRequest) =>
    request<LandingIngestAllResponse>("/api/v1/landing/ingest-all", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  bindLandingProject: (
    projectFolder: string,
    payload: LandingProjectBindRequest,
  ) =>
    request<LandingProjectBindResponse>(
      `/api/v1/landing/projects/${encodeURIComponent(projectFolder)}/bind`,
      {
        method: "POST",
        body: JSON.stringify(payload),
      },
    ),
  bootstrapProjectFromFolder: (payload: LandingProjectBootstrapInput) =>
    request<LandingProjectBootstrapResult>(
      "/api/v1/landing/projects/bootstrap-from-folder",
      {
        method: "POST",
        body: JSON.stringify(payload),
      },
    ),
  registerProjectFiles: (
    projectId: number,
    payload: ProjectFileRegisterInput,
  ) =>
    request<ProjectOperationResult>(
      `/api/v1/projects/${projectId}/files/register`,
      {
        method: "POST",
        body: JSON.stringify(payload),
      },
    ),
  uploadProjectFiles: (
    projectId: number,
    intakeType: "owner_requirements" | "drawing" | "specification",
    files: File[],
  ) => {
    const form = new FormData();
    form.append("intake_type", intakeType);
    for (const file of files) {
      form.append("files", file);
    }
    return request<ProjectFileUploadResult>(
      `/api/v1/projects/${projectId}/files/upload`,
      {
        method: "POST",
        body: form,
      },
    );
  },
  landingScan: (payload: {
    project_folder: string;
    dry_run?: boolean;
    include_pdf_metadata?: boolean;
    update_manifest?: boolean;
  }) =>
    request<unknown>("/api/v1/landing/scan", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  scanLanding: (projectFolder: string, dryRun = true) =>
    request<unknown>("/api/v1/landing/scan", {
      method: "POST",
      body: JSON.stringify({
        project_folder: projectFolder,
        dry_run: dryRun,
        include_pdf_metadata: true,
      }),
    }),
  scanLandingForProject: (projectId: number) =>
    request<ProjectOperationResult>(
      `/api/v1/projects/${projectId}/landing/scan`,
      { method: "POST" },
    ),
  landingRebuildManifest: (payload: {
    project_folder: string;
    dry_run?: boolean;
    preserve_existing?: boolean;
    include_pdf_metadata?: boolean;
  }) =>
    request<unknown>("/api/v1/landing/rebuild-manifest", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  rebuildManifest: (projectFolder: string, dryRun = false) =>
    request<unknown>("/api/v1/landing/rebuild-manifest", {
      method: "POST",
      body: JSON.stringify({
        project_folder: projectFolder,
        dry_run: dryRun,
        preserve_existing: true,
      }),
    }),
  rebuildManifestForProject: (projectId: number) =>
    request<ProjectOperationResult>(
      `/api/v1/projects/${projectId}/landing/rebuild-manifest`,
      { method: "POST" },
    ),
  landingIngest: (payload: {
    manifest_path: string;
    dry_run?: boolean;
    recalculate_readiness?: boolean;
  }) =>
    request<unknown>("/api/v1/landing/ingest", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  ingestLanding: (manifestPath: string, dryRun = true) =>
    request<unknown>("/api/v1/landing/ingest", {
      method: "POST",
      body: JSON.stringify({
        manifest_path: manifestPath,
        dry_run: dryRun,
        recalculate_readiness: true,
      }),
    }),
  dryRunIngest: (projectId: number) =>
    request<ProjectOperationResult>(
      `/api/v1/projects/${projectId}/landing/ingest/dry-run`,
      { method: "POST" },
    ),
  realIngest: (projectId: number) =>
    request<ProjectOperationResult>(
      `/api/v1/projects/${projectId}/landing/ingest`,
      { method: "POST" },
    ),
  retrySync: (projectId: number) =>
    request<ProjectOperationResult>(
      `/api/v1/projects/${projectId}/landing/ingest`,
      { method: "POST" },
    ),
  listExportSyncLogs: (exportId: number) =>
    request<SyncLog[]>(`/api/v1/exports/${exportId}/sync-logs`),
  seionSuggestions: (projectId: number) =>
    request<SeionPrediction[]>(
      `/api/v1/projects/${projectId}/seion/suggestions?status=suggested&limit=25`,
    ),
  listSeionSuggestions: (projectId: number) =>
    request<SeionPrediction[]>(
      `/api/v1/projects/${projectId}/seion/suggestions?status=suggested&limit=25`,
    ),
  acceptSeionSuggestion: (predictionId: number) =>
    request<SeionPrediction>(
      `/api/v1/seion/suggestions/${predictionId}/accept`,
      {
        method: "POST",
        body: JSON.stringify({
          reviewer_note: "Accepted from advisory dashboard panel.",
        }),
      },
    ),
  rejectSeionSuggestion: (predictionId: number) =>
    request<SeionPrediction>(
      `/api/v1/seion/suggestions/${predictionId}/reject`,
      {
        method: "POST",
        body: JSON.stringify({
          reviewer_note: "Rejected from advisory dashboard panel.",
        }),
      },
    ),
  getDevStatus: () => request<DevStatus>("/api/v1/dev/status"),
  runDevSmokeTest: () =>
    request<DevSmokeTest>("/api/v1/dev/smoke-test", { method: "POST" }),
  syncLogs: (exportId: number) =>
    request<SyncLog[]>(`/api/v1/exports/${exportId}/sync-logs`),
  listExportSyncLogsLegacy: (exportId: number) =>
    request<SyncLog[]>(`/api/v1/exports/${exportId}/sync-logs`),
  modelHealth: (modelId: number) =>
    request<ModelHealth>(`/api/v1/models/${modelId}/health`),
  listProjectDocuments: (projectId: number, filters?: URLSearchParams) =>
    request<LandingDocument[]>(
      `/api/v1/projects/${projectId}/documents${filters ? `?${filters.toString()}` : ""}`,
    ),
  documents: (projectId: number) =>
    request<LandingDocument[]>(`/api/v1/projects/${projectId}/documents`),
  listProjectDrawings: (projectId: number, filters?: URLSearchParams) =>
    request<LandingDocument[]>(
      `/api/v1/projects/${projectId}/drawings${filters ? `?${filters.toString()}` : ""}`,
    ),
  drawings: (projectId: number) =>
    request<LandingDocument[]>(`/api/v1/projects/${projectId}/drawings`),
  listProjectSpecifications: (projectId: number, filters?: URLSearchParams) =>
    request<LandingDocument[]>(
      `/api/v1/projects/${projectId}/specifications${filters ? `?${filters.toString()}` : ""}`,
    ),
  specifications: (projectId: number) =>
    request<LandingDocument[]>(`/api/v1/projects/${projectId}/specifications`),
  getDocument: (documentId: number) =>
    request<LandingDocument>(`/api/v1/documents/${documentId}`),
  getProjectDocument: (projectId: number, documentId: number) =>
    request<LandingDocument>(
      `/api/v1/projects/${projectId}/documents/${documentId}`,
    ),
  getProjectDocumentMetadata: (projectId: number, documentId: number) =>
    request<LandingDocument>(
      `/api/v1/projects/${projectId}/documents/${documentId}/metadata`,
    ),
  getProjectDocumentPreview: (projectId: number, documentId: number) =>
    request<DocumentPreview>(
      `/api/v1/projects/${projectId}/documents/${documentId}/preview`,
    ),
  getProjectDocumentText: (projectId: number, documentId: number) =>
    request<{
      document_id: number;
      text_preview?: string | null;
      available: boolean;
      message?: string | null;
    }>(`/api/v1/projects/${projectId}/documents/${documentId}/text`),
  getProjectDocumentPdfUrl: (projectId: number, documentId: number) =>
    `${API_BASE_URL}/api/v1/projects/${projectId}/documents/${documentId}/pdf`,
  getDocumentPreview: (documentId: number) =>
    request<{
      document_id: number;
      text_preview?: string | null;
      available: boolean;
      message?: string | null;
    }>(`/api/v1/documents/${documentId}/text-preview`),
  listViewpoints: (projectId: number) =>
    request<LandingDocument[]>(`/api/v1/projects/${projectId}/viewpoints`),
  getViewpoint: (projectId: number, viewpointId: number) =>
    request<LandingDocument>(
      `/api/v1/projects/${projectId}/viewpoints/${viewpointId}`,
    ),
  getComplianceStatus: () =>
    request<ComplianceStatus>("/api/v1/compliance/status"),
  listComplianceCorpora: () =>
    request<ComplianceCorpus[]>("/api/v1/compliance/corpora"),
  getComplianceCorpus: (corpusId: number) =>
    request<ComplianceCorpus>(`/api/v1/compliance/corpora/${corpusId}`),
  listComplianceRules: (params?: URLSearchParams) =>
    request<ComplianceRule[]>(
      `/api/v1/compliance/rules${params ? `?${params.toString()}` : ""}`,
    ),
  previewNecCorpus: (payload: {
    name: string;
    code_family: string;
    edition?: string;
    jurisdiction?: string;
    source_type?: string;
    blocks_path?: string;
    edges_path?: string;
    structure_audit_path?: string;
    gates_path?: string;
    override_review_required?: boolean;
  }) =>
    request<ComplianceLoaderPreview>("/api/v1/compliance/corpora/nec/preview", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  importNecCorpus: (payload: {
    name: string;
    code_family: string;
    edition?: string;
    jurisdiction?: string;
    source_type?: string;
    blocks_path?: string;
    edges_path?: string;
    structure_audit_path?: string;
    gates_path?: string;
    override_review_required?: boolean;
  }) =>
    request<ComplianceImportResult>("/api/v1/compliance/corpora/nec/import", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  getDebugLogs: (params?: URLSearchParams) =>
    request<{
      items: DebugLog[];
      count: number;
      limit: number;
      offset: number;
    }>(`/api/v1/debug/logs${params ? `?${params.toString()}` : ""}`),
  getDebugLog: (logId: number) =>
    request<DebugLog>(`/api/v1/debug/logs/${logId}`),
  getDebugLogsSummary: () =>
    request<DebugLogSummary>("/api/v1/debug/logs/summary"),
  postFrontendLog: (payload: Record<string, unknown>) =>
    request<{
      ok: boolean;
      log_id: number;
      request_id: string;
      run_id: string;
    }>("/api/v1/debug/logs/frontend", {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  getDebugEnvironment: () =>
    request<Record<string, unknown>>("/api/v1/debug/environment"),
  getDebugPipelineState: (projectId?: number) =>
    request<Record<string, unknown>>(
      `/api/v1/debug/pipeline-state${projectId ? `?project_id=${projectId}` : ""}`,
    ),
  getProjectDebugTimeline: (projectId: number) =>
    request<{ project_id: number; items: DebugLog[] }>(
      `/api/v1/debug/projects/${projectId}/timeline`,
    ),
  createDebugBundle: (projectId?: number) =>
    request<Record<string, unknown>>("/api/v1/debug/bundle", {
      method: "POST",
      body: JSON.stringify(projectId ? { project_id: projectId } : {}),
    }),
  resolveModelEvidence: (projectId: number) =>
    request<{
      project_id: number;
      state: string;
      latest_export_id: number | null;
      requirements_checked: number;
      requirements_with_candidates: number;
      candidate_evidence_created: number;
      candidate_evidence_updated: number;
      requirements_missing_model_evidence: number;
      review_required: number;
      warnings: string[];
      error?: string;
    }>(`/api/v1/projects/${projectId}/evidence/resolve-model`, {
      method: "POST",
    }),

  // Requirement Audit & Evaluation Bundle v1
  listRequirementAuditRuns: (projectId: number) =>
    request<RequirementAuditRun[]>(
      `/api/v1/projects/${projectId}/requirement-audits`,
    ),
  getRequirementAuditRun: (projectId: number, runId: number) =>
    request<RequirementAuditRun>(
      `/api/v1/projects/${projectId}/requirement-audits/${runId}`,
    ),
  listRequirementAuditRecords: (projectId: number, runId: number) =>
    request<RequirementAuditRecord[]>(
      `/api/v1/projects/${projectId}/requirement-audits/${runId}/records`,
    ),
  listRequirementCoherenceFindings: (projectId: number, runId: number) =>
    request<RequirementCoherenceFinding[]>(
      `/api/v1/projects/${projectId}/requirement-audits/${runId}/coherence`,
    ),
  createRequirementReviewDecision: (
    projectId: number,
    runId: number,
    recordId: number,
    payload: {
      action: string;
      reason: string;
      reviewer_name?: string;
      resulting_status?: string;
    },
  ) =>
    request<RequirementReviewDecision>(
      `/api/v1/projects/${projectId}/requirement-audits/${runId}/records/${recordId}/review`,
      { method: "POST", body: JSON.stringify(payload) },
    ),
};

export function fallbackReadiness(project: ProjectSummary): ProjectReadiness {
  const health = project.model_health_score ?? 0;
  const syncScore = project.last_sync_at ? 85 : 0;
  const requirementCoverage = 0;
  const overall = Math.round(
    requirementCoverage * 0.5 + health * 0.3 + syncScore * 0.2,
  );

  return {
    project_id: project.id,
    project_title: project.project_title,
    client_id: project.client_id,
    client_name: project.client_name,
    overall_readiness: overall,
    label: overall >= 75 ? "On Track" : overall >= 50 ? "At Risk" : "Critical",
    requirement_coverage: {
      score: requirementCoverage,
      label: "Critical",
      detail: project.client_id
        ? "No accepted evidence â€” readiness not yet available"
        : "No client linked",
    },
    qaqc_health: {
      score: health,
      label: health >= 75 ? "On Track" : health > 0 ? "At Risk" : "Critical",
      detail: `${project.open_issues} open issues`,
    },
    sync_freshness: {
      score: syncScore,
      label: syncScore ? "On Track" : "Critical",
      detail: project.last_sync_at
        ? "Latest sync available"
        : "No completed sync",
    },
    open_issues: {
      critical: project.critical_issues,
      high: project.high_issues,
      medium: project.medium_issues,
      low: project.low_issues,
    },
    latest_sync_at: project.last_sync_at,
    trade_readiness: [],
    gap_summary: {
      critical: project.critical_issues,
      high: project.high_issues,
      medium: project.medium_issues,
      low: project.low_issues,
    },
    top_gaps: [],
    recommended_actions: [],
  };
}


