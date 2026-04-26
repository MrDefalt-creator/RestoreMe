export type AgentStatus = 'online' | 'stale' | 'offline'
export type PendingAgentStatus = 'pending' | 'approved'

export type Agent = {
  id: string
  name: string
  machineName: string
  osType: string
  version: string
  status: AgentStatus
  createdAt: string
  lastSeenAt: string | null
}

export type PendingAgent = {
  id: string
  machineName: string
  osType: string
  version: string
  status: PendingAgentStatus
  createdAt: string
  approvedAgentId: string | null
}

export type ApprovePendingAgentInput = {
  pendingId: string
  name: string
}
