import apiClient from './client'
import type { PagedResponse, SortDir } from './pagination'

export interface Job {
  id: string
  name?: string
  status: 'pending' | 'completed' | 'failed' | 'running'
  // Nullable when the owning agent or policy was deleted with "keep
  // history". In that case the snapshot fields below carry the display
  // names that were captured at delete time.
  policyId: string | null
  policyName?: string | null
  agentId: string | null
  agentName?: string | null
  startedAt: string
  completedAt?: string | null
  errorMessage?: string | null
}

export async function getJobs(): Promise<Job[]> {
  const response = await apiClient.get('/api/backupjobs')
  return response.data
}

export type JobSortKey = 'startedAt' | 'completedAt' | 'status'

export interface JobsPageParams {
  page: number
  pageSize: number
  sortBy?: JobSortKey
  sortDir?: SortDir
  status?: Job['status']
}

export async function getJobsPage(params: JobsPageParams): Promise<PagedResponse<Job>> {
  const response = await apiClient.get('/api/backupjobs', { params })
  return response.data
}

export async function getJobById(jobId: string): Promise<Job> {
  const response = await apiClient.get(`/api/backupjobs/${jobId}`)
  return response.data
}

export async function getJobsByAgent(agentId: string): Promise<Job[]> {
  const response = await apiClient.get(`/api/backupjobs/agent/${agentId}`)
  return response.data
}

export async function getJobsByPolicy(policyId: string): Promise<Job[]> {
  const response = await apiClient.get(`/api/backupjobs/policy/${policyId}`)
  return response.data
}

export async function startJob(jobId: string): Promise<Job> {
  const response = await apiClient.post(`/api/backupjobs/${jobId}/start`)
  return response.data
}

export async function failJob(jobId: string, reason: string): Promise<void> {
  await apiClient.post(`/api/backupjobs/${jobId}/fail`, { reason })
}
