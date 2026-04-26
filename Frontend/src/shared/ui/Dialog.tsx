import type { PropsWithChildren, ReactNode } from 'react'

import { cn } from '@/shared/lib/cn'

type DialogProps = PropsWithChildren<{
  open: boolean
  title: string
  description?: string
  footer?: ReactNode
  onClose: () => void
}>

export function Dialog({
  open,
  title,
  description,
  footer,
  onClose,
  children,
}: DialogProps) {
  if (!open) {
    return null
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-[rgba(15,23,42,0.18)] p-4 backdrop-blur-[2px]">
      <div className="absolute inset-0" onClick={onClose} aria-hidden="true" />
      <div
        role="dialog"
        aria-modal="true"
        className={cn(
          'relative z-10 w-full max-w-xl rounded-[28px] border border-slate-200/90 bg-[rgba(255,255,255,0.96)] p-6 shadow-[0_24px_60px_rgba(16,32,51,0.16)]',
        )}
      >
        <div className="mb-5 space-y-2">
          <h2 className="text-xl font-semibold text-ink-950">{title}</h2>
          {description ? (
            <p className="text-sm text-ink-800">{description}</p>
          ) : null}
        </div>
        <div className="space-y-4">{children}</div>
        {footer ? <div className="mt-6 flex justify-end gap-3">{footer}</div> : null}
      </div>
    </div>
  )
}
