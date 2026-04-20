export type BackupPolicy = {
  id: string
  agentId: string
  name: string
  sourcePath: string
  isEnabled: boolean
  intervalSeconds: number
  nextRunAt: string
  lastRunAt: string | null
  createdAt: string
}

export type UpsertPolicyInput = {
  agentId: string
  name: string
  sourcePath: string
  intervalSeconds: number
  isEnabled: boolean
}
