import {
  ArrowRight,
  Check,
  Database,
  FileJson,
  FileSearch,
  FileText,
  RefreshCw,
  Scan,
  Server,
  ShieldCheck,
  X,
} from "lucide-react";

export type DataFlowNodeStatus = "idle" | "discovered" | "running" | "success" | "partial" | "failed" | "blocked";

export type DataFlowNode = {
  key: string;
  label: string;
  subtitle: string;
  icon: React.ComponentType<{ size?: number; className?: string }>;
  status: DataFlowNodeStatus;
  message?: string;
};

type DataFlowDiagramProps = {
  nodes: DataFlowNode[];
  className?: string;
  animated?: boolean;
};

const STATUS_STYLES: Record<DataFlowNodeStatus, { nodeClass: string; textClass: string; badgeLabel: string; badgeClass: string }> = {
  idle: {
    nodeClass: "border-border bg-surface text-muted",
    textClass: "text-muted",
    badgeLabel: "Waiting",
    badgeClass: "bg-surface-2 text-muted border-border",
  },
  discovered: {
    nodeClass: "border-accent-soft bg-accent-soft text-accent",
    textClass: "text-accent",
    badgeLabel: "Discovered",
    badgeClass: "bg-accent-soft text-accent border-accent-soft",
  },
  running: {
    nodeClass: "border-accent bg-accent-soft text-accent ema-anim-running",
    textClass: "text-accent",
    badgeLabel: "Running",
    badgeClass: "bg-accent-soft text-accent border-accent",
  },
  success: {
    nodeClass: "border-success bg-success-soft text-success",
    textClass: "text-success",
    badgeLabel: "Done",
    badgeClass: "bg-success-soft text-success border-success",
  },
  partial: {
    nodeClass: "border-warning bg-warning-soft text-warning",
    textClass: "text-warning",
    badgeLabel: "Partial",
    badgeClass: "bg-warning-soft text-warning border-warning",
  },
  failed: {
    nodeClass: "border-danger bg-danger-soft text-danger",
    textClass: "text-danger",
    badgeLabel: "Failed",
    badgeClass: "bg-danger-soft text-danger border-danger",
  },
  blocked: {
    nodeClass: "border-advisory bg-advisory-soft text-advisory",
    textClass: "text-advisory",
    badgeLabel: "Blocked",
    badgeClass: "bg-advisory-soft text-advisory border-advisory",
  },
};

function nodeIcon(node: DataFlowNode, Icon: React.ComponentType<{ size?: number; className?: string }>, status: DataFlowNodeStatus) {
  if (status === "success") return <Check size={18} className="ema-anim-success" />;
  if (status === "failed") return <X size={18} />;
  if (status === "running") return <RefreshCw size={18} className="ema-anim-spin" />;
  if (status === "partial") return <RefreshCw size={18} className="text-warning" />;
  return <Icon size={18} />;
}

export function DataFlowDiagram({ nodes, className = "", animated = true }: DataFlowDiagramProps) {
  return (
    <div className={`space-y-0 ${className}`} data-anim={animated ? "data-flow" : undefined}>
      {nodes.map((node, i) => {
        const isLast = i === nodes.length - 1;
        const style = STATUS_STYLES[node.status];
        const Icon = node.icon;

        return (
          <div key={node.key} className="flex items-start gap-3">
            <div className="flex flex-col items-center">
              <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full border-2 transition-all duration-300 ${style.nodeClass} ${node.status === "running" ? "ema-anim-running" : ""}`}>
                {nodeIcon(node, Icon, node.status)}
              </div>
              {!isLast && (
                <div className={`mt-1 h-8 w-0.5 rounded-full transition-colors duration-300 ${
                  node.status === "success" ? "bg-success" :
                  node.status === "failed" ? "bg-danger" :
                  node.status === "running" ? "bg-accent ema-connector-pulse" :
                  "bg-border"
                }`} />
              )}
            </div>

            <div className={`min-w-0 flex-1 rounded-lg border px-3 py-2.5 transition-all duration-300 ${
              node.status === "running" ? "border-accent/40 bg-accent-soft/30" :
              node.status === "success" ? "border-success/30 bg-success-soft/30" :
              node.status === "failed" ? "border-danger/30 bg-danger-soft/30" :
              "border-line bg-surface"
            }`}>
              <div className="flex items-center justify-between gap-2">
                <div className="min-w-0">
                  <div className={`truncate text-sm font-semibold ${style.textClass}`}>
                    {node.label}
                  </div>
                  <div className="truncate text-xs text-muted">
                    {node.subtitle}
                  </div>
                </div>
                <span className={`shrink-0 rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider ${style.badgeClass}`}>
                  {style.badgeLabel}
                </span>
              </div>
              {node.message && (
                <p className="mt-1 text-xs text-muted">{node.message}</p>
              )}
            </div>

            {!isLast && (
              <div className="hidden items-center sm:flex">
                <ArrowRight size={14} className={`mx-1 shrink-0 ${
                  node.status === "success" ? "text-success" :
                  node.status === "running" ? "text-accent" :
                  "text-muted"
                }`} />
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

export function buildDataFlowNodes(
  status: {
    scanDone: boolean;
    manifestDone: boolean;
    dryRunDone: boolean;
    ingestDone: boolean;
    snapshotDone: boolean;
    isRunning: boolean;
    ingestHasError?: boolean;
  },
): DataFlowNode[] {
  const runningOp = status.isRunning;
  return [
    {
      key: "scan",
      label: "Scan Landing Zone",
      subtitle: "Discover files in landing directory",
      icon: Scan,
      status: status.scanDone ? "success" : runningOp ? "discovered" : "idle",
    },
    {
      key: "manifest",
      label: "Rebuild Manifest",
      subtitle: "Register files in document index",
      icon: FileJson,
      status: status.manifestDone ? "success" : status.scanDone ? "discovered" : "idle",
    },
    {
      key: "dry-run",
      label: "Dry Run Ingest",
      subtitle: "Preview what would be ingested",
      icon: FileSearch,
      status: status.dryRunDone ? "success" : status.manifestDone ? "discovered" : "idle",
    },
    {
      key: "ingest",
      label: "Run Ingest",
      subtitle: "Write evidence candidates to database",
      icon: Database,
      status: status.ingestDone ? (status.ingestHasError ? "partial" : "success") :
             status.dryRunDone ? "discovered" : "idle",
      message: status.ingestHasError ? "Partial with known blockers" : undefined,
    },
    {
      key: "snapshot",
      label: "Readiness Snapshot",
      subtitle: "Create immutable point-in-time assessment",
      icon: ShieldCheck,
      status: status.snapshotDone ? "success" :
             status.ingestDone ? "discovered" : "idle",
    },
    {
      key: "dashboard",
      label: "Dashboard Update",
      subtitle: "Results reflected in UI",
      icon: Server,
      status: status.snapshotDone ? "success" : "idle",
    },
  ];
}
