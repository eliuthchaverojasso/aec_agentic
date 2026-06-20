import { AlertCircle, Box, Cable, FileWarning, PlugZap, Table2, FileJson, Clock } from "lucide-react";
import { useMemo, useState } from "react";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { KpiCard } from "../components/KpiCard";
import { ProgressBar } from "../components/ProgressBar";
import { StatusBadge } from "../components/StatusBadge";
import { formatDateTime, formatNumber } from "../lib/format";
import type { ExportRecord, Issue, ModelHealth } from "../types";

type ModelHealthPageProps = {
  modelHealth?: ModelHealth | null;
  issues: Issue[];
  latestExport?: ExportRecord | null;
  onOpenIssues: (filter?: { severity?: "high" | "critical" | "all"; search?: string }) => void;
};

export function ModelHealthPage({ modelHealth, issues, latestExport, onOpenIssues }: ModelHealthPageProps) {
  const elementsByCategory = Object.entries(modelHealth?.elements_by_category || {}).map(([name, value]) => ({ name, value }));
  const elementsByLevel = Object.entries(modelHealth?.elements_by_level || {}).map(([name, value]) => ({ name, value }));
  const issueData = [
    { name: "Critical", value: modelHealth?.critical_issues || 0 },
    { name: "High", value: modelHealth?.high_issues || 0 },
    { name: "Medium", value: modelHealth?.medium_issues || 0 },
    { name: "Low", value: modelHealth?.low_issues || 0 },
  ];
  const ruleData = Object.entries(
    issues.reduce<Record<string, number>>((counts, issue) => {
      const key = issue.rule_code || "Unknown";
      counts[key] = (counts[key] || 0) + 1;
      return counts;
    }, {}),
  ).map(([name, value]) => ({ name, value }));

  const [exportExpanded, setExportExpanded] = useState(false);

  if (!latestExport && (!modelHealth || modelHealth.total_elements === 0)) {
    return (
      <div className="space-y-6">
        <section className="ema-notice-warning p-8 text-center">
          <h2 className="text-lg font-semibold text-ink">No Revit export data available</h2>
          <p className="mt-2 text-sm text-muted">
            Export model from Revit ribbon or place export JSON under Revit Exports, then rebuild manifest and run ingest.
          </p>
          <p className="mt-2 text-xs text-muted">
            Model Health shows QA/QC quality metrics from the latest export and is separate from Owner Requirement Readiness.
          </p>
        </section>
      </div>
    );
  }

  return (
    <div className="ema-page ema-page-shell space-y-6">
      {/* Latest Export Info */}
      {latestExport && (
        <section className="ema-card p-5">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <FileJson size={18} className="text-info" />
              <h3 className="text-lg font-semibold text-ink">Latest Export</h3>
            </div>
            <button
              type="button"
              className="text-sm text-accent hover:underline"
              onClick={() => setExportExpanded(!exportExpanded)}
            >
              {exportExpanded ? "Collapse" : "Details"}
            </button>
          </div>
          <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
            <div className="ema-card p-3">
              <div className="text-xs font-semibold text-muted">Filename</div>
              <div className="mt-1 text-sm font-semibold text-ink truncate">{latestExport.file_name || "—"}</div>
            </div>
            <div className="ema-card p-3">
              <div className="text-xs font-semibold text-muted">Status</div>
              <div className="mt-1"><StatusBadge value={latestExport.status} /></div>
            </div>
            <div className="ema-card p-3">
              <div className="text-xs font-semibold text-muted">Elements</div>
              <div className="mt-1 text-sm font-semibold text-ink">{latestExport.element_count != null ? formatNumber(latestExport.element_count) : "—"}</div>
            </div>
            <div className="ema-card p-3">
              <div className="text-xs font-semibold text-muted">Exported</div>
              <div className="mt-1 text-sm text-ink">{formatDateTime(latestExport.started_at)}</div>
            </div>
            <div className="ema-card p-3">
              <div className="text-xs font-semibold text-muted">Model Health</div>
              <div className="mt-1 text-sm font-semibold text-ink">{modelHealth?.model_health_score != null ? `${Math.round(modelHealth.model_health_score)}%` : "—"}</div>
            </div>
          </div>
          {exportExpanded && (
            <div className="mt-4 ema-card p-4">
              <p className="text-xs font-semibold text-muted mb-2">Export Details</p>
              <div className="grid gap-2 text-sm sm:grid-cols-3">
                <div><span className="text-muted">Export ID:</span> {latestExport.id}</div>
                <div><span className="text-muted">Model ID:</span> {latestExport.model_id}</div>
                <div><span className="text-muted">Type:</span> {latestExport.export_type}</div>
                <div><span className="text-muted">Completed:</span> {formatDateTime(latestExport.completed_at) || "—"}</div>
                <div><span className="text-muted">Duration:</span> {latestExport.duration_seconds != null ? `${latestExport.duration_seconds}s` : "—"}</div>
              </div>
            </div>
          )}
        </section>
      )}

      {/* QA/QC Rule Cards */}
      <section className="ema-card p-5">
        <h3 className="text-base font-semibold text-ink">QA/QC Rules</h3>
        <p className="mt-1 text-sm text-muted">Deterministic rule checks run during ingestion.</p>
        <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {["R001", "R002", "R003", "R004"].map((code) => {
            const count = issues.filter((i) => i.rule_code === code).length;
            return (
              <div key={code} className="ema-card p-3">
                <div className="flex justify-between items-center">
                  <span className="text-xs font-semibold text-muted">{ruleName(code)}</span>
                  <span className={`text-sm font-semibold ${count > 0 ? "text-danger" : "text-accent"}`}>
                    {count} {count === 1 ? "issue" : "issues"}
                  </span>
                </div>
                <p className="mt-1 text-xs text-muted">{ruleDescription(code)}</p>
              </div>
            );
          })}
        </div>
      </section>

      {/* KPI Cards */}
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
        <KpiCard label="Total Elements" value={formatNumber(modelHealth?.total_elements)} detail="Across latest export" icon={Box} tone="teal" />
        <KpiCard label="Missing Parameters" value={formatNumber(modelHealth?.low_issues)} detail="Level and metadata gaps" icon={FileWarning} tone="amber" />
        <KpiCard label="Unconnected Elements" value={formatNumber(modelHealth?.high_issues)} detail="Connection checks" icon={Cable} tone="rose" />
        <KpiCard label="Open Issues" value={String(issues.length)} detail="Requires attention" icon={AlertCircle} tone="rose" />
        <KpiCard label="Last Sync" value={modelHealth?.last_sync_at ? formatDateTime(modelHealth.last_sync_at) : "—"} detail="Most recent export" icon={Clock} tone="violet" />
      </div>

      <section className="ema-cta-strip">
        <div>
          <h2 className="text-xl font-semibold">Model Quality Actions</h2>
          <p className="mt-1 text-sm text-subtle">Review and resolve model quality issues</p>
        </div>
        <div className="flex gap-3">
          <button
            className="ema-btn-secondary inline-flex h-10 items-center gap-2 px-4"
            type="button"
            onClick={() => onOpenIssues({ severity: "all" })}
          >
            <PlugZap size={16} />
            View Element Issues
          </button>
          <button
            className="ema-btn-secondary inline-flex h-10 items-center px-4 border-danger text-danger"
            type="button"
            onClick={() => onOpenIssues({ severity: "high" })}
          >
            Open Failed Checks
          </button>
        </div>
      </section>

      <div className="grid gap-5 xl:grid-cols-3">
        <ListPanel title="Elements by Category" rows={elementsByCategory.slice(0, 6)} />
        <ListPanel title="Elements by Level" rows={elementsByLevel.slice(0, 6)} />
        <ListPanel title="Distribution" rows={elementsByCategory.slice(0, 4)} progress />
      </div>

      <div className="grid gap-5 xl:grid-cols-2">
        <ChartPanel title="Issues by Severity" data={issueData} color="var(--ema-chart-3)" />
        <ChartPanel title="Rule Distribution" data={ruleData} color="var(--ema-chart-1)" />
      </div>

      <section className="ema-card" data-no-glass>
        <div className="ema-card-header">
          <div>
            <h3 className="text-lg font-semibold text-ink">Failed Checks - Detailed View</h3>
            <p className="mt-1 text-sm text-muted">Showing first {Math.min(issues.length, 5)} of {issues.length} loaded issues. Use Enterprise Issues for full pagination.</p>
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-line text-sm">
            <thead className="bg-surface-2 text-left text-xs font-semibold text-muted uppercase">
              <tr>
                <th className="px-5 py-3">Check Name</th>
                <th className="px-5 py-3">Category</th>
                <th className="px-5 py-3">Failed Elements</th>
                <th className="px-5 py-3">Severity</th>
                <th className="px-5 py-3">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {issues.slice(0, 5).map((issue) => (
                <tr key={issue.id} className="hover:bg-surface-2">
                  <td className="px-5 py-4 font-semibold text-ink">{issue.message || issue.issue_type}</td>
                  <td className="px-5 py-4 text-muted">{issue.rule_code || "Rule"}</td>
                  <td className="px-5 py-4 font-semibold text-danger">1 element</td>
                  <td className="px-5 py-4"><StatusBadge value={issue.severity} /></td>
                  <td className="px-5 py-4">
                    <button className="font-semibold text-accent hover:underline" type="button" onClick={() => onOpenIssues({ search: issue.rule_code || issue.issue_type || "" })}>
                      View Elements
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

function ruleName(code: string): string {
  const map: Record<string, string> = {
    R001: "Element Without Level",
    R002: "Unconnected Fixture",
    R003: "Fixture Missing Circuit",
    R004: "Panel Without Source",
  };
  return map[code] || code;
}

function ruleDescription(code: string): string {
  const map: Record<string, string> = {
    R001: "Elements missing level parameter",
    R002: "Fixture without panel assignment",
    R003: "Panel assigned but no circuit number",
    R004: "Panel without supply from source",
  };
  return map[code] || "";
}

function ListPanel({ title, rows, progress = false }: { title: string; rows: Array<{ name: string; value: number }>; progress?: boolean }) {
  const total = rows.reduce((sum, row) => sum + row.value, 0) || 1;
  return (
    <section className="ema-card p-5">
      <h3 className="text-base font-semibold text-ink">{title}</h3>
      <div className="mt-4 space-y-3">
        {rows.map((row, index) => (
          <div key={row.name}>
            <div className="flex justify-between gap-3 text-sm">
              <span className="text-muted">{row.name}</span>
              <span className="font-semibold text-ink">{formatNumber(row.value)}</span>
            </div>
            {progress && <div className="mt-2"><ProgressBar value={(row.value / total) * 100} tone={index % 2 ? "violet" : "blue"} /></div>}
          </div>
        ))}
      </div>
    </section>
  );
}

function ChartPanel({ title, data, color }: { title: string; data: Array<{ name: string; value: number }>; color: string }) {
  return (
    <section className="ema-card p-5">
      <h3 className="text-base font-semibold text-ink">{title}</h3>
      <div className="mt-4 h-72">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data}>
            <CartesianGrid stroke="var(--ema-chart-grid)" />
            <XAxis dataKey="name" tick={{ fontSize: 11 }} />
            <YAxis />
            <Tooltip formatter={(value) => formatNumber(Number(value))} />
            <Bar dataKey="value" fill={color} radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </section>
  );
}
