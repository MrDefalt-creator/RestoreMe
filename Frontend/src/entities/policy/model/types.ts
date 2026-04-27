export type PolicyType = 'filesystem' | 'postgres' | 'mysql'
export type DatabaseAuthMode = 'integrated' | 'credentials'
export type DatabaseEngine = 'postgres' | 'mysql'

export type BackupPolicyDatabaseSettings = {
  engine: DatabaseEngine
  authMode: DatabaseAuthMode
  host: string | null
  port: number | null
  databaseName: string
  username: string | null
  password: string | null
}

export type BackupPolicy = {
  id: string
  agentId: string
  type: PolicyType
  name: string
  sourcePath: string
  isEnabled: boolean
  intervalSeconds: number
  nextRunAt: string
  lastRunAt: string | null
  createdAt: string
  databaseSettings: BackupPolicyDatabaseSettings | null
}

export type UpsertPolicyInput = {
  agentId: string
  type: PolicyType
  name: string
  sourcePath: string
  intervalSeconds: number
  isEnabled: boolean
  databaseSettings: BackupPolicyDatabaseSettings | null
}
