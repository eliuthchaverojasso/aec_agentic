type ConfirmModalProps = {
  title: string;
  message: string;
  confirmLabel?: string;
  isOpen: boolean;
  onCancel: () => void;
  onConfirm: () => void;
};

export function ConfirmModal({
  title,
  message,
  confirmLabel = "Confirm",
  isOpen,
  onCancel,
  onConfirm,
}: ConfirmModalProps) {
  if (!isOpen) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/30 px-4">
      <div className="ema-glass-panel w-full max-w-md p-5">
        <h2 className="text-base font-semibold text-ink">{title}</h2>
        <p className="mt-2 text-sm leading-6 text-muted">{message}</p>
        <div className="mt-5 flex justify-end gap-2">
          <button
            type="button"
            className="ema-button-secondary h-9 px-3 text-sm font-semibold"
            onClick={onCancel}
          >
            Cancel
          </button>
          <button
            type="button"
            className="ema-button-primary h-9 px-3 text-sm font-semibold"
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
