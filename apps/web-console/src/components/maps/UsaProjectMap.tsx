import { Crosshair, LocateFixed, Minus, Plus } from "lucide-react";
import { useMemo, useState } from "react";
import type { ExecutiveProject } from "../../types";
import { ProjectMapLegend } from "./ProjectMapLegend";
import { ProjectMapMarker } from "./ProjectMapMarker";
import { ProjectMapPopover } from "./ProjectMapPopover";
import type { ProjectLocation } from "../../types";
import { normalizeProjectLocation, projectToMapPoint, resolveMarkerCollisions } from "./projectMapUtils";
import { useUsaProjection, UsaMapRenderer } from "./usaMapRenderer";

type Props = {
  projects: ExecutiveProject[];
  selectedProjectId?: string | number | null;
  onSelectProject?: (project: ExecutiveProject) => void;
  onOpenProject?: (project: ExecutiveProject) => void;
  onOpenViewer?: (project: ExecutiveProject) => void;
  onOpenProcessing?: (project: ExecutiveProject) => void;
  onOpenDebug?: (project: ExecutiveProject) => void;
  className?: string;
};

const VIEWBOX = { width: 900, height: 580 };
const ZOOM_MIN = 0.6;
const ZOOM_MAX = 2.8;
const PAN_STEP = 14;
const PAN_CLAMP = 300;

export function UsaProjectMap({
  projects,
  selectedProjectId,
  onSelectProject,
  onOpenProject,
  onOpenViewer,
  onOpenProcessing,
  onOpenDebug,
  className = "",
}: Props) {
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [selectedId, setSelectedId] = useState<string | number | null>(selectedProjectId ?? projects[0]?.id ?? null);
  const [showSynthetic, setShowSynthetic] = useState(true);

  const projection = useUsaProjection(VIEWBOX.width, VIEWBOX.height);

  const mappedProjects = useMemo(
    () => {
      const raw = projects
        .map((project) => {
          const location = normalizeProjectLocation(project);
          return { project, location, point: projectToMapPoint(location, projection) };
        })
        .filter((item): item is { project: ExecutiveProject; location: ProjectLocation; point: { x: number; y: number } } => item.point !== null)
        .filter((item) => showSynthetic || !item.location.isSynthetic);
      return resolveMarkerCollisions(raw, selectedProjectId ?? selectedId);
    },
    [projects, projection, showSynthetic, selectedProjectId, selectedId],
  );

  const selected = mappedProjects.find((item) => item.project.id === (selectedProjectId ?? selectedId)) || mappedProjects[0] || null;

  const selectProject = (project: ExecutiveProject) => {
    setSelectedId(project.id);
    onSelectProject?.(project);
  };

  const resetViewport = () => {
    setZoom(1);
    setPan({ x: 0, y: 0 });
  };

  const clampPan = (next: { x: number; y: number }) => ({
    x: Math.max(-PAN_CLAMP, Math.min(PAN_CLAMP, next.x)),
    y: Math.max(-PAN_CLAMP, Math.min(PAN_CLAMP, next.y)),
  });

  const doZoomIn = () => setZoom((value) => Math.min(ZOOM_MAX, Number((value + 0.12).toFixed(2))));
  const doZoomOut = () => setZoom((value) => Math.max(ZOOM_MIN, Number((value - 0.12).toFixed(2))));

  return (
    <section className={`ema-liquid-map-shell p-4 ${className}`}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="font-semibold text-ink">USA Project Map</h3>
          <p className="mt-1 max-w-2xl text-xs text-muted">
            Local USA geographic map with contiguous state boundaries · Synthetic demo coordinates are labeled as Demo Location when source coordinates are unavailable.
          </p>
        </div>
        <div className="flex flex-wrap gap-2 text-xs">
          <span className="ema-pill">Local Demo</span>
          <span className="ema-pill">Demo Coordinates</span>
          <span className="ema-pill">No External Map API</span>
        </div>
      </div>

      <div className="mt-3 grid gap-3 lg:grid-cols-[1fr_19rem]">
        <div className="ema-map-viewport">
          <div className="ema-map-controls">
            <button type="button" className="ema-map-control" onClick={doZoomIn} aria-label="Zoom in">
              <Plus size={14} />
            </button>
            <button type="button" className="ema-map-control" onClick={doZoomOut} aria-label="Zoom out">
              <Minus size={14} />
            </button>
            <button type="button" className="ema-map-control" onClick={resetViewport} aria-label="Reset USA map viewport">
              <LocateFixed size={14} />
              Reset
            </button>
            <button type="button" className="ema-map-control" onClick={() => setShowSynthetic((value) => !value)}>
              <Crosshair size={14} />
              {showSynthetic ? "Hide Demo" : "Show Demo"}
            </button>
          </div>

          {mappedProjects.length ? (
            <svg className="ema-map-svg" viewBox={`0 0 ${VIEWBOX.width} ${VIEWBOX.height}`} role="img" aria-label="Local demo USA project map viewport">
              <defs>
                <linearGradient id="ema-map-land" x1="0%" x2="100%" y1="0%" y2="100%">
                  <stop offset="0%" stopColor="var(--ema-accent-soft)" />
                  <stop offset="56%" stopColor="var(--ema-surface-2)" />
                  <stop offset="100%" stopColor="var(--ema-info-soft)" />
                </linearGradient>
              </defs>
              <rect width={VIEWBOX.width} height={VIEWBOX.height} className="ema-map-bg" />
              <UsaMapRenderer
                width={VIEWBOX.width}
                height={VIEWBOX.height}
                zoom={zoom}
                panX={pan.x}
                panY={pan.y}
              />
              {mappedProjects.map(({ project, location, point }) =>
                point ? (
                  <ProjectMapMarker
                    key={String(project.id)}
                    project={project}
                    location={location}
                    x={point.x}
                    y={point.y}
                    selected={project.id === selected?.project.id}
                    onSelect={() => selectProject(project)}
                  />
                ) : null,
              )}
            </svg>
          ) : (
            <div className="ema-liquid-empty-state p-6 text-sm text-muted">
              No project coordinates available. Add location metadata in Project Setup or use demo coordinates.
            </div>
          )}
          <div className="ema-map-pan-controls" aria-label="Map pan controls">
            <button type="button" className="ema-map-control" onClick={() => setPan((value) => clampPan({ ...value, y: value.y + PAN_STEP }))}>North</button>
            <button type="button" className="ema-map-control" onClick={() => setPan((value) => clampPan({ ...value, x: value.x + PAN_STEP }))}>West</button>
            <button type="button" className="ema-map-control" onClick={() => setPan((value) => clampPan({ ...value, x: value.x - PAN_STEP }))}>East</button>
            <button type="button" className="ema-map-control" onClick={() => setPan((value) => clampPan({ ...value, y: value.y - PAN_STEP }))}>South</button>
          </div>
        </div>

        <div className="space-y-3">
          <ProjectMapLegend />
          {selected ? (
            <ProjectMapPopover
              project={selected.project}
              location={selected.location}
              onClose={() => setSelectedId(null)}
              onOpenProject={onOpenProject}
              onOpenViewer={onOpenViewer}
              onOpenProcessing={onOpenProcessing}
              onOpenDebug={onOpenDebug}
            />
          ) : null}
        </div>
      </div>

      <div className="mt-3 ema-solid-data-surface rounded-lg p-3 text-xs text-muted">
        Demo Location markers are synthetic and do not represent official project addresses. No Google, Mapbox, ArcGIS, or external tile API is used. Map uses simplified local state geometry.
      </div>

      <div className="mt-3 grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
        {mappedProjects.map(({ project, location }) => (
          <button
            key={`list-${project.id}`}
            type="button"
            className="ema-map-project-row"
            onClick={() => selectProject(project)}
          >
            <span className="font-semibold text-ink">{project.name}</span>
            <span className="text-muted">{String(project.status || "demo").replace("_", " ")} · {location.isSynthetic ? "Demo Location" : location.label || "Project Location"}</span>
          </button>
        ))}
      </div>
    </section>
  );
}
