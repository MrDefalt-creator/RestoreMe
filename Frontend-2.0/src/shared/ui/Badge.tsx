import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'

import { cn } from '@/shared/lib/cn'

const badgeVariants = cva(
  'inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-medium transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2',
  {
    variants: {
      variant: {
        default:
          'border-transparent bg-primary text-primary-foreground hover:bg-primary/80',
        secondary:
          'border-transparent bg-secondary text-secondary-foreground hover:bg-secondary/80',
        destructive:
          'border-transparent bg-destructive text-destructive-foreground hover:bg-destructive/80',
        success:
          'border-transparent bg-success text-success-foreground hover:bg-success/80',
        warning:
          'border-transparent bg-warning text-warning-foreground hover:bg-warning/80',
        accent:
          'border-transparent bg-accent text-accent-foreground hover:bg-accent/80',
        neutral:
          'border-transparent bg-secondary text-secondary-foreground hover:bg-secondary/80',
        outline: 'text-foreground',
        outlineDot: 'border-border bg-transparent text-muted-foreground gap-1.5',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  },
)

const dotToneClass: Record<DotTone, string> = {
  success: 'bg-success',
  warning: 'bg-warning',
  destructive: 'bg-destructive',
  neutral: 'bg-muted-foreground',
  primary: 'bg-primary',
}

type DotTone = 'success' | 'warning' | 'destructive' | 'neutral' | 'primary'

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {
  dot?: DotTone
}

function Badge({ className, variant, dot, children, ...props }: BadgeProps) {
  return (
    <div className={cn(badgeVariants({ variant }), className)} {...props}>
      {dot ? <span className={cn('h-1.5 w-1.5 rounded-full', dotToneClass[dot])} aria-hidden /> : null}
      {children}
    </div>
  )
}

export { Badge }
