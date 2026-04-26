import { startTransition, useDeferredValue, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'

import { useUiStore } from '@/app/store/ui-store'
import { getAgents } from '@/entities/agent/api'
import { getJobs } from '@/entities/job/api'
import { getPolicies } from '@/entities/policy/api'
import { formatDateTime } from '@/shared/lib/format'
import { queryKeys } from '@/shared/lib/query'
import { Badge } from '@/shared/ui/Badge'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import { Select } from '@/shared/ui/Select'

export function JobsPage() {
  const [search, setSearch] = useState('')
  const deferredSearch = useDeferredValue(search)
  const selectedJobId = useUiStore((state) => state.selectedJobId)
  const setSelectedJobId = useUiStore((state) => state.setSelectedJobId)
  const jobFilter = useUiStore((state) => state.jobFilter)
  const setJobFilter = useUiStore((state) => state.setJobFilter)

  const jobsQuery = useQuery({
    queryKey: [...queryKeys.jobs, jobFilter],
    queryFn: () => getJobs(jobFilter),
  })
  const agentsQuery = useQuery({
    queryKey: queryKeys.agents,
    queryFn: getAgents,
  })
  const policiesQuery = useQuery({
    queryKey: queryKeys.policies,
    queryFn: getPolicies,
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
        eyebrow="Jobs"
        title="Backup execution history"
        description="Inspect job status, completion time and the last known failure reason without losing the surrounding queue context."
      />

      <Card className="grid gap-4 lg:grid-cols-[1fr_220px]">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-800/60" />
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            className="pl-10"
            placeholder="Search by id, error, agent or policy"
          />
        </div>
        <Select value={jobFilter} onChange={(event) => setJobFilter(event.target.value as typeof jobFilter)}>
          <option value="all">All jobs</option>
          <option value="running">Running</option>
          <option value="completed">Completed</option>
          <option value="failed">Failed</option>
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
                        {label}
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
                      <td className="px-4 py-3 text-ink-800">{agentNameMap.get(job.agentId) ?? 'Unknown agent'}</td>
                      <td className="px-4 py-3 text-ink-800">{policyNameMap.get(job.policyId) ?? 'Unknown policy'}</td>
                      <td className="px-4 py-3">
                        <Badge tone={job.status === 'completed' ? 'success' : job.status === 'failed' ? 'danger' : 'accent'}>
                          {job.status}
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
                <p className="text-xs font-semibold uppercase tracking-[0.24em] text-accent-600">Job details</p>
                <h2 className="mt-2 text-2xl font-semibold text-ink-950">{policyNameMap.get(selectedJob.policyId) ?? 'Unknown policy'}</h2>
                <p className="mt-1 text-sm text-ink-800">
                  {agentNameMap.get(selectedJob.agentId) ?? 'Unknown agent'}
                </p>
              </div>

              <div className="grid gap-3 md:grid-cols-2">
                <MetaCard label="Status" value={selectedJob.status} />
                <MetaCard label="Started" value={formatDateTime(selectedJob.startedAt)} />
                <MetaCard label="Completed" value={formatDateTime(selectedJob.completedAt)} />
                <MetaCard label="Job id" value={selectedJob.id} />
              </div>

              <div className="rounded-3xl border border-surface-200 bg-surface-50 p-4">
                <p className="text-xs uppercase tracking-[0.2em] text-ink-800/70">Error message</p>
                <p className="mt-3 text-sm text-ink-900">
                  {selectedJob.errorMessage ?? 'No error message recorded for this job.'}
                </p>
              </div>
            </Card>
          ) : null}
        </div>
      ) : (
        <EmptyState title="No jobs found" description="Adjust the current filter or wait for the next backup execution to arrive." />
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
