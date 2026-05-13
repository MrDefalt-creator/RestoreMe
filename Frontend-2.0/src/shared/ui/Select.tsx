import type { SelectHTMLAttributes } from 'react'
import { ChevronDown } from 'lucide-react'

import { cn } from '@/shared/lib/cn'

export function Select({
  className,
  children,
  ...props
}: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <div className="relative">
      <select
        className={cn(
          'h-10 w-full appearance-none rounded-lg border border-input bg-background px-3 pr-10 text-sm text-foreground shadow-[inset_0_1px_0_hsl(var(--foreground)/0.03)] outline-none transition duration-150 focus:border-ring/70 focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:bg-secondary disabled:opacity-70',
          className,
        )}
        {...props}
      >
        {children}
      </select>
      <ChevronDown className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
    </div>
  )
}
