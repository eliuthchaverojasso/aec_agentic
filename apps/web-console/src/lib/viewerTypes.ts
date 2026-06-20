export type ViewerSourceFormat =
  | "dwfx"
  | "dwf"
  | "rvt"
  | "nwd"
  | "nwc"
  | "ifc"
  | "svf"
  | "svf2"
  | "gltf"
  | "glb"
  | "unknown";

export type ViewerAdapterMode =
  | "placeholder"
  | "external"
  | "registered_package"
  | "aps_not_configured"
  | "aps_derivative_pending"
  | "aps_ready"
  | "aps_error"
  | "local_ifc_future"
  | "local_gltf_future";

export type ViewerPackage = {
  id: string | number;
  projectId: string | number;
  documentId?: string | number;
  fileName: string;
  sourceFormat: ViewerSourceFormat;
  sourcePath?: string;
  relativePath?: string;
  sizeBytes?: number;
  checksum?: string;
  lastIndexedAt?: string;
  evidenceStatus: "candidate" | "needs_review" | "accepted" | "rejected" | "metadata_only";
  viewerMode: ViewerAdapterMode;
  viewerStatusLabel: string;
  aps?: {
    bucketKey?: string;
    objectKey?: string;
    urn?: string;
    derivativeStatus?: "not_submitted" | "in_progress" | "success" | "failed";
    derivativeFormat?: "svf" | "svf2";
    manifestAvailable?: boolean;
  };
  metadata?: Record<string, unknown>;
};

export type EvidenceTraceLink = {
  id: string;
  sourceType:
    | "issue"
    | "requirement"
    | "document"
    | "drawing"
    | "specification"
    | "model_element"
    | "viewer_package"
    | "viewpoint"
    | "manual_review";
  sourceId?: string | number;
  label: string;
  status: "candidate" | "needs_review" | "accepted" | "rejected" | "pending";
  reference?: string;
  metadata?: Record<string, unknown>;
};

export type ViewpointReference = {
  id: string;
  name: string;
  status: "pending" | "available" | "external" | "not_configured";
  modelId?: string | number;
  viewerPackageId?: string | number;
  elementUniqueId?: string;
  revitElementId?: string;
  camera?: unknown;
  sectionBox?: unknown;
  createdFrom?: "revit_export" | "dwfx" | "manual" | "future_aps";
};
