export interface BackupPolicy {
  id: string
  name: string
  agentId: string
  type: 'filesystem' | 'postgres' | 'mysql'
  isEnabled: boolean
  intervalSeconds: number
  sourcePath?: string
  nextRunAt: string
  lastRunAt: string
  createdAt: string
  updatedAt: string
}
