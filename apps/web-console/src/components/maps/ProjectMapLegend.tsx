import type { ExecutiveProjectStatus } from "../../types";
import { labelForMapStatus } from "./projectMapUtils";

const statuses: ExecutiveProjectStatus[] = ["on_track", "in_execution", "behind", "blocked", "historical", "demo"];

export function ProjectMapLegend() {
  return (
    <div className="ema-map-legend" aria-label="Project marker legend">
      {statuses.map((status) => (
        <span key={status} className="inline-flex items-center gap-1.5">
          <span className={`ema-map-legend-dot ema-map-marker--${status.replace("_", "-")}`} aria-hidden />
          {labelForMapStatus(status)}
        </span>
      ))}
      <span className="inline-flex items-center gap-1.5">
        <span className="ema-map-legend-dot ema-map-marker--synthetic" aria-hidden />
        Demo Location
      </span>
    </div>
  );
}
