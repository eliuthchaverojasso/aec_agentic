import { useCallback, useEffect, useMemo, useState } from "react";
import { api, fallbackReadiness } from "./api/client";
import { Layout, type PageKey } from "./components/Layout";
import { Toast, type ToastState } from "./components/Toast";
import { IssuesPage } from "./pages/IssuesPage";
import { DrawingReelPage } from "./pages/DrawingReelPage";
import { DevModePage } from "./pages/DevModePage";
import { DocumentsPage } from "./pages/DocumentsPage";
import { ModelHealthPage } from "./pages/ModelHealthPage";
import { AppearancePage } from "./pages/AppearancePage";
import { CompliancePage } from "./pages/CompliancePage";
import { ProcessingPage } from "./pages/ProcessingPage";
import { ProjectOverviewPage } from "./pages/ProjectOverviewPage";
import { ExecutiveOverviewPage } from "./pages/ExecutiveOverviewPage";
import { RequirementsPage } from "./pages/RequirementsPage";
import { RequirementAuditsPage } from "./pages/RequirementAuditsPage";
import { ReportsPage } from "./pages/ReportsPage";
import { RolesPermissionsPage } from "./pages/RolesPermissionsPage";
import { SettingsPage } from "./pages/SettingsPage";
import { SystemHealthPage } from "./pages/SystemHealthPage";
import { TradeReadinessPage } from "./pages/TradeReadinessPage";
import { UsersPage } from "./pages/UsersPage";
import { ProjectSetupPage } from "./pages/ProjectSetupPage";
import { DebugLogsPage } from "./pages/DebugLogsPage";
import { LoginPage, type DemoSession } from "./pages/LoginPage";
import { latestExportForProject } from "./lib/derived";
import type {
  Client,
  ExportRecord,
  Issue,
  LandingDocument,
  LandingProjectDiscoveryResponse,
  ModelHealth,
  ProjectReadiness,
  ProjectRequirementsResponse,
  ProjectSummary,
  ReadinessAction,
  ReadinessSnapshot,
  Requirement,
  SeionPrediction,
  SyncLog,
  DevStatus,
} from "./types";
import { useAuth } from "./context/AuthContext";

export default function App() {
  const PROJECT_KEY = "ema-ai-selected-project-id";
  const DEMO_SESSION_KEY = "ema-ai-demo-session";
  const { isAuthenticated, logout } = useAuth();
  const [activePage, setActivePage] = useState<PageKey>("projects");
  const [issueFilter, setIssueFilter] = useState<{
    severity?: "high" | "critical" | "all";
    search?: string;
  } | null>(null);
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [exports, setExports] = useState<ExportRecord[]>([]);
  const [clients, setClients] = useState<Client[]>([]);
  const [issues, setIssues] = useState<Issue[]>([]);
  const [requirements, setRequirements] = useState<Requirement[]>([]);
  const [projectRequirements, setProjectRequirements] =
    useState<ProjectRequirementsResponse | null>(null);
  const [documents, setDocuments] = useState<LandingDocument[]>([]);
  const [readiness, setReadiness] = useState<ProjectReadiness | null>(null);
  const [readinessActions, setReadinessActions] = useState<ReadinessAction[]>(
    [],
  );
  const [readinessSnapshots, setReadinessSnapshots] = useState<
    ReadinessSnapshot[]
  >([]);
  const [seionSuggestions, setSeionSuggestions] = useState<SeionPrediction[]>(
    [],
  );
  const [syncLogs, setSyncLogs] = useState<SyncLog[]>([]);
  const [modelHealth, setModelHealth] = useState<ModelHealth | null>(null);
  const [devStatus, setDevStatus] = useState<DevStatus | null>(null);
  const [landingDiscovery, setLandingDiscovery] =
    useState<LandingProjectDiscoveryResponse | null>(null);
  const [selectedProjectId, setSelectedProjectId] = useState<
    number | undefined
  >();
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [demoSession, setDemoSession] = useState<DemoSession | null>(() => {
    try {
      const raw = window.localStorage.getItem(DEMO_SESSION_KEY);
      return raw ? sanitizeDemoSession(JSON.parse(raw)) : null;
    } catch {
      try {
        window.localStorage.removeItem(DEMO_SESSION_KEY);
      } catch {
        // Local demo session recovery must never block initial render.
      }
      return null;
    }
  });
  const [pathname, setPathname] = useState(() => window.location.pathname);

  useEffect(() => {
    const onPopState = () => setPathname(window.location.pathname);
    window.addEventListener("popstate", onPopState);
    return () => window.removeEventListener("popstate", onPopState);
  }, []);

  const showToast = (message: string, tone: ToastState["tone"] = "info", position: ToastState["position"] = "bottom") => {
    setToast({ message, tone, position });
    window.setTimeout(() => setToast(null), 3500);
  };

  const openIssues = (filter?: {
    severity?: "high" | "critical" | "all";
    search?: string;
  }) => {
    setIssueFilter(filter || null);
    setActivePage("issues");
  };

  const reloadSeionSuggestions = (projectId: number) => {
    api
      .seionSuggestions(projectId)
      .then(setSeionSuggestions)
      .catch(() => setSeionSuggestions([]));
  };

  const reviewSeionSuggestion = (
    predictionId: number,
    action: "accept" | "reject",
  ) => {
    const request =
      action === "accept"
        ? api.acceptSeionSuggestion(predictionId)
        : api.rejectSeionSuggestion(predictionId);
    request
      .then((updated) => {
        setSeionSuggestions((current) =>
          current.map((item) => (item.id === updated.id ? updated : item)),
        );
        showToast(`SEION advisory suggestion ${updated.status}.`, "success");
      })
      .catch((err: Error) => showToast(err.message, "warning"));
  };

  useEffect(() => {
    let mounted = true;
    setLoading(true);
    Promise.all([api.projects(), api.exports(), api.clients()])
      .then(([projectRows, exportRows, clientRows]) => {
        if (!mounted) {
          return;
        }
        setProjects(projectRows);
        setExports(exportRows);
        setClients(clientRows);
        let persisted = Number.NaN;
        try {
          persisted = Number(window.localStorage.getItem(PROJECT_KEY) || "");
        } catch {
          persisted = Number.NaN;
        }
        const persistedValid = projectRows.some((row) => row.id === persisted);
        setSelectedProjectId(
          (current) =>
            current ?? (persistedValid ? persisted : projectRows[0]?.id),
        );
        setError(null);
        api
          .getDevStatus()
          .then(setDevStatus)
          .catch(() => setDevStatus(null));
        api
          .getLandingProjects()
          .then(setLandingDiscovery)
          .catch(() => {});
      })
      .catch((err: Error) => setError(err.message))
      .finally(() => mounted && setLoading(false));

    return () => {
      mounted = false;
    };
  }, []);

  useEffect(() => {
    if (selectedProjectId) {
      try {
        window.localStorage.setItem(PROJECT_KEY, String(selectedProjectId));
      } catch {
        // Local demo persistence failures should not affect app usability.
      }
    }
  }, [selectedProjectId]);

  useEffect(() => {
    if (!demoSession?.selectedProjectId) {
      return;
    }
    if (projects.some((p) => p.id === demoSession.selectedProjectId)) {
      setSelectedProjectId(demoSession.selectedProjectId);
    }
  }, [demoSession, projects]);

  const selectedProject = useMemo(
    () => projects.find((project) => project.id === selectedProjectId),
    [projects, selectedProjectId],
  );
  const latestExport = useMemo(
    () => latestExportForProject(exports, selectedProjectId),
    [exports, selectedProjectId],
  );

  const refreshSelectedProjectData = useCallback(async () => {
    if (!selectedProject) {
      return;
    }

    try {
      const nextReadiness = await api.readiness(selectedProject.id).catch(() =>
        fallbackReadiness(selectedProject),
      );
      setReadiness(nextReadiness);

      const [nextRequirements, nextProjectRequirements, nextReadinessActions, nextReadinessSnapshots] =
        await Promise.all([
          selectedProject.client_id ?? nextReadiness.client_id
            ? api.requirements(selectedProject.client_id ?? nextReadiness.client_id ?? 0).catch(() => ({ items: [] }))
            : Promise.resolve({ items: [] }),
          api.projectRequirements(selectedProject.id).catch(() => null),
          api.readinessActions(selectedProject.id).catch(() => []),
          api.readinessSnapshots(selectedProject.id).catch(() => []),
        ]);

      setRequirements(nextRequirements.items ?? []);
      setProjectRequirements(nextProjectRequirements);
      setReadinessActions(nextReadinessActions);
      setReadinessSnapshots(nextReadinessSnapshots);

      const [nextIssues, nextDocuments] = await Promise.all([
        api.issues(selectedProject.id).catch(() => ({ items: [] })),
        api.documents(selectedProject.id).catch(() => []),
      ]);
      setIssues(nextIssues.items ?? []);
      setDocuments(nextDocuments);
      reloadSeionSuggestions(selectedProject.id);
    } catch {
      // Refresh failures should not clear the current dashboard state.
    }
  }, [selectedProject]);

  useEffect(() => {
    if (!selectedProject) {
      return;
    }

    let mounted = true;
    setReadiness(null);
    setIssues([]);
    setRequirements([]);
    setProjectRequirements(null);
    setDocuments([]);
    setReadinessActions([]);
    setReadinessSnapshots([]);
    setSeionSuggestions([]);
    setSyncLogs([]);
    setModelHealth(null);

    const loadReadinessAndRequirements = async () => {
      const nextReadiness = await api
        .readiness(selectedProject.id)
        .catch(() => fallbackReadiness(selectedProject));

      if (!mounted) {
        return;
      }

      setReadiness(nextReadiness);
      api
        .readinessActions(selectedProject.id)
        .then((rows) => mounted && setReadinessActions(rows))
        .catch(() => mounted && setReadinessActions([]));
      api
        .readinessSnapshots(selectedProject.id)
        .then((rows) => mounted && setReadinessSnapshots(rows))
        .catch(() => mounted && setReadinessSnapshots([]));
      api
        .projectRequirements(selectedProject.id)
        .then((response) => mounted && setProjectRequirements(response))
        .catch(() => mounted && setProjectRequirements(null));

      const clientId = selectedProject.client_id ?? nextReadiness.client_id;
      if (!clientId) {
        setRequirements([]);
        return;
      }

      api
        .requirements(clientId)
        .then((response) => {
          if (mounted) {
            setRequirements(response.items);
          }
        })
        .catch(() => mounted && setRequirements([]));
    };

    loadReadinessAndRequirements();

    api
      .issues(selectedProject.id)
      .then((response) => {
        if (mounted) {
          setIssues(response.items);
        }
      })
      .catch(() => mounted && setIssues([]));

    api
      .documents(selectedProject.id)
      .then((rows) => {
        if (mounted) {
          setDocuments(rows);
        }
      })
      .catch(() => mounted && setDocuments([]));

    reloadSeionSuggestions(selectedProject.id);

    return () => {
      mounted = false;
    };
  }, [selectedProject]);

  useEffect(() => {
    if (selectedProjectId == null || projects.length === 0) {
      return;
    }
    if (projects.some((project) => project.id === selectedProjectId)) {
      return;
    }
    setSelectedProjectId(projects[0]?.id);
  }, [projects, selectedProjectId]);

  useEffect(() => {
    let mounted = true;
    if (!latestExport) {
      setSyncLogs([]);
      setModelHealth(null);
      return;
    }

    api
      .syncLogs(latestExport.id)
      .then((rows) => mounted && setSyncLogs(rows))
      .catch(() => mounted && setSyncLogs([]));

    api
      .modelHealth(latestExport.model_id)
      .then((health) => mounted && setModelHealth(health))
      .catch(() => mounted && setModelHealth(null));

    return () => {
      mounted = false;
    };
  }, [latestExport]);

  const page = (() => {
    if (loading) {
      return (
        <PanelMessage
          title="Loading dashboard"
          detail="Connecting to EMA AI API."
        />
      );
    }
    if (error) {
      return <PanelMessage title="Backend unavailable" detail={error} />;
    }

    switch (activePage) {
      case "projects":
        return (
          <ExecutiveOverviewPage
            projects={projects}
            readiness={readiness}
            documents={documents}
            landingDiscovery={landingDiscovery}
            onSelectProject={(projectId) => {
              setSelectedProjectId(projectId);
              setActivePage("overview");
            }}
            onOpenProcessing={() => setActivePage("processing")}
            onOpenDebug={() => setActivePage("debugLogs")}
            onOpenViewer={() => setActivePage("viewer")}
            onOpenProjectSetup={() => setActivePage("projectSetup")}
          />
        );
      case "overview":
        return (
          <ProjectOverviewPage
            project={selectedProject}
            readiness={readiness}
            projectRequirements={projectRequirements}
            exports={exports}
            issues={issues}
            requirements={requirements}
            documents={documents}
            readinessActions={readinessActions}
            readinessSnapshots={readinessSnapshots}
            seionSuggestions={seionSuggestions}
            onAcceptSeionSuggestion={(predictionId) =>
              reviewSeionSuggestion(predictionId, "accept")
            }
            onRejectSeionSuggestion={(predictionId) =>
              reviewSeionSuggestion(predictionId, "reject")
            }
            onToast={showToast}
            onOpenViewer={() => setActivePage("viewer")}
            onOpenProcessing={() => setActivePage("processing")}
            onOpenDocuments={() => setActivePage("documents")}
            onOpenDebugLogs={() => setActivePage("debugLogs")}
          />
        );
      case "viewer":
        return (
          <ProjectOverviewPage
            project={selectedProject}
            readiness={readiness}
            projectRequirements={projectRequirements}
            exports={exports}
            issues={issues}
            requirements={requirements}
            documents={documents}
            readinessActions={readinessActions}
            readinessSnapshots={readinessSnapshots}
            seionSuggestions={seionSuggestions}
            onAcceptSeionSuggestion={(predictionId) =>
              reviewSeionSuggestion(predictionId, "accept")
            }
            onRejectSeionSuggestion={(predictionId) =>
              reviewSeionSuggestion(predictionId, "reject")
            }
            onToast={showToast}
            forceViewerTab
            onOpenViewer={() => setActivePage("viewer")}
            onOpenProcessing={() => setActivePage("processing")}
            onOpenDocuments={() => setActivePage("documents")}
            onOpenDebugLogs={() => setActivePage("debugLogs")}
          />
        );
      case "trades":
        return (
          <TradeReadinessPage
            project={selectedProject}
            readiness={readiness}
            issues={issues}
            documents={documents}
            projectRequirements={projectRequirements}
          />
        );
      case "requirements":
        return (
          <RequirementsPage
            selectedProject={selectedProject}
            clients={clients}
            requirements={requirements}
            projectRequirements={projectRequirements}
            issues={issues}
            readiness={readiness}
            onToast={showToast}
            onRefreshProjectData={refreshSelectedProjectData}
          />
        );
      case "requirementAudits":
        return (
          <RequirementAuditsPage
            projectId={selectedProject?.id}
            onToast={showToast}
          />
        );
      case "issues":
        return (
          <IssuesPage
            issues={issues}
            requirements={requirements}
            initialFilter={issueFilter}
          />
        );
      case "reports":
        return (
          <ReportsPage
            projects={projects}
            readiness={readiness}
            documents={documents}
          />
        );
      case "compliance":
        return <CompliancePage project={selectedProject} />;
      case "modelHealth":
        return (
          <ModelHealthPage
            modelHealth={modelHealth}
            issues={issues}
            latestExport={latestExport}
            onOpenIssues={openIssues}
          />
        );
      case "documents":
        return (
          <DocumentsPage project={selectedProject} documents={documents} />
        );
      case "processing":
        return (
          <ProcessingPage
            project={selectedProject}
            latestExport={latestExport}
            syncLogs={syncLogs}
            issues={issues}
            documents={documents}
            snapshots={readinessSnapshots}
            onSnapshotCreated={(snapshot) =>
              setReadinessSnapshots((current) => [snapshot, ...current])
            }
            onToast={showToast}
            onOpenDebugLogs={() => setActivePage("debugLogs")}
            onSelectProject={(projectId) => {
              setSelectedProjectId(projectId);
            }}
            onOpenDocuments={() => setActivePage("documents")}
            onOpenRequirements={() => setActivePage("requirements")}
            onOpenProjectSetup={() => setActivePage("projectSetup")}
          />
        );
      case "drawingReel":
        return (
          <DrawingReelPage
            project={selectedProject}
            documents={documents}
            readiness={readiness}
          />
        );
      case "settings":
        return (
          <SettingsPage
            project={selectedProject}
            clients={clients}
            readiness={readiness}
            onBound={(project, client) => {
              setProjects((current) =>
                current.map((item) =>
                  item.id === project.id ? project : item,
                ),
              );
              setClients((current) =>
                current.some((item) => item.id === client.id)
                  ? current
                  : [...current, client],
              );
              showToast("Project client binding updated.", "success");
            }}
            onToast={showToast}
          />
        );
      case "users":
        return <UsersPage />;
      case "projectSetup":
        return (
          <ProjectSetupPage
            clients={clients}
            onToast={showToast}
            onProjectCreated={(project) => {
              setProjects((current) => {
                const exists = current.some((row) => row.id === project.id);
                return exists
                  ? current.map((row) =>
                      row.id === project.id ? project : row,
                    )
                  : [project, ...current];
              });
              setSelectedProjectId(project.id);
              setActivePage("processing");
            }}
          />
        );
      case "roles":
        return <RolesPermissionsPage />;
      case "appearance":
        return <AppearancePage />;
      case "devMode":
        return <DevModePage project={selectedProject} />;
      case "systemHealth":
        return <SystemHealthPage status={devStatus} />;
      case "debugLogs":
        return <DebugLogsPage project={selectedProject} projects={projects} />;
      default:
        return null;
    }
  })();

  if (!isAuthenticated || pathname === "/login") {
    return (
      <>
        <LoginPage
          projects={projects}
          onOpenSystemHealth={() => {
            const stored: DemoSession = {
              email: "alex@ema.ai",
              role: "Executive",
              projectName: "All Demo Projects",
              environment: "Local",
              createdAt: new Date().toISOString(),
            };
            try {
              window.localStorage.setItem(
                DEMO_SESSION_KEY,
                JSON.stringify(stored),
              );
            } catch {
              // Ignore local demo storage failures and continue routing.
            }
            setDemoSession(stored);
            window.history.pushState({}, "", "/");
            setPathname("/");
            setActivePage("systemHealth");
          }}
          onEnter={(session) => {
            setDemoSession(session);
            window.history.pushState({}, "", "/");
            setPathname("/");
            if (session.selectedProjectId) {
              setSelectedProjectId(session.selectedProjectId);
            }
            setActivePage("projects");
          }}
          onToast={showToast}
        />
        <Toast toast={toast} onClose={() => setToast(null)} />
      </>
    );
  }

  return (
    <Layout
      activePage={activePage}
      onPageChange={setActivePage}
      projects={projects}
      selectedProject={selectedProject}
      selectedProjectId={selectedProjectId}
      readiness={readiness}
      onProjectChange={setSelectedProjectId}
      onToast={showToast}
      onResetDemoSession={() => {
        logout();
        window.history.pushState({}, "", "/login");
        setPathname("/login");
      }}
      // onResetDemoSession={() => {
      //   try {
      //     window.localStorage.removeItem(DEMO_SESSION_KEY);
      //   } catch {
      //     // Ignore local demo storage failures and continue routing.
      //   }
      //   setDemoSession(null);
      //   window.history.pushState({}, "", "/login");
      //   setPathname("/login");
      // }}
    >
      {page}
      <Toast toast={toast} onClose={() => setToast(null)} />
    </Layout>
  );
}

function sanitizeDemoSession(value: unknown): DemoSession | null {
  if (!value || typeof value !== "object") {
    return null;
  }
  const record = value as Partial<DemoSession>;
  return {
    email:
      typeof record.email === "string" && record.email
        ? record.email
        : "alex@ema.ai",
    role:
      typeof record.role === "string" && record.role
        ? record.role
        : "Executive",
    projectName:
      typeof record.projectName === "string" && record.projectName
        ? record.projectName
        : "All Demo Projects",
    environment:
      typeof record.environment === "string" && record.environment
        ? record.environment
        : "Local",
    selectedProjectId:
      typeof record.selectedProjectId === "number"
        ? record.selectedProjectId
        : undefined,
    createdAt:
      typeof record.createdAt === "string" && record.createdAt
        ? record.createdAt
        : new Date().toISOString(),
  };
}

function PanelMessage({ title, detail }: { title: string; detail: string }) {
  return (
    <section className="ema-card p-8 text-center">
      <h2 className="text-base font-semibold text-ink">{title}</h2>
      <p className="mt-2 text-sm text-muted">{detail}</p>
    </section>
  );
}
