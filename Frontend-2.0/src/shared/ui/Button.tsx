import * as React from 'react'
import { Slot } from '@radix-ui/react-slot'
import { cva, type VariantProps } from 'class-variance-authority'

import { cn } from '@/shared/lib/cn'

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg text-sm font-medium transition duration-150 ease-out focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-45',
  {
    variants: {
      variant: {
        primary:
          'bg-primary text-primary-foreground shadow-[0_8px_24px_hsl(var(--primary)/0.20)] hover:-translate-y-0.5 hover:bg-primary/90 hover:shadow-[0_12px_30px_hsl(var(--primary)/0.22)]',
        secondary:
          'border border-border bg-card text-secondary-foreground hover:bg-secondary',
        ghost: 'hover:bg-accent hover:text-accent-foreground',
        outline:
          'border border-input bg-background hover:bg-accent hover:text-accent-foreground hover:border-accent/50',
        danger:
          'bg-destructive text-destructive-foreground hover:bg-destructive/90',
        success:
          'bg-success text-success-foreground hover:bg-success/90',
        warning:
          'bg-warning text-warning-foreground hover:bg-warning/90',
        neutral:
          'bg-muted text-muted-foreground hover:bg-secondary',
      },
      size: {
        sm: 'h-9 px-3 text-xs',
        md: 'h-10 px-4 py-2',
        lg: 'h-14 px-8 text-base',
        icon: 'h-10 w-10',
      },
    },
    defaultVariants: {
      variant: 'primary',
      size: 'md',
    },
  },
)

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : 'button'
    return (
      <Comp
        className={cn(buttonVariants({ variant, size, className }))}
        ref={ref}
        {...props}
      />
    )
  },
)
Button.displayName = 'Button'

export { Button }
