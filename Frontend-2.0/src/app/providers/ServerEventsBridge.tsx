import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'

import { useAuthStore } from '@/app/store/auth-store'
import { connectServerEvents, type ServerEventTopic } from '@/shared/api/events'
import { env } from '@/shared/config/env'
import { useI18n } from '@/shared/i18n'
import { queryKeys } from '@/shared/lib/query'

// Which query-cache prefixes each server topic invalidates. Dashboard
// aggregates derive from jobs/artifacts/agents/policies, so those topics
// refresh it too. `restoreStatus` keys start with 'restore'.
const TOPIC_QUERY_KEYS: Record<ServerEventTopic, readonly (readonly string[])[]> = {
  jobs: [queryKeys.jobs, queryKeys.dashboard],
  artifacts: [queryKeys.artifacts, queryKeys.dashboard],
  restores: [['restore']],
  agents: [queryKeys.agents, queryKeys.dashboard],
  policies: [queryKeys.policies, queryKeys.dashboard],
  users: [queryKeys.users],
  'notification-channels': [queryKeys.notificationChannels],
}

// A finishing backup emits artifacts + jobs within milliseconds; batching
// with a short trailing window turns that burst into one invalidation
// sweep instead of back-to-back refetches of the same dashboard summary.
const FLUSH_DELAY_MS = 250

/**
 * Bridges the backend SSE stream into TanStack Query: every announced
 * topic marks the matching query groups stale, which refetches whatever
 * is currently on screen. Mounted once inside the query + i18n providers.
 *
 * Not connected in mock mode, when signed out, or when the operator chose
 * "Manual refresh only" — that preference means no background updates of
 * any kind.
 */
export function ServerEventsBridge() {
  const queryClient = useQueryClient()
  const user = useAuthStore((state) => state.user)
  const { refreshIntervalMs } = useI18n()
  const isManual = refreshIntervalMs === false

  useEffect(() => {
    if (!env.isLive || !user || isManual) return

    const pending = new Set<ServerEventTopic>()
    let timer: number | null = null

    const flush = () => {
      timer = null
      const topics = [...pending]
      pending.clear()
      for (const topic of topics) {
        for (const queryKey of TOPIC_QUERY_KEYS[topic]) {
          void queryClient.invalidateQueries({ queryKey })
        }
      }
    }

    const disconnect = connectServerEvents((topic) => {
      pending.add(topic)
      timer ??= window.setTimeout(flush, FLUSH_DELAY_MS)
    })

    return () => {
      if (timer !== null) window.clearTimeout(timer)
      disconnect()
    }
  }, [queryClient, user, isManual])

  return null
}
