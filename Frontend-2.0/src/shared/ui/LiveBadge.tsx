import { useEffect, useState } from 'react'
import { useIsFetching, useQueryClient } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'

import { Badge } from './Badge'
import { useI18n } from '@/shared/i18n'

const STALE_THRESHOLD_MS = 60_000
const OFFLINE_THRESHOLD_MS = 180_000
// Slow tick — only needed so the dot can flip Live → Stale → Offline while
// nothing else triggers a re-render. The previous 1 s counter felt like its
// own clock; this one is invisible to the operator.
const TICK_MS = 30_000

function formatAge(ageMs: number) {
  if (!Number.isFinite(ageMs)) return '—'
  if (ageMs < 60_000) return `${Math.max(0, Math.floor(ageMs / 1000))}s ago`
  return `${Math.floor(ageMs / 60_000)}m ago`
}

export function LiveBadge() {
  const queryClient = useQueryClient()
  const isFetching = useIsFetching()
  const { t } = useI18n()
  // `now` updates only every TICK_MS so the dot can flip Live → Stale →
  // Offline. Storing it in state keeps the render function pure under
  // react-hooks/purity.
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), TICK_MS)
    return () => clearInterval(id)
  }, [])

  const fetchedQueries = queryClient
    .getQueryCache()
    .findAll()
    .filter((q) => q.state.dataUpdatedAt > 0)
  const minUpdatedAt =
    fetchedQueries.length > 0
      ? Math.min(...fetchedQueries.map((q) => q.state.dataUpdatedAt))
      : 0

  const ageMs = minUpdatedAt > 0 ? now - minUpdatedAt : Number.POSITIVE_INFINITY

  let dot: 'destructive' | 'warning' | 'success'
  let label: string

  if (ageMs > OFFLINE_THRESHOLD_MS) {
    dot = 'destructive'
    label = t('Offline')
  } else if (ageMs > STALE_THRESHOLD_MS) {
    dot = 'warning'
    label = t('Stale')
  } else {
    dot = 'success'
    label = t('Live')
  }

  return (
    <button
      type="button"
      onClick={() => void queryClient.invalidateQueries()}
      className="rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      title={`${t('Last sync')}: ${formatAge(ageMs)}`}
      aria-label={t('Refresh data')}
    >
      <Badge variant="outlineDot" dot={dot} className="cursor-pointer">
        {label}
        {isFetching > 0 ? <RefreshCw className="ml-1 h-3 w-3 animate-spin" /> : null}
      </Badge>
    </button>
  )
}
