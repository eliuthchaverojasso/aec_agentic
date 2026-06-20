import {
  CheckCircle2,
  ClipboardCopy,
  Database,
  Edit3,
  FolderOpen,
  PlusCircle,
  RefreshCw,
  Save,
  Settings,
  Trash2,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { api, API_BASE_URL } from "../api/client";
import { LandingRootOverview } from "../components/LandingRootOverview";
import { StatusBadge } from "../components/StatusBadge";
import { useDebugLogger } from "../hooks/useDebugLogger";
import type {
  Client,
  LandingProjectDiscovery,
  LandingProjectDiscoveryResponse,
  LandingProjectSummary,
  ProjectBindingJson,
  ProjectSummary,
} from "../types";

const isCloudEnvironment =
  API_BASE_URL.includes("ema-ai-demo.shokworks.io") ||
  (!API_BASE_URL.includes("localhost") && !API_BASE_URL.includes("127.0.0.1"));

const dashboardUrl = typeof window !== "undefined" ? window.location.origin : API_BASE_URL;

type Props = {
  clients: Client[];
  projects?: ProjectSummary[];
  selectedProject?: ProjectSummary;
  onProjectCreated: (project: ProjectSummary, selectedModelId?: number | null) => void;
  onToast: (message: string, tone?: "success" | "info" | "warning") => void;
  onOpenProcessing?: () => void;
};

type SetupMode = "create" | "edit";

type ProjectMilestone = {
  id: string;
  name: string;
  percentage: number;
  dueDate: string;
};

const DISCIPLINES = [
  "Mechanical",
  "Electrical",
  "Plumbing",
  "Technology",
  "Lighting",
  "Fire Protection",
];

// Configurable via VITE_DEFAULT_LANDING_ROOT; empty by default so no machine-specific
// path is shipped. The field stays user-editable in the setup form.
const DEFAULT_LANDING_ROOT = import.meta.env.VITE_DEFAULT_LANDING_ROOT ?? "";

function formatDateInput(date: Date) {
  return date.toISOString().slice(0, 10);
}

function addWeeks(date: Date, weeks: number) {
  const next = new Date(date);
  next.setDate(next.getDate() + weeks * 7);
  return next;
}

function getDefaultProjectMilestones(): ProjectMilestone[] {
  const today = new Date();

  return [
    {
      id: "dd30",
      name: "DD 30%",
      percentage: 30,
      dueDate: formatDateInput(addWeeks(today, 2)),
    },
    {
      id: "dd50",
      name: "DD 50%",
      percentage: 50,
      dueDate: formatDateInput(addWeeks(today, 4)),
    },
    {
      id: "dd95",
      name: "DD 95%",
      percentage: 95,
      dueDate: formatDateInput(addWeeks(today, 6)),
    },
  ];
}

export function ProjectSetupPage({
  clients,
  projects = [],
  selectedProject,
  onProjectCreated,
  onToast,
  onOpenProcessing,
}: Props) {
  const logAction = useDebugLogger();

  const [setupMode, setSetupMode] = useState<SetupMode>("create");

  const [landingRoot, setLandingRoot] = useState(DEFAULT_LANDING_ROOT);
  const [projectName, setProjectName] = useState("");
  const [projectCode, setProjectCode] = useState("");
  const [projectFolder, setProjectFolder] = useState("");

  const [projectMilestones, setProjectMilestones] = useState<ProjectMilestone[]>(
    getDefaultProjectMilestones,
  );
  const [activeMilestoneId, setActiveMilestoneId] = useState("dd50");

  const [clientId, setClientId] = useState<number | "new" | "">("");
  const [clientName, setClientName] = useState("");
  const [clientCode, setClientCode] = useState("");

  const [modelName, setModelName] = useState("");
  const [selectedDisciplines, setSelectedDisciplines] = useState<string[]>(
    DISCIPLINES.slice(0, 5),
  );

  const [selectedExistingProjectId, setSelectedExistingProjectId] = useState<number | "">("");
  const [discoverResults, setDiscoverResults] = useState<LandingProjectDiscovery[]>([]);
  const [selectedDiscoveredFolder, setSelectedDiscoveredFolder] = useState("");

  const [lastBindingJson, setLastBindingJson] = useState<ProjectBindingJson | null>(null);
  const [landingDiscovery, setLandingDiscovery] =
    useState<LandingProjectDiscoveryResponse | null>(null);
  const [landingDiscoveryLoading, setLandingDiscoveryLoading] = useState(false);
  const [landingDiscoveryError, setLandingDiscoveryError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [landingCleanupResult, setLandingCleanupResult] = useState<unknown | null>(null);

  const resolvedFolder = useMemo(
    () => projectFolder.trim() || selectedDiscoveredFolder || projectName.trim(),
    [projectFolder, selectedDiscoveredFolder, projectName],
  );

  const activeMilestone = useMemo(
    () =>
      projectMilestones.find((item) => item.id === activeMilestoneId) ||
      projectMilestones[0],
    [projectMilestones, activeMilestoneId],
  );

  const discoveredFolders = useMemo(() => {
    const fromOverview =
      landingDiscovery?.projects.map((project) => ({
        folder: project.project_folder,
        name: project.project_name,
        clientName: project.client_name || project.client_suggestion?.client_name || "",
        clientCode: project.client_suggestion?.client_code || "",
        manifest: project.manifest_exists,
        status: project.status,
        projectId: project.project_id,
        clientId: project.client_id,
        raw: project,
      })) ?? [];

    const fromDiscover = discoverResults.map((project) => ({
      folder: project.project_folder_name,
      name: project.project_folder_name,
      clientName: "",
      clientCode: "",
      manifest: project.has_manifest,
      status: project.has_manifest ? "ready" : "needs_manifest",
      projectId: null,
      clientId: null,
      raw: null,
    }));

    const merged = [...fromOverview, ...fromDiscover];
    const seen = new Set<string>();

    return merged.filter((row) => {
      if (seen.has(row.folder)) {
        return false;
      }

      seen.add(row.folder);
      return true;
    });
  }, [landingDiscovery, discoverResults]);

  const selectedLandingFolder = useMemo(
    () => discoveredFolders.find((folder) => folder.folder === selectedDiscoveredFolder),
    [discoveredFolders, selectedDiscoveredFolder],
  );

  const existingProjects = useMemo(() => {
    const fromProps = projects;

    const fromLanding =
      landingDiscovery?.projects
        .filter((project) => project.project_id != null)
        .map((project) => ({
          id: project.project_id as number,
          project_title: project.project_name,
          project_name: project.project_folder,
          project_code: undefined,
          client_id: project.client_id ?? undefined,
          client_name: project.client_name || undefined,
          phase: activeMilestone?.name || "DD 50%",
          active_models: 0,
          open_issues: 0,
          critical_issues: 0,
          high_issues: 0,
          medium_issues: 0,
          low_issues: 0,
          model_health_score: 0,
          last_sync_at: undefined,
        })) ?? [];

    const map = new Map<number, ProjectSummary>();

    for (const project of [...fromLanding, ...fromProps]) {
      map.set(project.id, project as ProjectSummary);
    }

    return Array.from(map.values());
  }, [projects, landingDiscovery, activeMilestone?.name]);

  useEffect(() => {
    if (selectedProject && setupMode === "edit") {
      loadProjectIntoForm(selectedProject);
    }
  }, [selectedProject, setupMode]);

  useEffect(() => {
    if (setupMode !== "edit" || selectedExistingProjectId === "") {
      return;
    }

    const project = existingProjects.find((item) => item.id === selectedExistingProjectId);

    if (project) {
      loadProjectIntoForm(project);
    } else {
      void loadProjectFromApi(selectedExistingProjectId);
    }
  }, [selectedExistingProjectId, setupMode, existingProjects]);

  useEffect(() => {
    void refreshLandingRoot();
  }, []);

  function loadProjectIntoForm(project: ProjectSummary) {
    setSelectedExistingProjectId(project.id);
    setProjectName(project.project_title || project.project_name || "");
    setProjectCode(project.project_code || "");
    setProjectFolder(project.project_name || project.project_title || "");

    const phase = project.phase || activeMilestone?.name || "DD 50%";
    const matchingMilestone = projectMilestones.find((item) => item.name === phase);

    if (matchingMilestone) {
      setActiveMilestoneId(matchingMilestone.id);
    } else if (phase) {
      const customMilestone: ProjectMilestone = {
        id: `existing-${Date.now()}`,
        name: phase,
        percentage: inferMilestonePercentage(phase),
        dueDate: formatDateInput(addWeeks(new Date(), 2)),
      };

      setProjectMilestones((current) => [...current, customMilestone]);
      setActiveMilestoneId(customMilestone.id);
    }

    setClientId(project.client_id ?? "");
    setClientName(project.client_name || "");
    setModelName(project.project_name ? `MEP-${project.project_name}` : "");
  }

  async function loadProjectFromApi(projectId: number) {
    try {
      const project = await api.getProject(projectId);
      loadProjectIntoForm(project);
      onProjectCreated(project, null);
    } catch (error) {
      onToast(error instanceof Error ? error.message : String(error), "warning");
    }
  }

  async function refreshLandingRoot() {
    setLandingDiscoveryLoading(true);
    setLandingDiscoveryError(null);

    try {
      const response = await api.getLandingProjects();
      setLandingDiscovery(response);

      setDiscoverResults(
        response.projects.map((project) => ({
          project_folder_name: project.project_folder,
          has_manifest: project.manifest_exists,
          latest_revit_export:
            project.counts.revit_exports > 0 ? project.manifest_path || null : null,
          counts: {
            revit_exports: project.counts.revit_exports,
            drawings: project.counts.drawings,
            owner_requirements: project.counts.owner_requirements,
            specifications: project.counts.specifications,
            sidecars: project.counts.revit_meta,
          },
          warnings: project.warnings,
        })),
      );
    } catch (error) {
      setLandingDiscovery(null);
      setLandingDiscoveryError(error instanceof Error ? error.message : String(error));
    } finally {
      setLandingDiscoveryLoading(false);
    }
  }

  function applyDiscoveredFolder(folderName: string) {
    setSelectedDiscoveredFolder(folderName);

    const folder = discoveredFolders.find((item) => item.folder === folderName);

    if (!folder) {
      return;
    }

    setProjectFolder(folder.folder);
    setProjectName((current) => current || folder.name || folder.folder);

    if (folder.clientCode) {
      setClientCode((current) => current || folder.clientCode);
    }

    if (folder.clientName) {
      setClientName((current) => current || folder.clientName);
    }

    if (folder.projectId) {
      setSelectedExistingProjectId(folder.projectId);
      setSetupMode("edit");
    }

    if (folder.clientId) {
      setClientId(folder.clientId);
    }
  }

  function buildBindingJson(
    project: ProjectSummary,
    modelId: number | null = null,
  ): ProjectBindingJson {
    return {
      environment: isCloudEnvironment ? "Cloud" : "Local", api_base_url: API_BASE_URL, dashboard_url: dashboardUrl, landing_root: isCloudEnvironment ? "" : landingRoot,
      project_folder_name: resolvedFolder || project.project_name || project.project_title,
      project_display_name: project.project_title || project.project_name,
      project_code: project.project_code || projectCode || undefined,
      client_code: clientCode || undefined,
      client_name: project.client_name || clientName || undefined,
      project_id: project.id,
      client_id: project.client_id ?? (typeof clientId === "number" ? clientId : null),
      model_id: modelId,
      current_milestone: project.phase || activeMilestone?.name || "DD 50%",
      use_landing_structure: !isCloudEnvironment, sync_mode: isCloudEnvironment ? "cloud_upload" : "local_landing",
      milestones: projectMilestones,
    } as ProjectBindingJson;
  }

  async function bindDiscoveredProject(row: LandingProjectSummary) {
    const started = Date.now();
    setBusy(true);

    try {
      const bindPayload =
        typeof clientId === "number"
          ? {
              project_id: row.project_id ?? undefined,
              client_id: clientId,
              milestone: activeMilestone?.name || "DD 50%",
              project_name: row.project_name,
            }
          : clientId === "new"
            ? {
                project_id: row.project_id ?? undefined,
                client_code: clientCode || row.client_suggestion?.client_code || undefined,
                client_name: clientName || row.client_suggestion?.client_name || undefined,
                milestone: activeMilestone?.name || "DD 50%",
                project_name: row.project_name,
              }
            : row.client_id
              ? {
                  project_id: row.project_id ?? undefined,
                  client_id: row.client_id,
                  milestone: activeMilestone?.name || "DD 50%",
                  project_name: row.project_name,
                }
              : row.client_suggestion?.client_code
                ? {
                    project_id: row.project_id ?? undefined,
                    client_code: row.client_suggestion.client_code,
                    client_name:
                      row.client_suggestion.client_name ||
                      row.client_suggestion.client_code,
                    milestone: activeMilestone?.name || "DD 50%",
                    project_name: row.project_name,
                  }
                : null;

      if (!bindPayload) {
        onToast(
          "Select an existing client or create a new client before binding the landing folder.",
          "warning",
        );
        return;
      }

      const response = row.project_id
        ? await api.bindLandingProject(row.project_folder, bindPayload)
        : await api.bootstrapProjectFromFolder({
            landing_root: landingRoot,
            project_folder_name: row.project_folder,
            project_display_name: row.project_name,
            project_code: projectCode || undefined,
            client_code: (bindPayload as { client_code?: string }).client_code,
            client_name: (bindPayload as { client_name?: string }).client_name,
            environment: "Local",
          });

      const project = await api.getProject(response.project_id);

      onProjectCreated(project, null);
      setSetupMode("edit");
      setSelectedExistingProjectId(project.id);
      setProjectName(project.project_title);
      setProjectFolder(row.project_folder);
      setSelectedDiscoveredFolder(row.project_folder);
      setLastBindingJson(buildBindingJson(project, null));

      onToast(`Bound ${row.project_folder} successfully.`, "success");

      await logAction({
        action: "bind_landing_project",
        route: "/project-setup",
        project_id: project.id,
        project_name: project.project_title,
        status: "success",
        duration_ms: Date.now() - started,
      });

      await refreshLandingRoot();
    } catch (error) {
      await logAction({
        action: "bind_landing_project",
        route: "/project-setup",
        status: "failed",
        severity: "error",
        duration_ms: Date.now() - started,
        error: error instanceof Error ? error.message : String(error),
      });

      onToast(error instanceof Error ? error.message : String(error), "warning");
    } finally {
      setBusy(false);
    }
  }

  async function runCreate() {
    const started = Date.now();
    setBusy(true);

    try {
      const payload = {
        name: projectName,
        project_code: projectCode || undefined,
        client_id: typeof clientId === "number" ? clientId : undefined,
        client_code: clientId === "new" ? clientCode || undefined : undefined,
        client_name: clientId === "new" ? clientName || undefined : undefined,
        current_milestone: activeMilestone?.name || "DD 50%",
        enabled_disciplines: selectedDisciplines,
        landing_project_folder: resolvedFolder || undefined,
        environment: isCloudEnvironment ? "Cloud" : "Local", storage_mode: isCloudEnvironment ? "Cloud Upload" : "Local Landing",
        project_milestones: projectMilestones,
      };

      const project = await api.createProject(payload as Parameters<typeof api.createProject>[0]);

      await api.configureProjectLanding(project.id, {
        landing_root: landingRoot,
        project_folder_name:
          resolvedFolder || project.project_name || project.project_title,
        create_folders: true,
      });

      let modelId: number | null = null;

      if (modelName.trim()) {
        const model = await api.createProjectModel(project.id, {
          model_name: modelName.trim(),
          model_type: "Revit",
          discipline: "MEP",
          source_system: "Revit",
        });

        modelId = model.id;
      }

      const binding = buildBindingJson(project, modelId);
      setLastBindingJson(binding);

      setSetupMode("edit");
      setSelectedExistingProjectId(project.id);
      onProjectCreated(project, modelId);

      await logAction({
        action: "project_setup_create",
        route: "/project-setup",
        project_id: project.id,
        project_name: project.project_name || project.project_title,
        status: "success",
        duration_ms: Date.now() - started,
      });

      onToast("Project setup complete. Continue in Processing / Sync.", "success");
      void refreshLandingRoot();
    } catch (error) {
      await logAction({
        action: "project_setup_create",
        route: "/project-setup",
        status: "failed",
        severity: "error",
        duration_ms: Date.now() - started,
        error: error instanceof Error ? error.message : String(error),
      });

      onToast(error instanceof Error ? error.message : String(error), "warning");
    } finally {
      setBusy(false);
    }
  }

  async function runSaveExisting() {
    const started = Date.now();

    if (!selectedExistingProjectId) {
      onToast("Select an existing project before saving changes.", "warning");
      return;
    }

    setBusy(true);

    try {
      const dynamicApi = api as unknown as {
        updateProject?: (
          projectId: number,
          payload: Record<string, unknown>,
        ) => Promise<ProjectSummary>;
      };

      let project: ProjectSummary | null = null;

      if (dynamicApi.updateProject) {
        project = await dynamicApi.updateProject(selectedExistingProjectId, {
          name: projectName,
          project_code: projectCode || undefined,
          client_id: typeof clientId === "number" ? clientId : undefined,
          client_code: clientId === "new" ? clientCode || undefined : undefined,
          client_name: clientId === "new" ? clientName || undefined : undefined,
          current_milestone: activeMilestone?.name || "DD 50%",
          enabled_disciplines: selectedDisciplines,
          milestone_due_date: activeMilestone?.dueDate || undefined,
          project_milestones: projectMilestones,
        });
      }

      await api.configureProjectLanding(selectedExistingProjectId, {
        landing_root: landingRoot,
        project_folder_name: resolvedFolder || projectName,
        create_folders: true,
      });

      if (!project) {
        project = await api.getProject(selectedExistingProjectId);
        onToast(
          "Landing configuration saved. Project metadata update endpoint was not detected in api/client.ts.",
          "info",
        );
      } else {
        onToast("Project configuration updated.", "success");
      }

      let modelId: number | null = null;

      if (modelName.trim()) {
        try {
          const model = await api.createProjectModel(project.id, {
            model_name: modelName.trim(),
            model_type: "Revit",
            discipline: "MEP",
            source_system: "Revit",
          });

          modelId = model.id;
        } catch {
          modelId = null;
        }
      }

      setLastBindingJson(buildBindingJson(project, modelId));
      onProjectCreated(project, modelId);

      await logAction({
        action: "project_setup_edit",
        route: "/project-setup",
        project_id: project.id,
        project_name: project.project_name || project.project_title,
        status: "success",
        duration_ms: Date.now() - started,
      });

      void refreshLandingRoot();
    } catch (error) {
      await logAction({
        action: "project_setup_edit",
        route: "/project-setup",
        status: "failed",
        severity: "error",
        duration_ms: Date.now() - started,
        error: error instanceof Error ? error.message : String(error),
      });

      onToast(error instanceof Error ? error.message : String(error), "warning");
    } finally {
      setBusy(false);
    }
  }

  async function runDiscover() {
    const started = Date.now();
    setBusy(true);

    try {
      const response = await api.discoverLandingProjects(landingRoot);
      setDiscoverResults(response.projects);

      await logAction({
        action: "discover_landing_projects",
        route: "/project-setup",
        status: "success",
        duration_ms: Date.now() - started,
        message: `folders=${response.projects.length}`,
      });

      onToast(`Discovered ${response.projects.length} landing folder(s).`, "info");
      void refreshLandingRoot();
    } catch (error) {
      await logAction({
        action: "discover_landing_projects",
        route: "/project-setup",
        status: "failed",
        severity: "error",
        duration_ms: Date.now() - started,
        error: error instanceof Error ? error.message : String(error),
      });

      onToast(error instanceof Error ? error.message : String(error), "warning");
    } finally {
      setBusy(false);
    }
  }

  async function runBootstrap() {
    const started = Date.now();

    if (!selectedDiscoveredFolder) {
      onToast("Select a discovered folder first.", "warning");
      return;
    }

    setBusy(true);

    try {
      const response = await api.bootstrapProjectFromFolder({
        landing_root: landingRoot,
        project_folder_name: selectedDiscoveredFolder,
        project_display_name: projectName || selectedDiscoveredFolder,
        project_code: projectCode || undefined,
        client_code: clientCode || undefined,
        client_name: clientName || undefined,
        environment: "Local",
      });

      const project = await api.getProject(response.project_id);

      onProjectCreated(project, null);
      setSetupMode("edit");
      setSelectedExistingProjectId(project.id);
      setProjectName(project.project_title);
      setProjectFolder(response.project_folder_name);
      setLastBindingJson(buildBindingJson(project, null));

      await logAction({
        action: "bootstrap_project_from_folder",
        route: "/project-setup",
        project_id: response.project_id,
        project_name: response.project_name || response.project_folder_name,
        status: "success",
        duration_ms: Date.now() - started,
      });

      onToast("Project bootstrapped from landing folder.", "success");
      void refreshLandingRoot();
    } catch (error) {
      await logAction({
        action: "bootstrap_project_from_folder",
        route: "/project-setup",
        status: "failed",
        severity: "error",
        duration_ms: Date.now() - started,
        error: error instanceof Error ? error.message : String(error),
      });

      onToast(error instanceof Error ? error.message : String(error), "warning");
    } finally {
      setBusy(false);
    }
  }

  function resetCreateForm() {
    setSetupMode("create");
    setSelectedExistingProjectId("");
    setProjectName("");
    setProjectCode("");
    setProjectFolder("");
    setSelectedDiscoveredFolder("");
    setClientId("");
    setClientName("");
    setClientCode("");
    setModelName("");
    setProjectMilestones(getDefaultProjectMilestones());
    setActiveMilestoneId("dd50");
    setSelectedDisciplines(DISCIPLINES.slice(0, 5));
    setLastBindingJson(null);
  }

  function regenerateBindingJson() {
    if (setupMode === "edit" && selectedExistingProjectId) {
      const project =
        existingProjects.find((item) => item.id === selectedExistingProjectId) ||
        selectedProject;

      if (project) {
        setLastBindingJson(
          buildBindingJson({
            ...project,
            project_title: projectName || project.project_title,
            project_name: projectFolder || project.project_name,
            project_code: projectCode || project.project_code,
            phase: activeMilestone?.name || project.phase,
            client_name: clientName || project.client_name,
            client_id:
              typeof clientId === "number"
                ? clientId
                : project.client_id,
          }),
        );

        onToast("Binding JSON regenerated.", "success");
        return;
      }
    }

    onToast("Create or select a project before generating binding JSON.", "warning");
  }

  function addProjectMilestone() {
    const nextIndex = projectMilestones.length + 1;
    const lastDate =
      projectMilestones.length > 0
        ? new Date(projectMilestones[projectMilestones.length - 1].dueDate)
        : new Date();

    const nextMilestone: ProjectMilestone = {
      id: `custom-${Date.now()}`,
      name: `Milestone ${nextIndex}`,
      percentage: Math.min(100, nextIndex * 25),
      dueDate: formatDateInput(addWeeks(lastDate, 2)),
    };

    setProjectMilestones((current) => [...current, nextMilestone]);
  }

  function updateProjectMilestone(
    milestoneId: string,
    patch: Partial<ProjectMilestone>,
  ) {
    setProjectMilestones((current) =>
      current.map((row) =>
        row.id === milestoneId ? { ...row, ...patch } : row,
      ),
    );
  }

  function removeProjectMilestone(milestoneId: string) {
    setProjectMilestones((current) => {
      const next = current.filter((row) => row.id !== milestoneId);

      if (activeMilestoneId === milestoneId && next[0]) {
        setActiveMilestoneId(next[0].id);
      }

      return next.length > 0 ? next : current;
    });
  }


  async function runLandingDedupe(dryRun: boolean) {
    const started = Date.now();
    const selectedLandingProject =
      landingDiscovery?.projects.find((row: LandingProjectSummary) => row.project_folder === selectedDiscoveredFolder) ||
      landingDiscovery?.projects.find((row: LandingProjectSummary) => row.project_folder === projectFolder) ||
      landingDiscovery?.projects.find((row: LandingProjectSummary) => row.project_name === projectName);

    if (!selectedLandingProject) {
      onToast("Select a discovered landing folder first.", "warning");
      return;
    }

    if (!selectedLandingProject.project_id) {
      onToast("Selected landing folder is not bound to a project yet. Bind or bootstrap it first.", "warning");
      return;
    }

    setBusy(true);
    setLandingCleanupResult(null);

    try {
      const response = await api.dedupeProjectLanding(selectedLandingProject.project_id, {
        category: "owner_requirements",
        dry_run: dryRun,
        delete_files: true,
        rebuild_manifest: true,
        prefer_clean_filename: true,
      });

      setLandingCleanupResult(response);

      const counts = response.counts || { deleted_documents: 0, deleted_files: 0 };
      onToast(
        dryRun
          ? `Dedupe dry run complete. ${counts.deleted_files || 0} duplicate file(s), ${counts.deleted_documents || 0} duplicate index row(s) found.`
          : `Landing cleanup complete. ${counts.deleted_files || 0} file(s), ${counts.deleted_documents || 0} index row(s) removed.`,
        "success",
      );

      await logAction({
        action: dryRun ? "landing_dedupe_dry_run" : "landing_dedupe_execute",
        route: "/project-setup",
        project_id: selectedLandingProject.project_id,
        project_name: selectedLandingProject.project_name,
        status: "success",
        duration_ms: Date.now() - started,
        message: `folder=${selectedLandingProject.project_folder}`,
      });

      await refreshLandingRoot();
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setLandingCleanupResult({ ok: false, error: message });
      await logAction({
        action: dryRun ? "landing_dedupe_dry_run" : "landing_dedupe_execute",
        route: "/project-setup",
        status: "failed",
        severity: "error",
        duration_ms: Date.now() - started,
        error: message,
      });
      onToast(message, "warning");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="ema-page ema-page-shell space-y-6">
      <section className="ema-liquid-section p-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h2 className="text-2xl font-semibold text-ink">Project Setup</h2>
            <p className="mt-1 max-w-4xl text-sm text-muted">
              Create or edit project configuration, landing folder binding, model metadata,
              project milestones, and Revit connection JSON.
            </p>
            <p className="mt-2 text-xs text-subtle">
              Requirement-to-milestone mapping is managed from Owner Requirements after
              the project is created.
            </p>
          </div>

          <div className="flex rounded-xl border border-line bg-surface p-1">
            <button
              type="button"
              className={`inline-flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-semibold transition ${
                setupMode === "create"
                  ? "bg-accent text-inverse"
                  : "text-muted hover:bg-surface-2 hover:text-ink"
              }`}
              onClick={resetCreateForm}
            >
              <PlusCircle size={15} />
              Create New
            </button>

            <button
              type="button"
              className={`inline-flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-semibold transition ${
                setupMode === "edit"
                  ? "bg-accent text-inverse"
                  : "text-muted hover:bg-surface-2 hover:text-ink"
              }`}
              onClick={() => setSetupMode("edit")}
            >
              <Edit3 size={15} />
              Edit Existing
            </button>
          </div>
        </div>
      </section>

      <section className="grid gap-4 lg:grid-cols-4">
        <StepCard
          number="1"
          title="Select Landing Folder"
          description="Choose the folder that represents the project."
          active
        />
        <StepCard
          number="2"
          title="Bind Client + Project"
          description="Set owner, project metadata, model, and disciplines."
          active
        />
        <StepCard
          number="3"
          title="Project Milestones"
          description="Define names, percentages, due dates, and active milestone."
          active
        />
        <StepCard
          number="4"
          title="Generate Binding"
          description="Create/save configuration and generate Revit binding JSON."
          active
        />
      </section>

      {setupMode === "edit" && (
        <section className="ema-card p-5">
          <div className="flex items-center gap-2">
            <Edit3 size={18} className="text-accent" />
            <h3 className="text-lg font-semibold text-ink">Select Existing Project</h3>
          </div>

          <div className="mt-4 grid gap-3 md:grid-cols-[1fr_auto]">
            <select
              className="ema-input w-full px-3 py-2 text-sm"
              value={selectedExistingProjectId}
              onChange={(event) => {
                const value = event.target.value ? Number(event.target.value) : "";
                setSelectedExistingProjectId(value);
              }}
            >
              <option value="">Select existing project</option>
              {existingProjects.map((project) => (
                <option key={project.id} value={project.id}>
                  {project.project_title || project.project_name} · ID {project.id}
                </option>
              ))}
            </select>

            <button
              type="button"
              className="ema-btn-secondary inline-flex items-center gap-2"
              onClick={() => void refreshLandingRoot()}
              disabled={busy}
            >
              <RefreshCw size={15} />
              Refresh
            </button>
          </div>
        </section>
      )}

      <section className="ema-card p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <div className="flex items-center gap-2">
              <FolderOpen size={18} className="text-accent" />
              <h3 className="text-lg font-semibold text-ink">
                Step 1 - Select Landing Folder
              </h3>
            </div>
            <p className="mt-1 text-sm text-muted">
              Select a discovered landing folder, review detection status, and use the
              client suggestion as advisory input only.
            </p>
          </div>

          <button
            disabled={busy}
            onClick={runDiscover}
            type="button"
            className="ema-btn-secondary inline-flex items-center gap-2"
          >
            <RefreshCw size={15} />
            Discover Landing Projects
          </button>
        </div>

        <div className="mt-4 grid gap-3 md:grid-cols-[1fr_auto]">
          <label className="text-sm">
            Discovered Folder
            <select
              className="ema-input mt-1 w-full px-3 py-2"
              value={selectedDiscoveredFolder}
              onChange={(event) => applyDiscoveredFolder(event.target.value)}
            >
              <option value="">Select discovered folder</option>
              {discoveredFolders.map((folder) => (
                <option key={folder.folder} value={folder.folder}>
                  {folder.folder}
                </option>
              ))}
            </select>
          </label>

          <button
            disabled={busy || !selectedDiscoveredFolder}
            onClick={runBootstrap}
            type="button"
            className="ema-btn-secondary mt-6 inline-flex items-center gap-2"
          >
            <Database size={15} />
            Bootstrap From Folder
          </button>
        </div>

        {selectedLandingFolder && (
          <div className="mt-4 grid gap-3 md:grid-cols-4">
            <InfoCard label="Project Detected" value={selectedLandingFolder.name || "-"} />
            <InfoCard
              label="Client Suggestion"
              value={selectedLandingFolder.clientName || "No suggestion"}
            />
            <InfoCard
              label="Manifest"
              value={selectedLandingFolder.manifest ? "Ready" : "Missing"}
              badge={selectedLandingFolder.manifest ? "ready" : "needs manifest"}
            />
            <InfoCard
              label="Status"
              value={String(selectedLandingFolder.status || "unknown")}
            />
          </div>
        )}

        <div className="mt-4">
          <label className="text-sm">
            Landing Root
            <input
              className="ema-input mt-1 w-full px-3 py-2"
              value={landingRoot}
              onChange={(event) => setLandingRoot(event.target.value)}
            />
          </label>
        </div>
      </section>

      <section className="ema-card p-5">
        <div className="flex items-center gap-2">
          <Settings size={18} className="text-accent" />
          <h3 className="text-lg font-semibold text-ink">
            Step 2 - Bind Client + Project
          </h3>
        </div>

        <p className="mt-1 text-sm text-muted">
          Configure the project identity, owner/client binding, model name, and enabled disciplines.
        </p>

        <div className="mt-5 grid gap-3 md:grid-cols-2">
          <label className="text-sm">
            Client
            <select
              className="ema-input mt-1 w-full px-3 py-2"
              value={clientId}
              onChange={(event) =>
                setClientId(
                  event.target.value === "new"
                    ? "new"
                    : event.target.value
                      ? Number(event.target.value)
                      : "",
                )
              }
            >
              <option value="">Unlinked</option>
              {clients.map((client) => (
                <option key={client.id} value={client.id}>
                  {client.display_name}
                </option>
              ))}
              <option value="new">Create New Client</option>
            </select>
          </label>

          <label className="text-sm">
            Project Name
            <input
              className="ema-input mt-1 w-full px-3 py-2"
              value={projectName}
              onChange={(event) => setProjectName(event.target.value)}
              placeholder="ROCHELL ES"
            />
          </label>

          <label className="text-sm">
            Project Code
            <input
              className="ema-input mt-1 w-full px-3 py-2"
              value={projectCode}
              onChange={(event) => setProjectCode(event.target.value)}
              placeholder="ROCHELL_ES"
            />
          </label>

          <label className="text-sm">
            Project Folder Name
            <input
              className="ema-input mt-1 w-full px-3 py-2"
              value={projectFolder}
              onChange={(event) => setProjectFolder(event.target.value)}
              placeholder="ROCHELL ES"
            />
          </label>

          {clientId === "new" && (
            <>
              <label className="text-sm">
                Client Name
                <input
                  className="ema-input mt-1 w-full px-3 py-2"
                  value={clientName}
                  onChange={(event) => setClientName(event.target.value)}
                  placeholder="Rochell ES Owner"
                />
              </label>

              <label className="text-sm">
                Client Code
                <input
                  className="ema-input mt-1 w-full px-3 py-2"
                  value={clientCode}
                  onChange={(event) => setClientCode(event.target.value)}
                  placeholder="ROCHELL_ES_OWNER"
                />
              </label>
            </>
          )}

          <label className="text-sm md:col-span-2">
            Model Name
            <input
              className="ema-input mt-1 w-full px-3 py-2"
              value={modelName}
              onChange={(event) => setModelName(event.target.value)}
              placeholder="MEP-ROCHELL ES"
            />
          </label>
        </div>

        <div className="mt-5">
          <div className="text-sm font-semibold text-ink">Disciplines</div>
          <div className="mt-2 flex flex-wrap gap-2">
            {DISCIPLINES.map((discipline) => (
              <button
                key={discipline}
                type="button"
                onClick={() =>
                  setSelectedDisciplines((current) =>
                    current.includes(discipline)
                      ? current.filter((value) => value !== discipline)
                      : [...current, discipline],
                  )
                }
                className={`rounded border px-2 py-1 text-xs transition ${
                  selectedDisciplines.includes(discipline)
                    ? "ema-chip ema-chip-accent"
                    : "border-line text-muted hover:bg-surface-2"
                }`}
              >
                {discipline}
              </button>
            ))}
          </div>
        </div>
      </section>

      <section className="ema-card p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h3 className="text-lg font-semibold text-ink">
              Step 3 - Project Milestones
            </h3>
            <p className="mt-1 text-sm text-muted">
              Define milestone labels, percentages, due dates, and the active milestone for this project.
            </p>
          </div>

          <button
            type="button"
            className="ema-btn-secondary inline-flex items-center gap-2"
            onClick={addProjectMilestone}
          >
            <PlusCircle size={15} />
            Add Milestone
          </button>
        </div>

        <div className="mt-4 grid gap-3">
          {projectMilestones.map((item, index) => {
            const isActive = item.id === activeMilestoneId;

            return (
              <div
                key={item.id}
                className={`grid gap-3 rounded-xl border p-3 md:grid-cols-[auto_1.4fr_0.7fr_0.9fr_auto] ${
                  isActive
                    ? "border-accent/40 bg-accent/[0.04]"
                    : "border-line bg-surface-2"
                }`}
              >
                <label className="flex items-center gap-2 text-sm text-muted">
                  <input
                    type="radio"
                    name="activeMilestone"
                    checked={isActive}
                    onChange={() => setActiveMilestoneId(item.id)}
                  />
                  Active
                </label>

                <label className="text-sm">
                  Milestone Name
                  <input
                    className="ema-input mt-1 w-full px-3 py-2"
                    value={item.name}
                    onChange={(event) =>
                      updateProjectMilestone(item.id, { name: event.target.value })
                    }
                  />
                </label>

                <label className="text-sm">
                  Percentage
                  <input
                    type="number"
                    min={0}
                    max={100}
                    className="ema-input mt-1 w-full px-3 py-2"
                    value={item.percentage}
                    onChange={(event) => {
                      const value = Number(event.target.value);
                      updateProjectMilestone(item.id, {
                        percentage: Number.isFinite(value) ? value : 0,
                      });
                    }}
                  />
                </label>

                <label className="text-sm">
                  Due Date
                  <input
                    type="date"
                    className="ema-input mt-1 w-full px-3 py-2"
                    value={item.dueDate}
                    onChange={(event) =>
                      updateProjectMilestone(item.id, { dueDate: event.target.value })
                    }
                  />
                </label>

                <div className="flex items-end">
                  <button
                    type="button"
                    className="ema-btn-secondary inline-flex w-full items-center justify-center gap-2"
                    disabled={projectMilestones.length <= 1}
                    onClick={() => removeProjectMilestone(item.id)}
                  >
                    <Trash2 size={14} />
                    Remove
                  </button>
                </div>

                <div className="md:col-span-5">
                  <div className="flex flex-wrap gap-2 text-xs">
                    <span className="ema-pill">#{index + 1}</span>
                    <span className="ema-pill">{item.percentage}%</span>
                    {isActive && <span className="ema-pill">Current milestone</span>}
                  </div>
                </div>
              </div>
            );
          })}
        </div>

        <div className="mt-4 rounded-lg border border-line bg-surface-2 p-3 text-xs text-muted">
          Default milestones are DD 30%, DD 50%, and DD 95%, spaced two weeks apart.
          Requirement-to-milestone assignment is handled in Owner Requirements.
        </div>
      </section>

      <section className="ema-card p-5">
        <div className="flex items-center gap-2">
          <CheckCircle2 size={18} className="text-accent" />
          <h3 className="text-lg font-semibold text-ink">
            Step 4 - Generate Binding
          </h3>
        </div>

        <p className="mt-1 text-sm text-muted">
          Create or save the project configuration, generate the Revit binding JSON,
          and continue to Processing / Sync.
        </p>

        <div className="mt-5 flex flex-wrap gap-2">
          {setupMode === "create" ? (
            <button
              disabled={busy}
              onClick={runCreate}
              type="button"
              className="ema-btn-primary inline-flex items-center gap-2"
            >
              <PlusCircle size={16} />
              Create Project
            </button>
          ) : (
            <button
              disabled={busy || !selectedExistingProjectId}
              onClick={runSaveExisting}
              type="button"
              className="ema-btn-primary inline-flex items-center gap-2"
            >
              <Save size={16} />
              Save Changes
            </button>
          )}

          <button
            disabled={busy}
            onClick={regenerateBindingJson}
            type="button"
            className="ema-btn-secondary inline-flex items-center gap-2"
          >
            <ClipboardCopy size={16} />
            Generate Binding JSON
          </button>

          {selectedLandingFolder?.raw && (
            <button
              disabled={busy}
              type="button"
              className="ema-btn-secondary"
              onClick={() =>
                void bindDiscoveredProject(selectedLandingFolder.raw as LandingProjectSummary)
              }
            >
              Bind Selected Folder
            </button>
          )}

          {onOpenProcessing && (
            <button
              type="button"
              className="ema-btn-secondary"
              onClick={onOpenProcessing}
            >
              Continue to Processing / Sync
            </button>
          )}
        </div>
      </section>


      <section className="ema-card p-5">
        <h3 className="text-lg font-semibold text-ink">Landing Cleanup / Dedupe</h3>
        <p className="text-sm text-muted">
          Remove duplicate landing files before ingest. Use dry run first; execute cleanup only after reviewing the result.
        </p>
        <div className="mt-3 grid gap-3 md:grid-cols-[minmax(260px,1fr)_auto_auto]">
          <select
            className="ema-input px-3 py-2 text-sm"
            value={selectedDiscoveredFolder}
            onChange={(e) => setSelectedDiscoveredFolder(e.target.value)}
          >
            <option value="">Select landing folder</option>
            {(landingDiscovery?.projects || []).map((row: LandingProjectSummary) => (
              <option key={row.project_folder} value={row.project_folder}>
                {row.project_folder} {row.project_id ? `(Project ${row.project_id})` : "(unbound)"}
              </option>
            ))}
          </select>
          <button
            disabled={busy || !selectedDiscoveredFolder}
            onClick={() => void runLandingDedupe(true)}
            type="button"
            className="ema-btn-secondary"
          >
            Dry Run Dedupe
          </button>
          <button
            disabled={busy || !selectedDiscoveredFolder}
            onClick={() => {
              if (window.confirm("Delete duplicate owner requirement files and rebuild manifest?")) {
                void runLandingDedupe(false);
              }
            }}
            type="button"
            className="ema-btn-primary"
          >
            Clean Duplicates
          </button>
        </div>
        {landingCleanupResult ? (
          <pre className="ema-solid-json-surface mt-3 max-h-72 overflow-auto rounded p-3 text-xs">
            {JSON.stringify(landingCleanupResult, null, 2)}
          </pre>
        ) : null}
      </section>

      {lastBindingJson && (
        <section className="ema-card p-5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h3 className="text-lg font-semibold text-ink">Revit Binding JSON</h3>
              <p className="mt-1 text-sm text-muted">
                Copy this JSON into the Revit-side connector configuration for the
                selected project.
              </p>
            </div>

            <button
              type="button"
              className="ema-btn-secondary inline-flex items-center gap-2"
              onClick={() => {
                void navigator.clipboard.writeText(
                  JSON.stringify(lastBindingJson, null, 2),
                );
                onToast("Binding JSON copied.", "success");
              }}
            >
              <ClipboardCopy size={15} />
              Copy JSON
            </button>
          </div>

          <pre className="ema-solid-json-surface mt-3 max-h-96 overflow-auto rounded p-3 text-xs">
            {JSON.stringify(lastBindingJson, null, 2)}
          </pre>
        </section>
      )}

      <details className="ema-card p-5">
        <summary className="cursor-pointer text-lg font-semibold text-ink">
          Advanced Landing Root Overview
        </summary>

        <div className="mt-4">
          <LandingRootOverview
            discovery={landingDiscovery}
            loading={landingDiscoveryLoading}
            error={landingDiscoveryError}
            onRefresh={refreshLandingRoot}
            onRebuildDryRun={async () => {
              setBusy(true);
              try {
                const response = await api.rebuildAllLandingManifests({
                  dry_run: true,
                  preserve_existing: true,
                  infer_client_from_owner_requirements: true,
                });

                setLandingDiscoveryError(null);
                onToast(
                  `Rebuilt ${response.project_count} landing manifest(s) in dry run.`,
                  "success",
                );
                await refreshLandingRoot();
              } catch (error) {
                const message = error instanceof Error ? error.message : String(error);
                setLandingDiscoveryError(message);
                onToast(message, "warning");
              } finally {
                setBusy(false);
              }
            }}
            onIngestDryRun={async () => {
              setBusy(true);
              try {
                const response = await api.ingestAllLandingProjects({
                  dry_run: true,
                  project_folders: null,
                  require_client_for_owner_requirements: true,
                  create_snapshot: false,
                  preserve_existing: true,
                });

                setLandingDiscoveryError(null);
                onToast(
                  `Dry-run ingest completed for ${response.project_count} landing project(s).`,
                  "success",
                );
                await refreshLandingRoot();
              } catch (error) {
                const message = error instanceof Error ? error.message : String(error);
                setLandingDiscoveryError(message);
                onToast(message, "warning");
              } finally {
                setBusy(false);
              }
            }}
            onBindProject={bindDiscoveredProject}
            onSelectProject={(landingProject) => {
              applyDiscoveredFolder(landingProject.project_folder);
              onToast(`Selected ${landingProject.project_folder}.`, "info");
            }}
            footerNote="Owner Requirements ingestion requires project/client binding. Suggestion is advisory only and never auto-binds."
          />
        </div>
      </details>
    </div>
  );
}

function StepCard({
  number,
  title,
  description,
  active,
}: {
  number: string;
  title: string;
  description: string;
  active?: boolean;
}) {
  return (
    <div
      className={`ema-card p-4 ${
        active ? "border-accent/30 bg-accent/[0.03]" : ""
      }`}
    >
      <div className="flex items-start gap-3">
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-accent text-sm font-semibold text-inverse">
          {number}
        </div>

        <div>
          <div className="font-semibold text-ink">{title}</div>
          <div className="mt-1 text-sm text-muted">{description}</div>
        </div>
      </div>
    </div>
  );
}

function InfoCard({
  label,
  value,
  badge,
}: {
  label: string;
  value: string;
  badge?: string;
}) {
  return (
    <div className="ema-card p-3">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">
        {label}
      </div>

      <div className="mt-1 truncate text-sm font-semibold text-ink">{value}</div>

      {badge && (
        <div className="mt-2">
          <StatusBadge value={badge} />
        </div>
      )}
    </div>
  );
}

function inferMilestonePercentage(value: string) {
  const match = value.match(/(\d+)/);

  if (!match) {
    return 50;
  }

  const percentage = Number(match[1]);

  if (!Number.isFinite(percentage)) {
    return 50;
  }

  return Math.max(0, Math.min(100, percentage));
}




