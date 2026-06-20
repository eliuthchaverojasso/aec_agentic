import { Eye } from "lucide-react";
import type { ViewerPackage } from "../../lib/viewerTypes";

export function ViewerStatusCard({ pkg }: { pkg?: ViewerPackage }) {
  if (!pkg) {
    return (
      <div className="ema-card p-4">
        <p className="text-sm text-muted">No viewer package selected.</p>
      </div>
    );
  }

  return (
    <div className="ema-card p-4">
      <div className="flex items-center justify-between gap-2">
        <h4 className="text-sm font-semibold text-ink">Viewer Status</h4>
        <Eye size={14} className="text-muted" />
      </div>
      <div className="mt-3 flex flex-wrap gap-2 text-xs">
        <span className="ema-pill">{pkg.viewerStatusLabel}</span>
        <span className="ema-pill">Format: {pkg.sourceFormat.toUpperCase()}</span>
        <span className="ema-pill">Evidence: {pkg.evidenceStatus}</span>
        <span className="ema-pill">Not Official Evidence</span>
      </div>
    </div>
  );
}
