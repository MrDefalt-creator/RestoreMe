import { useMemo, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { RefreshCw, ShieldAlert } from 'lucide-react'

import { useAuthStore } from '@/app/store/auth-store'
import { getAuditLogs } from '@/entities/audit-log'
import type { AuditLogEntry } from '@/entities/audit-log'
import { useI18n } from '@/shared/i18n'
import { cn } from '@/shared/lib/cn'
import { formatDateTime, formatRelativeTime, formatUtcIso } from '@/shared/lib/format'
import { queryKeys } from '@/shared/lib/query'
import { useUrlFilterState } from '@/shared/lib/useUrlFilterState'
import {
  categorize,
  renderAuditMessage,
  actorHue,
  type AuditCategory,
} from '@/shared/lib/audit-templates'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'

const PAGE_SIZE = 50
const CATEGORIES: AuditCategory[] = ['Users', 'Agents', 'Policies', 'Backups', 'Restores', 'Security', 'Other']

export function AuditLogPage() {
  const { t } = useI18n()
  const currentUser = useAuthStore((state) => state.user)
  const isAdmin = currentUser?.role === 'admin'

  const [page, setPage] = useState(1)
  const [actionFilter, setActionFilter] = useUrlFilterState<string>('action', '')
  const [pendingAction, setPendingAction] = useState(actionFilter)
  const [activeCategories, setActiveCategories] = useState<AuditCategory[]>([])

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

  const data = query.data
  const total = data?.total ?? 0
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const visibleItems = useMemo(() => {
    const entries = data?.items ?? []
    if (activeCategories.length === 0) return entries
    return entries.filter((entry) => activeCategories.includes(categorize(entry.action)))
  }, [data?.items, activeCategories])

  function toggleCategory(cat: AuditCategory) {
    setActiveCategories((prev) =>
      prev.includes(cat) ? prev.filter((c) => c !== cat) : [...prev, cat]
    )
  }

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

  return (
    <div className="space-y-8">
      <SectionHeading
        eyebrow={t('Security')}
        title={t('Audit log')}
        description={t('Trace of admin and agent lifecycle actions in the control plane.')}
      />

      <Card>
        <CardContent>
          <div className="flex flex-wrap items-center gap-2">
            {CATEGORIES.map((cat) => (
              <Button
                key={cat}
                type="button"
                variant={activeCategories.includes(cat) ? 'primary' : 'outline'}
                size="sm"
                onClick={() => toggleCategory(cat)}
              >
                {t(cat)}
              </Button>
            ))}
            <div className="ml-auto flex items-center gap-2">
              <form
                className="flex gap-2"
                onSubmit={(event) => {
                  event.preventDefault()
                  setPage(1)
                  setActionFilter(pendingAction.trim())
                }}
              >
                <Input
                  value={pendingAction}
                  onChange={(event) => setPendingAction(event.target.value)}
                  placeholder={t('Filter by action...')}
                  className="min-w-[180px]"
                />
                <Button type="submit" variant="secondary" size="sm">{t('Apply')}</Button>
                {actionFilter ? (
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => { setPendingAction(''); setActionFilter(''); setPage(1) }}
                  >
                    {t('Clear')}
                  </Button>
                ) : null}
              </form>
              <Button
                type="button"
                variant="secondary"
                size="icon"
                onClick={() => void query.refetch()}
                disabled={query.isFetching}
                title={t('Refresh data')}
                aria-label={t('Refresh data')}
              >
                <RefreshCw className={cn('h-4 w-4', query.isFetching ? 'animate-spin' : '')} />
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {visibleItems.length ? (
        <Card>
          <CardContent className="p-0">
            <div className="divide-y divide-border">
              {visibleItems.map((entry) => (
                <AuditRow key={entry.id} entry={entry} />
              ))}
            </div>
            <div className="flex items-center justify-between gap-3 border-t border-border px-4 py-3 text-sm text-muted-foreground">
              <span>
                {t('Page')} {page} / {totalPages} · {total} {t('records')}
                {activeCategories.length > 0 && ` · ${visibleItems.length} ${t('shown')}`}
              </span>
              <div className="flex gap-2">
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page <= 1 || query.isFetching}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  {t('Previous page')}
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page >= totalPages || query.isFetching}
                  onClick={() => setPage((p) => p + 1)}
                >
                  {t('Next page')}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      ) : (
        <EmptyState
          title={t('No audit records found')}
          description={
            activeCategories.length > 0
              ? t('No events in the selected categories on this page.')
              : t('Audit events appear here as administrators and agents act on the system.')
          }
        />
      )}
    </div>
  )
}

function AuditRow({ entry }: { entry: AuditLogEntry }) {
  const isSystem = !entry.actorUsername
  const hue = isSystem ? null : actorHue(entry.actorUsername!)

  return (
    <div className="flex items-start gap-4 px-4 py-3">
      <div
        className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold"
        style={
          isSystem
            ? { background: 'hsl(var(--muted))', color: 'hsl(var(--muted-foreground))' }
            : { background: `hsl(${hue} 60% 50% / 0.15)`, color: `hsl(${hue} 60% 40%)` }
        }
      >
        {isSystem ? (
          <ShieldAlert className="h-3.5 w-3.5" />
        ) : (
          entry.actorUsername!.charAt(0).toUpperCase()
        )}
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-sm text-foreground">{renderAuditMessage(entry)}</p>
        <p className="mt-0.5 text-xs text-muted-foreground" title={formatUtcIso(entry.occurredAtUtc)}>
          {formatDateTime(entry.occurredAtUtc)} · {formatRelativeTime(entry.occurredAtUtc)}
          {entry.targetId ? ` · ${entry.targetId.slice(0, 8)}` : ''}
        </p>
      </div>
    </div>
  )
}
