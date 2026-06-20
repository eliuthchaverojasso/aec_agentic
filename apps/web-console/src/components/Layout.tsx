import {
  Bell,
  Box,
  ClipboardCheck,
  FolderKanban,
  Gauge,
  Menu,
  RefreshCw,
  Search,
  ShieldCheck,
  X,
} from "lucide-react";
import type React from "react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { brandConfig } from "../brand/brandConfig";
import { demoMilestone, projectDisplayName } from "../lib/derived";
import { formatDateTime } from "../lib/format";
import { type SidebarMode } from "../lib/appearance";
import { useAppearanceSettings } from "../hooks/useAppearanceSettings";
import type { ProjectReadiness, ProjectSummary } from "../types";
import { PointerLight } from "./PointerLight";
import { useAuth } from "../context/AuthContext";

export type PageKey =
  | "projects"
  | "overview"
  | "trades"
  | "requirements"
  | "requirementAudits"
  | "drawingReel"
  | "viewer"
  | "modelHealth"
  | "processing"
  | "issues"
  | "compliance"
  | "documents"
  | "reports"
  | "settings"
  | "projectSetup"
  | "users"
  | "roles"
  | "appearance"
  | "devMode"
  | "systemHealth"
  | "debugLogs";

type LayoutProps = {
  activePage: PageKey;
  onPageChange: (page: PageKey) => void;
  projects: ProjectSummary[];
  selectedProject?: ProjectSummary;
  selectedProjectId?: number;
  readiness?: ProjectReadiness | null;
  onProjectChange: (projectId: number) => void;
  onToast: (message: string, tone?: "success" | "info" | "warning") => void;
  onResetDemoSession?: () => void;
  children: React.ReactNode;
};

const globalNav: Array<{ key: PageKey; label: string; icon: typeof Gauge }> = [
  { key: "projects", label: "Projects", icon: FolderKanban },
];

const projectNav: Array<{ key: PageKey; label: string; icon: typeof Gauge }> = [
  { key: "overview", label: "Deliverable Tracker", icon: Gauge },
  { key: "requirements", label: "Owner Requirements", icon: ClipboardCheck },
  { key: "requirementAudits", label: "Requirement Audits", icon: ShieldCheck },
  { key: "processing", label: "Data Sync", icon: RefreshCw },
];

const adminNav: Array<{ key: PageKey; label: string; icon: typeof Gauge }> = [
  { key: "projectSetup", label: "Project Setup", icon: FolderKanban },
];

const pageTitles: Record<PageKey, { title: string; subtitle: string }> = {
  projects: {
    title: "Project Deliverable Readiness",
    subtitle:
      "Track owner requirement coverage, discipline readiness, QA/QC exposure, and sync freshness across active projects.",
  },
  overview: {
    title: "Project Deliverable Tracker",
    subtitle: "Deterministic readiness based on owner requirements, QA/QC checks, and sync freshness.",
  },
  trades: {
    title: "Trade Readiness",
    subtitle: "Discipline-level readiness, requirement gaps, and critical issue exposure.",
  },
  requirements: {
    title: "Owner Requirements",
    subtitle: "Owner requirements, evidence status, gaps, and next actions for the selected project.",
  },
  requirementAudits: {
    title: "Requirement Audits",
    subtitle: "Reproducible evaluation runs: per-requirement audit dossiers, coherence findings, and immutable human review.",
  },
  modelHealth: {
    title: "Model Health",
    subtitle: "Supporting model quality metrics for the selected Revit export.",
  },
  processing: {
    title: "Processing / Sync",
    subtitle: "Manage and monitor health, sync status, and issues for the selected project.",
  },
  issues: {
    title: "Enterprise Issues",
    subtitle: "Open issues and QA/QC gaps generated from model ingestion.",
  },
  compliance: {
    title: "Code Compliance",
    subtitle: "Review code corpora, candidate rules, and loader gate status. Candidate data is not official compliance.",
  },
  drawingReel: {
    title: "Drawing Reel and Sheet Tracker",
    subtitle: "Review drawing sheets by milestone, trade, revision status, owner requirement coverage, and open markups.",
  },
  viewer: {
    title: "Project Viewer",
    subtitle: "Registered model packages, derivative status, and evidence traceability.",
  },
  settings: {
    title: "Admin / Settings",
    subtitle: "Project binding, API status, local demo controls, and source-of-truth boundaries.",
  },
  projectSetup: {
    title: "Project Setup",
    subtitle: "Create/bind client, project, and model, then configure landing and generate Revit binding JSON.",
  },
  documents: {
    title: "Documents / Evidence Candidates",
    subtitle: "Indexed landing documents and evidence candidate status from backend ingestion.",
  },
  reports: {
    title: "Reports",
    subtitle: "Export deterministic dashboard summaries and operational datasets.",
  },
  users: {
    title: "Users",
    subtitle: "Local demo users for planning and workflow walkthroughs. Not production authentication.",
  },
  roles: {
    title: "Roles & Permissions",
    subtitle: "Local UI permission matrix for demo planning only.",
  },
  appearance: {
    title: "Appearance",
    subtitle: "Display controls persisted in local storage.",
  },
  devMode: {
    title: "Dev Mode",
    subtitle: "Run local backend connectivity checks and landing operations from the web app.",
  },
  systemHealth: {
    title: "System Health",
    subtitle: "Backend/database status and endpoint availability matrix for local operations.",
  },
  debugLogs: {
    title: "Debug / Logs",
    subtitle: "Local observability for project setup, landing sync, ingestion, and API operations.",
  },
};

export function Layout({
  activePage,
  onPageChange,
  projects,
  selectedProject,
  selectedProjectId,
  readiness,
  onProjectChange,
  onToast,
  onResetDemoSession,
  children,
}: LayoutProps) {
  const { user } = useAuth();
  const { settings } = useAppearanceSettings();
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);

  const sidebarModeRef = useRef<SidebarMode>("expanded");
  if (typeof document !== "undefined") {
    sidebarModeRef.current = settings.sidebarMode;
  }

  const glassSidebar = settings.visualTheme === "liquidGlass" || settings.translucentSidebar;
  const current = pageTitles[activePage];
  const projectName = projectDisplayName(selectedProject);
  const client = readiness?.client_name || selectedProject?.client_name || "Client pending";
  const organizationName = settings.whiteLabel.organizationName || brandConfig.productName;
  const dashboardTitle = settings.whiteLabel.dashboardTitle || brandConfig.appName;
  const footerText = settings.whiteLabel.footerText || "Local Demo · Not Production · Not Official Compliance";

  const searchResults = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    if (!query) {
      return projects.slice(0, 5);
    }
    return projects.filter((project) =>
      `${project.project_name || ""} ${project.project_title} ${project.client_name || ""}`
        .toLowerCase()
        .includes(query),
    );
  }, [projects, searchQuery]);

  const closeMobileSidebar = useCallback(() => setMobileSidebarOpen(false), []);

  useEffect(() => {
    if (mobileSidebarOpen) {
      document.body.style.overflow = "hidden";
    } else {
      document.body.style.overflow = "";
    }

    return () => {
      document.body.style.overflow = "";
    };
  }, [mobileSidebarOpen]);

  const routeKind: "qa" | "debug" | "operational" =
    activePage === "appearance" ? "qa" :
    activePage === "devMode" ? "debug" :
    activePage === "debugLogs" ? "debug" :
    activePage === "systemHealth" ? "debug" :
    "operational";

  useEffect(() => {
    document.documentElement.dataset.routeKind = routeKind;
    document.documentElement.dataset.operationalSafe = routeKind === "operational" ? "true" : "false";

    return () => {
      delete document.documentElement.dataset.routeKind;
      delete document.documentElement.dataset.operationalSafe;
    };
  }, [routeKind]);

  const renderSidebarContent = (mobile: boolean) => {
    const currentSidebarMode = sidebarModeRef.current;

    return (
      <div
        className={`flex h-full flex-col border-r border-line ${
          glassSidebar ? "ema-glass-nav ema-liquid-sidebar" : "bg-surface"
        }`}
      >
        <div
          className={`flex h-16 items-center gap-3 border-b border-line px-4 ${
            currentSidebarMode === "iconOnly" ? "justify-center" : ""
          }`}
        >
          <span className="inline-flex h-7 w-7 items-center justify-center rounded-md text-accent">
            <Box size={23} aria-hidden />
          </span>
          <div className={`sidebar-nav-text min-w-0 ${currentSidebarMode === "iconOnly" ? "hidden" : ""}`}>
            <div className="truncate text-[10px] font-semibold uppercase tracking-wide text-muted">
              {organizationName}
            </div>
            <div className="truncate text-lg font-semibold text-ink">{dashboardTitle}</div>
          </div>
        </div>

        <nav className="flex-1 overflow-y-auto px-2 py-4">
          {globalNav.map((item) => (
            <SidebarButton
              key={item.key}
              active={activePage === item.key}
              icon={item.icon}
              label={item.label}
              compact={currentSidebarMode === "iconOnly" || currentSidebarMode === "compact"}
              onClick={() => {
                onPageChange(item.key);
                closeMobileSidebar();
              }}
            />
          ))}

          <div className="sidebar-nav-text mt-6 px-3 text-xs font-semibold uppercase tracking-wide text-muted">
            Project
          </div>

          {projectNav.map((item) => (
            <SidebarButton
              key={item.key}
              active={activePage === item.key}
              icon={item.icon}
              label={item.label}
              compact={currentSidebarMode === "iconOnly" || currentSidebarMode === "compact"}
              onClick={() => {
                onPageChange(item.key);
                closeMobileSidebar();
              }}
            />
          ))}
        </nav>

        <div className="border-t border-line p-3">
          {adminNav.map((item) => (
            <SidebarButton
              key={item.key}
              active={activePage === item.key}
              icon={item.icon}
              label={item.label}
              compact={currentSidebarMode === "iconOnly" || currentSidebarMode === "compact"}
              onClick={() => {
                onPageChange(item.key);
                closeMobileSidebar();
              }}
            />
          ))}

          <div
            className={`ema-liquid-panel mt-4 flex items-center gap-3 px-3 py-3 ${
              currentSidebarMode === "iconOnly" ? "justify-center" : ""
            }`}
          >
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-accent text-xs font-semibold text-inverse">
              AD
            </div>
            {currentSidebarMode !== "iconOnly" && (
              <div className="min-w-0">
                <div className="truncate text-sm font-semibold text-ink">Alex Director</div>
                <div className="truncate text-xs text-muted">alex@ema.ai</div>
              </div>
            )}
          </div>
        </div>
      </div>
    );
  };

  return (
    <div className="ema-app-shell ema-material-page ema-route-gradient bg-canvas text-ink">
      <div className="ema-app-ambient" />
      <PointerLight />

      <aside className="ema-sidebar-desktop fixed inset-y-0 left-0 z-20 hidden lg:flex lg:flex-col">
        {renderSidebarContent(false)}
      </aside>

      {mobileSidebarOpen && (
        <div className="fixed inset-0 z-40 flex lg:hidden">
          <div
            className="fixed inset-0 bg-slate-950/30 backdrop-blur-sm"
            onClick={closeMobileSidebar}
            aria-hidden
          />
          <div className="relative z-50 w-64 shrink-0">
            {renderSidebarContent(true)}
          </div>
        </div>
      )}

      <div className="ema-main">
        <header className="ema-liquid-topbar sticky top-0 z-10 border-b border-line bg-surface">
          <div className="flex min-h-16 flex-wrap items-center justify-between gap-4 px-4 py-3 lg:px-6">
            <div className="flex min-w-0 items-center gap-3">
              <button
                className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-md text-muted hover:bg-surface-2 lg:hidden"
                type="button"
                onClick={() => setMobileSidebarOpen(true)}
                aria-label="Open navigation menu"
              >
                <Menu size={20} />
              </button>

              <div className="min-w-0">
                {activePage === "projects" ? (
                  <>
                    <h1 className="text-xl font-semibold text-ink lg:text-2xl">{current.title}</h1>
                    <p className="truncate text-sm text-muted">{current.subtitle}</p>
                  </>
                ) : (
                  <>
                    <div className="flex flex-wrap items-center gap-2">
                      <h1 className="text-lg font-semibold text-ink lg:text-xl">{projectName}</h1>
                      <span className="ema-liquid-chip px-2 py-1 text-xs font-medium text-accent">
                        Demo: {demoMilestone.stageLabel}
                      </span>
                    </div>
                    <p className="mt-1 truncate text-sm text-muted">
                      {client} <span className="mx-2">|</span> {current.title}
                    </p>
                  </>
                )}
              </div>
            </div>

            <div className="flex shrink-0 flex-wrap items-center gap-3">
              <label className="hidden items-center gap-2 text-sm text-muted xl:flex">
                Project
                <select
                  className="ema-select h-9 max-w-64 px-3 text-sm"
                  value={selectedProjectId ?? ""}
                  onChange={(event) => onProjectChange(Number(event.target.value))}
                >
                  {projects.map((project) => (
                    <option key={project.id} value={project.id}>
                      {projectDisplayName(project)}
                    </option>
                  ))}
                </select>
              </label>

              <div className="ema-liquid-capsule flex items-center gap-2 px-3 py-2 text-sm text-muted">
                <RefreshCw size={15} aria-hidden />
                Last Sync: {formatDateTime(readiness?.latest_sync_at || selectedProject?.last_sync_at)}
              </div>

              <button
                className="ema-liquid-button inline-flex h-9 w-9 items-center justify-center text-muted hover:text-ink"
                type="button"
                onClick={() => setNotificationsOpen((open) => !open)}
              >
                <Bell size={18} aria-label="Notifications" />
              </button>

              <button
                className="ema-liquid-button inline-flex h-9 w-9 items-center justify-center text-muted hover:text-ink"
                type="button"
                onClick={() => setSearchOpen(true)}
              >
                <Search size={18} aria-label="Search" />
              </button>
            </div>
          </div>

          {notificationsOpen && (
            <div className="ema-liquid-panel absolute right-16 top-14 z-30 w-80 p-4">
              <div className="text-sm font-semibold text-ink">Pilot notifications</div>
              <div className="mt-3 space-y-3 text-sm">
                <NotificationItem
                  title="Readiness behind"
                  detail={`${projectName} is currently ${readiness?.label || "pending"}.`}
                />
                <NotificationItem
                  title="Owner requirements loaded"
                  detail={`${client} requirements are available for review.`}
                />
                <NotificationItem
                  title="Latest sync complete"
                  detail={formatDateTime(readiness?.latest_sync_at || selectedProject?.last_sync_at)}
                />
              </div>
            </div>
          )}
        </header>

        <div className="relative" data-route-kind={routeKind}>
          <div className="refraction-field" />
          <main className="ema-route-content ema-page-shell">{children}</main>
        </div>

        <footer className="ema-liquid-topbar flex flex-wrap items-center justify-between gap-3 border-t border-line px-6 py-4 text-sm text-muted">
          <div className="flex flex-wrap gap-5">
            <span>{brandConfig.productName} Dashboard v2.5.0</span>
            <span className="ema-local-demo-watermark text-xs" data-always-visible="false">
              {footerText}
            </span>

            <button
              className="hover:text-ink"
              type="button"
              onClick={() => onToast("Documentation is included in the pilot package under docs/.", "info")}
            >
              {brandConfig.support.docsLabel}
            </button>

            {onResetDemoSession ? (
              <button className="hover:text-ink" type="button" onClick={onResetDemoSession}>
                {/* Reset Local Demo Session */}
                Logout
              </button>
            ) : null}

            <button
              className="hover:text-ink"
              type="button"
              onClick={() => onToast("Pilot support contact: Alex Director / EMA AI validation team.", "info")}
            >
              {brandConfig.support.supportLabel}
            </button>
          </div>

          <div className="flex flex-wrap gap-5">
            <span className="flex items-center gap-2">
              <span className="h-2 w-2 rounded-full bg-accent" />
              API Status: Operational
            </span>
            <span>Data Freshness: 99.8%</span>
          </div>
        </footer>
      </div>

      <PilotSettingsModal isOpen={settingsOpen} onClose={() => setSettingsOpen(false)} />

      <GlobalSearchModal
        isOpen={searchOpen}
        query={searchQuery}
        results={searchResults}
        onQueryChange={setSearchQuery}
        onClose={() => setSearchOpen(false)}
        onSelect={(projectId) => {
          onProjectChange(projectId);
          setSearchOpen(false);
        }}
      />
    </div>
  );
}

function NotificationItem({ title, detail }: { title: string; detail: string }) {
  return (
    <div className="rounded-md border border-line bg-surface-2 p-3">
      <div className="font-semibold text-ink">{title}</div>
      <div className="mt-1 text-muted">{detail}</div>
    </div>
  );
}

function PilotSettingsModal({ isOpen, onClose }: { isOpen: boolean; onClose: () => void }) {
  if (!isOpen) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/30 px-4">
      <div className="ema-glass-panel w-full max-w-lg p-5">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="text-base font-semibold text-ink">Pilot Settings</h2>
            <p className="mt-1 text-sm text-muted">Demo-safe configuration for the local validation package.</p>
          </div>
          <button type="button" className="rounded-md p-2 text-muted hover:bg-surface-2" onClick={onClose}>
            <X size={17} aria-label="Close settings" />
          </button>
        </div>

        <dl className="mt-5 grid gap-3 text-sm sm:grid-cols-2">
          <SettingItem label="API Base URL" value="http://localhost:8010" />
          <SettingItem label="Dashboard URL" value="http://localhost:5173" />
          <SettingItem label="Mode" value="Controlled pilot demo" />
          <SettingItem label="AI Query" value="Hidden from pilot nav" />
        </dl>
      </div>
    </div>
  );
}

function SettingItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-line bg-surface-2 p-3">
      <dt className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</dt>
      <dd className="mt-1 font-medium text-ink">{value}</dd>
    </div>
  );
}

function GlobalSearchModal({
  isOpen,
  query,
  results,
  onQueryChange,
  onClose,
  onSelect,
}: {
  isOpen: boolean;
  query: string;
  results: ProjectSummary[];
  onQueryChange: (query: string) => void;
  onClose: () => void;
  onSelect: (projectId: number) => void;
}) {
  if (!isOpen) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center bg-slate-950/30 px-4 pt-24">
      <div className="ema-glass-panel w-full max-w-2xl p-4">
        <div className="flex items-center gap-3">
          <Search size={18} className="text-muted" aria-hidden />
          <input
            autoFocus
            className="h-11 flex-1 border-0 bg-transparent text-base text-ink outline-none placeholder:text-muted"
            placeholder="Search projects, clients, or pilot data..."
            value={query}
            onChange={(event) => onQueryChange(event.target.value)}
          />
          <button type="button" className="rounded-md p-2 text-muted hover:bg-surface-2" onClick={onClose}>
            <X size={17} aria-label="Close search" />
          </button>
        </div>

        <div className="mt-4 divide-y divide-line rounded-lg border border-line">
          {results.map((project) => (
            <button
              key={project.id}
              type="button"
              className="flex w-full items-center justify-between px-4 py-3 text-left hover:bg-surface-2"
              onClick={() => onSelect(project.id)}
            >
              <span>
                <span className="block font-semibold text-ink">{projectDisplayName(project)}</span>
                <span className="text-sm text-muted">{project.client_name || "Client pending"}</span>
              </span>
              <span className="text-sm font-semibold text-accent">Open</span>
            </button>
          ))}

          {results.length === 0 && <div className="px-4 py-6 text-sm text-muted">No matching projects.</div>}
        </div>
      </div>
    </div>
  );
}

function SidebarButton({
  active,
  icon: Icon,
  label,
  compact,
  onClick,
}: {
  active: boolean;
  icon: typeof Gauge;
  label: string;
  compact: boolean;
  onClick: () => void;
}) {
  return (
    <button
      className={`ema-liquid-nav-item mt-1 flex h-10 w-full items-center gap-3 px-3 text-left text-sm font-semibold transition ${
        compact ? "justify-center" : ""
      } ${
        active
          ? "ema-liquid-selected text-accent"
          : "text-muted hover:bg-surface-2 hover:text-ink"
      }`}
      onClick={onClick}
      type="button"
      title={compact ? label : undefined}
    >
      <Icon size={18} aria-hidden className="shrink-0" />
      {!compact && <span className="truncate">{label}</span>}
    </button>
  );
}