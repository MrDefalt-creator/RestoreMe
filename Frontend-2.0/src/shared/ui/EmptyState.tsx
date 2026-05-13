import { cn } from '@/shared/lib/cn'

interface EmptyStateProps {
  icon?: React.ReactNode
  iconClassName?: string
  title: string
  description?: string
  action?: React.ReactNode
  className?: string
}

export function EmptyState({
  icon,
  iconClassName,
  title,
  description,
  action,
  className,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex min-h-36 flex-col items-center justify-center px-6 py-12 text-center',
        'rounded-lg border border-border bg-card/98 shadow-[var(--shadow-sm)]',
        className,
      )}
    >
      {icon && (
        <div
          className={cn(
            'mb-4 rounded-lg bg-secondary p-4 text-muted-foreground',
            iconClassName,
          )}
        >
          {icon}
        </div>
      )}
      <h3 className="text-lg font-semibold text-foreground">
        {title}
      </h3>
      {description && (
        <p className="mt-2 max-w-sm text-sm leading-6 text-muted-foreground">
          {description}
        </p>
      )}
      {action && (
        <div className="mt-4">{action}</div>
      )}
    </div>
  )
}
