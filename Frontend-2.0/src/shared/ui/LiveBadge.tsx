import { useState, useEffect } from 'react'
import { useIsFetching, useQueryClient } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'

import { Badge } from './Badge'
import { useI18n } from '@/shared/i18n'

export function LiveBadge() {
  const queryClient = useQueryClient()
  const isFetching = useIsFetching()
  const [now, setNow] = useState(() => Date.now())
  const { t } = useI18n()

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 1000)
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

  const ageSeconds = minUpdatedAt > 0 ? (now - minUpdatedAt) / 1000 : Infinity

  let dot: 'destructive' | 'warning' | 'success'
  let label: string

  // Thresholds are tuned to dashboard's 30s refetch floor + worst-case
  // 60s for metrics queries. Anything below 60s is "live", up to 3 min
  // is "stale" (operator might want to refresh), beyond that = "offline".
  if (ageSeconds > 180) {
    dot = 'destructive'
    label = t('Offline')
  } else if (ageSeconds > 60) {
    dot = 'warning'
    label = `${t('Stale')} · ${Math.floor(ageSeconds / 60)}m`
  } else {
    dot = 'success'
    label = `Live · ${Math.floor(ageSeconds)}s`
  }

  return (
    <button
      type="button"
      onClick={() => void queryClient.invalidateQueries()}
      className="rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      title={t('Refresh data')}
      aria-label={t('Refresh data')}
    >
      <Badge variant="outlineDot" dot={dot} className="cursor-pointer">
        {label}
        {isFetching > 0 ? <RefreshCw className="ml-1 h-3 w-3 animate-spin" /> : null}
      </Badge>
    </button>
  )
}
