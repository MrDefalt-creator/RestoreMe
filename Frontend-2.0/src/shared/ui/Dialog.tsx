import type { PropsWithChildren, ReactNode } from 'react'
import { X } from 'lucide-react'

import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui/Button'

type DialogProps = PropsWithChildren<{
  open: boolean
  title: string
  description?: string
  footer?: ReactNode
  className?: string
  onClose: () => void
}>

export function Dialog({
  open,
  title,
  description,
  footer,
  className,
  onClose,
  children,
}: DialogProps) {
  if (!open) {
    return null
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-background/90 p-4">
      <button className="absolute inset-0 cursor-default" onClick={onClose} aria-label="Close dialog" />
      <div
        role="dialog"
        aria-modal="true"
        className={cn(
          'relative z-10 flex max-h-[90vh] w-full max-w-2xl flex-col overflow-hidden rounded-lg border border-border bg-card shadow-[var(--shadow-xl)] animate-scale-in',
          className,
        )}
      >
        <div className="flex items-start justify-between gap-4 border-b border-border/80 px-6 py-5">
          <div className="space-y-2">
            <h2 className="text-xl font-semibold tracking-tight text-foreground">{title}</h2>
            {description ? (
              <p className="text-sm leading-6 text-muted-foreground">{description}</p>
            ) : null}
          </div>
          <Button variant="ghost" size="icon" onClick={onClose} title="Close">
            <X className="h-4 w-4" />
          </Button>
        </div>
        <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-6 py-5">{children}</div>
        {footer ? (
          <div className="flex flex-col-reverse gap-3 border-t border-border/80 bg-secondary/35 px-6 py-4 sm:flex-row sm:justify-end">
            {footer}
          </div>
        ) : null}
      </div>
    </div>
  )
}
