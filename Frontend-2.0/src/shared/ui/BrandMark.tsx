import logoUrl from '@/shared/assets/restoreme-mark.svg'
import { cn } from '@/shared/lib/cn'

interface BrandMarkProps {
  compact?: boolean
  className?: string
  subtitle?: string
}

export function BrandMark({
  compact = false,
  className,
  subtitle = 'Backup Console',
}: BrandMarkProps) {
  return (
    <div
      className={cn(
        'flex items-center gap-3',
        compact ? 'justify-center' : 'justify-start',
        className,
      )}
    >
      <img
        src={logoUrl}
        alt="RestoreMe logo"
        className={cn(
          'shrink-0 object-contain drop-shadow-[0_10px_24px_hsl(var(--foreground)/0.14)]',
          compact ? 'h-11 w-11' : 'h-12 w-12',
        )}
      />
      {!compact && (
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">
            RestoreMe
          </p>
          <h1 className="mt-0.5 text-lg font-semibold tracking-tight text-foreground">
            {subtitle}
          </h1>
        </div>
      )}
    </div>
  )
}
