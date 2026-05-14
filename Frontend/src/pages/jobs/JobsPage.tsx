import { startTransition, useDeferredValue, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'

import { useUiStore } from '@/app/store/ui-store'
import { getAgents } from '@/entities/agent/api'
import { getJobs } from '@/entities/job/api'
import { getPolicies } from '@/entities/policy/api'
import { formatDateTime } from '@/shared/lib/format'
import { useI18n } from '@/shared/i18n'
import { useLiveQueryOptions } from '@/shared/lib/useLiveQueryOptions'
import { queryKeys } from '@/shared/lib/query'
import { Badge } from '@/shared/ui/Badge'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import { Select } from '@/shared/ui/Select'

export function JobsPage() {
  const { t } = useI18n()
  const liveQueryOptions = useLiveQueryOptions()
  const [search, setSearch] = useState('')
  const deferredSearch = useDeferredValue(search)
  const selectedJobId = useUiStore((state) => state.selectedJobId)
  const setSelectedJobId = useUiStore((state) => state.setSelectedJobId)
  const jobFilter = useUiStore((state) => state.jobFilter)
  const setJobFilter = useUiStore((state) => state.setJobFilter)

  const jobsQuery = useQuery({
    queryKey: [...queryKeys.jobs, jobFilter],
    queryFn: () => getJobs(jobFilter),
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

  const jobs = jobsQuery.data ?? []
  const agentNameMap = new Map((agentsQuery.data ?? []).map((agent) => [agent.id, agent.name]))
  const policyNameMap = new Map((policiesQuery.data ?? []).map((policy) => [policy.id, policy.name]))
  const searchValue = deferredSearch.trim().toLowerCase()
  const filteredJobs = !searchValue
    ? jobs
    : jobs.filter((job) =>
        [job.id, job.errorMessage ?? '', agentNameMap.get(job.agentId) ?? '', policyNameMap.get(job.policyId) ?? '']
          .join(' ')
          .toLowerCase()
          .includes(searchValue),
      )

  const selectedJob =
    filteredJobs.find((job) => job.id === selectedJobId) ??
    jobs.find((job) => job.id === selectedJobId) ??
    filteredJobs[0] ??
    null

  return (
    <div className="space-y-8">
      <SectionHeading
        eyebrow={t('Jobs')}
        title={t('Backup execution history')}
        description={t('Inspect job status, completion time and the last known failure reason without losing the surrounding queue context.')}
      />

      <Card className="grid gap-4 lg:grid-cols-[1fr_220px]">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-800/60" />
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            className="pl-10"
            placeholder={t('Search by id, error, agent or policy')}
          />
        </div>
        <Select value={jobFilter} onChange={(event) => setJobFilter(event.target.value as typeof jobFilter)}>
          <option value="all">{t('All jobs')}</option>
          <option value="running">{t('Running')}</option>
          <option value="completed">{t('Completed')}</option>
          <option value="failed">{t('Failed')}</option>
        </Select>
      </Card>

      {filteredJobs.length ? (
        <div className="grid gap-5 xl:grid-cols-[1.15fr_0.85fr]">
          <Card className="overflow-hidden p-0">
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="bg-surface-100 text-ink-800">
                  <tr>
                    {['Job', 'Agent', 'Policy', 'Status', 'Started', 'Completed', 'Error'].map((label) => (
                      <th key={label} className="px-4 py-3 font-medium">
                        {t(label)}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {filteredJobs.map((job) => (
                    <tr
                      key={job.id}
                      className="cursor-pointer border-t border-surface-200 transition hover:bg-sky-50/40"
                      onClick={() =>
                        startTransition(() => {
                          setSelectedJobId(job.id)
                        })
                      }
                    >
                      <td className="px-4 py-3 font-mono text-xs text-ink-900">{job.id.slice(0, 8)}</td>
                      <td className="px-4 py-3 text-ink-800">{agentNameMap.get(job.agentId) ?? t('Unknown agent')}</td>
                      <td className="px-4 py-3 text-ink-800">{policyNameMap.get(job.policyId) ?? t('Unknown policy')}</td>
                      <td className="px-4 py-3">
                        <Badge tone={job.status === 'completed' ? 'success' : job.status === 'failed' ? 'danger' : 'accent'}>
                          {t(job.status)}
                        </Badge>
                      </td>
                      <td className="px-4 py-3 text-ink-800">{formatDateTime(job.startedAt)}</td>
                      <td className="px-4 py-3 text-ink-800">{formatDateTime(job.completedAt)}</td>
                      <td className="max-w-xs px-4 py-3 text-ink-800">{job.errorMessage ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          {selectedJob ? (
            <Card className="space-y-5">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.24em] text-accent-600">{t('Job details')}</p>
                <h2 className="mt-2 text-2xl font-semibold text-ink-950">{policyNameMap.get(selectedJob.policyId) ?? t('Unknown policy')}</h2>
                <p className="mt-1 text-sm text-ink-800">
                  {agentNameMap.get(selectedJob.agentId) ?? t('Unknown agent')}
                </p>
              </div>

              <div className="grid gap-3 md:grid-cols-2">
                <MetaCard label={t('Status')} value={t(selectedJob.status)} />
                <MetaCard label={t('Started')} value={formatDateTime(selectedJob.startedAt)} />
                <MetaCard label={t('Completed')} value={formatDateTime(selectedJob.completedAt)} />
                <MetaCard label={t('Job id')} value={selectedJob.id} />
              </div>

              <div className="rounded-3xl border border-surface-200 bg-surface-50 p-4">
                <p className="text-xs uppercase tracking-[0.2em] text-ink-800/70">{t('Error message')}</p>
                <p className="mt-3 text-sm text-ink-900">
                  {selectedJob.errorMessage ?? t('No error message recorded for this job.')}
                </p>
              </div>
            </Card>
          ) : null}
        </div>
      ) : (
        <EmptyState title={t('No jobs found')} description={t('Adjust the current filter or wait for the next backup execution to arrive.')} />
      )}
    </div>
  )
}

function MetaCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-surface-200 bg-surface-50 p-4">
      <p className="text-xs uppercase tracking-[0.2em] text-ink-800/70">{label}</p>
      <p className="mt-2 break-all text-sm font-medium text-ink-950">{value}</p>
    </div>
  )
}
