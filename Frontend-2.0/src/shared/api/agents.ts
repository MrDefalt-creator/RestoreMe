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

export async function revokeAgent(agentId: string): Promise<void> {
  await apiClient.post(`/api/agents/${agentId}/revoke`)
}

export async function deleteAgent(agentId: string): Promise<void> {
  await apiClient.delete(`/api/agents/${agentId}`)
}

export interface EnrollmentInfo {
  enrollmentToken: string
}

/**
 * @deprecated Returns the shared AgentEnrollment:EnrollmentToken — kept only
 * so legacy agents keep enrolling. New agents should be installed via the
 * per-agent install-token flow (POST /api/agents/install-tokens), which is
 * what the install-agent wizard now uses.
 */
export async function getEnrollmentInfo(): Promise<EnrollmentInfo> {
  const response = await apiClient.get<EnrollmentInfo>('/api/agents/enrollment-info')
  return response.data
}

export interface CreateInstallTokenRequest {
  preApprovedName?: string
  ttlMinutes?: number
}

export interface CreateInstallTokenResponse {
  id: string
  token: string
  expiresAt: string
}

export async function createInstallToken(
  input: CreateInstallTokenRequest = {},
): Promise<CreateInstallTokenResponse> {
  const response = await apiClient.post<CreateInstallTokenResponse>(
    '/api/agents/install-tokens',
    input,
  )
  return response.data
}

export async function getAgentById(agentId: string): Promise<Agent> {
  const response = await apiClient.get(`/api/agents/agent/${agentId}`)
  return response.data
}
