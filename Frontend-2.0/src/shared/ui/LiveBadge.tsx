import { useEffect, useState } from 'react'
import { useIsFetching, useQueryClient } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'

import { Badge } from './Badge'
import { useI18n } from '@/shared/i18n'
import { useServerEventsConnected } from '@/shared/lib/useServerEventsConnected'

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
  const sseConnected = useServerEventsConnected()
  // `now` updates only every TICK_MS so the dot can flip Live → Stale →
  // Offline. Storing it in state keeps the render function pure under
  // react-hooks/purity.
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), TICK_MS)
    return () => clearInterval(id)
  }, [])

  // Only consider queries that some mounted component is currently
  // observing. The previous "min over every cached query" pulled in
  // stale data from pages the user had already left (Jobs, Policies,
  // …) — once those crossed 180 s the badge flipped to Offline even
  // though the active page kept refetching.
  // Among the live ones, take the *newest* update: if anything on the
  // page has refreshed within STALE_THRESHOLD, the operator is looking
  // at fresh data and the badge should say so.
  const observedQueries = queryClient
    .getQueryCache()
    .findAll()
    .filter((q) => q.getObserversCount() > 0 && q.state.dataUpdatedAt > 0)
  const latestUpdatedAt =
    observedQueries.length > 0
      ? Math.max(...observedQueries.map((q) => q.state.dataUpdatedAt))
      : 0

  const ageMs = latestUpdatedAt > 0 ? now - latestUpdatedAt : Number.POSITIVE_INFINITY

  let dot: 'destructive' | 'warning' | 'success'
  let label: string

  // With the push stream open the server tells us the moment anything
  // changes — quiet data is current data, not stale data. Age-based decay
  // only applies while we're back on interval polling.
  if (sseConnected) {
    dot = 'success'
    label = t('Live')
  } else if (ageMs > OFFLINE_THRESHOLD_MS) {
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
      title={
        sseConnected
          ? t('Live updates connected — changes appear instantly')
          : `${t('Last sync')}: ${formatAge(ageMs)}`
      }
      aria-label={t('Refresh data')}
    >
      <Badge variant="outlineDot" dot={dot} className="cursor-pointer">
        {label}
        {isFetching > 0 ? <RefreshCw className="ml-1 h-3 w-3 animate-spin" /> : null}
      </Badge>
    </button>
  )
}
