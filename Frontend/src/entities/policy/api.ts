import { env } from '@/shared/config/env'
import { http } from '@/shared/api/http'
import {
  createPolicy as createPolicyMock,
  getPolicy as getPolicyMock,
  listPolicies as listPoliciesMock,
  togglePolicy as togglePolicyMock,
  updatePolicy as updatePolicyMock,
} from '@/shared/api/mockDb'
import type {
  BackupPolicy,
  BackupPolicyDatabaseSettings,
  UpsertPolicyInput,
} from '@/entities/policy/model/types'

type ApiDatabaseSettings = {
  engine: string
  authMode: string
  host: string | null
  port: number | null
  databaseName: string
  username: string | null
  password: string | null
}

type ApiPolicy = Omit<BackupPolicy, 'databaseSettings'> & {
  databaseSettings: ApiDatabaseSettings | null
}

type CreatePolicyResponse = {
  id?: string
  policyId?: string
  name: string
  agentId: string
}

function normalizeDatabaseSettings(
  settings: ApiDatabaseSettings | null,
): BackupPolicyDatabaseSettings | null {
  if (!settings) {
    return null
  }

  return {
    engine: settings.engine === 'mysql' ? 'mysql' : 'postgres',
    authMode: settings.authMode === 'credentials' ? 'credentials' : 'integrated',
    host: settings.host,
    port: settings.port,
    databaseName: settings.databaseName,
    username: settings.username,
    password: settings.password,
  }
}

function normalizePolicy(policy: ApiPolicy): BackupPolicy {
  return {
    ...policy,
    type: policy.type === 'mysql' ? 'mysql' : policy.type === 'postgres' ? 'postgres' : 'filesystem',
    databaseSettings: normalizeDatabaseSettings(policy.databaseSettings),
  }
}

function toApiDatabaseSettings(settings: UpsertPolicyInput['databaseSettings']) {
  if (!settings) {
    return null
  }

  return {
    engine: settings.engine,
    authMode: settings.authMode,
    host: settings.host,
    port: settings.port,
    databaseName: settings.databaseName,
    username: settings.username,
    password: settings.password,
  }
}

export async function getPolicies() {
  if (env.apiMode === 'mock') {
    return listPoliciesMock()
  }

  const response = await http.get<ApiPolicy[]>('/api/policies')
  return response.data.map(normalizePolicy)
}

export async function getPolicyById(policyId: string) {
  if (env.apiMode === 'mock') {
    return getPolicyMock(policyId)
  }

  const response = await http.get<ApiPolicy>(`/api/policies/${policyId}`)
  return normalizePolicy(response.data)
}

export async function createPolicy(input: UpsertPolicyInput) {
  if (env.apiMode === 'mock') {
    return createPolicyMock(input)
  }

  const response = await http.post<CreatePolicyResponse>(
    `/api/policies/create_policy/${input.agentId}`,
    {
      type: input.type,
      name: input.name,
      sourcePath: input.sourcePath || null,
      interval: input.intervalSeconds,
      databaseSettings: toApiDatabaseSettings(input.databaseSettings),
    },
  )

  const createdPolicyId = response.data.policyId ?? response.data.id

  if (!createdPolicyId) {
    throw new Error('Policy was created, but the API did not return its identifier.')
  }

  return getPolicyById(createdPolicyId)
}

export async function updatePolicy(policyId: string, input: UpsertPolicyInput) {
  if (env.apiMode === 'mock') {
    return updatePolicyMock(policyId, input)
  }

  const response = await http.put<ApiPolicy>(`/api/policies/${policyId}`, {
    agentId: input.agentId,
    type: input.type,
    name: input.name,
    sourcePath: input.sourcePath || null,
    intervalSeconds: input.intervalSeconds,
    isEnabled: input.isEnabled,
    databaseSettings: toApiDatabaseSettings(input.databaseSettings),
  })

  return normalizePolicy(response.data)
}

export async function togglePolicy(policyId: string) {
  if (env.apiMode === 'mock') {
    return togglePolicyMock(policyId)
  }

  const response = await http.patch<ApiPolicy>(
    `/api/policies/${policyId}/toggle`,
  )
  return normalizePolicy(response.data)
}
