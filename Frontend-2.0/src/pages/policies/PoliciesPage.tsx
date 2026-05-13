import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Search } from 'lucide-react'

import { PolicyFormDialog } from '@/features/policy-form/PolicyFormDialog'
import { getAgents } from '@/shared/api/agents'
import { getPolicies, togglePolicy, type BackupPolicy } from '@/shared/api/policies'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { Select } from '@/shared/ui/Select'
import { queryKeys } from '@/shared/lib/query'
import { formatDurationSeconds, formatDateTime } from '@/shared/lib/format'
import { toast } from 'sonner'

export function PoliciesPage() {
  const queryClient = useQueryClient()
  const policiesQuery = useQuery({
    queryKey: queryKeys.policies,
    queryFn: getPolicies,
  })
  const agentsQuery = useQuery({
    queryKey: queryKeys.agents,
    queryFn: getAgents,
  })

  const [search, setSearch] = useState('')
  const [editingPolicy, setEditingPolicy] = useState<BackupPolicy | null>(null)
  const [isDialogOpen, setIsDialogOpen] = useState(false)
  const [policyFilter, setPolicyFilter] = useState('all')
  const toggleMutation = useMutation({
    mutationFn: (policy: { id: string; isEnabled: boolean }) =>
      togglePolicy(policy.id),
    onSuccess: () => {
      toast.success('Policy state updated')
      void queryClient.invalidateQueries({ queryKey: queryKeys.policies })
      void queryClient.invalidateQueries({ queryKey: queryKeys.agents })
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : 'Unable to update policy')
    },
  })

  const policies = policiesQuery.data ?? []
  const searchValue = search.trim().toLowerCase()
  const filteredPolicies = policies.filter((policy) => {
    const matchesFilter =
      policyFilter === 'all' ||
      (policyFilter === 'enabled' ? policy.isEnabled : !policy.isEnabled)

    const matchesSearch =
      !searchValue ||
      [policy.name, policy.type, policy.sourcePath].join(' ').toLowerCase().includes(searchValue)

    return matchesFilter && matchesSearch
  })

  return (
    <div className="space-y-8">
      {/* Section Header */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-semibold tracking-tight text-foreground">
              Backup Policies
            </h1>
            <p className="mt-2 text-muted-foreground">
              Create and manage backup schedules
            </p>
          </div>
          <div className="flex items-center gap-3">
            <Badge variant="success">{filteredPolicies.length} policies</Badge>
            <Button
              onClick={() => {
                setEditingPolicy(null)
                setIsDialogOpen(true)
              }}
            >
              Create policy
            </Button>
          </div>
        </div>
      </div>

      {/* Filters */}
      <Card>
        <CardContent className="flex flex-col sm:flex-row gap-4">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search policies..."
              className="pl-10"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <Select value={policyFilter} onChange={(event) => setPolicyFilter(event.target.value as 'all' | 'enabled' | 'disabled')}>
            <option value="all">All policies</option>
            <option value="enabled">Enabled only</option>
            <option value="disabled">Disabled only</option>
          </Select>
        </CardContent>
      </Card>

      {/* Policies Table */}
      <Card className="overflow-hidden">
        <CardHeader>
          <CardTitle className="text-xl">Recent policies</CardTitle>
          <CardDescription>View and manage backup schedules</CardDescription>
        </CardHeader>
        <CardContent>
          {filteredPolicies.length ? (
            <div className="overflow-hidden rounded-lg border border-border">
              <table className="min-w-full text-sm">
                <thead className="bg-secondary/70">
                  <tr>
                    <Th>Policy</Th>
                    <Th>Type</Th>
                    <Th>Path</Th>
                    <Th>Interval</Th>
                    <Th>Next Run</Th>
                    <Th>State</Th>
                    <Th>Actions</Th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {filteredPolicies.map((policy) => (
                    <tr key={policy.id} className="hover:bg-secondary/35">
                      <Td>{policy.name}</Td>
                      <Td className="uppercase tracking-wider">{formatPolicyType(policy.type)}</Td>
                      <Td className="max-w-[150px] truncate">
                        {policy.sourcePath || 'N/A'}
                      </Td>
                      <Td>{formatDurationSeconds(policy.intervalSeconds)}</Td>
                      <Td className="text-muted-foreground">
                        {formatDateTime(policy.nextRunAt)}
                      </Td>
                      <Td>
                        <Badge variant={policy.isEnabled ? 'success' : 'neutral'}>
                          {policy.isEnabled ? 'enabled' : 'disabled'}
                        </Badge>
                      </Td>
                      <Td>
                        <div className="flex gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => {
                              setEditingPolicy(policy)
                              setIsDialogOpen(true)
                            }}
                          >
                            Edit
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            disabled={toggleMutation.isPending}
                            onClick={() => toggleMutation.mutate({
                              id: policy.id,
                              isEnabled: policy.isEnabled,
                            })}
                          >
                            Toggle
                          </Button>
                        </div>
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <EmptyState
              title="No policies found"
              description="Adjust the filter or create the first backup policy."
            />
          )}
        </CardContent>
      </Card>
      <PolicyFormDialog
        open={isDialogOpen}
        agents={agentsQuery.data ?? []}
        policy={editingPolicy}
        onClose={() => setIsDialogOpen(false)}
      />
    </div>
  )
}

function Th({ children }: { children: React.ReactNode }) {
  return (
    <th className="px-4 py-3 text-left font-medium text-muted-foreground">
      {children}
    </th>
  )
}

function Td({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <td className={`px-4 py-3 ${className || ''}`}>
      {children}
    </td>
  )
}

function formatPolicyType(type: string): string {
  switch (type) {
    case 'postgres':
      return 'PostgreSQL'
    case 'mysql':
      return 'MySQL'
    default:
      return 'Filesystem'
  }
}
