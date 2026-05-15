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
import { useI18n } from '@/shared/i18n'
import { useLiveQueryOptions } from '@/shared/lib/useLiveQueryOptions'
import { useAuthStore } from '@/app/store/auth-store'

export function PoliciesPage() {
  const { t } = useI18n()
  const liveQueryOptions = useLiveQueryOptions()
  const role = useAuthStore((state) => state.user?.role)
  const canWrite = role === 'admin' || role === 'operator'
  const queryClient = useQueryClient()
  const policiesQuery = useQuery({
    queryKey: queryKeys.policies,
    queryFn: getPolicies,
    ...liveQueryOptions,
  })
  const agentsQuery = useQuery({
    queryKey: queryKeys.agents,
    queryFn: getAgents,
    ...liveQueryOptions,
  })

  const [search, setSearch] = useState('')
  const [editingPolicy, setEditingPolicy] = useState<BackupPolicy | null>(null)
  const [isDialogOpen, setIsDialogOpen] = useState(false)
  const [policyFilter, setPolicyFilter] = useState('all')
  const toggleMutation = useMutation({
    mutationFn: (policy: { id: string; isEnabled: boolean }) =>
      togglePolicy(policy.id),
    onSuccess: () => {
      toast.success(t('Policy state updated'))
      void queryClient.invalidateQueries({ queryKey: queryKeys.policies })
      void queryClient.invalidateQueries({ queryKey: queryKeys.agents })
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Unable to update policy'))
    },
  })

  const policies = policiesQuery.data ?? []
  const agentNameMap = new Map((agentsQuery.data ?? []).map((agent) => [agent.id, agent.name]))
  const searchValue = search.trim().toLowerCase()
  const filteredPolicies = policies.filter((policy) => {
    const matchesFilter =
      policyFilter === 'all' ||
      (policyFilter === 'enabled' ? policy.isEnabled : !policy.isEnabled)

    const matchesSearch =
      !searchValue ||
      [
        policy.name,
        formatPolicyType(policy.type, t),
        formatPolicyTarget(policy),
        policy.databaseSettings?.databaseName ?? '',
        policy.databaseSettings?.host ?? '',
        agentNameMap.get(policy.agentId) ?? '',
      ].join(' ').toLowerCase().includes(searchValue)

    return matchesFilter && matchesSearch
  })

  return (
    <div className="space-y-8">
      {/* Section Header */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-semibold tracking-tight text-foreground">
              {t('Backup Policies')}
            </h1>
            <p className="mt-2 text-muted-foreground">
              {t('Create and manage backup schedules')}
            </p>
          </div>
          <div className="flex items-center gap-3">
            <Badge variant="success">{t('{count} policies', { count: filteredPolicies.length })}</Badge>
            {canWrite ? (
              <Button
                onClick={() => {
                  setEditingPolicy(null)
                  setIsDialogOpen(true)
                }}
              >
                {t('Create policy')}
              </Button>
            ) : null}
          </div>
        </div>
      </div>

      {/* Filters */}
      <Card>
        <CardContent className="flex flex-col sm:flex-row gap-4">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder={t('Search policies...')}
              className="pl-10"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <Select value={policyFilter} onChange={(event) => setPolicyFilter(event.target.value as 'all' | 'enabled' | 'disabled')}>
            <option value="all">{t('All policies')}</option>
            <option value="enabled">{t('Enabled only')}</option>
            <option value="disabled">{t('Disabled only')}</option>
          </Select>
        </CardContent>
      </Card>

      {/* Policies Table */}
      <Card className="overflow-hidden">
        <CardHeader>
          <CardTitle className="text-xl">{t('Recent policies')}</CardTitle>
          <CardDescription>{t('View and manage backup schedules')}</CardDescription>
        </CardHeader>
        <CardContent>
          {filteredPolicies.length ? (
            <div className="overflow-hidden rounded-lg border border-border">
              <table className="min-w-full text-sm">
                <thead className="bg-secondary/70">
                  <tr>
                    <Th>{t('Policy')}</Th>
                    <Th>{t('Type')}</Th>
                    <Th>{t('Path')}</Th>
                    <Th>{t('Interval')}</Th>
                    <Th>{t('Next Run')}</Th>
                    <Th>{t('State')}</Th>
                    {canWrite ? <Th>{t('Actions')}</Th> : null}
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {filteredPolicies.map((policy) => (
                    <tr key={policy.id} className="hover:bg-secondary/35">
                      <Td>{policy.name}</Td>
                      <Td className="uppercase tracking-wider">{formatPolicyType(policy.type, t)}</Td>
                      <Td className="max-w-[220px] truncate">
                        {formatPolicyTarget(policy) || t('N/A')}
                      </Td>
                      <Td>{formatDurationSeconds(policy.intervalSeconds)}</Td>
                      <Td className="text-muted-foreground">
                        {formatDateTime(policy.nextRunAt)}
                      </Td>
                      <Td>
                        <Badge variant={policy.isEnabled ? 'success' : 'neutral'}>
                          {policy.isEnabled ? t('enabled') : t('disabled')}
                        </Badge>
                      </Td>
                      {canWrite ? (
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
                              {t('Edit')}
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
                              {t('Toggle')}
                            </Button>
                          </div>
                        </Td>
                      ) : null}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <EmptyState
              title={t('No policies found')}
              description={canWrite
                ? t('Adjust the filter or create the first backup policy.')
                : t('No backup policies are available for the current filter.')}
            />
          )}
        </CardContent>
      </Card>
      <PolicyFormDialog
        open={canWrite && isDialogOpen}
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

function formatPolicyType(type: string, t: (key: string) => string): string {
  switch (type) {
    case 'postgres':
      return t('PostgreSQL dump')
    case 'mysql':
      return t('MySQL dump')
    default:
      return t('Filesystem')
  }
}

function formatPolicyTarget(policy: BackupPolicy): string {
  if (policy.type === 'filesystem') {
    return policy.sourcePath
  }

  const databaseName = policy.databaseSettings?.databaseName ?? 'unknown-db'
  const host = policy.databaseSettings?.host || 'local'
  return `${databaseName} @ ${host}`
}
