export interface Agent {
  id: string
  name: string
  status: 'online' | 'stale' | 'offline'
  lastSeenAt: string
  policies?: {
    id: string
    name: string
    isEnabled: boolean
  }[]
}

export interface PendingAgent extends Agent {
  serverAddress: string
  heartbeatIntervalSeconds: number
}
