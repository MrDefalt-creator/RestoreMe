import * as React from 'react'
import * as RadixSwitch from '@radix-ui/react-switch'

import { cn } from '@/shared/lib/cn'

type SwitchSize = 'sm' | 'md'

interface SwitchProps
  extends Omit<React.ComponentPropsWithoutRef<typeof RadixSwitch.Root>, 'asChild'> {
  size?: SwitchSize
}

const rootSize: Record<SwitchSize, string> = {
  sm: 'h-4 w-7',
  md: 'h-5 w-9',
}

const thumbSize: Record<SwitchSize, string> = {
  sm: 'h-3 w-3 data-[state=checked]:translate-x-3',
  md: 'h-4 w-4 data-[state=checked]:translate-x-4',
}

export const Switch = React.forwardRef<
  React.ElementRef<typeof RadixSwitch.Root>,
  SwitchProps
>(({ className, size = 'md', ...props }, ref) => {
  return (
    <RadixSwitch.Root
      ref={ref}
      className={cn(
        'peer inline-flex shrink-0 cursor-pointer items-center rounded-full border border-transparent transition-colors',
        'bg-muted/80 hover:bg-muted',
        'data-[state=checked]:bg-primary data-[state=checked]:hover:bg-primary/90',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background',
        'disabled:cursor-not-allowed disabled:opacity-50',
        rootSize[size],
        className,
      )}
      {...props}
    >
      <RadixSwitch.Thumb
        className={cn(
          'pointer-events-none block translate-x-0.5 rounded-full bg-card shadow-[var(--shadow-sm)] ring-0 transition-transform',
          thumbSize[size],
        )}
      />
    </RadixSwitch.Root>
  )
})

Switch.displayName = 'Switch'

interface SwitchFieldProps {
  id?: string
  label: React.ReactNode
  description?: React.ReactNode
  checked: boolean
  onCheckedChange: (next: boolean) => void
  disabled?: boolean
  size?: SwitchSize
  className?: string
}

/**
 * Convenience wrapper: label + helper text on the left, switch on the right.
 * Clicking the row toggles the switch. Use this for settings / form rows.
 */
export function SwitchField({
  id,
  label,
  description,
  checked,
  onCheckedChange,
  disabled,
  size,
  className,
}: SwitchFieldProps) {
  const reactId = React.useId()
  const inputId = id ?? reactId
  return (
    <label
      htmlFor={inputId}
      className={cn(
        'flex cursor-pointer items-start justify-between gap-4 rounded-md py-1.5',
        disabled && 'cursor-not-allowed opacity-60',
        className,
      )}
    >
      <span className="flex min-w-0 flex-1 flex-col">
        <span className="text-sm font-medium text-foreground">{label}</span>
        {description ? (
          <span className="text-xs text-muted-foreground">{description}</span>
        ) : null}
      </span>
      <Switch
        id={inputId}
        checked={checked}
        onCheckedChange={onCheckedChange}
        disabled={disabled}
        size={size}
      />
    </label>
  )
}
