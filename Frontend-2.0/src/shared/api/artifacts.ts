import apiClient from './client'

export interface Artifact {
  id: string
  name?: string
  fileName?: string
  jobId: string
  size: number
  type?: 'filesystem' | 'postgres' | 'mysql'
  createdAt: string
  expiresAt?: string
  downloadUrl?: string
  objectKey?: string
  checksum?: string
}

export async function getArtifacts(): Promise<Artifact[]> {
  const response = await apiClient.get('/api/backupartifacts')
  return response.data
}

export async function getArtifactById(artifactId: string): Promise<Artifact> {
  const response = await apiClient.get(`/api/backupartifacts/${artifactId}`)
  return response.data
}

export async function getArtifactsByJob(jobId: string): Promise<Artifact[]> {
  const response = await apiClient.get(`/api/backupartifacts/job/${jobId}`)
  return response.data
}

export async function downloadArtifact(artifactId: string): Promise<Blob> {
  const response = await apiClient.get(`/api/backupartifacts/${artifactId}/download`, {
    responseType: 'blob',
  })
  return response.data
}

export interface RestoreRequest {
  artifactId: string
  targetAgentId?: string
  targetName?: string
  dryRun: boolean
  force: boolean
}

export interface RestoreStatus {
  id: string
  status: 'pending' | 'running' | 'completed' | 'failed'
  progress?: number
  bytesTotal?: number
  bytesDone?: number
  logTail?: string
  etaSeconds?: number
}

export async function requestRestore(body: RestoreRequest): Promise<{ restoreJobId: string }> {
  const response = await apiClient.post('/api/restore', body)
  return response.data
}

export async function getRestoreStatus(id: string): Promise<RestoreStatus> {
  const response = await apiClient.get(`/api/restore/${id}/status`)
  return response.data
}
