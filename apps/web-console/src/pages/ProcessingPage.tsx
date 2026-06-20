import {
  AlertTriangle,
  ArrowRight,
  Check,
  Database,
  FileJson,
  FileText,
  FolderOpen,
  Link2,
  Lock,
  MapPin,
  RefreshCw,
  Server,
  Shield,
  ShieldCheck,
  Upload,
  Eye,
} from "lucide-react";
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ComponentType,
} from "react";
import { api } from "../api/client";
import { BatchResultPanel } from "../components/BatchResultPanel";
import { ConfirmModal } from "../components/ConfirmModal";
import { DataFlowDiagram, buildDataFlowNodes } from "../components/DataFlowDiagram";
import { LandingRootOverview } from "../components/LandingRootOverview";
import { PipelineStepper } from "../components/PipelineStepper";
import { StatusBadge } from "../components/StatusBadge";
import { useDebugLogger } from "../hooks/useDebugLogger";
import { formatDateTime, formatNumber, parseDateValue } from "../lib/format";
import { PIPELINE_ORDER, getNextStep } from "../lib/pipelineState";
import type {
  ExportRecord,
  Issue,
  LandingDocument,
  LandingIngestAllResponse,
  LandingManifestBatchResponse,
  LandingStatus,
  ProjectSummary,
  ReadinessSnapshot,
  SyncLog,
} from "../types";

type ProcessingPageProps = {
  project?: ProjectSummary;
  latestExport?: ExportRecord;
  syncLogs: SyncLog[];
  issues: Issue[];
  documents: LandingDocument[];
  snapshots: ReadinessSnapshot[];
  onSnapshotCreated: (snapshot: ReadinessSnapshot) => void;
  onToast: (message: string, tone?: "success" | "info" | "warning") => void;
  onOpenDebugLogs?: () => void;
  onSelectProject?: (projectId: number) => void;
  onOpenDocuments?: () => void;
  onOpenRequirements?: () => void;
  onOpenProjectSetup?: () => void;
};

type OperationConfig = {
  label: string;
  icon: ComponentType<{ size?: number; "aria-hidden"?: boolean }>;
  tone: "secondary" | "primary" | "danger";
  write: boolean;
  requiresProject: boolean;
  group: "safe" | "preparation" | "write";
  nextSteps: Array<{ label: string; step: "validate" | "ingest" | "snapshot" }>;
  writeGuardLabel: string;
  writeGuardDescription: string;
};

type DataIntakeType = "owner_requirements" | "drawing" | "specification";

type LocalSelectedFile = {
  id: string;
  name: string;
  size: number;
  intakeType: DataIntakeType;
  selectedAt: string;
};

type FileBrowserRow = {
  id: string;
  fileName: string;
  type: string;
  status: string;
  modified: string;
  source: "Selected" | "Indexed";
};

type PipelineStepState =
  | "idle"
  | "queued"
  | "running"
  | "completed"
  | "partial"
  | "warning"
  | "failed"
  | "skipped"
  | "stale";

const PIPELINE_STORAGE_KEY = "ema-ai-processing-pipeline-state";

const ALL_OPERATIONS: OperationConfig[] = [
  {
    label: "Health check",
    icon: RefreshCw,
    tone: "secondary",
    write: false,
    requiresProject: false,
    group: "safe",
    nextSteps: [{ label: "Find files", step: "validate" }],
    writeGuardLabel: "",
    writeGuardDescription: "",
  },
  {
    label: "Landing Status",
    icon: FolderOpen,
    tone: "secondary",
    write: false,
    requiresProject: true,
    group: "safe",
    nextSteps: [{ label: "Find files", step: "validate" }],
    writeGuardLabel: "",
    writeGuardDescription: "",
  },
  {
    label: "Scan Landing",
    icon: FileJson,
    tone: "secondary",
    write: false,
    requiresProject: true,
    group: "safe",
    nextSteps: [{ label: "Build file list", step: "validate" }],
    writeGuardLabel: "",
    writeGuardDescription: "",
  },
  {
    label: "Rebuild Manifest",
    icon: Database,
    tone: "secondary",
    write: true,
    requiresProject: true,
    group: "preparation",
    nextSteps: [{ label: "Preview import", step: "ingest" }],
    writeGuardLabel: "Build file list?",
    writeGuardDescription:
      "This updates the landing manifest records. It is safe to retry and does not mark evidence as official.",
  },
  {
    label: "Dry Run Ingest",
    icon: AlertTriangle,
    tone: "secondary",
    write: false,
    requiresProject: true,
    group: "preparation",
    nextSteps: [{ label: "Import data", step: "ingest" }],
    writeGuardLabel: "",
    writeGuardDescription: "",
  },
  {
    label: "Run Ingest",
    icon: Check,
    tone: "danger",
    write: true,
    requiresProject: true,
    group: "write",
    nextSteps: [{ label: "Update dashboard", step: "snapshot" }],
    writeGuardLabel: "Import data into the dashboard?",
    writeGuardDescription:
      "This writes evidence candidates and imported records to PostgreSQL, then makes them available for readiness calculations.",
  },
  {
    label: "Create Snapshot",
    icon: ShieldCheck,
    tone: "primary",
    write: true,
    requiresProject: true,
    group: "write",
    nextSteps: [],
    writeGuardLabel: "Update dashboard snapshot?",
    writeGuardDescription:
      "This creates a read-only readiness snapshot for the selected project.",
  },
  {
    label: "Resolve Model Evidence",
    icon: Link2,
    tone: "primary",
    write: true,
    requiresProject: true,
    group: "write",
    nextSteps: [],
    writeGuardLabel: "Scan Revit export and create evidence candidates?",
    writeGuardDescription:
      "Scores elements from the latest Revit export against owner requirements and creates candidate evidence for reviewer acceptance. Candidates do not count as covered until accepted.",
  },
];

const OPERATION_GROUPS: Array<{
  key: "safe" | "preparation" | "write";
  label: string;
  description: string;
}> = [
  {
    key: "safe",
    label: "1. Check Files",
    description: "Confirm the project folder and detect available files.",
  },
  {
    key: "preparation",
    label: "2. Preview Sync",
    description: "Prepare the file list and preview what will be imported.",
  },
  {
    key: "write",
    label: "3. Update Dashboard",
    description: "Import data and refresh readiness results.",
  },
];

const DATA_INTAKE_TYPES: Array<{
  key: DataIntakeType;
  title: string;
  description: string;
  folder: string;
  accepted: string;
  acceptInput: string;
}> = [
  {
    key: "owner_requirements",
    title: "Owner Requirements",
    description: "Excel or CSV requirement sources.",
    folder: "Owner Requirements",
    accepted: ".xlsx, .xls, .csv",
    acceptInput: ".xlsx,.xls,.csv",
  },
  {
    key: "drawing",
    title: "Drawings",
    description: "PDF drawing sheets.",
    folder: "Drawings",
    accepted: ".pdf",
    acceptInput: ".pdf",
  },
  {
    key: "specification",
    title: "Specifications",
    description: "Specification PDFs or DOCX files.",
    folder: "Specifications",
    accepted: ".pdf, .docx",
    acceptInput: ".pdf,.docx",
  },
];

export function ProcessingPage({
  project,
  latestExport,
  syncLogs,
  issues,
  documents,
  snapshots,
  onSnapshotCreated,
  onToast,
  onOpenDebugLogs,
  onSelectProject,
  onOpenDocuments,
  onOpenRequirements,
  onOpenProjectSetup,
}: ProcessingPageProps) {
  const [confirmWrite, setConfirmWrite] = useState<OperationConfig | null>(null);
  const [apiResponse, setApiResponse] = useState<{
    operation: string;
    response: unknown;
    duration: number;
  } | null>(null);

  const [landingStatus, setLandingStatus] = useState<LandingStatus | null>(null);
  const [landingDiscovery, setLandingDiscovery] =
    useState<import("../types").LandingProjectDiscoveryResponse | null>(null);
  const [landingDiscoveryLoading, setLandingDiscoveryLoading] = useState(false);
  const [landingDiscoveryError, setLandingDiscoveryError] = useState<string | null>(null);
  const [landingBatchResult, setLandingBatchResult] =
    useState<LandingManifestBatchResponse | LandingIngestAllResponse | null>(null);

  const [landingConfirmOpen, setLandingConfirmOpen] = useState(false);
  const [rebuildAllConfirmOpen, setRebuildAllConfirmOpen] = useState(false);
  const [debugEnv, setDebugEnv] = useState<Record<string, unknown> | null>(null);
  const [sessionHistory, setSessionHistory] = useState<
    Array<{ operation: string; status: string; duration: number; timestamp: string }>
  >([]);
  const [writeGuardActive, setWriteGuardActive] = useState(true);

  const [heartbeatAt, setHeartbeatAt] = useState<string | null>(null);
  const [heartbeatError, setHeartbeatError] = useState<string | null>(null);

  const [activeIntakeType, setActiveIntakeType] =
    useState<DataIntakeType>("owner_requirements");
  const [localSelectedFiles, setLocalSelectedFiles] = useState<LocalSelectedFile[]>([]);

  const [pipelineState, setPipelineState] = useState<
    Record<string, { status: PipelineStepState; message: string | null }>
  >(() => {
    const initial: Record<string, { status: PipelineStepState; message: string | null }> = {};

    for (const step of PIPELINE_ORDER) {
      initial[step] = { status: "idle", message: null };
    }

    return initial;
  });

  const logAction = useDebugLogger();

  const safeOps = ALL_OPERATIONS.filter((operation) => operation.group === "safe");
  const prepOps = ALL_OPERATIONS.filter((operation) => operation.group === "preparation");
  const writeOps = ALL_OPERATIONS.filter((operation) => operation.group === "write");

  const isPipelineRunning = useMemo(
    () => Object.values(pipelineState).some((state) => state.status === "running"),
    [pipelineState],
  );

  const canRun = project != null;
  const localStorageKey = "ema-ai-selected-project-id";
  const selectedProjectSource = project ? "Project props / localStorage" : "None";

  const backendLandingDir = (debugEnv?.landing_dir as string) || "unknown";
  const filePathMode = (debugEnv?.file_path_mode as string) || "unknown";
  const isContainer = debugEnv?.container_hint === true;
  const pathMismatch = filePathMode === "container_path" && !isContainer;
  const pathMismatchWarning = pathMismatch
    ? "Backend uses container-style landing_dir (/app/landing). Windows host paths may be unreachable unless Docker volume is mapped correctly."
    : null;

  const selectedProjectFolder =
    project?.project_name || project?.project_title || "No project selected";

  const projectLandingPath =
    landingStatus?.project_landing_path ||
    (project ? `${backendLandingDir || "/app/landing"}/${selectedProjectFolder}` : "—");

  const documentCounts = useMemo(() => summarizeDocuments(documents), [documents]);

  const activeIntakeConfig =
    DATA_INTAKE_TYPES.find((item) => item.key === activeIntakeType) ||
    DATA_INTAKE_TYPES[0];

  const fileBrowserRows = useMemo<FileBrowserRow[]>(() => {
    const activeLabel = labelForIntakeType(activeIntakeType);

    const selectedRows = localSelectedFiles
      .filter((file) => file.intakeType === activeIntakeType)
      .map((file) => ({
        id: `selected-${file.id}`,
        fileName: file.name,
        type: labelForIntakeType(file.intakeType),
        status: "Selected only",
        modified: file.selectedAt,
        source: "Selected" as const,
      }));

    const indexedRows = documents
      .filter((document) => normalizeDocumentType(document) === activeLabel)
      .map((document) => ({
        id: `indexed-${document.id}`,
        fileName:
          document.file_name ||
          document.sheet_title ||
          document.spec_title ||
          "Unnamed file",
        type: normalizeDocumentType(document),
        status:
          document.evidence_status === "official"
            ? "Official"
            : document.evidence_status === "candidate"
              ? "Evidence Candidate"
              : "Indexed",
        modified: document.indexed_at || "",
        source: "Indexed" as const,
      }));

    return [...selectedRows, ...indexedRows].slice(0, 30);
  }, [documents, localSelectedFiles, activeIntakeType]);

  useEffect(() => {
    if (!project) return;

    try {
      const raw = window.localStorage.getItem(PIPELINE_STORAGE_KEY);
      if (!raw) return;

      const parsed = JSON.parse(raw) as {
        projectId?: number;
        pipelineState?: Record<string, { status: PipelineStepState; message: string | null }>;
        sessionHistory?: Array<{
          operation: string;
          status: string;
          duration: number;
          timestamp: string;
        }>;
        lastHeartbeatAt?: string;
      };

      if (parsed.projectId === project.id && parsed.pipelineState) {
        setPipelineState(parsed.pipelineState);
      }

      if (parsed.projectId === project.id && parsed.sessionHistory) {
        setSessionHistory(parsed.sessionHistory.slice(0, 20));
      }

      if (parsed.projectId === project.id && parsed.lastHeartbeatAt) {
        setHeartbeatAt(parsed.lastHeartbeatAt);
      }
    } catch {
      // Ignore invalid cached state.
    }
  }, [project]);

  useEffect(() => {
    if (!project) return;

    try {
      window.localStorage.setItem(
        PIPELINE_STORAGE_KEY,
        JSON.stringify({
          projectId: project.id,
          pipelineState,
          sessionHistory: sessionHistory.slice(0, 20),
          lastHeartbeatAt: heartbeatAt,
          lastOperationAt: apiResponse ? new Date().toISOString() : null,
          lastStatus: apiResponse?.operation ?? null,
          lastWarningsCount: Array.isArray(
            (apiResponse?.response as Record<string, unknown> | null)?.warnings,
          )
            ? ((apiResponse?.response as { warnings: unknown[] }).warnings.length || 0)
            : 0,
          lastErrorsCount: Array.isArray(
            (apiResponse?.response as Record<string, unknown> | null)?.errors,
          )
            ? ((apiResponse?.response as { errors: unknown[] }).errors.length || 0)
            : 0,
          lastSnapshotScore: snapshots[0]?.overall_score ?? null,
          lastDocumentsCount: documents.length,
          lastIssuesCount: issues.length,
        }),
      );
    } catch {
      // Ignore localStorage persistence failures.
    }
  }, [project, pipelineState, sessionHistory, heartbeatAt, apiResponse, snapshots, documents, issues]);

  useEffect(() => {
    if (!project) return;

    api.getProjectLandingStatus(project.id).then(setLandingStatus).catch(() => {});
    api.getDebugEnvironment()
      .then((env) => setDebugEnv(env as Record<string, unknown>))
      .catch(() => {});
  }, [project]);

  const refreshLandingDiscovery = useCallback(async () => {
    setLandingDiscoveryLoading(true);
    setLandingDiscoveryError(null);

    try {
      const response = await api.getLandingProjects();
      setLandingDiscovery(response);
    } catch (error) {
      setLandingDiscovery(null);
      setLandingDiscoveryError(error instanceof Error ? error.message : String(error));
    } finally {
      setLandingDiscoveryLoading(false);
    }
  }, []);

  useEffect(() => {
    void refreshLandingDiscovery();
  }, [refreshLandingDiscovery]);

  const runLandingManifestDryRun = useCallback(async () => {
    try {
      const response = await api.rebuildAllLandingManifests({
        dry_run: true,
        preserve_existing: true,
        infer_client_from_owner_requirements: true,
      });

      setLandingBatchResult(response);
      void refreshLandingDiscovery();
      onToast(`File lists rebuilt in preview for ${response.project_count} project(s).`, "success");
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setLandingDiscoveryError(message);
      onToast(message, "warning");
    }
  }, [onToast, refreshLandingDiscovery]);

  const runLandingRebuildAll = useCallback(async () => {
    try {
      const response = await api.rebuildAllLandingManifests({
        dry_run: false,
        preserve_existing: true,
        infer_client_from_owner_requirements: true,
      });

      setLandingBatchResult(response);
      void refreshLandingDiscovery();
      onToast(`File lists rebuilt for ${response.project_count} project(s).`, "success");
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setLandingDiscoveryError(message);
      onToast(message, "warning");
    } finally {
      setRebuildAllConfirmOpen(false);
    }
  }, [onToast, refreshLandingDiscovery]);

  const runLandingIngestDryRun = useCallback(async () => {
    try {
      const response = await api.ingestAllLandingProjects({
        dry_run: true,
        project_folders: null,
        require_client_for_owner_requirements: true,
        create_snapshot: false,
        preserve_existing: true,
      });

      setLandingBatchResult(response);
      void refreshLandingDiscovery();
      onToast(`Import preview completed for ${response.project_count} project(s).`, "success");
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setLandingDiscoveryError(message);
      onToast(message, "warning");
    }
  }, [onToast, refreshLandingDiscovery]);

  const runLandingIngestAll = useCallback(async () => {
    try {
      const response = await api.ingestAllLandingProjects({
        dry_run: false,
        project_folders: null,
        require_client_for_owner_requirements: true,
        create_snapshot: false,
        preserve_existing: true,
      });

      setLandingBatchResult(response);
      void refreshLandingDiscovery();
      onToast(
        `Imported ${response.success} project(s), ${response.partial} partial, ${response.failed} failed.`,
        "success",
      );
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setLandingDiscoveryError(message);
      onToast(message, "warning");
    } finally {
      setLandingConfirmOpen(false);
    }
  }, [onToast, refreshLandingDiscovery]);

  useEffect(() => {
    if (!project) return;

    let mounted = true;

    const tick = async () => {
      try {
        await Promise.all([
          api.getProjectLandingStatus(project.id),
          api.getDebugPipelineState(project.id),
          api.health(),
        ]);

        if (!mounted) return;

        setHeartbeatError(null);
        const now = new Date().toISOString();
        setHeartbeatAt(now);

        setPipelineState((previous) => {
          const next = { ...previous };
          const hasRunning = Object.values(next).some((row) => row.status === "running");

          if (!hasRunning) {
            const lastDone = [...PIPELINE_ORDER]
              .reverse()
              .find((step) => next[step]?.status === "completed");

            if (lastDone && next[lastDone]) {
              next[lastDone] = {
                ...next[lastDone],
                status: "stale",
                message: "System check complete",
              };
            }
          }

          return next;
        });
      } catch (error) {
        if (!mounted) return;
        setHeartbeatError(error instanceof Error ? error.message : String(error));
      }
    };

    void tick();

    const timer = window.setInterval(tick, 30000);

    return () => {
      mounted = false;
      window.clearInterval(timer);
    };
  }, [project]);

  const runOperation = async (
    label: string,
    fn: () => Promise<unknown>,
    operation: OperationConfig,
  ) => {
    const started = Date.now();

    setApiResponse({ operation: friendlyOperationLabel(label), response: null, duration: 0 });

    try {
      setPipelineState((previous) => {
        const next = { ...previous };

        for (const key of PIPELINE_ORDER) {
          if (next[key].status === "idle") {
            next[key] = { status: "running", message: "In progress..." };
            break;
          }
        }

        return next;
      });

      const response = await fn();
      const duration = Date.now() - started;

      setApiResponse({ operation: friendlyOperationLabel(label), response, duration });

      setPipelineState((previous) => {
        const next = { ...previous };

        for (const key of PIPELINE_ORDER) {
          if (next[key].status === "running") {
            next[key] = { status: "completed", message: "Done" };
          } else if (next[key].status === "idle") {
            break;
          }
        }

        return next;
      });

      setSessionHistory((previous) => [
        {
          operation: friendlyOperationLabel(label),
          status: "completed",
          duration,
          timestamp: new Date().toISOString(),
        },
        ...previous,
      ]);

      await logAction({
        action: label,
        route: "/processing",
        project_id: project?.id,
        project_name: project?.project_name || project?.project_title,
        status: "success",
        severity: "info",
        duration_ms: duration,
      });

      onToast(`${friendlyOperationLabel(label)} completed.`, "success");

      if (operation.nextSteps.length > 0) {
        onToast(`Next: ${operation.nextSteps[0].label}`, "info");
      }

      if (label === "Landing Status" && project) {
        api.getProjectLandingStatus(project.id).then(setLandingStatus).catch(() => {});
        api.getDebugEnvironment()
          .then((env) => setDebugEnv(env as Record<string, unknown>))
          .catch(() => {});
      }

      if (label === "Create Snapshot" && response && typeof response === "object" && "id" in response) {
        onSnapshotCreated(response as ReadinessSnapshot);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      const duration = Date.now() - started;

      setApiResponse({
        operation: friendlyOperationLabel(label),
        response: { error: message },
        duration,
      });

      setPipelineState((previous) => {
        const next = { ...previous };

        for (const key of PIPELINE_ORDER) {
          if (next[key].status === "running") {
            next[key] = { status: "failed", message: message.slice(0, 80) };
            break;
          } else if (next[key].status === "idle") {
            break;
          }
        }

        return next;
      });

      setSessionHistory((previous) => [
        {
          operation: friendlyOperationLabel(label),
          status: "failed",
          duration,
          timestamp: new Date().toISOString(),
        },
        ...previous,
      ]);

      await logAction({
        action: label,
        route: "/processing",
        project_id: project?.id,
        project_name: project?.project_name || project?.project_title,
        status: "failed",
        severity: "error",
        duration_ms: duration,
        error: message,
      });

      onToast(`${friendlyOperationLabel(label)} failed.`, "warning");
    }
  };

  async function handleLocalFileSelection(files: FileList | null) {
    if (!files || files.length === 0) return;

    const picked = Array.from(files);

    if (!project) {
      const selected = picked.map((file) => ({
        id: `${activeIntakeType}-${file.name}-${Date.now()}-${Math.random()}`,
        name: file.name,
        size: file.size,
        intakeType: activeIntakeType,
        selectedAt: new Date().toISOString(),
      }));

      setLocalSelectedFiles((current) => [...selected, ...current]);

      onToast(
        `${selected.length} file(s) selected locally. Select a project to upload files into landing folders.`,
        "info",
      );
      return;
    }

    try {
      const uploadResult = await api.uploadProjectFiles(project.id, activeIntakeType, picked);
      await api
        .getProjectLandingStatus(project.id)
        .then(setLandingStatus)
        .catch(() => {});

      const suffix = uploadResult.counts.failed > 0 ? " (with warnings)" : "";
      onToast(
        `${uploadResult.counts.uploaded}/${uploadResult.counts.requested} file(s) uploaded to ${uploadResult.target_folder}${suffix}.`,
        uploadResult.counts.failed > 0 ? "warning" : "success",
      );
      return;
    } catch (error) {
      const selected = picked.map((file) => ({
        id: `${activeIntakeType}-${file.name}-${Date.now()}-${Math.random()}`,
        name: file.name,
        size: file.size,
        intakeType: activeIntakeType,
        selectedAt: new Date().toISOString(),
      }));

      setLocalSelectedFiles((current) => [...selected, ...current]);

      onToast(
        `Upload failed. Files kept as local selection only: ${error instanceof Error ? error.message : String(error)}`,
        "warning",
      );
      return;
    }
  }

  return (
    <div className="ema-page ema-page-shell space-y-6">
      <section className="ema-liquid-section p-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h2 className="text-2xl font-semibold text-ink">Data Sync</h2>
            <p className="mt-1 max-w-4xl text-sm text-muted">
              Add files, review what was found, preview the sync, and update the readiness dashboard
              {project ? ` for ${project.project_name || project.project_title}` : ""}.
            </p>
          </div>

          <div className="ema-liquid-capsule px-3 py-2 text-xs">
            <div className="flex items-center gap-2 font-semibold text-ink">
              <span
                className={`inline-flex h-2.5 w-2.5 rounded-full ${
                  heartbeatError ? "bg-danger" : "bg-accent ema-anim-heartbeat"
                }`}
              />
              System Status
            </div>
            <div className="text-muted">
              {heartbeatAt ? `Last checked ${formatDateTime(heartbeatAt)}` : "Waiting for first check"}
            </div>
            <div className="text-[10px] font-semibold uppercase tracking-wide text-subtle">
              User controlled · No automatic import
            </div>
            {heartbeatError ? <div className="text-danger">Backend unavailable: {heartbeatError}</div> : null}
          </div>
        </div>

        <div className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <StatusTile label="Project" value={project?.project_name || project?.project_title || "—"} />
          <StatusTile label="Project ID" value={project?.id ?? "—"} mono />
          <StatusTile label="Client" value={project?.client_name || "No client linked"} />
          <StatusTile label="Protection" value={writeGuardActive ? "On" : "Off"} />
          <StatusTile label="Selection Source" value={selectedProjectSource} detail={`localStorage: ${localStorageKey}`} />
        </div>

        <div className="mt-4 flex items-center gap-2">
          <label className="flex items-center gap-2 text-sm text-muted">
            <input
              type="checkbox"
              checked={writeGuardActive}
              onChange={(event) => setWriteGuardActive(event.target.checked)}
              className="ema-checkbox h-4 w-4"
            />
            <span className="font-medium">Require confirmation before updating data</span>
          </label>
        </div>
      </section>

      <section className="ema-card p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div className="flex items-center gap-2">
              <Upload size={18} className="text-accent" />
              <h3 className="text-lg font-semibold text-ink">Data Intake / File Browser</h3>
            </div>
            <p className="mt-1 text-sm text-muted">
              Choose a file type, select files, and review what is already indexed for this project.
            </p>
          </div>

          <div className="ema-liquid-capsule px-3 py-2 text-xs text-muted">
            Folder: <span className="font-mono text-ink">{projectLandingPath}</span>
          </div>
        </div>

        <div className="mt-5 grid gap-3 md:grid-cols-3">
          {DATA_INTAKE_TYPES.map((item) => {
            const active = activeIntakeType === item.key;
            const count =
              item.key === "owner_requirements"
                ? documentCounts.ownerRequirements
                : item.key === "drawing"
                  ? documentCounts.drawings
                  : documentCounts.specifications;

            return (
              <button
                key={item.key}
                type="button"
                onClick={() => setActiveIntakeType(item.key)}
                className={`rounded-2xl border p-4 text-left transition hover:-translate-y-0.5 hover:shadow-md ${
                  active ? "border-accent/40 bg-accent/[0.04]" : "border-line bg-surface"
                }`}
              >
                <div className="flex items-start justify-between gap-3">
                  <FileText size={20} className={active ? "text-accent" : "text-muted"} />
                  <StatusBadge value={`${count} indexed`} />
                </div>
                <div className="mt-3 font-semibold text-ink">{item.title}</div>
                <div className="mt-1 text-sm text-muted">{item.description}</div>
                <div className="mt-3 text-xs text-subtle">
                  Folder: <span className="font-mono">{item.folder}</span>
                </div>
              </button>
            );
          })}
        </div>

        <div className="mt-5 rounded-2xl border border-line bg-surface-2 p-4">
          <div className="grid gap-4 lg:grid-cols-[1fr_auto]">
            <div>
              <div className="text-sm font-semibold text-ink">{activeIntakeConfig.title}</div>
              <p className="mt-1 text-sm text-muted">
                Accepted files: {activeIntakeConfig.accepted}. Target folder:{" "}
                <span className="font-mono">{activeIntakeConfig.folder}</span>.
              </p>
            </div>

            <label className="ema-btn-secondary inline-flex cursor-pointer items-center gap-2">
              <Upload size={15} />
              Select Files
              <input
                type="file"
                className="hidden"
                multiple
                accept={activeIntakeConfig.acceptInput}
                onChange={(event) => {
                  void handleLocalFileSelection(event.target.files);
                  event.target.value = "";
                }}
              />
            </label>
          </div>

          <div className="mt-3 rounded-lg border border-line bg-surface p-3 text-xs text-muted">

            {project
              ? <>Files are uploaded immediately to <span className="font-mono">{projectLandingPath}</span> when a project is selected. If upload fails,
            they remain listed as <strong>Selected only</strong>.</>
              : <>Select a project first. Without a project, selected files are queued locally and not uploaded.</>
            }

          </div>
        </div>

        <div className="mt-5">
          <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
            <div>
              <h4 className="font-semibold text-ink">
                {activeIntakeConfig.title} File Browser
              </h4>
              <p className="mt-1 text-sm text-muted">
                Showing only {activeIntakeConfig.title.toLowerCase()} for the selected project.
              </p>
            </div>

            <StatusBadge value={`${fileBrowserRows.length} visible`} />
          </div>

          <div className="overflow-x-auto rounded-xl border border-line">
            <table className="min-w-full divide-y divide-line text-sm">
              <thead className="bg-surface-2 text-left text-xs font-semibold uppercase tracking-wide text-muted">
                <tr>
                  <th className="px-4 py-3">File Name</th>
                  <th className="px-4 py-3">Type</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3">Modified / Selected</th>
                  <th className="px-4 py-3">Source</th>
                </tr>
              </thead>

              <tbody className="divide-y divide-line bg-surface">
                {fileBrowserRows.map((row) => (
                  <tr key={row.id}>
                    <td className="px-4 py-3 font-medium text-ink">{row.fileName}</td>
                    <td className="px-4 py-3 text-muted">{row.type}</td>
                    <td className="px-4 py-3">
                      <StatusBadge value={row.status} />
                    </td>
                    <td className="px-4 py-3 text-muted">
                      {row.modified ? formatDateTime(row.modified) : "—"}
                    </td>
                    <td className="px-4 py-3 text-muted">{row.source}</td>
                  </tr>
                ))}

                {fileBrowserRows.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-4 py-6 text-center text-muted">
                      No {activeIntakeConfig.title.toLowerCase()} found yet. Select files or scan the landing folder.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </section>

      <section className="ema-card p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h3 className="text-lg font-semibold text-ink">Landing Readiness Summary</h3>
            <p className="text-sm text-muted">
              Current landing root inventory before sync operations.
            </p>
          </div>

          <button
            type="button"
            className="ema-btn-secondary inline-flex items-center gap-2"
            onClick={refreshLandingDiscovery}
          >
            <RefreshCw size={15} />
            Refresh
          </button>
        </div>

        <div className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-6">
          <MetricTile label="Project Folders" value={landingDiscovery?.project_count ?? "—"} />
          <MetricTile
            label="Ready"
            value={landingDiscovery?.projects.filter((row) => row.status === "ready").length ?? "—"}
          />
          <MetricTile
            label="Needs Manifest"
            value={landingDiscovery?.projects.filter((row) => row.status === "needs_manifest").length ?? "—"}
          />
          <MetricTile label="Revit Exports" value={landingDiscovery?.totals?.revit_exports ?? "—"} />
          <MetricTile label="Drawings" value={landingDiscovery?.totals?.drawings ?? documentCounts.drawings} />
          <MetricTile label="Owner Req." value={landingDiscovery?.totals?.owner_requirements ?? documentCounts.ownerRequirements} />
        </div>

        {landingDiscovery?.projects?.some((row) => row.counts.owner_requirements > 0 && !row.client_id) && (
          <div className="ema-notice-warning mt-4 p-3 text-xs">
            One or more projects have owner requirement files detected on disk but no client binding.
            Bind the project to a client or add client_code to the manifest, then preview import again.
          </div>
        )}
      </section>

      <section className="ema-card p-5">
        <div className="flex items-center gap-2">
          <RefreshCw
            size={16}
            className={`text-accent ${isPipelineRunning ? "ema-anim-spin" : ""}`}
            aria-hidden
          />
          <h3 className="text-lg font-semibold text-ink">Sync Pipeline</h3>
        </div>
        <p className="mt-1 text-sm text-muted">
          Find files → Build file list → Preview import → Import data → Update dashboard.
        </p>

        <div className="mt-6 overflow-x-auto pb-2">
          <PipelineStepper
            steps={PIPELINE_ORDER}
            stepStates={pipelineState}
            className="min-w-[30rem]"
          />
        </div>

        <div className="mt-5">
          <DataFlowDiagram
            nodes={buildDataFlowNodes({
              scanDone: pipelineState.received?.status === "completed",
              manifestDone: pipelineState.validation?.status === "completed",
              dryRunDone: pipelineState.parsing?.status === "completed",
              ingestDone: pipelineState.qa_qc_checks?.status === "completed",
              snapshotDone: pipelineState.dashboard_update?.status === "completed",
              isRunning: isPipelineRunning,
              ingestHasError: pipelineState.qa_qc_checks?.status === "failed",
            })}
            animated
          />
        </div>
      </section>

      <section className="ema-card p-5">
        <h3 className="text-lg font-semibold text-ink">Sync Steps</h3>
        <p className="mt-1 text-sm text-muted">
          Follow these steps from left to right: check files, preview the sync, then update the dashboard.
        </p>

        <div className="mt-4 grid gap-4 lg:grid-cols-3">
          {OPERATION_GROUPS.map((group) => {
            const operations =
              group.key === "safe" ? safeOps : group.key === "preparation" ? prepOps : writeOps;

            return (
              <div key={group.key} className="rounded-2xl border border-line bg-surface p-4">
                <div className="flex items-center gap-2">
                  <Shield
                    size={16}
                    className={
                      group.key === "safe"
                        ? "text-success"
                        : group.key === "preparation"
                          ? "text-warning"
                          : "text-danger"
                    }
                  />
                  <span className="font-semibold text-ink">{group.label}</span>
                </div>

                <p className="mt-1 text-xs text-muted">{group.description}</p>

                <div className="mt-3 flex flex-col gap-2">
                  {operations.map((operation) => {
                    const disabled = (!canRun && operation.requiresProject) || isPipelineRunning;
                    const Icon = operation.icon;
                    const buttonClass =
                      operation.tone === "primary"
                        ? "ema-btn-primary"
                        : operation.tone === "danger"
                          ? "ema-btn-secondary border-danger text-danger"
                          : "ema-btn-secondary";

                    return (
                      <button
                        key={operation.label}
                        className={`${buttonClass} inline-flex items-center justify-between gap-2`}
                        type="button"
                        disabled={disabled}
                        onClick={() => {
                          if (disabled) return;

                          if (operation.write && writeGuardActive) {
                            setConfirmWrite(operation);
                            return;
                          }

                          void runOperation(
                            operation.label,
                            () => resolveOperationPromise(operation.label, project),
                            operation,
                          );
                        }}
                      >
                        <span className="inline-flex items-center gap-2">
                          <Icon size={16} aria-hidden />
                          {friendlyOperationLabel(operation.label)}
                        </span>

                        <span className="inline-flex items-center gap-1 opacity-70">
                          {operation.nextSteps.length > 0 ? <ArrowRight size={14} aria-hidden /> : null}
                          {operation.write ? <Lock size={12} aria-hidden /> : null}
                        </span>
                      </button>
                    );
                  })}
                </div>
              </div>
            );
          })}
        </div>

        {project == null && (
          <p className="mt-3 text-sm text-muted">
            Select a project to enable project-scoped operations.
          </p>
        )}

        {onOpenDebugLogs && (
          <button
            type="button"
            className="ema-btn-secondary mt-4"
            onClick={onOpenDebugLogs}
          >
            Open in Debug Logs
          </button>
        )}
      </section>

      {apiResponse && (
        <section data-no-glass className="ema-card p-5">
          <div className="flex items-center justify-between">
            <h3 className="text-lg font-semibold text-ink">Last Sync Result</h3>
            <span className="text-xs text-muted">{apiResponse.duration}ms</span>
          </div>

          <div className="mt-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            <div className="ema-card px-3 py-2 text-xs">
              <span className="font-semibold text-muted">Action:</span> {apiResponse.operation}
            </div>
            <div className="ema-card px-3 py-2 text-xs">
              <span className="font-semibold text-muted">Duration:</span> {apiResponse.duration}ms
            </div>
          </div>

          <details className="mt-3">
            <summary className="cursor-pointer text-sm font-semibold text-ink">Response JSON</summary>
            <pre className="ema-solid-json-surface mt-2 max-h-80 overflow-auto p-3 text-xs">
              {JSON.stringify(apiResponse.response, null, 2)}
            </pre>
          </details>
        </section>
      )}

      {landingBatchResult && (
        <BatchResultPanel
          result={landingBatchResult}
          onClose={() => setLandingBatchResult(null)}
        />
      )}

      {(() => {
        const lastCompleted = [...PIPELINE_ORDER]
          .reverse()
          .find((step) => pipelineState[step]?.status === "completed");

        const nextStep = getNextStep(lastCompleted ?? null);

        if (!nextStep) return null;

        const nextLabels = ALL_OPERATIONS.find((operation) =>
          operation.label.toLowerCase().includes(nextStep.split("_")[0]),
        )?.nextSteps.map((next) => next.label).join(", ");

        if (!nextLabels) return null;

        return (
          <section data-no-glass className="ema-card border-warning bg-warning-soft p-5">
            <h3 className="text-lg font-semibold text-warning">Recommended Next Step</h3>
            <p className="mt-2 text-sm text-warning">Proceed with: {nextLabels}.</p>
          </section>
        );
      })()}

      <section className="ema-card p-5">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-lg font-semibold text-ink">Landing Document Index</h3>
            <p className="text-sm text-muted">
              Indexed local files registered as evidence candidates.
            </p>
          </div>
          <FileText size={19} className="text-info" aria-hidden />
        </div>

        <div className="mt-5 grid gap-4 md:grid-cols-4">
          <MetricCard title="Indexed Documents" value={documents.length} detail="Local metadata records" />
          <MetricCard title="Drawings" value={documentCounts.drawings} detail="PDF sheets indexed" />
          <MetricCard title="Specifications" value={documentCounts.specifications} detail="PDF specs indexed" />
          <MetricCard title="Owner Requirements" value={documentCounts.ownerRequirements} detail="Excel source status" />
        </div>
      </section>

      <section className="ema-card p-5">
        <h3 className="text-lg font-semibold text-ink">Readiness Snapshots</h3>

        {snapshots.length === 0 ? (
          <p className="mt-2 text-sm text-muted">No snapshots yet. Create one after validation.</p>
        ) : (
          <div className="mt-4 space-y-2">
            {snapshots.slice(0, 5).map((snapshot) => (
              <div key={snapshot.id} className="ema-card flex items-center justify-between p-3 text-sm">
                <span className="text-muted">{formatDateTime(snapshot.created_at)}</span>
                <span className="font-semibold text-ink">
                  {Math.round(snapshot.overall_score)}% {snapshot.label}
                </span>
              </div>
            ))}
          </div>
        )}
      </section>

      <details className="ema-card p-5">
        <summary className="cursor-pointer text-lg font-semibold text-ink">
          Advanced Diagnostics
        </summary>

        <div className="mt-5 space-y-6">
          <section>
            <div className="flex items-center gap-2">
              <MapPin size={18} className="text-purple-500" />
              <h3 className="text-lg font-semibold text-ink">Environment & Path Mapping</h3>
            </div>

            <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <StatusTile label="Backend landing_dir" value={backendLandingDir} mono />
              <StatusTile label="Path mode" value={filePathMode} mono />
              <StatusTile label="Container detected" value={isContainer ? "Yes (inside Docker)" : "No (host)"} />
              <StatusTile label="Project folder" value={project?.project_name || "—"} />
            </div>

            {pathMismatchWarning && (
              <div className="ema-notice-warning mt-3 flex items-start gap-2 p-3">
                <AlertTriangle size={16} className="mt-0.5 shrink-0 text-warning" />
                <div>
                  <p className="text-sm font-semibold text-warning">Path mismatch detected</p>
                  <p className="mt-1 text-xs text-warning">{pathMismatchWarning}</p>
                </div>
              </div>
            )}
          </section>

          <section>
            <div className="flex items-center gap-2">
              <Server size={18} className="text-accent" />
              <h3 className="text-lg font-semibold text-ink">Landing Status</h3>
            </div>

            {!project ? (
              <p className="mt-3 text-sm text-muted">Select a project to view landing status.</p>
            ) : landingStatus ? (
              <LandingStatusDetails landingStatus={landingStatus} />
            ) : (
              <p className="mt-3 text-sm text-muted">Loading landing status...</p>
            )}
          </section>

          <section>
            <div className="flex items-center gap-2">
              <Eye size={18} className="text-indigo-500" />
              <h3 className="text-lg font-semibold text-ink">Latest Export</h3>
            </div>

            {latestExport ? (
              <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <StatusTile label="Filename" value={latestExport.file_name || "—"} mono />
                <div className="ema-card p-3">
                  <div className="text-xs font-semibold text-muted">Status</div>
                  <div className="mt-1">
                    <StatusBadge value={latestExport.status} />
                  </div>
                </div>
                <StatusTile label="Modified" value={formatDateTime(latestExport.completed_at) || "—"} />
                <StatusTile
                  label="Element Count"
                  value={latestExport.element_count ? formatNumber(latestExport.element_count) : "—"}
                />
              </div>
            ) : (
              <p className="mt-3 text-sm text-muted">No export data available.</p>
            )}
          </section>

          <section data-no-glass id="details" className="ema-card">
            <div className="border-b border-line px-5 py-4">
              <h3 className="text-lg font-semibold text-ink">Sync Step Details</h3>
            </div>

            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-line text-sm">
                <thead className="bg-surface-2 text-left text-xs font-semibold text-muted">
                  <tr>
                    <th className="px-4 py-3">Step</th>
                    <th className="px-4 py-3">Status</th>
                    <th className="px-4 py-3">Started</th>
                    <th className="px-4 py-3">Duration</th>
                    <th className="px-4 py-3">Message</th>
                  </tr>
                </thead>

                <tbody className="divide-y divide-line bg-surface">
                  {syncLogs.slice(0, 10).map((log) => {
                    const startedAt = parseDateValue(log.started_at);
                    const completedAt = parseDateValue(log.completed_at);
                    const elapsed =
                      startedAt && completedAt
                        ? `${((completedAt.getTime() - startedAt.getTime()) / 1000).toFixed(1)}s`
                        : null;

                    return (
                      <tr key={log.id}>
                        <td className="px-4 py-3 font-medium text-ink">{log.step}</td>
                        <td className="px-4 py-3">
                          <StatusBadge value={log.status} />
                        </td>
                        <td className="px-4 py-3 text-muted">{formatDateTime(log.started_at)}</td>
                        <td className="px-4 py-3 text-muted">{elapsed ?? "—"}</td>
                        <td className="px-4 py-3 text-muted">{log.message ?? "—"}</td>
                      </tr>
                    );
                  })}

                  {syncLogs.length === 0 && (
                    <tr>
                      <td colSpan={5} className="px-4 py-6 text-center text-muted">
                        No sync logs available.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>

          {sessionHistory.length > 0 && (
            <section data-no-glass className="ema-card p-5">
              <h3 className="text-lg font-semibold text-ink">Session History</h3>
              <div className="mt-3 overflow-x-auto">
                <table className="min-w-full divide-y divide-line text-sm">
                  <thead className="bg-surface-2 text-left text-xs font-semibold text-muted">
                    <tr>
                      <th className="px-4 py-3">Action</th>
                      <th className="px-4 py-3">Status</th>
                      <th className="px-4 py-3">Duration</th>
                      <th className="px-4 py-3">Timestamp</th>
                    </tr>
                  </thead>

                  <tbody className="divide-y divide-line bg-surface">
                    {sessionHistory.map((entry, index) => (
                      <tr key={`${entry.operation}-${entry.timestamp}-${index}`}>
                        <td className="px-4 py-3 font-medium text-ink">{entry.operation}</td>
                        <td className="px-4 py-3">
                          <StatusBadge value={entry.status === "completed" ? "completed" : "failed"} />
                        </td>
                        <td className="px-4 py-3 text-muted">{entry.duration}ms</td>
                        <td className="px-4 py-3 text-muted">{formatDateTime(entry.timestamp)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}

          <section>
            <LandingRootOverview
              discovery={landingDiscovery}
              loading={landingDiscoveryLoading}
              error={landingDiscoveryError}
              onRefresh={refreshLandingDiscovery}
              onRebuildDryRun={runLandingManifestDryRun}
              onRebuildAll={() => setRebuildAllConfirmOpen(true)}
              onIngestDryRun={runLandingIngestDryRun}
              onIngestAll={() => setLandingConfirmOpen(true)}
              onSelectProject={(landingProject) => {
                if (landingProject.project_id && onSelectProject) {
                  onSelectProject(landingProject.project_id);
                }
              }}
              onOpenDocuments={(landingProject) => {
                if (landingProject.project_id && onSelectProject) {
                  onSelectProject(landingProject.project_id);
                  onOpenDocuments?.();
                }
              }}
              onOpenRequirements={(landingProject) => {
                if (landingProject.project_id && onSelectProject) {
                  onSelectProject(landingProject.project_id);
                  onOpenRequirements?.();
                }
              }}
              onOpenProjectSetup={(landingProject) => {
                if (landingProject.project_id && onSelectProject) {
                  onSelectProject(landingProject.project_id);
                  onOpenProjectSetup?.();
                }
              }}
              onOpenDebugLogs={(landingProject) => {
                if (landingProject.project_id && onSelectProject) {
                  onSelectProject(landingProject.project_id);
                  onOpenDebugLogs?.();
                }
              }}
              footerNote={
                landingBatchResult
                  ? "success" in landingBatchResult
                    ? `Last batch: ${landingBatchResult.success} success, ${landingBatchResult.partial} partial, ${landingBatchResult.failed} failed.`
                    : `Last batch: ${landingBatchResult.updated} manifest updates, ${landingBatchResult.skipped} skipped.`
                  : null
              }
            />
          </section>
        </div>
      </details>

      <ConfirmModal
        isOpen={rebuildAllConfirmOpen}
        title="Build file lists for all landing projects?"
        message="This updates landing manifest records for all discovered projects. Existing entries are preserved. Use preview first if needed."
        confirmLabel="Build File Lists"
        onCancel={() => setRebuildAllConfirmOpen(false)}
        onConfirm={() => {
          void runLandingRebuildAll();
        }}
      />

      <ConfirmModal
        isOpen={landingConfirmOpen}
        title="Import all landing project data?"
        message="This writes landing records for all discovered projects to local PostgreSQL. Use Preview Import first if you want to review before importing."
        confirmLabel="Import All"
        onCancel={() => setLandingConfirmOpen(false)}
        onConfirm={() => {
          void runLandingIngestAll();
        }}
      />

      <ConfirmModal
        isOpen={confirmWrite !== null}
        title={confirmWrite?.writeGuardLabel || "Confirm update"}
        message={confirmWrite?.writeGuardDescription || "This action updates data in PostgreSQL."}
        confirmLabel="Confirm"
        onCancel={() => setConfirmWrite(null)}
        onConfirm={() => {
          if (!confirmWrite || !project) {
            setConfirmWrite(null);
            return;
          }

          void runOperation(
            confirmWrite.label,
            () => resolveOperationPromise(confirmWrite.label, project),
            confirmWrite,
          );

          setConfirmWrite(null);
        }}
      />
    </div>
  );
}

function resolveOperationPromise(label: string, project?: ProjectSummary) {
  if (label === "Health check") {
    return api.health();
  }

  if (!project) {
    throw new Error("Select a project before running this operation.");
  }

  if (label === "Landing Status") {
    return api.getProjectLandingStatus(project.id);
  }

  if (label === "Scan Landing") {
    return api.scanLandingForProject(project.id);
  }

  if (label === "Rebuild Manifest") {
    return api.rebuildManifestForProject(project.id);
  }

  if (label === "Dry Run Ingest") {
    return api.dryRunIngest(project.id);
  }

  if (label === "Run Ingest") {
    return api.realIngest(project.id);
  }

  if (label === "Create Snapshot") {
    return api.createReadinessSnapshot(project.id);
  }

  if (label === "Resolve Model Evidence") {
    return api.resolveModelEvidence(project.id);
  }

  throw new Error("Unknown operation");
}

function friendlyOperationLabel(label: string) {
  switch (label) {
    case "Health check":
      return "Check System";
    case "Landing Status":
      return "Check Project Folder";
    case "Scan Landing":
      return "Find Files";
    case "Rebuild Manifest":
      return "Build File List";
    case "Dry Run Ingest":
      return "Preview Import";
    case "Run Ingest":
      return "Import Data";
    case "Create Snapshot":
      return "Update Dashboard";
    case "Resolve Model Evidence":
      return "Resolve Model Evidence";
    default:
      return label;
  }
}

function StatusTile({
  label,
  value,
  detail,
  mono,
}: {
  label: string;
  value: string | number;
  detail?: string;
  mono?: boolean;
}) {
  return (
    <div className="ema-card p-3">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</div>
      <div className={`mt-1 truncate text-sm font-semibold text-ink ${mono ? "font-mono" : ""}`}>
        {value}
      </div>
      {detail && <div className="mt-0.5 truncate text-[10px] text-subtle">{detail}</div>}
    </div>
  );
}

function MetricTile({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="ema-card p-3">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</div>
      <div className="mt-1 text-2xl font-semibold text-ink">{value}</div>
    </div>
  );
}

function MetricCard({
  title,
  value,
  detail,
}: {
  title: string;
  value: number;
  detail: string;
}) {
  return (
    <div className="ema-card p-4">
      <div className="text-2xl font-semibold text-ink">{value}</div>
      <div className="text-sm font-medium text-muted">{title}</div>
      <div className="mt-1 text-xs text-subtle">{detail}</div>
    </div>
  );
}

function LandingStatusDetails({ landingStatus }: { landingStatus: LandingStatus }) {
  if (landingStatus.folder_found === false) {
    return (
      <div className="ema-notice-warning mt-3 p-4">
        <div className="font-semibold text-warning">Landing Folder Not Found</div>
        <p className="mt-2 text-sm text-warning">
          Project "{landingStatus.requested_folder || landingStatus.project_name}" has no matching folder at{" "}
          <code className="rounded bg-surface-2 px-1">{landingStatus.landing_root}</code>.
        </p>

        {landingStatus.available_folders && landingStatus.available_folders.length > 0 && (
          <div className="mt-3">
            <p className="text-xs font-semibold text-warning">Available folders:</p>
            <ul className="mt-1 space-y-1">
              {landingStatus.available_folders.map((folder) => (
                <li key={folder} className="flex items-center gap-2 text-sm">
                  <span className="text-warning">📁</span>
                  <span
                    className={
                      folder === landingStatus.suggested_folder
                        ? "font-semibold text-warning"
                        : "text-warning"
                    }
                  >
                    {folder}
                  </span>
                  {folder === landingStatus.suggested_folder && (
                    <span className="ema-chip ema-chip-warning px-1.5 py-0.5 text-xs">
                      suggested
                    </span>
                  )}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="mt-4 space-y-4">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <StatusTile label="Landing Path" value={landingStatus.project_landing_path || "—"} mono />
        <StatusTile
          label="Manifest Status"
          value={landingStatus.counts?.files_found > 0 ? "Files indexed" : "No files"}
        />
        <StatusTile label="Manifest Path" value={landingStatus.operation || "—"} mono />
        <StatusTile label="Next Actions" value={landingStatus.next_actions?.join(", ") || "—"} />
      </div>

      {landingStatus.folder_status && (
        <div>
          <div className="text-sm font-semibold text-muted">Standard folders</div>
          <div className="mt-2 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            {Object.entries(landingStatus.folder_status).map(([folder, exists]) => (
              <div key={folder} className="ema-card flex items-center gap-2 px-3 py-2 text-sm">
                <span className={exists ? "text-success" : "text-danger"}>{exists ? "✓" : "✗"}</span>
                <span className="text-ink">{folder}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {landingStatus.counts && Object.keys(landingStatus.counts).length > 0 && (
        <div>
          <div className="text-sm font-semibold text-muted">File counts</div>
          <div className="mt-2 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            {Object.entries(landingStatus.counts)
              .filter(([, value]) => value > 0)
              .map(([key, count]) => (
                <div key={key} className="ema-card flex items-center justify-between px-3 py-2 text-sm">
                  <span className="text-ink">{key.replace(/_/g, " ")}</span>
                  <span className="font-semibold text-ink">{count}</span>
                </div>
              ))}
          </div>
        </div>
      )}
    </div>
  );
}

function summarizeDocuments(documents: LandingDocument[]) {
  return documents.reduce(
    (counts, document) => {
      const category = normalizeDocumentType(document);

      if (category === "Drawing") {
        counts.drawings += 1;
      }

      if (category === "Specification") {
        counts.specifications += 1;
      }

      if (category === "Owner Requirements") {
        counts.ownerRequirements += 1;
      }

      return counts;
    },
    {
      drawings: 0,
      specifications: 0,
      ownerRequirements: 0,
    },
  );
}

function normalizeDocumentType(document: LandingDocument) {
  const raw = `${document.document_category || ""} ${document.file_type || ""}`.toLowerCase();

  if (raw.includes("owner")) {
    return "Owner Requirements";
  }

  if (raw.includes("drawing")) {
    return "Drawing";
  }

  if (raw.includes("spec")) {
    return "Specification";
  }

  return "Supporting";
}

function labelForIntakeType(value: DataIntakeType) {
  switch (value) {
    case "owner_requirements":
      return "Owner Requirements";
    case "drawing":
      return "Drawing";
    case "specification":
      return "Specification";
  }
}