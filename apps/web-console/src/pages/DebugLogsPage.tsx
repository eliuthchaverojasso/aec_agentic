import { useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  Bug,
  ChevronDown,
  ChevronRight,
  Clock,
  Copy,
  RefreshCw,
  Terminal,
} from "lucide-react";
import { api } from "../api/client";
import { ErrorState } from "../components/ErrorState";
import { LoadingState } from "../components/LoadingState";
import type { DebugLog, ProjectSummary } from "../types";
import { formatDateTime } from "../lib/format";

type Props = {
  project?: ProjectSummary;
  projects: ProjectSummary[];
};

type SummaryData = {
  total: number;
  errors: number;
  warnings: number;
  last_operation: string;
};

export function DebugLogsPage({ project, projects }: Props) {
  const [logs, setLogs] = useState<DebugLog[]>([]);
  const [summary, setSummary] = useState<SummaryData | null>(null);
  const [environment, setEnvironment] = useState<Record<string, unknown> | null>(null);
  const [status, setStatus] = useState<"idle" | "loading" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  const [projectId, setProjectId] = useState<number | "">("");
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [copiedId, setCopiedId] = useState<number | null>(null);
  const [operationTypeFilter, setOperationTypeFilter] = useState<string>("all");

  useEffect(() => {
    setProjectId(project?.id || "");
  }, [project?.id]);

  const load = async () => {
    setStatus("loading");
    setError(null);
    try {
      const params = new URLSearchParams();
      if (projectId) {
        params.set("project_id", String(projectId));
      }
      params.set("limit", "200");
      const [rows, sum, env] = await Promise.all([
        api.getDebugLogs(params),
        api.getDebugLogsSummary(),
        api.getDebugEnvironment(),
      ]);
      setLogs(rows.items || []);
      setSummary(sum as unknown as SummaryData);
      setEnvironment(env);
      setStatus("idle");
    } catch (err) {
      setStatus("error");
      setError(err instanceof Error ? err.message : String(err));
    }
  };

  useEffect(() => {
    void load();
  }, [projectId]);

  const operationTypes = useMemo(() => {
    const types = new Set(logs.map((l) => l.operation_type).filter(Boolean));
    return ["all", ...Array.from(types).sort()];
  }, [logs]);

  const filteredLogs = useMemo(() => {
    if (operationTypeFilter === "all") return logs;
    return logs.filter((l) => l.operation_type === operationTypeFilter);
  }, [logs, operationTypeFilter]);

  const latest = useMemo(() => filteredLogs[0], [filteredLogs]);

  const handleCopy = async (log: DebugLog) => {
    await navigator.clipboard.writeText(JSON.stringify(log, null, 2));
    setCopiedId(log.id);
    setTimeout(() => setCopiedId(null), 2000);
  };

  if (status === "loading" && logs.length === 0) {
    return <LoadingState message="Loading debug logs..." />;
  }

  if (status === "error") {
    return <ErrorState message={error || "Failed to load debug logs."} onRetry={load} />;
  }

  return (
    <section className="ema-page ema-page-shell space-y-5">
      {/* Header */}
      <div className="ema-card p-5">
        <div className="flex items-center gap-3">
          <Bug size={22} className="text-accent" aria-hidden />
          <div>
            <h2 className="text-xl font-semibold text-ink">Debug / Logs</h2>
            <p className="mt-1 text-sm text-muted">
              Local observability for setup, landing, ingestion, and API operations. Logs are diagnostic only.
            </p>
          </div>
        </div>
      </div>

      {/* Controls + Summary */}
      <div className="ema-card p-5">
        <div className="flex flex-wrap items-end gap-3">
          <label className="text-sm text-muted">
            Project
            <select
              className="ema-input ml-2 px-2 py-1"
              value={projectId}
              onChange={(e) => setProjectId(e.target.value ? Number(e.target.value) : "")}
            >
              <option value="">All</option>
              {projects.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.project_name || p.project_title}
                </option>
              ))}
            </select>
          </label>
          <div className="flex flex-wrap gap-1">
            {operationTypes.map((type) => (
              <button
                key={type}
                type="button"
                className={`rounded-md px-2 py-1 text-xs font-medium transition ${
                  operationTypeFilter === type
                    ? "bg-accent text-white"
                    : "bg-surface-2 text-muted hover:bg-surface-3"
                }`}
                onClick={() => setOperationTypeFilter(type)}
              >
                {type === "all" ? "All" : type}
              </button>
            ))}
          </div>
          <button type="button" className="ema-btn-secondary inline-flex items-center gap-2" onClick={() => void load()}>
            <RefreshCw size={14} aria-hidden />
            Refresh
          </button>
          <button
            type="button"
            className="ema-btn-secondary inline-flex items-center gap-2"
            onClick={async () => {
              const bundle = await api.createDebugBundle(projectId ? Number(projectId) : undefined);
              await navigator.clipboard.writeText(JSON.stringify(bundle, null, 2));
            }}
          >
            <Terminal size={14} aria-hidden />
            Generate Debug Bundle
          </button>
        </div>

        <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Metric label="Total Logs" value={String(summary?.total || logs.length)} icon={Clock} />
          <Metric label="Errors" value={String(summary?.errors || 0)} icon={AlertTriangle} tone="danger" />
          <Metric label="Warnings" value={String(summary?.warnings || 0)} icon={AlertTriangle} tone="warning" />
          <Metric label="Last Operation" value={latest?.operation_type || "none"} icon={Terminal} />
        </div>
      </div>

      {/* Environment Diagnostics */}
      <div data-no-glass className="ema-card p-5">
        <div className="flex items-center gap-2">
          <Terminal size={16} className="text-muted" aria-hidden />
          <h3 className="font-semibold text-ink">Environment Diagnostics</h3>
        </div>
        <details className="mt-3">
          <summary className="cursor-pointer text-sm text-muted hover:text-ink">
            {environment ? "Show environment details" : "No environment data"}
          </summary>
          <pre className="ema-solid-json-surface mt-3 max-h-56 overflow-auto rounded p-3 text-xs text-ink">
            {JSON.stringify(environment || {}, null, 2)}
          </pre>
        </details>
      </div>

      {/* Operation Timeline */}
      <div data-no-glass className="ema-card p-5">
        <div className="flex items-center gap-2">
          <Clock size={16} className="text-muted" aria-hidden />
          <h3 className="font-semibold text-ink">Operation Timeline</h3>
          <span className="ml-auto text-xs text-muted">{filteredLogs.length} entries</span>
        </div>
        {filteredLogs.length === 0 ? (
          <div className="mt-4 flex flex-col items-center gap-2 py-8 text-muted">
            <Terminal size={28} className="opacity-40" aria-hidden />
            <p className="text-sm">No debug logs yet.</p>
            <p className="text-xs">Run a Processing / Sync operation to generate logs.</p>
          </div>
        ) : (
          <div className="mt-4 space-y-1">
            {filteredLogs.map((log) => {
              const isExpanded = expandedId === log.id;
              const errorsList = log.errors_json?.length > 0 ? log.errors_json : [];
              const warningsList = log.warnings_json?.length > 0 ? log.warnings_json : [];
              const hasDetails = log.request_summary_json && Object.keys(log.request_summary_json).length > 0;

              return (
                <div key={log.id} className="ema-timeline-card">
                  <div className={`ema-timeline-dot ${toneDotClass(log.severity)}`} />
                  <button
                    type="button"
                    className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm hover:bg-surface-2 transition"
                    onClick={() => setExpandedId(isExpanded ? null : log.id)}
                  >
                    <span className={`ema-pill ${tonePillClass(log.severity)} shrink-0`}>
                      {log.severity}
                    </span>
                    <span className="flex-1 truncate font-medium text-ink">
                      {log.operation_type}
                    </span>
                    <span className="shrink-0 text-xs text-muted">
                      {log.status}
                    </span>
                    <span className="shrink-0 text-xs text-subtle">
                      {log.duration_ms != null ? `${log.duration_ms}ms` : "—"}
                    </span>
                    <span className="shrink-0 text-xs text-subtle">
                      {formatDateTime(log.started_at)}
                    </span>
                    {isExpanded ? (
                      <ChevronDown size={14} className="shrink-0 text-muted" />
                    ) : (
                      <ChevronRight size={14} className="shrink-0 text-muted" />
                    )}
                  </button>
                  {isExpanded && (
                    <div className="ml-4 border-l-2 border-line pl-4 pb-3">
                      <div className="mt-2 flex flex-wrap gap-2 text-xs text-muted">
                        {log.request_id && (
                          <span className="rounded-md bg-surface-2 px-2 py-1 font-mono">
                            request: {log.request_id}
                          </span>
                        )}
                        {log.run_id && (
                          <span className="rounded-md bg-surface-2 px-2 py-1 font-mono">
                            run: {log.run_id}
                          </span>
                        )}
                        {log.endpoint && (
                          <span className="rounded-md bg-surface-2 px-2 py-1 font-mono">
                            {log.method || "GET"} {log.endpoint}
                          </span>
                        )}
                        {log.project_name && (
                          <span className="rounded-md bg-surface-2 px-2 py-1 font-mono">
                            project: {log.project_name}
                          </span>
                        )}
                      </div>

                      {errorsList.length > 0 && (
                        <div className="mt-2 space-y-1">
                          {errorsList.map((err, i) => (
                            <p key={i} className="text-xs text-danger">
                              {typeof err === "string" ? err : JSON.stringify(err)}
                            </p>
                          ))}
                        </div>
                      )}

                      {warningsList.length > 0 && (
                        <div className="mt-2 space-y-1">
                          {warningsList.map((warn, i) => (
                            <p key={i} className="text-xs text-warning">
                              {typeof warn === "string" ? warn : JSON.stringify(warn)}
                            </p>
                          ))}
                        </div>
                      )}

                      {hasDetails && (
                        <details className="mt-2">
                          <summary className="cursor-pointer text-xs text-muted hover:text-ink">
                            Request/Response summary
                          </summary>
                          <pre className="ema-solid-json-surface mt-2 max-h-40 overflow-auto rounded p-2 text-xs text-ink">
                            {JSON.stringify(log.request_summary_json, null, 2)}
                          </pre>
                          <pre className="ema-solid-json-surface mt-2 max-h-40 overflow-auto rounded p-2 text-xs text-ink">
                            {JSON.stringify(log.response_summary_json, null, 2)}
                          </pre>
                        </details>
                      )}

                      <div className="mt-3 flex items-center gap-2">
                        <button
                          type="button"
                          className="inline-flex items-center gap-1 rounded-md bg-surface-2 px-2 py-1 text-xs text-muted hover:text-ink transition"
                          onClick={() => handleCopy(log)}
                        >
                          <Copy size={12} aria-hidden />
                          {copiedId === log.id ? "Copied!" : "Copy log"}
                        </button>
                      </div>
                      <pre className="ema-solid-json-surface mt-2 max-h-60 overflow-auto rounded p-2 text-xs text-ink">
                        {JSON.stringify(log, null, 2)}
                      </pre>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </section>
  );
}

function Metric({
  label,
  value,
  icon: Icon,
  tone,
}: {
  label: string;
  value: string;
  icon: React.ComponentType<{ size?: number; className?: string }>;
  tone?: "danger" | "warning";
}) {
  return (
    <div className="ema-card p-3">
      <div className="flex items-center gap-2">
        <Icon size={14} className={tone === "danger" ? "text-danger" : tone === "warning" ? "text-warning" : "text-muted"} aria-hidden />
        <div className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</div>
      </div>
      <div className="mt-1 text-lg font-semibold text-ink">{value}</div>
    </div>
  );
}

function tonePillClass(severity: string): string {
  if (severity === "error" || severity === "critical") return "ema-pill-danger";
  if (severity === "warning") return "ema-pill-warning";
  return "ema-pill-success";
}

function toneDotClass(severity: string): string {
  if (severity === "error" || severity === "critical") return "ema-timeline-dot-danger";
  if (severity === "warning") return "ema-timeline-dot-warning";
  return "ema-timeline-dot-info";
}
