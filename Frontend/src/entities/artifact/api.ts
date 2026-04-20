import { env } from '@/shared/config/env'
import { http } from '@/shared/api/http'
import { listArtifacts as listArtifactsMock } from '@/shared/api/mockDb'
import type { BackupArtifact } from '@/entities/artifact/model/types'

export async function getArtifacts(jobId?: string) {
  if (env.apiMode === 'mock') {
    return listArtifactsMock(jobId)
  }

  const route = jobId
    ? `/api/backupartifacts/job/${jobId}`
    : '/api/backupartifacts'
  const response = await http.get<BackupArtifact[]>(route)
  return response.data
}
