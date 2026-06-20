import type { EvidenceTraceLink, ViewpointReference, ViewerPackage } from "../../lib/viewerTypes";

type Props = {
  pkg?: ViewerPackage;
  links: EvidenceTraceLink[];
  viewpoints: ViewpointReference[];
};

export function TraceabilityPanel({ pkg, links, viewpoints }: Props) {
  return (
    <div className="ema-card p-4">
      <h4 className="text-sm font-semibold text-ink">Traceability</h4>
      <p className="mt-1 text-xs text-muted">
        Evidence candidate links are advisory until deterministic workflow acceptance.
      </p>
      <div className="mt-3 space-y-2">
        {links.length === 0 ? (
          <p className="text-sm text-muted">No linked issues/requirements/documents yet.</p>
        ) : (
          links.slice(0, 8).map((link) => (
            <div key={link.id} className="rounded-md border border-line bg-surface-2 px-3 py-2 text-xs">
              <div className="font-semibold text-ink">{link.label}</div>
              <div className="mt-1 text-muted">{link.sourceType} · {link.status}</div>
            </div>
          ))
        )}
      </div>
      <div className="mt-4">
        <h5 className="text-xs font-semibold uppercase tracking-wide text-muted">Viewpoints</h5>
        <div className="mt-2 space-y-2">
          {(viewpoints.length ? viewpoints : [{ id: "vp-none", name: "Viewpoint pending", status: "pending" as const }]).map((vp) => (
            <div key={vp.id} className="rounded-md border border-line bg-surface-2 px-3 py-2 text-xs">
              <div className="font-semibold text-ink">{vp.name}</div>
              <div className="mt-1 text-muted">{vp.status}</div>
            </div>
          ))}
        </div>
      </div>
      {pkg ? (
        <p className="mt-4 text-xs text-muted">
          Package: {pkg.fileName} · {pkg.sourceFormat.toUpperCase()}
        </p>
      ) : null}
    </div>
  );
}
