import apiClient from './client'

export interface Agent {
  id: string
  name: string
  machineName?: string
  osType?: string
  version?: string
  status: 'online' | 'stale' | 'offline'
  createdAt?: string
  lastSeenAt: string | null
  policies?: {
    id: string
    name: string
    isEnabled: boolean
  }[]
}

export interface PendingAgent {
  id: string
  machineName: string
  osType: string
  version: string
  status: string
  createdAt: string
  approvedAgentId: string | null
}

export async function getAgents(): Promise<Agent[]> {
  const response = await apiClient.get('/api/agents')
  return response.data
}

export async function getPendingAgents(): Promise<PendingAgent[]> {
  const response = await apiClient.get('/api/agents/pending')
  return response.data
}

export async function approveAgent(input: { pendingId: string; name: string }): Promise<void> {
  await apiClient.post(`/api/agents/approve/${input.pendingId}`, { name: input.name })
}

export async function rejectAgent(pendingId: string): Promise<void> {
  await apiClient.post(`/api/agents/reject/${pendingId}`)
}

export async function getAgentById(agentId: string): Promise<Agent> {
  const response = await apiClient.get(`/api/agents/agent/${agentId}`)
  return response.data
}
