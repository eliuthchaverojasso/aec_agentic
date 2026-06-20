import { useMemo, useState } from "react";
import type { Issue, LandingDocument, ProjectRequirementRow } from "../../types";
import { isViewerPackageCandidate, toViewerPackage } from "../../lib/viewerAdapters";
import type { EvidenceTraceLink, ViewpointReference } from "../../lib/viewerTypes";
import { ViewerPackageSelector } from "./ViewerPackageSelector";
import { ViewerStatusCard } from "./ViewerStatusCard";
import { ViewerCanvas } from "./ViewerCanvas";
import { TraceabilityPanel } from "./TraceabilityPanel";

type Props = {
  projectId: number;
  documents: LandingDocument[];
  issues: Issue[];
  requirementRows?: ProjectRequirementRow[];
  onOpenProcessing?: () => void;
  onOpenDocuments?: () => void;
  onOpenDebug?: () => void;
};

export function ProjectViewerPanel({
  projectId,
  documents,
  issues,
  requirementRows = [],
  onOpenProcessing,
  onOpenDocuments,
  onOpenDebug,
}: Props) {
  const packages = useMemo(
    () => documents.filter(isViewerPackageCandidate).map((doc) => toViewerPackage(projectId, doc)),
    [documents, projectId],
  );
  const [selectedId, setSelectedId] = useState<string | number | undefined>(packages[0]?.id);
  const selected = useMemo(
    () => packages.find((pkg) => String(pkg.id) === String(selectedId)) || packages[0],
    [packages, selectedId],
  );

  const traceLinks: EvidenceTraceLink[] = useMemo(() => {
    const issueLinks = issues.slice(0, 4).map((issue) => {
      const status: "accepted" | "needs_review" = issue.status === "closed" ? "accepted" : "needs_review";
      return {
        id: `issue-${issue.id}`,
        sourceType: "issue" as const,
        sourceId: issue.id,
        label: issue.message || issue.issue_type || `Issue ${issue.id}`,
        status,
        reference: issue.element_unique_id || undefined,
      };
    });
    const reqLinks = requirementRows.slice(0, 4).map((req) => {
      const reqStatus: "accepted" | "needs_review" | "pending" =
        req.readiness_status === "covered"
          ? "accepted"
          : req.readiness_status === "needs_review"
            ? "needs_review"
            : "pending";
      return {
      id: `req-${req.requirement_id}`,
      sourceType: "requirement" as const,
      sourceId: req.requirement_id,
      label: req.requirement_text,
      status: reqStatus,
      reference: req.discipline,
      };
    });
    return [...issueLinks, ...reqLinks];
  }, [issues, requirementRows]);

  const viewpoints: ViewpointReference[] = useMemo(
    () =>
      documents
        .filter((doc) => doc.file_type === "viewpoint_json")
        .slice(0, 5)
        .map((doc) => ({
          id: `vp-${doc.id}`,
          name: doc.file_name,
          status: "available",
          viewerPackageId: selected?.id,
          createdFrom: "manual",
        })),
    [documents, selected?.id],
  );

  return (
    <section className="space-y-4">
      <div className="grid gap-4 xl:grid-cols-[1fr_360px]">
        <div className="space-y-4">
          <ViewerPackageSelector packages={packages} selectedId={selectedId} onSelect={setSelectedId} />
          <ViewerCanvas
            pkg={selected}
            onOpenProcessing={onOpenProcessing}
            onOpenDocuments={onOpenDocuments}
            onOpenDebug={onOpenDebug}
          />
        </div>
        <div className="space-y-4">
          <ViewerStatusCard pkg={selected} />
          <TraceabilityPanel pkg={selected} links={traceLinks} viewpoints={viewpoints} />
        </div>
      </div>
    </section>
  );
}
