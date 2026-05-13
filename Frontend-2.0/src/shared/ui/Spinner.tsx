import { cn } from '@/shared/lib/cn'
import { Loader2 } from 'lucide-react'
import type { HTMLAttributes, ReactNode } from 'react'

interface SpinnerProps extends HTMLAttributes<HTMLDivElement> {
  size?: 'sm' | 'md' | 'lg'
  children?: ReactNode
}

export function Spinner({
  size = 'md',
  className,
  children,
  ...props
}: SpinnerProps) {
  return (
    <div
      className={cn(
        'relative flex items-center justify-center',
        className,
      )}
      {...props}
    >
      {children ? (
        children
      ) : (
        <Loader2
          className={cn(
            'animate-spin',
            size === 'sm' && 'h-4 w-4',
            size === 'md' && 'h-5 w-5',
            size === 'lg' && 'h-7 w-7',
          )}
        />
      )}
    </div>
  )
}
