import type { SelectHTMLAttributes } from 'react'

import { cn } from '@/shared/lib/cn'

export function Select({
  className,
  children,
  ...props
}: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      className={cn(
        'h-11 w-full rounded-xl border border-surface-300 bg-white px-3 text-sm text-ink-950 outline-none transition focus:border-accent-500 focus:ring-4 focus:ring-accent-500/10',
        className,
      )}
      {...props}
    >
      {children}
    </select>
  )
}
