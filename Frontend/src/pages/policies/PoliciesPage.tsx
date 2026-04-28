import { useDeferredValue, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import { toast } from 'sonner'

import { useAuthStore } from '@/app/store/auth-store'
import { useUiStore } from '@/app/store/ui-store'
import { PolicyFormDialog } from '@/features/policy-form/PolicyFormDialog'
import { getAgents } from '@/entities/agent/api'
import { getPolicies, togglePolicy } from '@/entities/policy/api'
import type { BackupPolicy } from '@/entities/policy/model/types'
import { formatDateTime, formatDurationSeconds } from '@/shared/lib/format'
import { queryKeys } from '@/shared/lib/query'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import { Select } from '@/shared/ui/Select'

function formatPolicyType(type: BackupPolicy['type']) {
  switch (type) {
    case 'postgres':
      return 'PostgreSQL dump'
    case 'mysql':
      return 'MySQL dump'
    default:
      return 'Filesystem'
  }
}

function formatPolicyTarget(policy: BackupPolicy) {
  if (policy.type === 'filesystem') {
    return policy.sourcePath
  }

  const databaseName = policy.databaseSettings?.databaseName ?? 'unknown-db'
  const host = policy.databaseSettings?.host || 'local'
  return `${databaseName} @ ${host}`
}

export function PoliciesPage() {
  const [search, setSearch] = useState('')
  const [editingPolicy, setEditingPolicy] = useState<BackupPolicy | null>(null)
  const [isDialogOpen, setIsDialogOpen] = useState(false)
  const role = useAuthStore((state) => state.user?.role)
  const canWrite = role === 'operator' || role === 'admin'
  const policyFilter = useUiStore((state) => state.policyFilter)
  const setPolicyFilter = useUiStore((state) => state.setPolicyFilter)
  const deferredSearch = useDeferredValue(search)
  const queryClient = useQueryClient()

  const agentsQuery = useQuery({
    queryKey: queryKeys.agents,
    queryFn: getAgents,
  })
  const policiesQuery = useQuery({
    queryKey: queryKeys.policies,
    queryFn: getPolicies,
  })

  const toggleMutation = useMutation({
    mutationFn: togglePolicy,
    onSuccess: () => {
      toast.success('Policy state updated')
      void queryClient.invalidateQueries({ queryKey: queryKeys.policies })
      void queryClient.invalidateQueries({ queryKey: queryKeys.dashboard })
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : 'Unable to toggle policy')
    },
  })

  const agentNameMap = new Map((agentsQuery.data ?? []).map((agent) => [agent.id, agent.name]))
  const policies = policiesQuery.data ?? []
  const searchValue = deferredSearch.trim().toLowerCase()
  const filteredPolicies = policies.filter((policy) => {
    const matchesFilter =
      policyFilter === 'all' ||
      (policyFilter === 'enabled' ? policy.isEnabled : !policy.isEnabled)

    const matchesSearch =
      !searchValue ||
      [
        policy.name,
        formatPolicyTarget(policy),
        formatPolicyType(policy.type),
        policy.databaseSettings?.databaseName ?? '',
        policy.databaseSettings?.host ?? '',
        agentNameMap.get(policy.agentId) ?? '',
      ]
        .join(' ')
        .toLowerCase()
        .includes(searchValue)

    return matchesFilter && matchesSearch
  })

  return (
    <div className="space-y-8">
      <SectionHeading
        eyebrow="Policies"
        title="Backup scheduling rules"
        description="Create filesystem backup schedules or logical database dump policies without losing visibility into the target agent and next execution window."
        action={
          canWrite ? (
            <Button
              onClick={() => {
                setEditingPolicy(null)
                setIsDialogOpen(true)
              }}
            >
              Create policy
            </Button>
          ) : null
        }
      />

      <Card className="grid gap-4 md:grid-cols-[1fr_220px]">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-800/60" />
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search policy, target, type or agent"
            className="pl-10"
          />
        </div>
        <Select value={policyFilter} onChange={(event) => setPolicyFilter(event.target.value as typeof policyFilter)}>
          <option value="all">All policies</option>
          <option value="enabled">Enabled only</option>
          <option value="disabled">Disabled only</option>
        </Select>
      </Card>

      {filteredPolicies.length ? (
        <Card className="overflow-hidden p-0">
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-surface-100 text-ink-800">
                <tr>
                  {['Policy', 'Type', 'Agent', 'Target', 'Interval', 'Next run', 'Last run', 'State', 'Actions'].map((label) => (
                    <th key={label} className="px-4 py-3 font-medium">
                      {label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filteredPolicies.map((policy) => (
                  <tr key={policy.id} className="border-t border-surface-200">
                    <td className="px-4 py-3 font-medium text-ink-950">{policy.name}</td>
                    <td className="px-4 py-3 text-ink-800">{formatPolicyType(policy.type)}</td>
                    <td className="px-4 py-3 text-ink-800">{agentNameMap.get(policy.agentId) ?? 'Unknown agent'}</td>
                    <td className="px-4 py-3 text-ink-800">{formatPolicyTarget(policy)}</td>
                    <td className="px-4 py-3 text-ink-800">{formatDurationSeconds(policy.intervalSeconds)}</td>
                    <td className="px-4 py-3 text-ink-800">{formatDateTime(policy.nextRunAt)}</td>
                    <td className="px-4 py-3 text-ink-800">{formatDateTime(policy.lastRunAt)}</td>
                    <td className="px-4 py-3">
                      <Badge tone={policy.isEnabled ? 'success' : 'neutral'}>
                        {policy.isEnabled ? 'enabled' : 'disabled'}
                      </Badge>
                    </td>
                    <td className="px-4 py-3">
                      {canWrite ? (
                        <div className="flex gap-2">
                          <Button
                            size="sm"
                            variant="secondary"
                            onClick={() => {
                              setEditingPolicy(policy)
                              setIsDialogOpen(true)
                            }}
                          >
                            Edit
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() => toggleMutation.mutate(policy.id)}
                          >
                            Toggle
                          </Button>
                        </div>
                      ) : (
                        <span className="text-xs font-medium uppercase tracking-[0.18em] text-ink-800/55">
                          Read only
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      ) : (
        <EmptyState title="No policies found" description="Adjust the filter or create the first filesystem or database backup policy for an approved agent." />
      )}

      <PolicyFormDialog
        open={canWrite && isDialogOpen}
        agents={agentsQuery.data ?? []}
        policy={editingPolicy}
        onClose={() => setIsDialogOpen(false)}
      />
    </div>
  )
}
