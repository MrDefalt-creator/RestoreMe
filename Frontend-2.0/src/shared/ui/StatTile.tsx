import * as React from 'react'
import { cn } from '@/shared/lib/cn'

type StatTileTone = 'neutral' | 'primary' | 'accent' | 'success' | 'warning' | 'destructive'

interface StatTileProps {
  icon: React.ReactNode
  label: string
  value: number | string
  detail?: string
  tone?: StatTileTone
  className?: string
}

const toneStyles: Record<StatTileTone, { bar: string; iconBg: string; iconFg: string }> = {
  neutral:     { bar: 'bg-muted-foreground/70',    iconBg: 'bg-secondary',           iconFg: 'text-muted-foreground' },
  primary:     { bar: '[background:hsl(var(--primary)/0.7)]',     iconBg: '[background:hsl(var(--primary)/0.08)]',     iconFg: 'text-primary' },
  accent:      { bar: '[background:hsl(var(--accent)/0.7)]',      iconBg: '[background:hsl(var(--accent)/0.08)]',      iconFg: 'text-accent-foreground' },
  success:     { bar: '[background:hsl(var(--success)/0.7)]',     iconBg: '[background:hsl(var(--success)/0.08)]',     iconFg: 'text-success' },
  warning:     { bar: '[background:hsl(var(--warning)/0.7)]',     iconBg: '[background:hsl(var(--warning)/0.08)]',     iconFg: 'text-warning' },
  destructive: { bar: '[background:hsl(var(--destructive)/0.7)]', iconBg: '[background:hsl(var(--destructive)/0.08)]', iconFg: 'text-destructive' },
}

export function StatTile({ icon, label, value, detail, tone = 'neutral', className }: StatTileProps) {
  const styles = toneStyles[tone]

  return (
    <div
      className={cn(
        'relative overflow-hidden rounded-lg border border-border bg-card shadow-[var(--shadow-md)]',
        className,
      )}
      style={{ paddingTop: 'var(--space-card-py, 1rem)', paddingBottom: 'var(--space-card-py, 1rem)' }}
    >
      <div className="px-5">
        <div className="flex items-center justify-between gap-3">
          <div>
            <p className="text-sm text-muted-foreground">{label}</p>
            <p className="mt-1 text-2xl font-semibold tracking-tight text-foreground">{value}</p>
          </div>
          <span className={cn('flex h-11 w-11 items-center justify-center rounded-lg', styles.iconBg, styles.iconFg)}>
            {icon}
          </span>
        </div>
        {detail && (
          <p className="mt-2 text-xs text-muted-foreground">{detail}</p>
        )}
      </div>

      <div className={cn('absolute inset-x-0 bottom-0 h-[3px]', styles.bar)} />
    </div>
  )
}
