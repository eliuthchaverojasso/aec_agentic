import { Box, ExternalLink } from "lucide-react";
import type { ViewerPackage } from "../../lib/viewerTypes";

type Props = {
  pkg?: ViewerPackage;
  onOpenProcessing?: () => void;
  onOpenDocuments?: () => void;
  onOpenDebug?: () => void;
};

export function ApsViewerPlaceholder({ pkg, onOpenProcessing, onOpenDocuments, onOpenDebug }: Props) {
  return (
    <div className="ema-card p-5">
      <div className="flex items-center gap-3">
        <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-accent-soft text-accent">
          <Box size={22} />
        </span>
        <div>
          <h3 className="text-lg font-semibold text-ink">Model / Viewer</h3>
          <p className="text-sm text-muted">
            Registered model packages, derivative status, and traceability.
          </p>
        </div>
      </div>
      <div className="mt-4 rounded-lg border border-line bg-surface-2 p-4 text-sm">
        <p className="font-medium text-ink">{pkg?.fileName || "No package selected"}</p>
        <p className="mt-1 text-muted">
          APS derivative viewer is not configured in this local demo.
        </p>
        <p className="mt-1 text-muted">
          Browser-native DWFx rendering is not active in this local demo.
        </p>
      </div>
      <div className="mt-4 flex flex-wrap gap-2">
        <button type="button" className="ema-btn-secondary inline-flex items-center gap-2">
          <ExternalLink size={14} />
          Open Externally
        </button>
        <button type="button" className="ema-btn-secondary" onClick={onOpenProcessing}>
          Open Processing / Sync
        </button>
        <button type="button" className="ema-btn-secondary" onClick={onOpenDocuments}>
          Open Documents / Evidence
        </button>
        <button type="button" className="ema-btn-secondary" onClick={onOpenDebug}>
          Open Debug / Logs
        </button>
      </div>
    </div>
  );
}
