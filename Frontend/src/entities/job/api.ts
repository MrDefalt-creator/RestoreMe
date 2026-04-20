import { env } from '@/shared/config/env'
import { http } from '@/shared/api/http'
import { getJob as getJobMock, listJobs as listJobsMock } from '@/shared/api/mockDb'
import type { BackupJob, BackupJobStatus } from '@/entities/job/model/types'

export async function getJobs(status?: BackupJobStatus | 'all') {
  if (env.apiMode === 'mock') {
    return listJobsMock(status)
  }

  const response = await http.get<BackupJob[]>('/api/backupjobs')
  return status && status !== 'all'
    ? response.data.filter((job) => job.status === status)
    : response.data
}

export async function getJobById(jobId: string) {
  if (env.apiMode === 'mock') {
    return getJobMock(jobId)
  }

  const response = await http.get<BackupJob>(`/api/backupjobs/${jobId}`)
  return response.data
}
