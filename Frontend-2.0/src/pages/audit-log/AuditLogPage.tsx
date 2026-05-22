import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'

import { useAuthStore } from '@/app/store/auth-store'
import { getAuditLogs } from '@/entities/audit-log'
import { useI18n } from '@/shared/i18n'
import { cn } from '@/shared/lib/cn'
import { formatDateTime } from '@/shared/lib/format'
import { queryKeys } from '@/shared/lib/query'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'

const PAGE_SIZE = 50

export function AuditLogPage() {
  const { t } = useI18n()
  const currentUser = useAuthStore((state) => state.user)
  const isAdmin = currentUser?.role === 'admin'

  const [page, setPage] = useState(1)
  const [actionFilter, setActionFilter] = useState('')
  const [pendingAction, setPendingAction] = useState('')

  const query = useQuery({
    queryKey: queryKeys.auditLogs(page, PAGE_SIZE, actionFilter || undefined),
    queryFn: () =>
      getAuditLogs({
        page,
        pageSize: PAGE_SIZE,
        action: actionFilter || undefined,
      }),
    enabled: isAdmin,
    placeholderData: keepPreviousData,
  })

  if (!isAdmin) {
    return (
      <div className="space-y-8">
        <SectionHeading
          eyebrow={t('Security')}
          title={t('Audit log')}
          description={t('Trace of admin and agent lifecycle actions in the control plane.')}
        />
        <EmptyState
          title={t('Administrator access required')}
          description={t('Sign in as an administrator to view the audit log.')}
        />
      </div>
    )
  }

  const data = query.data
  const items = data?.items ?? []
  const total = data?.total ?? 0
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  return (
    <div className="space-y-8">
      <SectionHeading
        eyebrow={t('Security')}
        title={t('Audit log')}
        description={t('Trace of admin and agent lifecycle actions in the control plane.')}
      />

      <Card>
        <CardContent>
          <form
            className="flex flex-wrap items-end gap-3"
            onSubmit={(event) => {
              event.preventDefault()
              setPage(1)
              setActionFilter(pendingAction.trim())
            }}
          >
            <div className="flex flex-col gap-1">
              <label className="text-sm text-muted-foreground" htmlFor="audit-action">
                {t('Action')}
              </label>
              <Input
                id="audit-action"
                value={pendingAction}
                onChange={(event) => setPendingAction(event.target.value)}
                placeholder="user.create"
                className="min-w-[220px]"
              />
            </div>
            <Button type="submit" variant="secondary">
              {t('Apply')}
            </Button>
            {actionFilter ? (
              <Button
                type="button"
                variant="ghost"
                onClick={() => {
                  setPendingAction('')
                  setActionFilter('')
                  setPage(1)
                }}
              >
                {t('Clear')}
              </Button>
            ) : null}
            <Button
              type="button"
              variant="secondary"
              size="icon"
              className="ml-auto"
              onClick={() => void query.refetch()}
              disabled={query.isFetching}
              title={t('Refresh data')}
              aria-label={t('Refresh data')}
            >
              <RefreshCw className={cn('h-4 w-4', query.isFetching ? 'animate-spin' : '')} />
            </Button>
          </form>
        </CardContent>
      </Card>

      {items.length ? (
        <Card>
          <CardContent className="p-0">
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm align-middle">
                <thead className="bg-muted/50 text-muted-foreground">
                  <tr>
                    {['When', 'Actor', 'Action', 'Target', 'Details'].map((label) => (
                      <th key={label} className="px-4 py-3 font-medium align-middle">
                        {t(label)}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {items.map((entry) => (
                    <tr key={entry.id} className="border-t border-border align-top">
                      <td className="whitespace-nowrap px-4 py-3 text-muted-foreground">
                        {formatDateTime(entry.occurredAtUtc)}
                      </td>
                      <td className="px-4 py-3 text-foreground">
                        {entry.actorUsername ?? (
                          <span className="text-muted-foreground/70">{t('System')}</span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <Badge variant="neutral">{entry.action}</Badge>
                      </td>
                      <td className="px-4 py-3 font-mono text-xs text-muted-foreground">
                        {entry.targetId ?? '—'}
                      </td>
                      <td className="px-4 py-3 text-muted-foreground">{entry.details ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="flex items-center justify-between gap-3 border-t border-border px-4 py-3 text-sm text-muted-foreground">
              <span>
                {t('Page')} {page} / {totalPages} · {total} {t('records')}
              </span>
              <div className="flex gap-2">
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page <= 1 || query.isFetching}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  {t('Previous')}
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page >= totalPages || query.isFetching}
                  onClick={() => setPage((p) => p + 1)}
                >
                  {t('Next')}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      ) : (
        <EmptyState
          title={t('No audit records found')}
          description={t('Audit events appear here as administrators and agents act on the system.')}
        />
      )}
    </div>
  )
}
