import { env } from '@/shared/config/env'
import { http } from '@/shared/api/http'
import {
  createPolicy as createPolicyMock,
  getPolicy as getPolicyMock,
  listPolicies as listPoliciesMock,
  togglePolicy as togglePolicyMock,
  updatePolicy as updatePolicyMock,
} from '@/shared/api/mockDb'
import type { BackupPolicy, UpsertPolicyInput } from '@/entities/policy/model/types'

type CreatePolicyResponse = {
  id?: string
  policyId?: string
  name: string
  agentId: string
}

export async function getPolicies() {
  if (env.apiMode === 'mock') {
    return listPoliciesMock()
  }

  const response = await http.get<BackupPolicy[]>('/api/policies')
  return response.data
}

export async function getPolicyById(policyId: string) {
  if (env.apiMode === 'mock') {
    return getPolicyMock(policyId)
  }

  const response = await http.get<BackupPolicy>(`/api/policies/${policyId}`)
  return response.data
}

export async function createPolicy(input: UpsertPolicyInput) {
  if (env.apiMode === 'mock') {
    return createPolicyMock(input)
  }

  const response = await http.post<CreatePolicyResponse>(
    `/api/policies/create_policy/${input.agentId}`,
    {
      name: input.name,
      sourcePath: input.sourcePath,
      interval: input.intervalSeconds,
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

  const response = await http.put<BackupPolicy>(`/api/policies/${policyId}`, {
    agentId: input.agentId,
    name: input.name,
    sourcePath: input.sourcePath,
    interval: input.intervalSeconds,
    isEnabled: input.isEnabled,
  })

  return response.data
}

export async function togglePolicy(policyId: string) {
  if (env.apiMode === 'mock') {
    return togglePolicyMock(policyId)
  }

  const response = await http.patch<BackupPolicy>(
    `/api/policies/${policyId}/toggle`,
  )
  return response.data
}
