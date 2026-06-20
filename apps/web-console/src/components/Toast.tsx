export type ToastState = {
  message: string;
  tone?: "success" | "info" | "warning";
  position?: "top" | "bottom";
};

export function Toast({ toast, onClose }: { toast: ToastState | null; onClose: () => void }) {
  if (!toast) {
    return null;
  }

  const styles = {
    success: "bg-success-soft text-success",
    info: "bg-info-soft text-info",
    warning: "bg-warning-soft text-warning",
  };

  return (
    <div className={`fixed ${toast.position === "top" ? "top-5" : "bottom-5"} right-5 z-50 max-w-sm`}>
      <div className={`ema-status-card rounded-lg border px-4 py-3 text-sm font-medium ${styles[toast.tone || "info"]}`}>
        <div className="flex items-start gap-3">
          <span className="flex-1">{toast.message}</span>
          <button type="button" className="shrink-0 text-xs font-semibold text-ink" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
