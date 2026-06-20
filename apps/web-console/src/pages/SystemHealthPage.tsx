import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { DevStatus } from "../types";

type EndpointCheck = {
  endpoint: string;
  method: string;
  status: "pending" | "ok" | "fail";
  detail?: string;
};

const LANDING_ENDPOINTS: Array<{ endpoint: string; method: string; check: () => Promise<unknown> }> = [
  { endpoint: "/api/v1/landing/projects", method: "GET", check: () => api.getLandingProjects() },
  { endpoint: "/api/v1/landing/rebuild-all-manifests", method: "POST (dry-run)", check: () => api.rebuildAllLandingManifests({ dry_run: true }) },
  { endpoint: "/api/v1/landing/ingest-all", method: "POST (dry-run)", check: () => api.ingestAllLandingProjects({ dry_run: true }) },
  { endpoint: "/api/v1/landing/projects/discover", method: "POST", check: () => api.discoverLandingProjects(".") },
];

const CORE_ENDPOINTS: Array<{ endpoint: string; method: string; check: () => Promise<unknown> }> = [
  { endpoint: "/api/v1/dev/status", method: "GET", check: () => api.getDevStatus() },
  { endpoint: "/api/v1/dev/smoke-test", method: "GET", check: () => api.runDevSmokeTest() },
  { endpoint: "/health", method: "GET", check: () => api.health() },
];

type EndpointSection = { label: string; items: typeof CORE_ENDPOINTS };

export function SystemHealthPage({ status }: { status: DevStatus | null }) {
  const [environment, setEnvironment] = useState<Record<string, unknown> | null>(null);
  const [debugSummary, setDebugSummary] = useState<Record<string, unknown> | null>(null);
  const [landingChecks, setLandingChecks] = useState<EndpointCheck[]>(() =>
    LANDING_ENDPOINTS.map((e) => ({ endpoint: e.endpoint, method: e.method, status: "pending" as const })),
  );
  const [coreChecks, setCoreChecks] = useState<EndpointCheck[]>(() =>
    CORE_ENDPOINTS.map((e) => ({ endpoint: e.endpoint, method: e.method, status: "pending" as const })),
  );

  useEffect(() => {
    api.getDebugEnvironment().then(setEnvironment).catch(() => setEnvironment(null));
    api.getDebugLogsSummary().then((s) => setDebugSummary(s as unknown as Record<string, unknown>)).catch(() => setDebugSummary(null));

    for (const ep of LANDING_ENDPOINTS) {
      ep.check()
        .then(() => setLandingChecks((prev) => prev.map((c) => c.endpoint === ep.endpoint ? { ...c, status: "ok" } : c)))
        .catch((err: Error) => setLandingChecks((prev) => prev.map((c) => c.endpoint === ep.endpoint ? { ...c, status: "fail", detail: err.message } : c)));
    }

    for (const ep of CORE_ENDPOINTS) {
      ep.check()
        .then(() => setCoreChecks((prev) => prev.map((c) => c.endpoint === ep.endpoint ? { ...c, status: "ok" } : c)))
        .catch((err: Error) => setCoreChecks((prev) => prev.map((c) => c.endpoint === ep.endpoint ? { ...c, status: "fail", detail: err.message } : c)));
    }
  }, []);

  const backend = status?.backend_health || status?.status || "unknown";
  const database = status?.database_health || status?.database || "unknown";

  return (
    <section className="ema-page ema-page-shell space-y-5">
      <div className="ema-liquid-section p-5">
        <h2 className="text-lg font-semibold text-ink">System Health</h2>
        <p className="mt-1 text-sm text-muted">Backend and database status for local development mode.</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Metric label="Backend Status" value={backend} />
        <Metric label="Database" value={database} />
        <Metric label="API Version" value={status?.app_version || status?.version || "unknown"} />
        <Metric label="Readiness Availability" value={status?.readiness_available ? "available" : "unknown"} />
      </div>

      {/* Core Endpoints */}
      <EndpointMatrixSection title="Core Endpoints" checks={coreChecks} />

      {/* Landing Endpoints */}
      <EndpointMatrixSection title="Landing Endpoints" checks={landingChecks} subtitle="Landing root path and project discovery endpoints. Failures may indicate missing landing root config." />

      {/* Landing Path Diagnostics */}
      <div className="ema-solid-data-surface rounded-lg p-5">
        <h3 className="font-semibold text-ink">Landing Path Configuration</h3>
        <div className="mt-3 space-y-2 text-sm">
          <DiagnosticRow label="Landing Root Configured" value={status?.landing_root_configured ? "Yes" : "No / Unknown"} />
          <DiagnosticRow label="Default Project Folder" value={status?.default_project_folder ?? "Not set"} />
          <DiagnosticRow label="Selected Project Folder" value={status?.selected_project_folder ?? "Not set"} />
          {status?.endpoint_availability && (
            <>
              {Object.entries(status.endpoint_availability).map(([ep, ok]) => (
                <DiagnosticRow key={ep} label={ep} value={ok ? "Available" : "Unavailable"} />
              ))}
            </>
          )}
        </div>
      </div>

      <div className="ema-solid-data-surface rounded-lg p-5">
        <h3 className="font-semibold text-ink">Debug Environment</h3>
        <pre className="ema-solid-json-surface mt-3 overflow-auto rounded-md p-3 text-xs">{JSON.stringify(environment || {}, null, 2)}</pre>
      </div>
      <div className="ema-solid-data-surface rounded-lg p-5">
        <h3 className="font-semibold text-ink">Latest Debug Summary</h3>
        <pre className="ema-solid-json-surface mt-3 overflow-auto rounded-md p-3 text-xs">{JSON.stringify(debugSummary || {}, null, 2)}</pre>
      </div>
    </section>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="ema-liquid-health-card ema-anim-hover-lift p-4">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</div>
      <div className="mt-1 text-base font-semibold text-ink">{value}</div>
    </div>
  );
}

function EndpointMatrixSection({ title, checks, subtitle }: { title: string; checks: EndpointCheck[]; subtitle?: string }) {
  return (
    <div className="ema-solid-data-surface rounded-lg p-5">
      <h3 className="font-semibold text-ink">{title}</h3>
      {subtitle && <p className="mt-1 text-xs text-muted">{subtitle}</p>}
      <div className="mt-3 overflow-x-auto">
        <table className="min-w-full divide-y divide-line text-sm">
          <thead className="text-left text-xs uppercase tracking-wide text-muted">
            <tr>
              <th className="px-3 py-2">Status</th>
              <th className="px-3 py-2">Method</th>
              <th className="px-3 py-2">Endpoint</th>
              <th className="px-3 py-2">Detail</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {checks.map((check) => (
              <tr key={check.endpoint + check.method}>
                <td className="px-3 py-2">
                  {check.status === "pending" && <span className="inline-block h-3 w-3 rounded-full" style={{ backgroundColor: "var(--ema-text-muted)" }} title="Pending" />}
                  {check.status === "ok" && <span className="inline-block h-3 w-3 rounded-full" style={{ backgroundColor: "var(--ema-success)" }} title="OK" />}
                  {check.status === "fail" && <span className="inline-block h-3 w-3 rounded-full" style={{ backgroundColor: "var(--ema-error)" }} title="Failed" />}
                </td>
                <td className="px-3 py-2 font-mono text-xs text-muted">{check.method}</td>
                <td className="px-3 py-2 font-mono text-xs text-ink">{check.endpoint}</td>
                <td className="px-3 py-2 text-xs text-muted">{check.detail || (check.status === "ok" ? "Responded" : "-")}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function DiagnosticRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between rounded-md border border-line bg-surface px-3 py-2">
      <span className="text-muted">{label}</span>
      <span className="font-mono text-xs text-ink">{value}</span>
    </div>
  );
}
