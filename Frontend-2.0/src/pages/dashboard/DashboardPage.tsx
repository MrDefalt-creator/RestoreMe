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

import { getAgents, getPendingAgents } from '@/shared/api/agents'
import { getArtifacts } from '@/shared/api/artifacts'
import { getJobs } from '@/shared/api/jobs'
import { getPolicies } from '@/shared/api/policies'
import { Badge } from '@/shared/ui/Badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { formatDateTime, formatFileSize } from '@/shared/lib/format'
import { queryKeys } from '@/shared/lib/query'

type AttentionItem = {
  title: string
  detail: string
  tone: 'warning' | 'destructive' | 'neutral'
}

export function DashboardPage() {
  const agentsQuery = useQuery({
    queryKey: queryKeys.agents,
    queryFn: getAgents,
  })
  const pendingAgentsQuery = useQuery({
    queryKey: queryKeys.pendingAgents,
    queryFn: getPendingAgents,
  })
  const policiesQuery = useQuery({
    queryKey: queryKeys.policies,
    queryFn: getPolicies,
  })
  const jobsQuery = useQuery({
    queryKey: queryKeys.jobs,
    queryFn: getJobs,
  })
  const artifactsQuery = useQuery({
    queryKey: queryKeys.artifacts,
    queryFn: getArtifacts,
  })

  const agents = agentsQuery.data ?? []
  const pendingAgents = pendingAgentsQuery.data ?? []
  const policies = policiesQuery.data ?? []
  const jobs = jobsQuery.data ?? []
  const artifacts = artifactsQuery.data ?? []
  const hasApiIssue = [
    agentsQuery,
    pendingAgentsQuery,
    policiesQuery,
    jobsQuery,
    artifactsQuery,
  ].some((query) => query.isError)

  const onlineAgents = agents.filter((agent) => agent.status === 'online').length
  const staleAgents = agents.filter((agent) => agent.status === 'stale').length
  const offlineAgents = agents.filter((agent) => agent.status === 'offline').length
  const activePolicies = policies.filter((policy) => policy.isEnabled).length
  const failedJobs = jobs.filter((job) => job.status === 'failed')
  const unresolvedFailedJobs = getUnresolvedFailedJobs(jobs)
  const runningJobs = jobs.filter((job) => job.status === 'running').length
  const totalArtifactSize = artifacts.reduce((sum, artifact) => sum + artifact.size, 0)

  const attentionItems: AttentionItem[] = [
    ...(hasApiIssue
      ? [{
          title: 'API connection needs attention',
          detail: 'Some live data could not be loaded. Check backend availability.',
          tone: 'destructive' as const,
        }]
      : []),
    ...(pendingAgents.length
      ? [{
          title: `${pendingAgents.length} agent request${pendingAgents.length === 1 ? '' : 's'} waiting`,
          detail: 'Review pending machines before they can run backup policies.',
          tone: 'warning' as const,
        }]
      : []),
    ...(offlineAgents || staleAgents
      ? [{
          title: `${offlineAgents + staleAgents} agent${offlineAgents + staleAgents === 1 ? '' : 's'} not fully healthy`,
          detail: `${offlineAgents} offline / ${staleAgents} stale`,
          tone: 'warning' as const,
        }]
      : []),
    ...(unresolvedFailedJobs.length
      ? [{
          title: `${unresolvedFailedJobs.length} active backup issue${unresolvedFailedJobs.length === 1 ? '' : 's'}`,
          detail: unresolvedFailedJobs[0]?.errorMessage ?? 'Open Jobs to inspect the latest unresolved failure.',
          tone: 'destructive' as const,
        }]
      : []),
  ]

  const protectionState = hasApiIssue
    ? 'Needs connection'
    : attentionItems.length
      ? 'Needs attention'
      : agents.length || activePolicies
        ? 'Protected'
        : 'Ready to set up'

  const jobStatusRows = [
    { label: 'Completed', value: jobs.filter((job) => job.status === 'completed').length, tone: 'success' as const },
    { label: 'Running', value: runningJobs, tone: 'accent' as const },
    { label: 'Failed', value: failedJobs.length, tone: 'destructive' as const },
  ]
  const agentHealthRows = [
    { label: 'Online', value: onlineAgents, tone: 'success' as const },
    { label: 'Stale', value: staleAgents, tone: 'warning' as const },
    { label: 'Offline', value: offlineAgents, tone: 'neutral' as const },
  ]
  const policyRows = [
    { label: 'Filesystem', value: policies.filter((policy) => policy.type === 'filesystem').length, tone: 'accent' as const },
    { label: 'PostgreSQL', value: policies.filter((policy) => policy.type === 'postgres').length, tone: 'success' as const },
    { label: 'MySQL', value: policies.filter((policy) => policy.type === 'mysql').length, tone: 'warning' as const },
  ]
  const backupTrend = buildSevenDayTrend(jobs)
  const agentsById = new Map(agents.map((agent) => [agent.id, agent]))
  const policiesById = new Map(policies.map((policy) => [policy.id, policy]))
  const latestJobs = [...jobs].sort((a, b) => Date.parse(b.startedAt) - Date.parse(a.startedAt)).slice(0, 5)
  const latestArtifacts = [...artifacts].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt)).slice(0, 5)

  return (
    <div className="space-y-8">
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
                    Backup protection, at a glance.
                  </h1>
                  <p className="mt-3 max-w-xl text-base leading-7 text-muted-foreground">
                    RestoreMe keeps the operational view calm: agents, policies, recent jobs and recoverable artifacts in one place.
                  </p>
                </div>
              </div>
              <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-lg bg-secondary text-primary">
                <ShieldCheck className="h-8 w-8" strokeWidth={1.8} />
              </div>
            </div>

            <div className="mt-8 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <Metric icon={Server} label="Agents online" value={`${onlineAgents}/${agents.length}`} detail={`${offlineAgents} offline`} />
              <Metric icon={ShieldCheck} label="Active policies" value={activePolicies} detail={`${policies.length} total`} />
              <Metric icon={Clock3} label="Running jobs" value={runningJobs} detail={`${jobs.length} recorded`} />
              <Metric icon={Archive} label="Artifacts" value={artifacts.length} detail={artifacts.length ? formatFileSize(totalArtifactSize) : 'None yet'} />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Needs attention</CardTitle>
          </CardHeader>
          <CardContent>
            {attentionItems.length ? (
              <div className="space-y-3">
                {attentionItems.map((item) => (
                  <div key={item.title} className="rounded-lg border border-border bg-background/70 p-4">
                    <div className="flex items-start gap-3">
                      <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-warning" />
                      <div className="min-w-0">
                        <p className="font-medium text-foreground">{item.title}</p>
                        <p className="mt-1 text-sm leading-6 text-muted-foreground">{item.detail}</p>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <EmptyState
                icon={<CheckCircle2 className="h-7 w-7 text-success" />}
                title="Everything looks calm"
                description="No visible issues require operator attention right now."
              />
            )}
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-5 xl:grid-cols-[1.25fr_0.75fr]">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>Backup activity trend</CardTitle>
              <CalendarDays className="h-5 w-5 text-muted-foreground" />
            </div>
          </CardHeader>
          <CardContent>
            <div className="grid min-h-64 gap-5 lg:grid-cols-[1fr_220px]">
              <div className="flex items-end gap-2 rounded-lg border border-border bg-background/55 p-4">
                {backupTrend.map((day) => (
                  <TrendBar key={day.label} label={day.label} value={day.value} max={backupTrend.max} />
                ))}
              </div>
              <div className="grid gap-3">
                <InsightTile icon={Activity} label="Recorded runs" value={jobs.length} detail="Across all known policies" />
                <InsightTile icon={CheckCircle2} label="Success ratio" value={formatPercent(jobStatusRows[0].value, jobs.length)} detail="Completed jobs" />
                <InsightTile icon={Database} label="Stored data" value={formatFileSize(totalArtifactSize)} detail={`${artifacts.length} artifacts`} />
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>Protection mix</CardTitle>
              <PieChart className="h-5 w-5 text-muted-foreground" />
            </div>
          </CardHeader>
          <CardContent className="space-y-5">
            <ProgressGroup title="Agent health" total={agents.length} rows={agentHealthRows} />
            <ProgressGroup title="Job outcomes" total={jobs.length} rows={jobStatusRows} />
            <ProgressGroup title="Policy types" total={policies.length} rows={policyRows} />
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-5 lg:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Latest activity</CardTitle>
          </CardHeader>
          <CardContent>
            {jobs.length ? (
              <div className="divide-y divide-border">
                {latestJobs.map((job) => {
                  const policy = policiesById.get(job.policyId)
                  const agent = agentsById.get(job.agentId)
                  const jobTitle = job.policyName || job.name || policy?.name || `Backup job ${shortId(job.id)}`
                  const agentLabel = job.agentName || agent?.name || agent?.machineName || `Agent ${shortId(job.agentId)}`

                  return (
                  <div key={job.id} className="flex items-center justify-between gap-4 py-3">
                    <div className="min-w-0">
                      <p className="truncate font-medium text-foreground">{jobTitle}</p>
                      <p className="text-sm text-muted-foreground">{agentLabel}</p>
                    </div>
                    <div className="shrink-0 text-right">
                      <Badge variant={job.status === 'completed' ? 'success' : job.status === 'failed' ? 'destructive' : 'accent'}>
                        {job.status}
                      </Badge>
                      <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(job.startedAt)}</p>
                    </div>
                  </div>
                  )
                })}
              </div>
            ) : (
              <EmptyState
                title="No jobs yet"
                description="Backup activity will appear here after policies start running."
                className="min-h-52"
              />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Recoverable backups</CardTitle>
          </CardHeader>
          <CardContent>
            {artifacts.length ? (
              <div className="divide-y divide-border">
                {latestArtifacts.map((artifact) => {
                  const job = jobs.find((item) => item.id === artifact.jobId)
                  const policy = job ? policiesById.get(job.policyId) : undefined
                  const displayName = artifact.fileName || artifact.name || policy?.name || `Artifact ${shortId(artifact.id)}`

                  return (
                    <div key={artifact.id} className="flex items-center justify-between gap-4 py-3">
                      <div className="flex min-w-0 items-center gap-3">
                        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-secondary text-primary">
                          <HardDriveDownload className="h-4 w-4" />
                        </span>
                        <div className="min-w-0">
                          <p className="truncate font-medium text-foreground">{displayName}</p>
                          <p className="text-sm text-muted-foreground">{formatDateTime(artifact.createdAt)}</p>
                        </div>
                      </div>
                      <p className="shrink-0 text-sm text-muted-foreground">{formatFileSize(artifact.size)}</p>
                    </div>
                  )
                })}
              </div>
            ) : (
              <EmptyState
                title="No artifacts yet"
                description="Completed backups will appear here as recoverable artifacts."
                className="min-h-52"
              />
            )}
          </CardContent>
        </Card>
      </section>
    </div>
  )
}

function Metric({
  icon: Icon,
  label,
  value,
  detail,
}: {
  icon: typeof Server
  label: string
  value: number | string
  detail: string
}) {
  return (
    <div className="rounded-lg border border-border bg-background/70 p-4">
      <div className="flex items-center justify-between gap-3">
        <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-secondary text-primary">
          <Icon className="h-4 w-4" />
        </span>
        <p className="text-2xl font-semibold tracking-tight text-foreground">{value}</p>
      </div>
      <p className="mt-4 text-sm font-medium text-foreground">{label}</p>
      <p className="mt-1 text-sm text-muted-foreground">{detail}</p>
    </div>
  )
}

type ProgressTone = 'success' | 'accent' | 'warning' | 'destructive' | 'neutral'

type ProgressRow = {
  label: string
  value: number
  tone: ProgressTone
}

function buildSevenDayTrend(jobs: { startedAt: string }[]) {
  const days = Array.from({ length: 7 }, (_, index) => {
    const date = new Date()
    date.setHours(0, 0, 0, 0)
    date.setDate(date.getDate() - (6 - index))
    return {
      key: toLocalDateKey(date),
      label: date.toLocaleDateString(undefined, { weekday: 'short' }),
      value: 0,
    }
  })

  const byKey = new Map(days.map((day) => [day.key, day]))
  jobs.forEach((job) => {
    const date = new Date(job.startedAt)
    if (Number.isNaN(date.getTime())) {
      return
    }
    const key = toLocalDateKey(date)
    const day = byKey.get(key)
    if (day) {
      day.value += 1
    }
  })

  return Object.assign(days, {
    max: Math.max(1, ...days.map((day) => day.value)),
  })
}

function getUnresolvedFailedJobs<T extends { policyId: string; status: string; startedAt: string }>(jobs: T[]) {
  const latestByPolicy = new Map<string, T>()

  jobs.forEach((job) => {
    const current = latestByPolicy.get(job.policyId)
    if (!current || Date.parse(job.startedAt) > Date.parse(current.startedAt)) {
      latestByPolicy.set(job.policyId, job)
    }
  })

  return [...latestByPolicy.values()].filter((job) => job.status === 'failed')
}

function toLocalDateKey(date: Date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')

  return `${year}-${month}-${day}`
}

function TrendBar({
  label,
  value,
  max,
}: {
  label: string
  value: number
  max: number
}) {
  const height = Math.min(100, Math.max(value > 0 ? 10 : 0, Math.round((value / max) * 100)))

  return (
    <div className="flex min-w-0 flex-1 flex-col items-center gap-3">
      <div className="flex h-40 w-full items-end justify-center overflow-hidden rounded-lg bg-secondary/55 px-2 pb-2">
        <div
          className="w-full rounded-md bg-primary/85 shadow-[0_10px_26px_hsl(var(--primary)/0.16)] transition-all"
          style={{ height: `${height}%` }}
          title={`${value} jobs`}
        />
      </div>
      <div className="text-center">
        <p className="text-sm font-semibold text-foreground">{value}</p>
        <p className="text-xs text-muted-foreground">{label}</p>
      </div>
    </div>
  )
}

function InsightTile({
  icon: Icon,
  label,
  value,
  detail,
}: {
  icon: typeof Server
  label: string
  value: number | string
  detail: string
}) {
  return (
    <div className="rounded-lg border border-border bg-background/55 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-sm text-muted-foreground">{label}</p>
          <p className="mt-1 text-2xl font-semibold tracking-tight text-foreground">{value}</p>
        </div>
        <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-secondary text-primary">
          <Icon className="h-4 w-4" />
        </span>
      </div>
      <p className="mt-3 text-xs text-muted-foreground">{detail}</p>
    </div>
  )
}

function ProgressGroup({
  title,
  total,
  rows,
}: {
  title: string
  total: number
  rows: ProgressRow[]
}) {
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm font-medium text-foreground">{title}</p>
        <p className="text-xs text-muted-foreground">{total} total</p>
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

function shortId(id: string | undefined) {
  if (!id) {
    return 'unknown'
  }

  return id.slice(0, 8)
}
