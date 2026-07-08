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
  scheduleKind: 'interval' | 'cron'
  cronExpression: string | null
  timeZoneId: string | null
  windowStartMinutes: number | null
  windowEndMinutes: number | null
}
