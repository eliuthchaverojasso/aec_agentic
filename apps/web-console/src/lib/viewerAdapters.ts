import type { LandingDocument } from "../types";
import type { ViewerAdapterMode, ViewerPackage, ViewerSourceFormat } from "./viewerTypes";

const FORMAT_BY_EXT: Record<string, ViewerSourceFormat> = {
  ".dwfx": "dwfx",
  ".dwf": "dwf",
  ".rvt": "rvt",
  ".nwd": "nwd",
  ".nwc": "nwc",
  ".ifc": "ifc",
  ".svf": "svf",
  ".svf2": "svf2",
  ".glb": "glb",
  ".gltf": "gltf",
};

export function inferViewerSourceFormat(document: LandingDocument): ViewerSourceFormat {
  const ext = (document.file_ext || "").toLowerCase();
  return FORMAT_BY_EXT[ext] || "unknown";
}

export function isViewerPackageCandidate(document: LandingDocument): boolean {
  const format = inferViewerSourceFormat(document);
  return format !== "unknown";
}

function viewerModeForFormat(format: ViewerSourceFormat): ViewerAdapterMode {
  if (format === "ifc") return "local_ifc_future";
  if (format === "gltf" || format === "glb") return "local_gltf_future";
  if (format === "svf" || format === "svf2") return "aps_not_configured";
  if (format === "rvt" || format === "nwd" || format === "nwc" || format === "dwfx" || format === "dwf") {
    return "registered_package";
  }
  return "placeholder";
}

function statusLabel(mode: ViewerAdapterMode): string {
  switch (mode) {
    case "registered_package":
      return "Registered Package";
    case "aps_not_configured":
      return "APS Not Configured";
    case "aps_derivative_pending":
      return "APS Derivative Pending";
    case "aps_ready":
      return "APS Ready";
    case "aps_error":
      return "APS Error";
    case "external":
      return "External Viewer";
    case "local_ifc_future":
      return "Local IFC Future";
    case "local_gltf_future":
      return "Local glTF Future";
    default:
      return "Preview Unavailable";
  }
}

export function toViewerPackage(projectId: number, document: LandingDocument): ViewerPackage {
  const format = inferViewerSourceFormat(document);
  const mode = viewerModeForFormat(format);
  return {
    id: `${projectId}-${document.id}`,
    projectId,
    documentId: document.id,
    fileName: document.file_name,
    sourceFormat: format,
    relativePath: document.relative_path,
    sizeBytes: document.file_size_bytes ?? undefined,
    checksum: document.checksum_sha256 ?? undefined,
    lastIndexedAt: document.indexed_at ?? undefined,
    evidenceStatus: (document.evidence_status as ViewerPackage["evidenceStatus"]) || "candidate",
    viewerMode: mode,
    viewerStatusLabel: statusLabel(mode),
    aps: {
      derivativeStatus: "not_submitted",
      derivativeFormat: format === "svf2" ? "svf2" : "svf",
      manifestAvailable: format === "svf" || format === "svf2",
    },
    metadata: {
      ...document.metadata_json,
      aps_configured: false,
      can_open_externally: true,
      open_external_available: true,
      download_available: true,
    },
  };
}
