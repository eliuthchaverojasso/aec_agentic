import type { ExecutiveProject, ExecutiveProjectStatus, ProjectLocation, ProjectLocationSource } from "../../types";
import { DEMO_PROJECT_LOCATIONS } from "./demoProjectLocations";

export type ProjectMapAction = {
  id: "project" | "viewer" | "processing" | "debug";
  label: string;
};

export function getStableHash(value: string | number | undefined) {
  const text = String(value ?? "ema-demo-project");
  let hash = 0;
  for (let index = 0; index < text.length; index += 1) {
    hash = (hash * 31 + text.charCodeAt(index)) >>> 0;
  }
  return hash;
}

export function getDeterministicDemoLocation(project: Pick<ExecutiveProject, "id" | "name" | "projectCode">): ProjectLocation {
  const hash = getStableHash(project.projectCode || project.name || project.id);
  const location = DEMO_PROJECT_LOCATIONS[hash % DEMO_PROJECT_LOCATIONS.length];
  return {
    ...location,
    label: `${location.label} · Synthetic Demo`,
    source: "synthetic_demo",
    isSynthetic: true,
  };
}

export function normalizeProjectLocation(project: ExecutiveProject): ProjectLocation {
  const source = project.location?.source || (project.location?.isSynthetic ? "synthetic_demo" : "unknown");
  const hasCoordinates = Number.isFinite(project.location?.lat) && Number.isFinite(project.location?.lng);

  if (hasCoordinates && source !== "unknown") {
    return {
      ...project.location,
      source,
      isSynthetic: source === "synthetic_demo" || Boolean(project.location?.isSynthetic),
      label: project.location?.label || formatLocationLabel(project.location, source),
    };
  }

  return getDeterministicDemoLocation(project);
}

type ProjectionFn = (point: [number, number]) => [number, number] | null;
export type GeoProjection = ProjectionFn;

export function projectToMapPoint(
  location: ProjectLocation,
  projection: GeoProjection,
): { x: number; y: number } | null {
  const lat = location.lat ?? 37;
  const lng = location.lng ?? -96;
  const projected = projection([lng, lat]);
  if (!projected) return null;
  const [x, y] = projected;
  return { x: Math.max(10, Math.min(890, x)), y: Math.max(10, Math.min(490, y)) };
}

export function getProjectMapStatus(project: ExecutiveProject): ExecutiveProjectStatus {
  return project.status || "demo";
}

export function getProjectMarkerSize(project: ExecutiveProject) {
  const readiness = project.readinessScore ?? 70;
  const issues = project.openIssues ?? 0;
  const critical = project.criticalIssues ?? 0;
  const riskBoost = readiness < 50 ? 5 : readiness < 65 ? 3 : 0;
  return Math.max(7, Math.min(18, 7 + Math.sqrt(issues + critical * 2) + riskBoost));
}

export function getProjectMarkerLabel(project: ExecutiveProject, location = normalizeProjectLocation(project)) {
  const source = location.isSynthetic ? "Demo Location" : locationSourceLabel(location.source);
  return `${project.name}. ${labelForMapStatus(project.status)}. ${source}.`;
}

export function getProjectMarkerActions(): ProjectMapAction[] {
  return [
    { id: "project", label: "Open Project" },
    { id: "viewer", label: "Open Model / Viewer" },
    { id: "processing", label: "Open Processing / Sync" },
    { id: "debug", label: "Open Debug / Logs" },
  ];
}

export function locationSourceLabel(source: ProjectLocationSource) {
  switch (source) {
    case "manual": return "Manual";
    case "client_provided": return "Client Provided";
    case "geocoded": return "Geocoded";
    case "synthetic_demo": return "Demo Location";
    default: return "Unknown";
  }
}

export function labelForMapStatus(status: ExecutiveProjectStatus) {
  switch (status) {
    case "historical": return "Historical";
    case "in_execution": return "In Execution";
    case "on_track": return "On Track";
    case "behind": return "Behind";
    case "blocked": return "Blocked";
    case "demo": return "Demo";
  }
}

function formatLocationLabel(location: ProjectLocation | undefined, source: ProjectLocationSource) {
  const cityState = [location?.city, location?.state].filter(Boolean).join(", ");
  const base = cityState || location?.country || "Project Location";
  return source === "synthetic_demo" ? `${base} · Demo Location` : base;
}

export type MappedItem = {
  project: ExecutiveProject;
  location: ProjectLocation;
  point: { x: number; y: number };
};

const COLLISION_MIN_DIST = 28;

export function resolveMarkerCollisions(
  items: MappedItem[],
  selectedId?: string | number | null,
): MappedItem[] {
  if (items.length < 2) return items;

  const sorted = [...items].sort((a, b) => {
    const aSel = a.project.id === selectedId ? 1 : 0;
    const bSel = b.project.id === selectedId ? 1 : 0;
    return aSel - bSel;
  });

  // Build incrementally so each item can reference already-placed items.
  // Cannot use .map() here because reading adjusted[j] inside its own
  // initializer triggers a TDZ ReferenceError.
  const adjusted: MappedItem[] = [];
  for (let index = 0; index < sorted.length; index++) {
    const item = sorted[index];
    let { x, y } = item.point;
    const hash = getStableHash(item.project.id);
    const seed = (hash * 31 + index * 7) >>> 0;

    for (let j = 0; j < index; j++) {
      const other = adjusted[j];
      if (!other) continue;
      const dx = x - other.point.x;
      const dy = y - other.point.y;
      const dist = Math.sqrt(dx * dx + dy * dy);

      if (dist < COLLISION_MIN_DIST && dist > 0.1) {
        const pushDist = (COLLISION_MIN_DIST - dist) / 2;
        const angle = (seed % 628) / 100;
        x += Math.cos(angle) * pushDist;
        y += Math.sin(angle) * pushDist;
      } else if (dist < 0.1) {
        const pushDist = COLLISION_MIN_DIST / 2;
        const angle = (seed % 628) / 100;
        x += Math.cos(angle) * pushDist;
        y += Math.sin(angle) * pushDist;
      }
    }

    adjusted.push({ ...item, point: { x, y } });
  }

  return adjusted;
}
