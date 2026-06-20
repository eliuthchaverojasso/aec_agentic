import { useState } from "react";
import { api } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import type { Client, ProjectReadiness, ProjectSummary } from "../types";

type SettingsPageProps = {
  project?: ProjectSummary;
  clients: Client[];
  readiness?: ProjectReadiness | null;
  onBound: (project: ProjectSummary, client: Client) => void;
  onToast: (message: string, tone?: "success" | "info" | "warning") => void;
};

export function SettingsPage({ project, clients, readiness, onBound, onToast }: SettingsPageProps) {
  const [clientId, setClientId] = useState(project?.client_id ? String(project.client_id) : "");
  const [clientCode, setClientCode] = useState("");
  const [clientName, setClientName] = useState("");
  const [milestone, setMilestone] = useState(project?.phase || "DD 75");
  const [saving, setSaving] = useState(false);

  const bind = async () => {
    if (!project) {
      return;
    }
    setSaving(true);
    try {
      const payload = clientId
        ? { client_id: Number(clientId), current_milestone: milestone }
        : { client_code: clientCode, client_name: clientName, current_milestone: milestone };
      const result = await api.bindProjectClient(project.id, payload);
      onBound(result.project, result.client);
    } catch (error) {
      onToast(error instanceof Error ? error.message : "Client binding failed.", "warning");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="ema-page ema-page-shell space-y-6">
      <section className="ema-card p-5">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 className="text-xl font-semibold text-ink">Project Binding</h2>
            <p className="text-sm text-muted">Bind the project to its owner/client so owner requirements can be evaluated.</p>
          </div>
          <StatusBadge value={project?.client_id ? "Ready" : "Pending"} />
        </div>

        {!project?.client_id && (
          <div className="ema-notice-warning mt-4">
            Project has no client linked. Owner requirements cannot be evaluated until client binding is completed.
          </div>
        )}

        <div className="mt-5 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <Info label="Project ID" value={project?.id ? String(project.id) : "-"} />
          <Info label="Project Name" value={project?.project_name || project?.project_title || "-"} />
          <Info label="Project Code" value={project?.project_code || "-"} />
          <Info label="Job Number" value={project?.job_number || "-"} />
          <Info label="Revit Version" value={project?.revit_version || "-"} />
          <Info label="Current Client" value={readiness?.client_name || project?.client_name || "None"} />
          <Info label="Client ID" value={project?.client_id ? String(project.client_id) : "None"} />
          <Info label="Readiness" value={readiness ? `${Math.round(readiness.overall_readiness)}% ${readiness.label}` : "Pending"} />
        </div>
      </section>

      <section className="ema-card p-5">
        <h3 className="text-lg font-semibold text-ink">Assign Client</h3>
        <div className="mt-4 grid gap-4 md:grid-cols-2">
          <label className="text-sm font-semibold text-ink">
            Existing Client
            <select className="ema-select mt-2 h-10 w-full px-3 font-normal" value={clientId} onChange={(event) => setClientId(event.target.value)}>
              <option value="">Create or enter by code/name</option>
              {clients.map((client) => <option key={client.id} value={client.id}>{client.display_name} ({client.code})</option>)}
            </select>
          </label>
          <label className="text-sm font-semibold text-ink">
            Current Milestone
            <select className="ema-select mt-2 h-10 w-full px-3 font-normal" value={milestone} onChange={(event) => setMilestone(event.target.value)}>
              {["DD 50", "DD 75", "DD 95", "CD 50", "CD 75", "CD 95", "CD 100", "Permit Review", "Submit Package"].map((item) => <option key={item}>{item}</option>)}
            </select>
          </label>
          <label className="text-sm font-semibold text-ink">
            Client Code
            <input className="ema-input mt-2 h-10 w-full px-3 font-normal" value={clientCode} onChange={(event) => setClientCode(event.target.value)} placeholder="ROCKWALL_ISD" disabled={Boolean(clientId)} />
          </label>
          <label className="text-sm font-semibold text-ink">
            Client Name
            <input className="ema-input mt-2 h-10 w-full px-3 font-normal" value={clientName} onChange={(event) => setClientName(event.target.value)} placeholder="Rockwall ISD" disabled={Boolean(clientId)} />
          </label>
        </div>
        <button className="ema-btn-primary mt-5 px-4 py-2 disabled:opacity-50" type="button" disabled={saving || !project} onClick={bind}>
          {saving ? "Saving..." : "Save Project Binding"}
        </button>
      </section>

      <section className="ema-card p-5">
        <h3 className="text-lg font-semibold text-ink">Local Demo Controls</h3>
        <div className="mt-4 grid gap-4 md:grid-cols-3">
          <Info label="Backend URL" value="http://localhost:8010" />
          <Info label="Official Source" value="PostgreSQL" />
          <Info label="AI/SEION" value="Advisory only" />
        </div>
      </section>
    </div>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div className="ema-card p-4">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</div>
      <div className="mt-2 text-sm font-semibold text-ink">{value}</div>
    </div>
  );
}
