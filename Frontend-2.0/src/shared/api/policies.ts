import apiClient from './client'

export interface BackupPolicy {
  id: string
  name: string
  agentId: string
  type: 'filesystem' | 'postgres' | 'mysql'
  isEnabled: boolean
  intervalSeconds: number
  sourcePath: string
  databaseSettings?: {
    engine?: 'postgres' | 'mysql'
    authMode?: 'integrated' | 'credentials'
    host: string | null
    port: number | null
    databaseName: string
    username: string | null
    password: string | null
  } | null
  nextRunAt: string
  lastRunAt: string | null
  createdAt: string
  updatedAt?: string
}

export type UpsertPolicyInput = {
  agentId: string
  type: BackupPolicy['type']
  name: string
  sourcePath: string
  intervalSeconds: number
  isEnabled: boolean
  databaseSettings: BackupPolicy['databaseSettings']
}

export async function getPolicies(): Promise<BackupPolicy[]> {
  const response = await apiClient.get('/api/policies')
  return response.data
}

export async function getPolicyById(policyId: string): Promise<BackupPolicy> {
  const response = await apiClient.get(`/api/policies/${policyId}`)
  return response.data
}

export async function createPolicy(input: UpsertPolicyInput): Promise<BackupPolicy> {
  const response = await apiClient.post<{ id?: string; policyId?: string }>(`/api/policies/create_policy/${input.agentId}`, {
    type: input.type,
    name: input.name,
    sourcePath: input.sourcePath || null,
    interval: input.intervalSeconds,
    databaseSettings: input.databaseSettings,
  })
  const policyId = response.data.policyId ?? response.data.id
  if (!policyId) {
    throw new Error('Policy was created, but API did not return its id.')
  }
  return getPolicyById(policyId)
}

export async function updatePolicy(policyId: string, policy: UpsertPolicyInput): Promise<BackupPolicy> {
  const response = await apiClient.put(`/api/policies/${policyId}`, policy)
  return response.data
}

export async function togglePolicy(policyId: string): Promise<BackupPolicy> {
  const response = await apiClient.patch(`/api/policies/${policyId}/toggle`)
  return response.data
}
