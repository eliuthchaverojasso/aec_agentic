import { useMemo } from "react";
import {
  USA_STATES,
  type UsaStateFeatureCollection,
} from "./data/usaStateOutlines";

const MAP_PADDING = 30;
const BOUNDS = {
  minLng: -125,
  maxLng: -66,
  minLat: 24,
  maxLat: 50,
};

type Props = {
  width: number;
  height: number;
  zoom?: number;
  panX?: number;
  panY?: number;
  className?: string;
};

export type UsaProjection = (point: [number, number]) => [number, number] | null;

export function createUsaProjection(
  width: number,
  height: number,
): UsaProjection {
  const mapWidth = width - MAP_PADDING * 2;
  const mapHeight = height - MAP_PADDING * 2;
  return ([lng, lat]: [number, number]) => {
    if (!Number.isFinite(lng) || !Number.isFinite(lat)) {
      return null;
    }
    const x = MAP_PADDING + ((lng - BOUNDS.minLng) / (BOUNDS.maxLng - BOUNDS.minLng)) * mapWidth;
    const y = MAP_PADDING + ((BOUNDS.maxLat - lat) / (BOUNDS.maxLat - BOUNDS.minLat)) * mapHeight;
    return [x, y];
  };
}

export function useUsaProjection(width: number, height: number): UsaProjection {
  return useMemo(() => createUsaProjection(width, height), [width, height]);
}

function renderStatePaths(
  projection: UsaProjection,
  geoData: UsaStateFeatureCollection,
): Array<{ name: string; abbr: string; d: string }> {
  return geoData.features.map((feature) => ({
    name: feature.properties.name,
    abbr: feature.properties.abbr,
    d: polygonToPath(feature.geometry.coordinates, projection),
  }));
}

function polygonToPath(coordinates: number[][][], projection: UsaProjection): string {
  return coordinates
    .map((ring) => {
      const points = ring
        .map(([lng, lat]) => projection([lng, lat]))
        .filter((point): point is [number, number] => Boolean(point));
      if (!points.length) {
        return "";
      }
      const [first, ...rest] = points;
      return `M ${first[0].toFixed(2)} ${first[1].toFixed(2)} ${rest.map(([x, y]) => `L ${x.toFixed(2)} ${y.toFixed(2)}`).join(" ")} Z`;
    })
    .filter(Boolean)
    .join(" ");
}

export function UsaMapRenderer({
  width,
  height,
  zoom = 1,
  panX = 0,
  panY = 0,
  className = "",
}: Props) {
  const projection = useUsaProjection(width, height);
  const statePaths = useMemo(
    () => renderStatePaths(projection, USA_STATES),
    [projection],
  );

  return (
    <g
      className={className}
      style={{
        transform: `translate(${panX}px, ${panY}px) scale(${zoom})`,
        transformOrigin: `${width / 2}px ${height / 2}px`,
      }}
    >
      {statePaths.map((state) => (
        <path
          key={state.abbr}
          className="ema-usa-map-state"
          data-state-abbr={state.abbr}
          data-state-name={state.name}
          d={state.d}
          aria-hidden
        />
      ))}
    </g>
  );
}
