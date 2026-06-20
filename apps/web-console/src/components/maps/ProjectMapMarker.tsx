import type { ExecutiveProject, ProjectLocation } from "../../types";
import { getProjectMapStatus, getProjectMarkerLabel, getProjectMarkerSize } from "./projectMapUtils";

type Props = {
  project: ExecutiveProject;
  location: ProjectLocation;
  x: number;
  y: number;
  selected: boolean;
  onSelect: () => void;
};

export function ProjectMapMarker({ project, location, x, y, selected, onSelect }: Props) {
  const status = getProjectMapStatus(project);
  const radius = getProjectMarkerSize(project);

  return (
    <g
      className={`ema-map-marker ema-map-marker--${status.replace("_", "-")} ${location.isSynthetic ? "ema-map-marker--synthetic" : ""} ${selected ? "is-selected" : ""}`}
      role="button"
      tabIndex={0}
      aria-label={getProjectMarkerLabel(project, location)}
      onClick={onSelect}
      onKeyDown={(event) => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          onSelect();
        }
      }}
    >
      <circle className="ema-map-marker-ring" cx={x} cy={y} r={radius * 1.25} />
      <circle className="ema-map-marker-core" cx={x} cy={y} r={radius * 0.72} />
      {location.isSynthetic ? <circle className="ema-map-marker-demo-ring" cx={x} cy={y} r={radius * 1.7} /> : null}
    </g>
  );
}
