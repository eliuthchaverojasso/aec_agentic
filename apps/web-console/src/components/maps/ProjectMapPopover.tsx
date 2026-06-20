import type { ExecutiveProject, ProjectLocation } from "../../types";
import { formatDateTime } from "../../lib/format";
import { getProjectMarkerActions, labelForMapStatus, locationSourceLabel } from "./projectMapUtils";

type Props = {
  project: ExecutiveProject;
  location: ProjectLocation;
  onClose: () => void;
  onOpenProject?: (project: ExecutiveProject) => void;
  onOpenViewer?: (project: ExecutiveProject) => void;
  onOpenProcessing?: (project: ExecutiveProject) => void;
  onOpenDebug?: (project: ExecutiveProject) => void;
};

export function ProjectMapPopover({
  project,
  location,
  onClose,
  onOpenProject,
  onOpenViewer,
  onOpenProcessing,
  onOpenDebug,
}: Props) {
  const sourceLabel = location.isSynthetic ? "Demo Location" : locationSourceLabel(location.source);

  const runAction = (actionId: string) => {
    if (actionId === "project") onOpenProject?.(project);
    if (actionId === "viewer") onOpenViewer?.(project);
    if (actionId === "processing") onOpenProcessing?.(project);
    if (actionId === "debug") onOpenDebug?.(project);
  };

  return (
    <aside className="ema-map-popover" aria-live="polite">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h4 className="text-sm font-semibold text-ink">{project.name}</h4>
          <p className="mt-1 text-xs text-muted">{project.clientName || "Demo Client"} · {project.currentMilestone || "Milestone unavailable"}</p>
        </div>
        <button type="button" className="ema-map-control h-7 px-2 text-xs" onClick={onClose} aria-label="Close project map details">Close</button>
      </div>
      <dl className="mt-3 grid grid-cols-2 gap-2 text-xs">
        <MapDetail label="Status" value={labelForMapStatus(project.status)} />
        <MapDetail label="Readiness" value={`${project.readinessScore ?? 0}%`} />
        <MapDetail label="Open Issues" value={String(project.openIssues ?? 0)} />
        <MapDetail label="Documents" value={String(project.documentsIndexed ?? 0)} />
        <MapDetail label="Last Sync" value={formatDateTime(project.lastSync)} />
        <MapDetail label="Location Source" value={sourceLabel} />
      </dl>
      <div className="mt-3 rounded-lg border border-line bg-surface-solid/80 p-2 text-xs text-muted">
        {location.label || [location.city, location.state].filter(Boolean).join(", ") || "Project location unavailable"}
        {location.isSynthetic ? " · Synthetic demo coordinates, not an official project address." : null}
      </div>
      <div className="mt-3 flex flex-wrap gap-2">
        {getProjectMarkerActions().map((action) => (
          <button key={action.id} type="button" className="ema-btn-secondary text-xs" onClick={() => runAction(action.id)}>
            {action.label}
          </button>
        ))}
      </div>
    </aside>
  );
}

function MapDetail({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-line bg-surface-2/80 px-2 py-1.5">
      <dt className="text-[10px] font-semibold uppercase tracking-wide text-muted">{label}</dt>
      <dd className="mt-0.5 font-semibold text-ink">{value}</dd>
    </div>
  );
}
