import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, CheckCircle2, Clock3, Server } from 'lucide-react'

import { getAgents } from '@/entities/agent/api'
import { getPolicies } from '@/entities/policy/api'
import { getDashboardSummary } from '@/pages/dashboard/api'
import { formatDateTime } from '@/shared/lib/format'
import { useI18n } from '@/shared/i18n'
import { useLiveQueryOptions } from '@/shared/lib/useLiveQueryOptions'
import { queryKeys } from '@/shared/lib/query'
import { Badge } from '@/shared/ui/Badge'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { SectionHeading } from '@/shared/ui/SectionHeading'

export function DashboardPage() {
  const { t } = useI18n()
  const liveQueryOptions = useLiveQueryOptions()
  const summaryQuery = useQuery({
    queryKey: queryKeys.dashboard,
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

  const agents = agentsQuery.data ?? []
  const policies = policiesQuery.data ?? []
  const summary = summaryQuery.data

  const agentNameMap = new Map(agents.map((agent) => [agent.id, agent.name]))
  const policyNameMap = new Map(policies.map((policy) => [policy.id, policy.name]))

  return (
    <div className="space-y-8">
      <SectionHeading
        eyebrow={t('Operations overview')}
        title={t('Backup operations')}
        description={t('Use the dashboard to monitor machine health, pending approvals and the latest backup activity before diving into entity-specific workflows.')}
      />

      {summary ? (
        <>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <MetricCard icon={Server} title={t('Registered agents')} value={summary.totalAgents} detail={t('{online} online / {stale} stale / {offline} offline', { online: summary.onlineAgents, stale: summary.staleAgents, offline: summary.offlineAgents })} />
            <MetricCard icon={Clock3} title={t('Pending approvals')} value={summary.pendingAgents} detail={t('Registration requests waiting for review')} />
            <MetricCard icon={CheckCircle2} title={t('Active policies')} value={summary.activePolicies} detail={t('Enabled schedules across all machines')} />
            <MetricCard icon={AlertTriangle} title={t('Recent failures')} value={summary.recentErrors.length} detail={t('Latest failed jobs surfaced for quick triage')} />
          </div>

          <div className="grid gap-5 xl:grid-cols-[1.5fr_1fr]">
            <Card className="space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <h2 className="text-xl font-semibold text-ink-950">{t('Recent backup jobs')}</h2>
                  <p className="text-sm text-ink-800">{t('Latest execution attempts across agents and policies.')}</p>
                </div>
                <Badge tone="accent">{t('Recent activity')}</Badge>
              </div>
              <div className="space-y-3">
                {summary.recentJobs.map((job) => (
                  <div key={job.id} className="rounded-2xl border border-surface-200 bg-white p-4">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="font-medium text-ink-950">{policyNameMap.get(job.policyId) ?? t('Unknown policy')}</p>
                        <p className="text-sm text-ink-800">{agentNameMap.get(job.agentId) ?? t('Unknown agent')}</p>
                      </div>
                      <Badge tone={job.status === 'completed' ? 'success' : job.status === 'failed' ? 'danger' : 'accent'}>
                        {t(job.status)}
                      </Badge>
                    </div>
                    <p className="mt-3 text-sm text-ink-800">{t('Started')} {formatDateTime(job.startedAt)}</p>
                  </div>
                ))}
              </div>
            </Card>

            <Card className="space-y-4">
              <div>
                <h2 className="text-xl font-semibold text-ink-950">{t('Recent failures')}</h2>
                <p className="text-sm text-ink-800">{t('Latest backup errors remain visible here for quick review.')}</p>
              </div>
              {summary.recentErrors.length ? (
                <div className="space-y-3">
                  {summary.recentErrors.map((job) => (
                    <div key={job.id} className="rounded-2xl border border-orange-100 bg-orange-50/80 p-4">
                      <p className="font-medium text-ink-950">{policyNameMap.get(job.policyId) ?? t('Unknown policy')}</p>
                      <p className="mt-2 text-sm text-danger-500">{job.errorMessage}</p>
                      <p className="mt-3 text-xs uppercase tracking-[0.2em] text-ink-800/70">
                        {formatDateTime(job.completedAt)}
                      </p>
                    </div>
                  ))}
                </div>
              ) : (
                <EmptyState title={t('No recent failures')} description={t('The latest jobs completed without backup errors.')} />
              )}
            </Card>
          </div>
        </>
      ) : (
        <EmptyState title={t('Loading overview')} description={t('Summary metrics will appear as soon as the first data source responds.')} />
      )}
    </div>
  )
}

type MetricCardProps = {
  icon: typeof Server
  title: string
  value: number
  detail: string
}

function MetricCard({ icon: Icon, title, value, detail }: MetricCardProps) {
  return (
    <Card className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="rounded-2xl bg-sky-50 p-3 text-accent-600">
          <Icon className="h-5 w-5" />
        </div>
        <Badge tone="neutral">Overview</Badge>
      </div>
      <div>
        <p className="text-sm text-ink-800">{title}</p>
        <p className="mt-2 text-4xl font-semibold tracking-tight text-ink-950">{value}</p>
      </div>
      <p className="text-sm text-ink-800">{detail}</p>
    </Card>
  )
}
