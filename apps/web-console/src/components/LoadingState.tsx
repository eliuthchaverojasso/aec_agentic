import { Loader } from "lucide-react";

type LoadingStateProps = {
  message?: string;
};

export function LoadingState({ message = "Loading..." }: LoadingStateProps) {
  return (
    <div className="ema-loading-state">
      <Loader size={28} className="ema-loading-pulse text-muted" aria-hidden />
      <p className="text-sm text-muted">{message}</p>
    </div>
  );
}
