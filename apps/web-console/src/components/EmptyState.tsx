import { Inbox } from "lucide-react";

type EmptyStateProps = {
  icon?: React.ComponentType<{ size?: number; className?: string }>;
  title: string;
  description?: string;
  action?: React.ReactNode;
};

export function EmptyState({
  icon: Icon = Inbox,
  title,
  description,
  action,
}: EmptyStateProps) {
  return (
    <div className="ema-empty-state ema-state-pending">
      <Icon size={36} className="text-muted" aria-hidden />
      <div className="min-w-0">
        <p className="text-sm font-semibold text-muted">{title}</p>
        {description && <p className="mt-1 text-xs text-subtle">{description}</p>}
      </div>
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}
