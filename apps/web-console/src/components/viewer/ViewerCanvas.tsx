import type { ViewerPackage } from "../../lib/viewerTypes";
import { ApsViewerPlaceholder } from "./ApsViewerPlaceholder";

type Props = {
  pkg?: ViewerPackage;
  onOpenProcessing?: () => void;
  onOpenDocuments?: () => void;
  onOpenDebug?: () => void;
};

export function ViewerCanvas({ pkg, onOpenProcessing, onOpenDocuments, onOpenDebug }: Props) {
  if (!pkg) {
    return <ApsViewerPlaceholder onOpenProcessing={onOpenProcessing} onOpenDocuments={onOpenDocuments} onOpenDebug={onOpenDebug} />;
  }

  if (pkg.viewerMode === "aps_ready") {
    return (
      <div className="ema-card p-4">
        <div className="h-72 rounded-lg border border-line bg-surface-2 p-4 text-sm text-muted">
          APS-ready container placeholder. Live APS loading is not enabled in this local build.
        </div>
      </div>
    );
  }

  return <ApsViewerPlaceholder pkg={pkg} onOpenProcessing={onOpenProcessing} onOpenDocuments={onOpenDocuments} onOpenDebug={onOpenDebug} />;
}
