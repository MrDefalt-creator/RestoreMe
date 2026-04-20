import type { ButtonHTMLAttributes } from 'react'
import { cva, type VariantProps } from 'class-variance-authority'

import { cn } from '@/shared/lib/cn'

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 rounded-xl px-4 py-2 text-sm font-semibold transition hover:-translate-y-0.5 disabled:pointer-events-none',
  {
    variants: {
      variant: {
        primary:
          'bg-[#102033] text-white shadow-[0_10px_25px_rgba(16,32,51,0.18)] hover:bg-[#17314e] disabled:bg-slate-200 disabled:text-slate-500 disabled:shadow-none',
        secondary:
          'border border-surface-300 bg-white text-ink-900 hover:border-accent-500 hover:text-accent-600 disabled:border-slate-200 disabled:bg-slate-100 disabled:text-slate-400',
        ghost: 'text-ink-800 hover:bg-white/70 disabled:text-slate-400',
        danger:
          'bg-danger-500 text-white shadow-[0_10px_25px_rgba(194,65,12,0.2)] hover:bg-danger-500/90 disabled:bg-orange-200 disabled:text-orange-50 disabled:shadow-none',
      },
      size: {
        md: 'h-11',
        sm: 'h-9 rounded-lg px-3 text-xs',
      },
    },
    defaultVariants: {
      variant: 'primary',
      size: 'md',
    },
  },
)

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> &
  VariantProps<typeof buttonVariants>

export function Button({
  className,
  variant,
  size,
  type = 'button',
  ...props
}: ButtonProps) {
  return (
    <button
      type={type}
      className={cn(buttonVariants({ variant, size }), className)}
      {...props}
    />
  )
}
