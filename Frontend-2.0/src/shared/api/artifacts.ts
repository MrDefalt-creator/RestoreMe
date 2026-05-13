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
