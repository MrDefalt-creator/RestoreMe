import { useState } from 'react'
import * as RadixDialog from '@radix-ui/react-dialog'
import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { Link } from 'react-router-dom'
import { ExternalLink, X } from 'lucide-react'

import { getJobById, type Job } from '@/shared/api/jobs'
import { formatDateTime, formatDurationSeconds, formatRelativeTime } from '@/shared/lib/format'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { useI18n } from '@/shared/i18n'

type Tab = 'timeline' | 'logs' | 'metadata'

const statusVariant: Record<Job['status'], 'success' | 'destructive' | 'accent' | 'neutral'> = {
  pending: 'neutral',
  completed: 'success',
  failed: 'destructive',
  running: 'accent',
}

const statusDot: Record<Job['status'], string> = {
  pending: 'bg-muted-foreground',
  completed: 'bg-success',
  failed: 'bg-destructive',
  running: 'bg-warning',
}

export function JobDrawer() {
  const { t } = useI18n()
  const [searchParams, setSearchParams] = useSearchParams()
  const [tab, setTab] = useState<Tab>('timeline')
  const jobId = searchParams.get('id')
  const isOpen = !!jobId

  const jobQuery = useQuery({
    queryKey: ['job', jobId],
    queryFn: () => getJobById(jobId!),
    enabled: !!jobId,
  })

  const job = jobQuery.data

  function close() {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev)
      next.delete('id')
      return next
    })
    setTab('timeline')
  }

  const hasDuration = job?.completedAt && job?.startedAt
  const durationSeconds = hasDuration
    ? Math.max(0, Math.round((Date.parse(job.completedAt as string) - Date.parse(job.startedAt)) / 1000))
    : null

  const timelineEvents = job
    ? [
        { label: t('Created / queued'), time: job.startedAt, status: 'pending' as Job['status'] },
        ...(job.status !== 'pending' ? [{ label: t('Started'), time: job.startedAt, status: 'running' as Job['status'] }] : []),
        ...(job.completedAt ? [{ label: job.status === 'failed' ? t('Failed') : t('Completed'), time: job.completedAt, status: job.status }] : []),
      ]
    : []

  const TABS: { key: Tab; label: string }[] = [
    { key: 'timeline', label: t('Timeline') },
    { key: 'logs', label: t('Logs') },
    { key: 'metadata', label: t('Metadata') },
  ]

  return (
    <RadixDialog.Root open={isOpen} onOpenChange={(open) => { if (!open) close() }}>
      <RadixDialog.Portal>
        <RadixDialog.Overlay className="fixed inset-0 z-50 bg-background/60 backdrop-blur-sm data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
        <RadixDialog.Content className="fixed right-0 top-0 z-50 h-full w-full max-w-[440px] border-l border-border bg-card shadow-[var(--shadow-xl)] focus:outline-none data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:slide-out-to-right data-[state=open]:slide-in-from-right">
          <RadixDialog.Title className="sr-only">{t('Job details')}</RadixDialog.Title>
          <div className="flex h-full flex-col">
            <div className="flex items-center justify-between border-b border-border px-5 py-4">
              <div className="min-w-0">
                {job ? (
                  <>
                    <p className="truncate font-semibold text-foreground">
                      {job.policyName ?? job.name ?? `Job ${job.id.slice(0, 8)}`}
                    </p>
                    <div className="mt-0.5 flex items-center gap-2">
                      <Badge variant={statusVariant[job.status]} className="text-xs">{t(job.status)}</Badge>
                      {job.agentName && <span className="text-xs text-muted-foreground">{job.agentName}</span>}
                    </div>
                  </>
                ) : (
                  <div className="space-y-1.5">
                    <div className="h-4 w-40 animate-pulse rounded bg-secondary" />
                    <div className="h-3 w-24 animate-pulse rounded bg-secondary" />
                  </div>
                )}
              </div>
              <Button variant="ghost" size="icon" onClick={close} aria-label={t('Close')}>
                <X className="h-4 w-4" />
              </Button>
            </div>

            <div className="flex border-b border-border px-5">
              {TABS.map(({ key, label }) => (
                <button
                  key={key}
                  type="button"
                  onClick={() => setTab(key)}
                  className={
                    tab === key
                      ? 'border-b-2 border-primary pb-3 pt-3 text-sm font-medium text-foreground'
                      : 'pb-3 pt-3 text-sm text-muted-foreground hover:text-foreground mr-4'
                  }
                  style={tab === key ? { marginRight: '1rem' } : undefined}
                >
                  {label}
                </button>
              ))}
            </div>

            <div className="flex-1 overflow-y-auto p-5">
              {tab === 'timeline' && (
                <div className="relative space-y-0">
                  <div className="absolute left-[7px] top-2 bottom-2 w-px bg-border" />
                  {timelineEvents.map((event, i) => (
                    <div key={i} className="relative flex items-start gap-4 pb-6 last:pb-0">
                      <span className={`relative z-10 mt-1 h-3.5 w-3.5 shrink-0 rounded-full border-2 border-card ${statusDot[event.status]}`} />
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-foreground">{event.label}</p>
                        <p className="text-xs text-muted-foreground">{formatDateTime(event.time)}</p>
                        <p className="text-xs text-muted-foreground">{formatRelativeTime(event.time)}</p>
                      </div>
                    </div>
                  ))}
                  {!job && (
                    <div className="space-y-4">
                      {[1, 2].map((i) => (
                        <div key={i} className="flex gap-4">
                          <div className="h-3.5 w-3.5 rounded-full animate-pulse bg-secondary" />
                          <div className="space-y-1.5">
                            <div className="h-3 w-28 animate-pulse rounded bg-secondary" />
                            <div className="h-3 w-20 animate-pulse rounded bg-secondary" />
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}

              {tab === 'logs' && (
                <div>
                  {job?.errorMessage ? (
                    <pre className="rounded-md border border-destructive/30 bg-destructive/8 p-3 font-mono text-xs text-destructive leading-relaxed whitespace-pre-wrap break-words">
                      {job.errorMessage}
                    </pre>
                  ) : (
                    <p className="text-sm text-muted-foreground">{t('No logs available for this job.')}</p>
                  )}
                </div>
              )}

              {tab === 'metadata' && job && (
                <table className="w-full text-sm">
                  <tbody className="divide-y divide-border">
                    <tr>
                      <td className="py-2 pr-4 text-muted-foreground w-1/3">{t('Job ID')}</td>
                      <td className="py-2 font-mono text-xs truncate max-w-0">{job.id}</td>
                    </tr>
                    <tr>
                      <td className="py-2 pr-4 text-muted-foreground">{t('Agent')}</td>
                      <td className="py-2">
                        {job.agentId ? (
                          <Link
                            to={`/agents?id=${job.agentId}`}
                            onClick={close}
                            className="flex items-center gap-1 text-primary hover:underline"
                          >
                            {job.agentName ?? job.agentId.slice(0, 8)}
                            <ExternalLink className="h-3 w-3" />
                          </Link>
                        ) : (
                          <span className="text-muted-foreground">
                            {job.agentName ? `${job.agentName} (${t('deleted')})` : t('Agent (deleted)')}
                          </span>
                        )}
                      </td>
                    </tr>
                    <tr>
                      <td className="py-2 pr-4 text-muted-foreground">{t('Policy')}</td>
                      <td className="py-2">
                        {job.policyId ? (
                          <Link
                            to={`/policies?id=${job.policyId}`}
                            onClick={close}
                            className="flex items-center gap-1 text-primary hover:underline"
                          >
                            {job.policyName ?? job.policyId.slice(0, 8)}
                            <ExternalLink className="h-3 w-3" />
                          </Link>
                        ) : (
                          <span className="text-muted-foreground">
                            {job.policyName ? `${job.policyName} (${t('deleted')})` : t('Policy (deleted)')}
                          </span>
                        )}
                      </td>
                    </tr>
                    <tr>
                      <td className="py-2 pr-4 text-muted-foreground">{t('Started')}</td>
                      <td className="py-2">{formatDateTime(job.startedAt)}</td>
                    </tr>
                    {job.completedAt && (
                      <tr>
                        <td className="py-2 pr-4 text-muted-foreground">{t('Finished')}</td>
                        <td className="py-2">{formatDateTime(job.completedAt)}</td>
                      </tr>
                    )}
                    {durationSeconds !== null && (
                      <tr>
                        <td className="py-2 pr-4 text-muted-foreground">{t('Duration')}</td>
                        <td className="py-2">{formatDurationSeconds(durationSeconds)}</td>
                      </tr>
                    )}
                  </tbody>
                </table>
              )}

              {tab === 'metadata' && !job && (
                <div className="space-y-2">
                  {[1, 2, 3, 4].map((i) => (
                    <div key={i} className="flex gap-4 py-2">
                      <div className="h-3 w-16 animate-pulse rounded bg-secondary" />
                      <div className="h-3 w-32 animate-pulse rounded bg-secondary" />
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </RadixDialog.Content>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  )
}
