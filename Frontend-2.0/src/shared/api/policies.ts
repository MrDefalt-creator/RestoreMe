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
  retentionDays: number | null
}

export type UpsertPolicyInput = {
  agentId: string
  type: BackupPolicy['type']
  name: string
  sourcePath: string
  intervalSeconds: number
  isEnabled: boolean
  databaseSettings: BackupPolicy['databaseSettings']
  retentionDays: number | null
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
    retentionDays: input.retentionDays,
  })
  const policyId = response.data.policyId ?? response.data.id
  if (!policyId) {
    throw new Error('Policy was created, but API did not return its id.')
  }
  return getPolicyById(policyId)
}

export async function updatePolicy(policyId: string, policy: UpsertPolicyInput): Promise<BackupPolicy> {
  const response = await apiClient.put(`/api/policies/${policyId}`, {
    agentId: policy.agentId,
    type: policy.type,
    name: policy.name,
    sourcePath: policy.sourcePath,
    intervalSeconds: policy.intervalSeconds,
    isEnabled: policy.isEnabled,
    databaseSettings: policy.databaseSettings,
    retentionDays: policy.retentionDays,
  })
  return response.data
}

export async function togglePolicy(policyId: string): Promise<BackupPolicy> {
  const response = await apiClient.patch(`/api/policies/${policyId}/toggle`)
  return response.data
}
