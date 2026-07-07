import { env } from '@/shared/config/env'

// Mirrors the event names emitted by the backend's EventsController.
export type ServerEventTopic =
  | 'jobs'
  | 'artifacts'
  | 'restores'
  | 'agents'
  | 'policies'
  | 'users'
  | 'notification-channels'

export const SERVER_EVENT_TOPICS: ServerEventTopic[] = [
  'jobs',
  'artifacts',
  'restores',
  'agents',
  'policies',
  'users',
  'notification-channels',
]

// Connection state lives at module level (not in a store) so that
// useLiveQueryOptions can read it via useSyncExternalStore without
// coupling the shared layer to zustand.
let connected = false
const connectionListeners = new Set<() => void>()

function setConnected(next: boolean) {
  if (connected === next) return
  connected = next
  connectionListeners.forEach((listener) => listener())
}

export function subscribeServerEventsState(listener: () => void): () => void {
  connectionListeners.add(listener)
  return () => {
    connectionListeners.delete(listener)
  }
}

export function isServerEventsConnected(): boolean {
  return connected
}

/**
 * Opens the SSE stream and reports every topic the backend announces.
 * The HttpOnly auth cookie rides along via `withCredentials`. EventSource
 * reconnects on its own after a drop; while it does, `connected` is false
 * and interval polling (useLiveQueryOptions) takes over as the fallback.
 * Returns a cleanup function that closes the stream.
 */
export function connectServerEvents(onTopic: (topic: ServerEventTopic) => void): () => void {
  const source = new EventSource(`${env.apiBaseUrl}/api/events`, { withCredentials: true })

  for (const topic of SERVER_EVENT_TOPICS) {
    source.addEventListener(topic, () => onTopic(topic))
  }

  source.onopen = () => setConnected(true)
  source.onerror = () => setConnected(false)

  return () => {
    source.close()
    setConnected(false)
  }
}
