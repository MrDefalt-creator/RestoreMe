import * as React from 'react'
import { cn } from '@/shared/lib/cn'

type SegmentTone = 'neutral' | 'primary' | 'accent' | 'success' | 'warning' | 'destructive'

interface SegmentOption<T extends string> {
  value: T
  label: string
  count?: number
  tone?: SegmentTone
}

interface SegmentedControlProps<T extends string> {
  value: T
  onChange: (next: T) => void
  options: SegmentOption<T>[]
  'aria-label': string
  className?: string
}

const pillTone: Record<SegmentTone, string> = {
  neutral:     'bg-muted-foreground/15 text-muted-foreground',
  primary:     '[background:hsl(var(--primary)/0.15)] text-primary',
  accent:      '[background:hsl(var(--accent)/0.15)] text-accent-foreground',
  success:     '[background:hsl(var(--success)/0.15)] text-success',
  warning:     '[background:hsl(var(--warning)/0.15)] text-warning',
  destructive: '[background:hsl(var(--destructive)/0.15)] text-destructive',
}

export function SegmentedControl<T extends string>({
  value,
  onChange,
  options,
  'aria-label': ariaLabel,
  className,
}: SegmentedControlProps<T>) {
  const refs = React.useRef<(HTMLButtonElement | null)[]>([])

  function handleKeyDown(event: React.KeyboardEvent, index: number) {
    let next: number | null = null
    if (event.key === 'ArrowRight') next = (index + 1) % options.length
    else if (event.key === 'ArrowLeft') next = (index - 1 + options.length) % options.length
    else if (event.key === 'Home') next = 0
    else if (event.key === 'End') next = options.length - 1

    if (next !== null) {
      event.preventDefault()
      onChange(options[next].value)
      refs.current[next]?.focus()
    }
  }

  return (
    <div
      role="radiogroup"
      aria-label={ariaLabel}
      className={cn(
        'inline-flex rounded-lg border border-border bg-secondary/50 p-1 text-sm',
        className,
      )}
    >
      {options.map((option, index) => {
        const selected = option.value === value
        const tone = option.tone ?? 'neutral'
        return (
          <button
            key={option.value}
            ref={(el) => { refs.current[index] = el }}
            type="button"
            role="radio"
            aria-checked={selected}
            tabIndex={selected ? 0 : -1}
            onClick={() => onChange(option.value)}
            onKeyDown={(e) => handleKeyDown(e, index)}
            className={cn(
              'inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 font-medium transition-colors',
              selected
                ? 'bg-primary text-primary-foreground shadow-sm'
                : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {option.label}
            {option.count != null && (
              <span className={cn('rounded-full px-1.5 py-0.5 text-xs leading-none', selected ? 'bg-primary-foreground/20 text-primary-foreground' : pillTone[tone])}>
                {option.count}
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}
