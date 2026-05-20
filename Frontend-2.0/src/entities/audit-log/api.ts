import apiClient from '@/shared/api/client'
import type { AuditLogPage, AuditLogQuery } from './model'

interface RawEntry {
  id: string
  actorId: string | null
  actorUsername: string | null
  action: string
  targetId: string | null
  details: string | null
  occurredAt: string
}

export async function getAuditLogs(query: AuditLogQuery = {}): Promise<AuditLogPage> {
  const params = new URLSearchParams()
  if (query.from) params.set('from', query.from)
  if (query.to) params.set('to', query.to)
  if (query.action) params.set('action', query.action)
  if (query.actorId) params.set('actorId', query.actorId)
  if (query.page) params.set('page', String(query.page))
  if (query.pageSize) params.set('pageSize', String(query.pageSize))

  const suffix = params.toString()
  const response = await apiClient.get<{
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
