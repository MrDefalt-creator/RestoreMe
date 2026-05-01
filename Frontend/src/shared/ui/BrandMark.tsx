import logoUrl from '@/shared/assets/restoreme-mark.svg'
import { cn } from '@/shared/lib/cn'

type BrandMarkProps = {
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
          'shrink-0 object-contain drop-shadow-[0_12px_28px_rgba(3,12,23,0.18)]',
          compact ? 'h-12 w-12' : 'h-14 w-14',
        )}
      />
      {!compact ? (
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-[0.3em] text-sky-200/85">
            RestoreMe
          </p>
          <h1 className="mt-1 text-2xl font-semibold tracking-tight text-white">
            {subtitle}
          </h1>
        </div>
      ) : null}
    </div>
  )
}
