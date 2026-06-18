export interface AuditLogEntry {
  id: string
  actorId: string | null
  actorUsername: string | null
  action: string
  targetId: string | null
  details: string | null
  occurredAtUtc: string
}

export interface AuditLogPage {
  items: AuditLogEntry[]
  total: number
  page: number
  pageSize: number
}

export interface AuditLogQuery {
  from?: string
  to?: string
  action?: string
  actorId?: string
  page?: number
  pageSize?: number
}
