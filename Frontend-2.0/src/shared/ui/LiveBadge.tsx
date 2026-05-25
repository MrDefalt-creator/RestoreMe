import { useState, useEffect } from 'react'
import { useIsFetching, useQueryClient } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'

import { Badge } from './Badge'

export function LiveBadge() {
  const queryClient = useQueryClient()
  const isFetching = useIsFetching()
  const [now, setNow] = useState(() => Date.now())

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

  let variant: 'success' | 'warning' | 'destructive'
  let label: string

  if (ageSeconds > 120) {
    variant = 'destructive'
    label = 'Offline'
  } else if (ageSeconds > 30) {
    variant = 'warning'
    label = `Stale · ${Math.floor(ageSeconds / 60)}m`
  } else {
    variant = 'success'
    label = `Live · ${Math.floor(ageSeconds)}s`
  }

  return (
    <button
      type="button"
      onClick={() => void queryClient.invalidateQueries()}
      className="rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      title="Refresh all data"
      aria-label="Refresh all data"
    >
      <Badge variant={variant} className="cursor-pointer gap-1.5">
        {label}
        {isFetching > 0 ? <RefreshCw className="h-3 w-3 animate-spin" /> : null}
      </Badge>
    </button>
  )
}
