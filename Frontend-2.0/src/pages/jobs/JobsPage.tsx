import { useMemo, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Clock3,
  RefreshCw,
  Search,
  TimerReset,
  XCircle,
} from 'lucide-react'

import { getJobsPage, type Job, type JobSortKey } from '@/shared/api/jobs'
import { getAgents } from '@/shared/api/agents'
import { getDashboardSummary } from '@/shared/api/dashboard'
import { getPolicies } from '@/shared/api/policies'
import type { SortDir } from '@/shared/api/pagination'
import { queryKeys } from '@/shared/lib/query'
import { formatDateTime, formatDurationSeconds, formatRelativeTime } from '@/shared/lib/format'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import { Select } from '@/shared/ui/Select'
import { StatTile } from '@/shared/ui/StatTile'
import { SegmentedControl } from '@/shared/ui/SegmentedControl'
import { SkeletonList } from '@/shared/ui/Skeleton'
import { useI18n } from '@/shared/i18n'
import { useLiveQueryOptions } from '@/shared/lib/useLiveQueryOptions'
import { useUrlFilterState } from '@/shared/lib/useUrlFilterState'
import { JobDrawer } from '@/widgets/job-drawer'

type StatusFilter = 'all' | Job['status']

const STATUS_FILTERS: readonly StatusFilter[] = ['all', 'pending', 'running', 'failed', 'completed']
type AgentLookup = Awaited<ReturnType<typeof getAgents>>[number]
type PolicyLookup = Awaited<ReturnType<typeof getPolicies>>[number]

type JobSortOption = 'newest' | 'oldest' | 'status'
const SORT_OPTIONS: readonly JobSortOption[] = ['newest', 'oldest', 'status']
const SORT_MAP: Record<JobSortOption, { sortBy: JobSortKey; sortDir: SortDir }> = {
  newest: { sortBy: 'startedAt', sortDir: 'desc' },
  oldest: { sortBy: 'startedAt', sortDir: 'asc' },
  status: { sortBy: 'status', sortDir: 'asc' },
}

const PAGE_SIZE = 25

const statusVariant: Record<Job['status'], 'success' | 'destructive' | 'accent' | 'neutral'> = {
  pending: 'neutral',
  completed: 'success',
  failed: 'destructive',
  running: 'accent',
}

const EMPTY_JOBS: Job[] = []
const EMPTY_AGENTS: AgentLookup[] = []
const EMPTY_POLICIES: PolicyLookup[] = []

export function JobsPage() {
  const { t } = useI18n()
  const liveQueryOptions = useLiveQueryOptions()
  const [query, setQuery] = useState('')
  const [statusFilter, setStatusFilterRaw] = useUrlFilterState<StatusFilter>('status', 'all', STATUS_FILTERS)
  const [sortOption, setSortOptionRaw] = useUrlFilterState<JobSortOption>('sort', 'newest', SORT_OPTIONS)
  const [pageParam, setPageParam] = useUrlFilterState<string>('page', '1')
  const [, setSearchParams] = useSearchParams()

  const page = Math.max(1, Number.parseInt(pageParam, 10) || 1)

  // Changing the filter or sort restarts from page 1 — page numbers from
  // the old ordering would point at arbitrary rows.
  function setStatusFilter(next: StatusFilter) {
    setStatusFilterRaw(next)
    setPageParam('1')
  }
  function setSortOption(next: JobSortOption) {
    setSortOptionRaw(next)
    setPageParam('1')
  }

  const { sortBy, sortDir } = SORT_MAP[sortOption]
  const jobsQuery = useQuery({
    queryKey: queryKeys.jobsPage(page, sortOption, statusFilter === 'all' ? undefined : statusFilter),
    queryFn: () =>
      getJobsPage({
        page,
        pageSize: PAGE_SIZE,
        sortBy,
        sortDir,
        status: statusFilter === 'all' ? undefined : statusFilter,
      }),
    placeholderData: keepPreviousData,
    ...liveQueryOptions,
  })
  // Fleet-wide status counts for the tiles + segmented control; the paged
  // list itself only carries the rows of the current page.
  const summaryQuery = useQuery({
    queryKey: [...queryKeys.dashboard, 'summary'] as const,
    queryFn: getDashboardSummary,
    ...liveQueryOptions,
  })
  const agentsQuery = useQuery({
    queryKey: queryKeys.agents,
    queryFn: getAgents,
    ...liveQueryOptions,
  })
  const policiesQuery = useQuery({
    queryKey: queryKeys.policies,
    queryFn: getPolicies,
    ...liveQueryOptions,
  })

  const jobs = jobsQuery.data?.items ?? EMPTY_JOBS
  const total = jobsQuery.data?.total ?? 0
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))
  const agents = agentsQuery.data ?? EMPTY_AGENTS
  const policies = policiesQuery.data ?? EMPTY_POLICIES
  const agentsById = useMemo(() => new Map(agents.map((agent) => [agent.id, agent])), [agents])
  const policiesById = useMemo(() => new Map(policies.map((policy) => [policy.id, policy])), [policies])
  const normalizedQuery = query.trim().toLowerCase()

  // Status filtering happens server-side; the search box narrows the
  // rows of the current page (same pattern as the audit log categories).
  const visibleJobs = useMemo(() => {
    if (!normalizedQuery) return jobs
    return jobs.filter((job) => {
      const agent = job.agentId ? agentsById.get(job.agentId) : undefined
      const policy = job.policyId ? policiesById.get(job.policyId) : undefined
      const searchable = [
        job.name,
        job.policyName,
        job.agentName,
        agent?.name,
        agent?.machineName,
        policy?.name,
        policy?.type,
        job.status,
        job.errorMessage,
        job.id,
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase()

      return searchable.includes(normalizedQuery)
    })
  }, [agentsById, jobs, normalizedQuery, policiesById])

  const jobStats = summaryQuery.data?.jobs
  const stats = useMemo(() => {
    const completed = jobStats?.completed ?? 0
    const running = jobStats?.running ?? 0
    const failed = jobStats?.failed ?? 0
    const statsTotal = jobStats?.total ?? 0
    return {
      total: statsTotal,
      completed,
      running,
      failed,
      pending: Math.max(0, statsTotal - completed - running - failed),
    }
  }, [jobStats])

  return (
    <div className="space-y-7">
      <SectionHeading
        eyebrow={t('Execution')}
        title={t('Jobs')}
        description={t('Track backup runs as a timeline: what started, what finished, what failed, and where attention is needed.')}
        action={
          <Button variant="secondary" onClick={() => jobsQuery.refetch()} disabled={jobsQuery.isFetching}>
            <RefreshCw className={jobsQuery.isFetching ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />
            {t('Refresh')}
          </Button>
        }
      />

      <div className="grid gap-3 md:grid-cols-4">
        <StatTile icon={<Activity className="h-4 w-4" />} label={t('Total runs')} value={stats.total} />
        <StatTile icon={<CheckCircle2 className="h-4 w-4" />} label={t('Completed')} value={stats.completed} tone="success" />
        <StatTile icon={<XCircle className="h-4 w-4" />} label={t('Failed')} value={stats.failed} tone="destructive" />
        <StatTile icon={<TimerReset className="h-4 w-4" />} label={t('Running')} value={stats.running} tone="accent" />
      </div>

      <Card>
        <CardContent className="flex flex-col gap-3 lg:flex-row lg:items-center">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={t('Search this page by job, policy, agent, or error...')}
              className="pl-10"
            />
          </div>
          <Select
            value={sortOption}
            onChange={(event) => setSortOption(event.target.value as JobSortOption)}
            aria-label={t('Sort')}
            className="lg:w-auto"
          >
            <option value="newest">{t('Newest first')}</option>
            <option value="oldest">{t('Oldest first')}</option>
            <option value="status">{t('By status')}</option>
          </Select>
          <SegmentedControl
            value={statusFilter}
            onChange={setStatusFilter}
            aria-label={t('Filter by status')}
            options={[
              { value: 'all', label: t('all'), count: stats.total },
              { value: 'pending', label: t('pending'), count: stats.pending },
              { value: 'running', label: t('running'), count: stats.running, tone: 'accent' },
              { value: 'failed', label: t('failed'), count: stats.failed, tone: 'destructive' },
              { value: 'completed', label: t('completed'), count: stats.completed, tone: 'success' },
            ]}
          />
        </CardContent>
      </Card>

      {jobsQuery.isLoading ? (
        <SkeletonList count={6} columns={4} />
      ) : jobsQuery.isError ? (
        <EmptyState
          icon={<AlertTriangle className="h-8 w-8 text-warning" />}
          title={t('Jobs could not be loaded')}
          description={t('Check the API container and retry the execution timeline.')}
          action={
            <Button variant="secondary" onClick={() => jobsQuery.refetch()}>
              {t('Retry')}
            </Button>
          }
        />
      ) : visibleJobs.length ? (
        <Card className="overflow-hidden">
          <CardContent className="p-0">
            <div className="divide-y divide-border">
              {visibleJobs.map((job) => (
                <JobRow
                  key={job.id}
                  job={job}
                  agentLabel={formatAgentLabel(job, agentsById)}
                  title={formatJobTitle(job, policiesById)}
                  t={t}
                  onClick={() => setSearchParams((prev) => { const next = new URLSearchParams(prev); next.set('id', job.id); return next })}
                />
              ))}
            </div>
            <div className="flex items-center justify-between gap-3 border-t border-border px-4 py-3 text-sm text-muted-foreground">
              <span>
                {t('Page')} {page} / {totalPages} · {total} {t('records')}
                {normalizedQuery && ` · ${visibleJobs.length} ${t('shown')}`}
              </span>
              <div className="flex gap-2">
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page <= 1 || jobsQuery.isFetching}
                  onClick={() => setPageParam(String(Math.max(1, page - 1)))}
                >
                  {t('Previous page')}
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page >= totalPages || jobsQuery.isFetching}
                  onClick={() => setPageParam(String(page + 1))}
                >
                  {t('Next page')}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      ) : (
        <EmptyState
          title={total ? t('No jobs match these filters') : t('No jobs yet')}
          description={
            normalizedQuery
              ? t('No matches on this page — flip pages or clear the search.')
              : total
                ? t('Clear the search or switch the status filter.')
                : t('Execution history will appear here after policies run.')
          }
        />
      )}

      <JobDrawer />
    </div>
  )
}


function JobRow({
  job,
  title,
  agentLabel,
  t,
  onClick,
}: {
  job: Job
  title: string
  agentLabel: string
  t: (key: string) => string
  onClick?: () => void
}) {
  const hasDuration = job.completedAt && job.startedAt
  const durationSeconds = hasDuration
    ? Math.max(0, Math.round((Date.parse(job.completedAt as string) - Date.parse(job.startedAt)) / 1000))
    : null

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={onClick}
      onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') onClick?.() }}
      className="grid cursor-pointer gap-4 p-4 transition hover:bg-secondary/35 lg:grid-cols-[1.3fr_1fr_auto] lg:items-center"
    >
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <p className="truncate font-medium text-foreground">{title}</p>
          <Badge variant={statusVariant[job.status]}>{t(job.status)}</Badge>
        </div>
        <p className="mt-1 truncate text-sm text-muted-foreground">
          {agentLabel}
        </p>
      </div>

      <div className="grid grid-cols-2 gap-3 text-sm">
        <JobFact label={t('Started')} value={formatRelativeTime(job.startedAt)} />
        <JobFact
          label={t('Duration')}
          value={durationSeconds === null ? (job.status === 'running' ? t('Running now') : t('Unknown')) : formatDurationSeconds(durationSeconds)}
        />
      </div>

      <div className="flex items-center gap-2 text-sm text-muted-foreground lg:justify-end">
        <Clock3 className="h-4 w-4" />
        {formatDateTime(job.startedAt)}
      </div>

      {job.status === 'failed' && job.errorMessage ? (
        <div className="rounded-lg border border-destructive/20 bg-destructive/8 p-3 text-sm text-destructive lg:col-span-3">
          {job.errorMessage}
        </div>
      ) : null}
    </div>
  )
}

function formatJobTitle(job: Job, policiesById: Map<string, { name: string }>) {
  const livePolicyName = job.policyId ? policiesById.get(job.policyId)?.name : undefined
  return job.policyName || job.name || livePolicyName || `Backup job ${shortId(job.id)}`
}

function formatAgentLabel(job: Job, agentsById: Map<string, { name: string; machineName?: string }>) {
  const liveAgent = job.agentId ? agentsById.get(job.agentId) : undefined
  // agentName comes from the backend snapshot when the live row is gone;
  // fall back to a "(deleted)" hint if even the snapshot is missing.
  if (job.agentName) return job.agentName
  if (liveAgent) return liveAgent.name || liveAgent.machineName || `Agent ${shortId(job.agentId)}`
  if (job.agentId) return `Agent ${shortId(job.agentId)}`
  return 'Agent (deleted)'
}

function shortId(id: string | null | undefined) {
  return id ? id.slice(0, 8) : 'unknown'
}

function JobFact({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="truncate font-medium text-foreground">{value}</p>
    </div>
  )
}
