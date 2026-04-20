import { env } from '@/shared/config/env'
import { http } from '@/shared/api/http'
import {
  approvePendingAgent as approvePendingAgentMock,
  getAgent as getAgentMock,
  listAgents as listAgentsMock,
  listPendingAgents as listPendingAgentsMock,
} from '@/shared/api/mockDb'
import type {
  Agent,
  ApprovePendingAgentInput,
  PendingAgent,
} from '@/entities/agent/model/types'

type PendingStatusResponse = {
  status: number
  approvedAgentId: string | null
}

export async function getAgents() {
  if (env.apiMode === 'mock') {
    return listAgentsMock()
  }

  const response = await http.get<Agent[]>('/api/agents')
  return response.data
}

export async function getAgentById(agentId: string) {
  if (env.apiMode === 'mock') {
    return getAgentMock(agentId)
  }

  const response = await http.get<Agent>(`/api/agents/${agentId}`)
  return response.data
}

export async function getPendingAgents() {
  if (env.apiMode === 'mock') {
    return listPendingAgentsMock()
  }

  const response = await http.get<PendingAgent[]>('/api/agents/pending')
  return response.data
}

export async function approvePendingAgent(input: ApprovePendingAgentInput) {
  if (env.apiMode === 'mock') {
    return approvePendingAgentMock(input.pendingId, input.name)
  }

  const response = await http.post<string>(
    `/api/agents/approve/${input.pendingId}`,
    {
      name: input.name,
    },
  )

  return getAgentById(response.data)
}

export async function getPendingStatus(pendingId: string) {
  const response = await http.get<PendingStatusResponse>(
    `/api/agents/status/${pendingId}`,
  )
  return response.data
}
