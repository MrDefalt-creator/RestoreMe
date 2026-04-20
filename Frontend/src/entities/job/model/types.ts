export type BackupJobStatus = 'running' | 'completed' | 'failed'

export type BackupJob = {
  id: string
  agentId: string
  policyId: string
  status: BackupJobStatus
  startedAt: string
  completedAt: string | null
  errorMessage: string | null
}
