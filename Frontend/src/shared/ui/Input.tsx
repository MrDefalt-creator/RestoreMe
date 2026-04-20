import type { InputHTMLAttributes } from 'react'

import { cn } from '@/shared/lib/cn'

export function Input({
  className,
  ...props
}: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={cn(
        'h-11 w-full rounded-xl border border-surface-300 bg-white px-3 text-sm text-ink-950 outline-none transition placeholder:text-ink-800/50 focus:border-accent-500 focus:ring-4 focus:ring-accent-500/10',
        className,
      )}
      {...props}
    />
  )
}
