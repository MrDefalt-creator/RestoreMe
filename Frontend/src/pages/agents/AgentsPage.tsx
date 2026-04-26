import { startTransition, useDeferredValue, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'

import { useUiStore } from '@/app/store/ui-store'
import { getAgents } from '@/entities/agent/api'
import { getJobs } from '@/entities/job/api'
import { getPolicies } from '@/entities/policy/api'
import { formatDateTime, formatRelativeTime } from '@/shared/lib/format'
import { queryKeys } from '@/shared/lib/query'
import { Badge } from '@/shared/ui/Badge'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import type { AgentStatus } from '@/entities/agent/model/types'

export function AgentsPage() {
  const [search, setSearch] = useState('')
  const deferredSearch = useDeferredValue(search)
  const selectedAgentId = useUiStore((state) => state.selectedAgentId)
  const setSelectedAgentId = useUiStore((state) => state.setSelectedAgentId)

  const agentsQuery = useQuery({
    queryKey: queryKeys.agents,
    queryFn: getAgents,
  })
  const policiesQuery = useQuery({
    queryKey: queryKeys.policies,
    queryFn: getPolicies,
  })
  const jobsQuery = useQuery({
    queryKey: queryKeys.jobs,
    queryFn: () => getJobs('all'),
  })

  const agents = agentsQuery.data ?? []
  const searchValue = deferredSearch.trim().toLowerCase()
  const filteredAgents = !searchValue
    ? agents
    : agents.filter((agent) =>
        [agent.name, agent.machineName, agent.osType, agent.version]
          .join(' ')
          .toLowerCase()
          .includes(searchValue),
      )

  const selectedAgent =
    filteredAgents.find((agent) => agent.id === selectedAgentId) ??
    agents.find((agent) => agent.id === selectedAgentId) ??
    filteredAgents[0] ??
    null

  const selectedPolicies = (policiesQuery.data ?? []).filter(
    (policy) => policy.agentId === selectedAgent?.id,
  )
  const selectedJobs = (jobsQuery.data ?? []).filter(
    (job) => job.agentId === selectedAgent?.id,
  )

  return (
    <div className="space-y-8">
      <SectionHeading
        eyebrow="Agents"
        title="Registered machines"
        description="Track machine identity, heartbeat freshness and the policies attached to every approved agent."
      />

      <Card className="space-y-4">
        <div className="relative max-w-md">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-800/60" />
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search by agent, machine or OS"
            className="pl-10"
          />
        </div>
      </Card>

      {filteredAgents.length ? (
        <div className="grid gap-5 xl:grid-cols-[1.15fr_0.85fr]">
          <Card className="overflow-hidden p-0">
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="bg-surface-100 text-ink-800">
                  <tr>
                    {['Name', 'Machine', 'OS', 'Version', 'Status', 'Created', 'Last seen'].map((label) => (
                      <th key={label} className="px-4 py-3 font-medium">
                        {label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {filteredAgents.map((agent) => (
                    <tr
                      key={agent.id}
                      className="cursor-pointer border-t border-surface-200 transition hover:bg-sky-50/40"
                      onClick={() =>
                        startTransition(() => {
                          setSelectedAgentId(agent.id)
                        })
                      }
                    >
                      <td className="px-4 py-3 font-medium text-ink-950">{agent.name}</td>
                      <td className="px-4 py-3 text-ink-800">{agent.machineName}</td>
                      <td className="px-4 py-3 text-ink-800">{agent.osType}</td>
                      <td className="px-4 py-3 text-ink-800">{agent.version}</td>
                      <td className="px-4 py-3">
                        <Badge tone={getAgentStatusTone(agent.status)}>
                          {agent.status}
                        </Badge>
                      </td>
                      <td className="px-4 py-3 text-ink-800">{formatDateTime(agent.createdAt)}</td>
                      <td className="px-4 py-3 text-ink-800">{formatRelativeTime(agent.lastSeenAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          {selectedAgent ? (
            <Card className="space-y-6">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.24em] text-accent-600">Selected agent</p>
                <h2 className="mt-2 text-2xl font-semibold text-ink-950">{selectedAgent.name}</h2>
                <p className="mt-1 text-sm text-ink-800">
                  {selectedAgent.machineName} • {selectedAgent.osType}
                </p>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <MetaCard label="Version" value={selectedAgent.version} />
                <MetaCard label="Created" value={formatDateTime(selectedAgent.createdAt)} />
                <MetaCard label="Status" value={selectedAgent.status} />
                <MetaCard label="Last heartbeat" value={formatRelativeTime(selectedAgent.lastSeenAt)} />
              </div>

              <div className="space-y-3">
                <h3 className="text-lg font-semibold text-ink-950">Attached policies</h3>
                {selectedPolicies.length ? (
                  selectedPolicies.map((policy) => (
                    <div key={policy.id} className="rounded-2xl border border-surface-200 bg-surface-50 px-4 py-3">
                      <div className="flex items-center justify-between gap-3">
                        <p className="font-medium text-ink-950">{policy.name}</p>
                        <Badge tone={policy.isEnabled ? 'success' : 'neutral'}>
                          {policy.isEnabled ? 'enabled' : 'disabled'}
                        </Badge>
                      </div>
                      <p className="mt-2 text-sm text-ink-800">{policy.sourcePath}</p>
                    </div>
                  ))
                ) : (
                  <EmptyState title="No policies" description="The selected agent does not have backup policies yet." />
                )}
              </div>

              <div className="space-y-3">
                <h3 className="text-lg font-semibold text-ink-950">Recent jobs</h3>
                {selectedJobs.length ? (
                  selectedJobs.slice(0, 3).map((job) => (
                    <div key={job.id} className="rounded-2xl border border-surface-200 bg-white px-4 py-3">
                      <div className="flex items-center justify-between gap-3">
                        <p className="font-medium text-ink-950">{formatDateTime(job.startedAt)}</p>
                        <Badge tone={job.status === 'completed' ? 'success' : job.status === 'failed' ? 'danger' : 'accent'}>
                          {job.status}
                        </Badge>
                      </div>
                      {job.errorMessage ? (
                        <p className="mt-2 text-sm text-danger-500">{job.errorMessage}</p>
                      ) : null}
                    </div>
                  ))
                ) : (
                  <EmptyState title="No jobs yet" description="This agent has not produced any backup history in the current dataset." />
                )}
              </div>
            </Card>
          ) : null}
        </div>
      ) : (
        <EmptyState title="No agents found" description="Try another search term or approve a pending machine first." />
      )}
    </div>
  )
}

function MetaCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-surface-200 bg-surface-50 p-4">
      <p className="text-xs uppercase tracking-[0.2em] text-ink-800/70">{label}</p>
      <p className="mt-2 text-sm font-medium text-ink-950">{value}</p>
    </div>
  )
}

function getAgentStatusTone(status: AgentStatus) {
  switch (status) {
    case 'online':
      return 'success'
    case 'stale':
      return 'warning'
    default:
      return 'neutral'
  }
}
