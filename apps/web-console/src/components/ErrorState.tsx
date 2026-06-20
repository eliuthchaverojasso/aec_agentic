import { AlertTriangle } from "lucide-react";

type ErrorStateProps = {
  title?: string;
  message: string;
  onRetry?: () => void;
};

export function ErrorState({
  title = "Something went wrong",
  message,
  onRetry,
}: ErrorStateProps) {
  return (
    <div className="ema-error-state">
      <AlertTriangle size={32} className="text-danger" aria-hidden />
      <div className="min-w-0">
        <p className="text-sm font-semibold text-danger">{title}</p>
        <p className="mt-1 text-xs text-muted">{message}</p>
      </div>
      {onRetry && (
        <button type="button" className="ema-btn-secondary mt-2" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  );
}
