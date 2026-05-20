import { http } from '@/shared/api/http'

export type AuditLogEntry = {
  id: string
  actorId: string | null
  actorUsername: string | null
  action: string
  targetId: string | null
  details: string | null
  occurredAtUtc: string
}

export type AuditLogPage = {
  items: AuditLogEntry[]
  total: number
  page: number
  pageSize: number
}

export type AuditLogQuery = {
  from?: string
  to?: string
  action?: string
  actorId?: string
  page?: number
  pageSize?: number
}

type RawEntry = {
  id: string
  actorId: string | null
  actorUsername: string | null
  action: string
  targetId: string | null
  details: string | null
  occurredAt: string
}

export async function getAuditLogs(query: AuditLogQuery): Promise<AuditLogPage> {
  const params = new URLSearchParams()
  if (query.from) params.set('from', query.from)
  if (query.to) params.set('to', query.to)
  if (query.action) params.set('action', query.action)
  if (query.actorId) params.set('actorId', query.actorId)
  if (query.page) params.set('page', String(query.page))
  if (query.pageSize) params.set('pageSize', String(query.pageSize))

  const suffix = params.toString()
  const response = await http.get<{
    items: RawEntry[]
    total: number
    page: number
    pageSize: number
  }>(`/api/audit-logs${suffix ? `?${suffix}` : ''}`)

  return {
    items: response.data.items.map((x) => ({
      id: x.id,
      actorId: x.actorId,
      actorUsername: x.actorUsername,
      action: x.action,
      targetId: x.targetId,
      details: x.details,
      occurredAtUtc: x.occurredAt,
    })),
    total: response.data.total,
    page: response.data.page,
    pageSize: response.data.pageSize,
  }
}
