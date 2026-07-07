import { useSyncExternalStore } from 'react'

import { isServerEventsConnected, subscribeServerEventsState } from '@/shared/api/events'

// True while the SSE stream to the backend is open — push updates drive
// freshness and interval polling can stand down.
export function useServerEventsConnected(): boolean {
  return useSyncExternalStore(subscribeServerEventsState, isServerEventsConnected)
}
