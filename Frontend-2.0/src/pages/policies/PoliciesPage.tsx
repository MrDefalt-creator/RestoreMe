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
import { SectionHeading } from '@/shared/ui/SectionHeading'
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
    onMutate: async (policy) => {
      await queryClient.cancelQueries({ queryKey: queryKeys.policies })
      const previous = queryClient.getQueryData<BackupPolicy[]>(queryKeys.policies)
      queryClient.setQueryData<BackupPolicy[]>(queryKeys.policies, (old) =>
        (old ?? []).map((p) => (p.id === policy.id ? { ...p, isEnabled: !policy.isEnabled } : p)),
      )
      return { previous }
    },
    onError: (error, _vars, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKeys.policies, context.previous)
      }
      toast.error(error instanceof Error ? error.message : t('Unable to update policy'))
    },
    onSuccess: () => {
      toast.success(t('Policy state updated'))
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.policies })
      void queryClient.invalidateQueries({ queryKey: queryKeys.agents })
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
      <SectionHeading
        eyebrow={t('Schedules')}
        title={t('Policies')}
        description={t('Backup schedules and what each one protects.')}
        action={
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
        }
      />

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
            <>
              {/* Desktop / tablet — the table reads great when there's
                  horizontal room. Hidden on phones where we collapse to
                  stacked cards (the narrow column was forcing horizontal
                  scroll). */}
              <div className="hidden overflow-hidden rounded-lg border border-border md:block">
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

              {/* Mobile (<md) — stacked cards. Same data as the table but
                  laid out vertically with key:value pairs instead of
                  columns; readable without horizontal scroll. */}
              <div className="space-y-3 md:hidden">
                {filteredPolicies.map((policy) => (
                  <div
                    key={policy.id}
                    className="rounded-lg border border-border bg-card/80 p-4"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="truncate text-base font-semibold text-foreground">
                          {policy.name}
                        </p>
                        <p className="mt-0.5 text-xs uppercase tracking-wider text-muted-foreground">
                          {formatPolicyType(policy.type, t)}
                        </p>
                      </div>
                      <Badge variant={policy.isEnabled ? 'success' : 'neutral'}>
                        {policy.isEnabled ? t('enabled') : t('disabled')}
                      </Badge>
                    </div>

                    <dl className="mt-3 grid grid-cols-[max-content_1fr] gap-x-3 gap-y-1.5 text-sm">
                      <dt className="text-muted-foreground">{t('Path')}</dt>
                      <dd className="truncate text-foreground">
                        {formatPolicyTarget(policy) || t('N/A')}
                      </dd>
                      <dt className="text-muted-foreground">{t('Interval')}</dt>
                      <dd className="text-foreground">
                        {formatDurationSeconds(policy.intervalSeconds)}
                      </dd>
                      <dt className="text-muted-foreground">{t('Next Run')}</dt>
                      <dd className="text-foreground">
                        {formatDateTime(policy.nextRunAt)}
                      </dd>
                    </dl>

                    {canWrite ? (
                      <div className="mt-3 flex gap-2 border-t border-border pt-3">
                        <Button
                          variant="outline"
                          size="sm"
                          className="flex-1"
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
                          className="flex-1"
                          disabled={toggleMutation.isPending}
                          onClick={() => toggleMutation.mutate({
                            id: policy.id,
                            isEnabled: policy.isEnabled,
                          })}
                        >
                          {t('Toggle')}
                        </Button>
                      </div>
                    ) : null}
                  </div>
                ))}
              </div>
            </>
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
