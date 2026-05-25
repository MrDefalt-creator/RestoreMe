import {
  Activity,
  AlertTriangle,
  Archive,
  CalendarDays,
  CheckCircle2,
  Clock3,
  Database,
  HardDriveDownload,
  PieChart,
  Server,
  ShieldCheck,
} from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router-dom'

import {
  getDashboardMetrics,
  getDashboardSummary,
  type DashboardPeriod,
  type DashboardSummary,
} from '@/shared/api/dashboard'
import { useAuthStore } from '@/app/store/auth-store'
import { useUiStore } from '@/app/store/ui-store'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/Card'
import { StatTile } from '@/shared/ui/StatTile'
import { SegmentedControl } from '@/shared/ui/SegmentedControl'
import { TrendBarChart } from '@/shared/ui/charts/TrendBarChart'
import { StorageGrowthChart } from '@/shared/ui/charts/StorageGrowthChart'
import { TopFailingPoliciesChart } from '@/shared/ui/charts/TopFailingPoliciesChart'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Skeleton, SkeletonCard } from '@/shared/ui/Skeleton'
import { FirstRunCard } from '@/widgets/first-run'
import { formatDateTime, formatFileSize } from '@/shared/lib/format'
import { queryKeys } from '@/shared/lib/query'
import {
  useLiveQueryOptions,
  useLiveQueryOptionsWithFloor,
} from '@/shared/lib/useLiveQueryOptions'
import { useI18n, type Language } from '@/shared/i18n'

type AttentionItem = {
  title: string
  detail: string
  tone: 'warning' | 'destructive' | 'neutral'
  href?: string
  actionLabel?: string
}

const PERIOD_OPTIONS: DashboardPeriod[] = ['7d', '30d', '90d']
const DASHBOARD_SUMMARY_MIN_INTERVAL_MS = 30_000

const EMPTY_SUMMARY: DashboardSummary = {
  agents: { online: 0, stale: 0, offline: 0, total: 0 },
  pendingAgentsCount: 0,
  policies: { active: 0, total: 0, byType: { filesystem: 0, postgres: 0, mysql: 0 } },
  jobs: {
    completed: 0,
    running: 0,
    failed: 0,
    total: 0,
    last7Days: [],
    unresolvedFailures: [],
    recent: [],
  },
  artifacts: { total: 0, totalSize: 0, recent: [] },
}

export function DashboardPage() {
  const { language, t } = useI18n()
  const summaryOptions = useLiveQueryOptionsWithFloor(DASHBOARD_SUMMARY_MIN_INTERVAL_MS)
  const metricsOptions = useLiveQueryOptions()
  const [period, setPeriod] = useState<DashboardPeriod>('30d')

  const summaryQuery = useQuery({
    queryKey: [...queryKeys.dashboard, 'summary'] as const,
    queryFn: getDashboardSummary,
    ...summaryOptions,
  })
  const metricsQuery = useQuery({
    queryKey: [...queryKeys.dashboard, 'metrics', period] as const,
    queryFn: () => getDashboardMetrics(period),
    ...metricsOptions,
  })

  const user = useAuthStore((state) => state.user)
  const summary = summaryQuery.data ?? EMPTY_SUMMARY
  const isFirstLoad = summaryQuery.isLoading && !summaryQuery.data
  const firstRunDismissed = useUiStore((state) => state.firstRunDismissed)
  const showFirstRun = !isFirstLoad && summary.agents.total === 0 && !firstRunDismissed
  const { agents, pendingAgentsCount, policies, jobs, artifacts } = summary
  const hasApiIssue = summaryQuery.isError

  const attentionItems: AttentionItem[] = [
    ...(hasApiIssue
      ? [{
          title: t('API connection needs attention'),
          detail: t('Some live data could not be loaded. Check backend availability.'),
          tone: 'destructive' as const,
        }]
      : []),
    ...(pendingAgentsCount
      ? [{
          title: t('{count} agent request{plural} waiting', { count: pendingAgentsCount, plural: pendingAgentsCount === 1 ? '' : 's' }),
          detail: t('Review pending machines before they can run backup policies.'),
          tone: 'warning' as const,
          href: '/pending-agents',
          actionLabel: t('Review'),
        }]
      : []),
    ...(agents.offline || agents.stale
      ? [{
          title: t('{count} agent{plural} not fully healthy', { count: agents.offline + agents.stale, plural: agents.offline + agents.stale === 1 ? '' : 's' }),
          detail: t('{offline} offline / {stale} stale', { offline: agents.offline, stale: agents.stale }),
          tone: 'warning' as const,
          href: '/agents',
          actionLabel: t('Open'),
        }]
      : []),
    ...(jobs.unresolvedFailures.length
      ? [{
          title: t('{count} active backup issue{plural}', { count: jobs.unresolvedFailures.length, plural: jobs.unresolvedFailures.length === 1 ? '' : 's' }),
          detail: jobs.unresolvedFailures[0]?.errorMessage ?? t('Open Jobs to inspect the latest unresolved failure.'),
          tone: 'destructive' as const,
          href: '/jobs?status=failed',
          actionLabel: t('Open'),
        }]
      : []),
  ]

  const protectionState = hasApiIssue
    ? t('Needs connection')
    : attentionItems.length
      ? t('Needs attention')
      : agents.total || policies.active
        ? t('Protected')
        : t('Ready to set up')

  const jobStatusRows = [
    { label: t('Completed'), value: jobs.completed, tone: 'success' as const },
    { label: t('Running'), value: jobs.running, tone: 'accent' as const },
    { label: t('Failed'), value: jobs.failed, tone: 'destructive' as const },
  ]
  const agentHealthRows = [
    { label: t('Online'), value: agents.online, tone: 'success' as const },
    { label: t('Stale'), value: agents.stale, tone: 'warning' as const },
    { label: t('Offline'), value: agents.offline, tone: 'neutral' as const },
  ]
  const policyRows = [
    { label: t('Filesystem'), value: policies.byType.filesystem, tone: 'accent' as const },
    { label: 'PostgreSQL', value: policies.byType.postgres, tone: 'success' as const },
    { label: 'MySQL', value: policies.byType.mysql, tone: 'warning' as const },
  ]
  const backupTrend = buildSevenDayTrend(jobs.last7Days, language)

  const metrics = metricsQuery.data
  const storageGrowthSeries = metrics?.storageGrowthTimeseries ?? []
  const topFailingSeries = metrics?.topFailingPolicies ?? []
  const storageGrowthDelta = storageGrowthSeries.length
    ? storageGrowthSeries[storageGrowthSeries.length - 1].cumulativeBytes - storageGrowthSeries[0].cumulativeBytes
    : 0

  return (
    <div className="space-y-8">
      {showFirstRun ? (
        <FirstRunCard summary={summary} mustChangePassword={Boolean(user?.mustChangePassword)} />
      ) : null}
      {isFirstLoad ? (
        <HeroSkeleton />
      ) : showFirstRun ? null : (
        <section className="grid gap-5 lg:grid-cols-[1.35fr_0.85fr]">
          <Card className="overflow-hidden">
            <CardContent className="p-6">
              <div className="flex flex-col gap-8 md:flex-row md:items-start md:justify-between">
                <div className="max-w-2xl space-y-4">
                  <Badge variant={attentionItems.length || hasApiIssue ? 'warning' : 'success'}>
                    {protectionState}
                  </Badge>
                  <div>
                    <h1 className="text-4xl font-semibold tracking-tight text-foreground">
                      {t('Backup protection, at a glance.')}
                    </h1>
                    <p className="mt-3 max-w-xl text-base leading-7 text-muted-foreground">
                      {t('RestoreMe keeps the operational view calm: agents, policies, recent jobs and recoverable artifacts in one place.')}
                    </p>
                  </div>
                </div>
                <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-lg bg-secondary text-primary">
                  <ShieldCheck className="h-8 w-8" strokeWidth={1.8} />
                </div>
              </div>

              <div className="mt-8 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                <StatTile icon={<Server className="h-4 w-4" />} label={t('Agents online')} value={`${agents.online}/${agents.total}`} detail={t('{count} offline', { count: agents.offline })} tone="success" />
                <StatTile icon={<ShieldCheck className="h-4 w-4" />} label={t('Active policies')} value={policies.active} detail={t('{count} total', { count: policies.total })} tone="primary" />
                <StatTile icon={<Clock3 className="h-4 w-4" />} label={t('Running jobs')} value={jobs.running} detail={t('{count} recorded', { count: jobs.total })} tone="accent" />
                <StatTile icon={<Archive className="h-4 w-4" />} label={t('Artifacts')} value={artifacts.total} detail={artifacts.total ? formatFileSize(artifacts.totalSize) : t('None yet')} />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <div className="flex items-center gap-3">
                <CardTitle className="flex-1">{t('Needs attention')}</CardTitle>
                <Badge variant={attentionItems.length ? 'warning' : 'success'}>{attentionItems.length}</Badge>
                <Link to="/jobs?status=failed" className="text-sm text-muted-foreground hover:text-foreground">
                  {t('View all')} →
                </Link>
              </div>
            </CardHeader>
            <CardContent>
              {attentionItems.length ? (
                <div className="divide-y divide-border">
                  {attentionItems.map((item) => (
                    <div key={item.title} className="relative flex items-center gap-3 py-3 pl-4">
                      <div
                        className="absolute bottom-0 left-0 top-0 w-1 rounded-r"
                        style={{ background: `hsl(var(--${item.tone === 'neutral' ? 'muted-foreground' : item.tone}))` }}
                      />
                      <div className="min-w-0 flex-1">
                        <p className="font-medium text-foreground">{item.title}</p>
                        <p className="mt-0.5 text-sm text-muted-foreground">{item.detail}</p>
                      </div>
                      {item.href && item.actionLabel ? (
                        <Button variant="secondary" size="sm" asChild>
                          <Link to={item.href}>{item.actionLabel}</Link>
                        </Button>
                      ) : null}
                    </div>
                  ))}
                </div>
              ) : (
                <EmptyState
                  icon={<CheckCircle2 className="h-7 w-7 text-success" />}
                  title={t('Everything looks calm')}
                  description={t('No visible issues require operator attention right now.')}
                />
              )}
            </CardContent>
          </Card>
        </section>
      )}

      <section className="grid gap-5 xl:grid-cols-[1.25fr_0.75fr]">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>{t('Backup activity trend')}</CardTitle>
              <CalendarDays className="h-5 w-5 text-muted-foreground" />
            </div>
          </CardHeader>
          <CardContent>
            <div className="grid min-h-64 gap-5 lg:grid-cols-[1fr_220px]">
              <div className="rounded-lg border border-border bg-background/55 p-3">
                <TrendBarChart data={backupTrend} seriesLabel={t('Recorded runs')} />
              </div>
              <div className="grid gap-3">
                <StatTile icon={<Activity className="h-4 w-4" />} label={t('Recorded runs')} value={jobs.total} detail={t('Across all known policies')} />
                <StatTile icon={<CheckCircle2 className="h-4 w-4" />} label={t('Success ratio')} value={formatPercent(jobs.completed, jobs.total)} detail={t('Completed jobs')} tone="success" />
                <StatTile icon={<Database className="h-4 w-4" />} label={t('Stored data')} value={formatFileSize(artifacts.totalSize)} detail={t('{count} artifacts', { count: artifacts.total })} tone="accent" />
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>{t('Protection mix')}</CardTitle>
              <PieChart className="h-5 w-5 text-muted-foreground" />
            </div>
          </CardHeader>
          <CardContent className="space-y-5">
            <ProgressGroup title={t('Agent health')} total={agents.total} rows={agentHealthRows} totalLabel={t('{count} total', { count: agents.total })} />
            <ProgressGroup title={t('Job outcomes')} total={jobs.total} rows={jobStatusRows} totalLabel={t('{count} total', { count: jobs.total })} />
            <ProgressGroup title={t('Policy types')} total={policies.total} rows={policyRows} totalLabel={t('{count} total', { count: policies.total })} />
          </CardContent>
        </Card>
      </section>

      <section className="space-y-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-xl font-semibold tracking-tight text-foreground">
              {t('Storage and reliability')}
            </h2>
            <p className="text-sm text-muted-foreground">
              {t('Aggregated trends from the selected lookback window.')}
            </p>
          </div>
          <SegmentedControl
            value={period}
            onChange={setPeriod}
            options={PERIOD_OPTIONS.map((p) => ({
              value: p,
              label: ({ '7d': t('7 days'), '30d': t('30 days'), '90d': t('90 days') } as Record<string, string>)[p],
            }))}
            aria-label={t('Period')}
          />
        </div>

        <div className="grid gap-5 xl:grid-cols-[1.35fr_1fr]">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between gap-3">
                <CardTitle>{t('Storage growth')}</CardTitle>
                <Database className="h-5 w-5 text-muted-foreground" />
              </div>
              <p className="mt-1 text-sm text-muted-foreground">
                {storageGrowthDelta > 0
                  ? t('+{size} added in this window', { size: formatFileSize(storageGrowthDelta) })
                  : t('No new bytes recorded in this window.')}
              </p>
            </CardHeader>
            <CardContent>
              {metricsQuery.isError ? (
                <EmptyState
                  title={t('Could not load metrics')}
                  description={t('Backend did not return the dashboard aggregation. Check connectivity.')}
                  className="min-h-52"
                />
              ) : storageGrowthSeries.length ? (
                <div className="rounded-lg border border-border bg-background/55 p-3">
                  <StorageGrowthChart data={storageGrowthSeries} seriesLabel={t('Stored data')} />
                </div>
              ) : (
                <EmptyState
                  title={t('No storage data yet')}
                  description={t('Storage growth will appear once artifacts start landing.')}
                  className="min-h-52"
                />
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <div className="flex items-center justify-between gap-3">
                <CardTitle>{t('Top failing policies')}</CardTitle>
                <AlertTriangle className="h-5 w-5 text-muted-foreground" />
              </div>
              <p className="mt-1 text-sm text-muted-foreground">
                {t('Policies with the most failed runs in this window.')}
              </p>
            </CardHeader>
            <CardContent>
              {metricsQuery.isError ? (
                <EmptyState
                  title={t('Could not load metrics')}
                  description={t('Backend did not return the dashboard aggregation. Check connectivity.')}
                  className="min-h-52"
                />
              ) : topFailingSeries.length ? (
                <div className="rounded-lg border border-border bg-background/55 p-3">
                  <TopFailingPoliciesChart data={topFailingSeries} seriesLabel={t('Failures')} />
                </div>
              ) : (
                <EmptyState
                  icon={<CheckCircle2 className="h-7 w-7 text-success" />}
                  title={t('No failures recorded')}
                  description={t('Every policy completed cleanly in this window.')}
                  className="min-h-52"
                />
              )}
            </CardContent>
          </Card>
        </div>
      </section>

      <section className="grid gap-5 lg:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>{t('Latest activity')}</CardTitle>
          </CardHeader>
          <CardContent>
            {jobs.recent.length ? (
              <div className="divide-y divide-border">
                {jobs.recent.map((job) => (
                  <div key={job.id} className="flex items-center justify-between gap-4 py-3">
                    <div className="min-w-0">
                      <p className="truncate font-medium text-foreground">{job.title}</p>
                      <p className="text-sm text-muted-foreground">{job.agentName}</p>
                    </div>
                    <div className="shrink-0 text-right">
                      <Badge variant={job.status === 'completed' ? 'success' : job.status === 'failed' ? 'destructive' : 'accent'}>
                        {t(job.status)}
                      </Badge>
                      <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(job.startedAt)}</p>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <EmptyState
                title={t('No jobs yet')}
                description={t('Backup activity will appear here after policies start running.')}
                className="min-h-52"
              />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t('Recoverable backups')}</CardTitle>
          </CardHeader>
          <CardContent>
            {artifacts.recent.length ? (
              <div className="divide-y divide-border">
                {artifacts.recent.map((artifact) => (
                  <div key={artifact.id} className="flex items-center justify-between gap-4 py-3">
                    <div className="flex min-w-0 items-center gap-3">
                      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-secondary text-primary">
                        <HardDriveDownload className="h-4 w-4" />
                      </span>
                      <div className="min-w-0">
                        <p className="truncate font-medium text-foreground">{artifact.displayName}</p>
                        <p className="text-sm text-muted-foreground">{formatDateTime(artifact.createdAt)}</p>
                      </div>
                    </div>
                    <p className="shrink-0 text-sm text-muted-foreground">{formatFileSize(artifact.size)}</p>
                  </div>
                ))}
              </div>
            ) : (
              <EmptyState
                title={t('No artifacts yet')}
                description={t('Completed backups will appear here as recoverable artifacts.')}
                className="min-h-52"
              />
            )}
          </CardContent>
        </Card>
      </section>
    </div>
  )
}


type ProgressTone = 'success' | 'accent' | 'warning' | 'destructive' | 'neutral'

type ProgressRow = {
  label: string
  value: number
  tone: ProgressTone
}

function buildSevenDayTrend(last7Days: { date: string; count: number }[], language: Language) {
  return last7Days.map((bucket) => {
    const date = new Date(`${bucket.date}T00:00:00Z`)
    return {
      key: bucket.date,
      label: date.toLocaleDateString(language === 'ru' ? 'ru-RU' : undefined, { weekday: 'short' }),
      value: bucket.count,
    }
  })
}


function ProgressGroup({
  title,
  total,
  rows,
  totalLabel,
}: {
  title: string
  total: number
  rows: ProgressRow[]
  totalLabel: string
}) {
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm font-medium text-foreground">{title}</p>
        <p className="text-xs text-muted-foreground">{totalLabel}</p>
      </div>
      <div className="space-y-2">
        {rows.map((row) => (
          <div key={row.label} className="space-y-1.5">
            <div className="flex items-center justify-between gap-3 text-sm">
              <span className="text-muted-foreground">{row.label}</span>
              <span className="font-medium text-foreground">{row.value}</span>
            </div>
            <div className="h-2 overflow-hidden rounded-full bg-secondary">
              <div
                className={`h-full rounded-full ${toneClass(row.tone)}`}
                style={{ width: `${total ? Math.max(4, (row.value / total) * 100) : 0}%` }}
              />
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

function toneClass(tone: ProgressTone) {
  switch (tone) {
    case 'success':
      return 'bg-success'
    case 'accent':
      return 'bg-primary'
    case 'warning':
      return 'bg-warning'
    case 'destructive':
      return 'bg-destructive'
    default:
      return 'bg-muted-foreground'
  }
}

function formatPercent(value: number, total: number) {
  if (!total) {
    return '0%'
  }

  return `${Math.round((value / total) * 100)}%`
}

function HeroSkeleton() {
  return (
    <section className="grid gap-5 lg:grid-cols-[1.35fr_0.85fr]">
      <Card>
        <CardContent className="p-6">
          <div className="space-y-3">
            <Skeleton className="h-5 w-20 rounded-full" />
            <Skeleton className="h-9 w-2/3" />
            <Skeleton className="h-4 w-1/2" />
          </div>
          <div className="mt-8 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <SkeletonCard />
            <SkeletonCard />
            <SkeletonCard />
            <SkeletonCard />
          </div>
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <Skeleton className="h-5 w-32" />
        </CardHeader>
        <CardContent className="space-y-3">
          <Skeleton className="h-14 rounded-lg" />
          <Skeleton className="h-14 rounded-lg" />
        </CardContent>
      </Card>
    </section>
  )
}

