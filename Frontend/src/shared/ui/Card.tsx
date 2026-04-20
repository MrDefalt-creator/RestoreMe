import type { HTMLAttributes } from 'react'

import { cn } from '@/shared/lib/cn'

export function Card({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        'rounded-3xl border border-slate-200/80 bg-[rgba(255,255,255,0.92)] p-5 shadow-[var(--shadow-panel)]',
        className,
      )}
      {...props}
    />
  )
}
