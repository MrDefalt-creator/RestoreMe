import type { ReactNode } from 'react'

import { Card } from '@/shared/ui/Card'

type EmptyStateProps = {
  title: string
  description: string
  action?: ReactNode
}

export function EmptyState({
  title,
  description,
  action,
}: EmptyStateProps) {
  return (
    <Card className="flex min-h-52 flex-col items-start justify-center gap-3 border-dashed bg-white/65">
      <div className="space-y-2">
        <h3 className="text-lg font-semibold text-ink-950">{title}</h3>
        <p className="max-w-xl text-sm text-ink-800">{description}</p>
      </div>
      {action}
    </Card>
  )
}
