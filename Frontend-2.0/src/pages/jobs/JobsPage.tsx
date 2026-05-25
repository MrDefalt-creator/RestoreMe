import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
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

import { getJobs, type Job } from '@/shared/api/jobs'
import { getAgents } from '@/shared/api/agents'
import { getPolicies } from '@/shared/api/policies'
import { queryKeys } from '@/shared/lib/query'
import { formatDateTime, formatDurationSeconds, formatRelativeTime } from '@/shared/lib/format'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import { StatTile } from '@/shared/ui/StatTile'
import { SegmentedControl } from '@/shared/ui/SegmentedControl'
import { SkeletonList } from '@/shared/ui/Skeleton'
import { useI18n } from '@/shared/i18n'
import { useLiveQueryOptions } from '@/shared/lib/useLiveQueryOptions'
import { JobDrawer } from '@/widgets/job-drawer'

type StatusFilter = 'all' | Job['status']
type AgentLookup = Awaited<ReturnType<typeof getAgents>>[number]
type PolicyLookup = Awaited<ReturnType<typeof getPolicies>>[number]

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
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all')
  const [, setSearchParams] = useSearchParams()
  const jobsQuery = useQuery({
    queryKey: queryKeys.jobs,
    queryFn: getJobs,
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

  const jobs = jobsQuery.data ?? EMPTY_JOBS
  const agents = agentsQuery.data ?? EMPTY_AGENTS
  const policies = policiesQuery.data ?? EMPTY_POLICIES
  const agentsById = useMemo(() => new Map(agents.map((agent) => [agent.id, agent])), [agents])
  const policiesById = useMemo(() => new Map(policies.map((policy) => [policy.id, policy])), [policies])
  const normalizedQuery = query.trim().toLowerCase()

  const filteredJobs = useMemo(() => {
    return jobs.filter((job) => {
      const agent = agentsById.get(job.agentId)
      const policy = policiesById.get(job.policyId)
      const matchesStatus = statusFilter === 'all' || job.status === statusFilter
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

      return matchesStatus && (!normalizedQuery || searchable.includes(normalizedQuery))
    })
  }, [agentsById, jobs, normalizedQuery, policiesById, statusFilter])

  const stats = useMemo(
    () => ({
      total: jobs.length,
      completed: jobs.filter((job) => job.status === 'completed').length,
      failed: jobs.filter((job) => job.status === 'failed').length,
      running: jobs.filter((job) => job.status === 'running').length,
      pending: jobs.filter((job) => job.status === 'pending').length,
    }),
    [jobs],
  )

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
              placeholder={t('Search by job, policy, agent, status, or error...')}
              className="pl-10"
            />
          </div>
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
      ) : filteredJobs.length ? (
        <Card className="overflow-hidden">
          <CardContent className="p-0">
            <div className="divide-y divide-border">
              {filteredJobs.map((job) => (
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
          </CardContent>
        </Card>
      ) : (
        <EmptyState
          title={jobs.length ? t('No jobs match these filters') : t('No jobs yet')}
          description={
            jobs.length
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
  return job.policyName || job.name || policiesById.get(job.policyId)?.name || `Backup job ${shortId(job.id)}`
}

function formatAgentLabel(job: Job, agentsById: Map<string, { name: string; machineName?: string }>) {
  const agent = agentsById.get(job.agentId)
  return job.agentName || agent?.name || agent?.machineName || `Agent ${shortId(job.agentId)}`
}

function shortId(id: string | undefined) {
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
