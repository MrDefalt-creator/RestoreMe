import { useMemo, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import { toast } from 'sonner'

import { useAuthStore } from '@/app/store/auth-store'
import {
  AlertTriangle,
  Clock3,
  Copy,
  Download,
  Laptop,
  MoreHorizontal,
  Search,
  Server,
  ShieldCheck,
  ShieldOff,
  SlidersHorizontal,
  Trash2,
  Wifi,
  X,
} from 'lucide-react'

import { deleteAgent, getAgents, revokeAgent, type Agent } from '@/shared/api/agents'
import { InstallAgentDialog } from '@/features/install-agent'
import { getPolicies, type BackupPolicy } from '@/shared/api/policies'
import { queryKeys } from '@/shared/lib/query'
import { formatDateTime, formatDurationSeconds, formatPolicyType, formatRelativeTime } from '@/shared/lib/format'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/Card'
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog'
import { Dialog } from '@/shared/ui/Dialog'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import { StatTile } from '@/shared/ui/StatTile'
import { Select } from '@/shared/ui/Select'
import { SkeletonCard } from '@/shared/ui/Skeleton'
import { useI18n } from '@/shared/i18n'
import { useLiveQueryOptions } from '@/shared/lib/useLiveQueryOptions'

const statusTone: Record<Agent['status'], 'success' | 'warning' | 'neutral'> = {
  online: 'success',
  stale: 'warning',
  offline: 'neutral',
}

const EMPTY_AGENTS: Agent[] = []
const EMPTY_POLICIES: BackupPolicy[] = []
type StatusFilter = 'all' | Agent['status']
type PolicyCoverageFilter = 'all' | 'with-policies' | 'without-policies'

export function AgentsPage() {
  const { t } = useI18n()
  const role = useAuthStore((state) => state.user?.role)
  const canInstall = role === 'admin' || role === 'operator'
  const liveQueryOptions = useLiveQueryOptions()
  const [query, setQuery] = useState('')
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [installOpen, setInstallOpen] = useState(false)
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all')
  const [osFilter, setOsFilter] = useState('all')
  const [policyCoverageFilter, setPolicyCoverageFilter] = useState<PolicyCoverageFilter>('all')
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

  const agents = agentsQuery.data ?? EMPTY_AGENTS
  const policies = policiesQuery.data ?? EMPTY_POLICIES
  const policiesByAgent = useMemo(() => groupPoliciesByAgent(policies), [policies])
  const normalizedQuery = query.trim().toLowerCase()
  const hasActiveFilters =
    statusFilter !== 'all' ||
    osFilter !== 'all' ||
    policyCoverageFilter !== 'all'

  const filteredAgents = useMemo(() => {
    return agents.filter((agent) => {
      const matchesStatus = statusFilter === 'all' || agent.status === statusFilter
      const matchesOs = osFilter === 'all' || (agent.osType ?? 'Unknown') === osFilter
      const policyCount = getAgentPolicies(agent, policiesByAgent).length
      const matchesPolicyCoverage =
        policyCoverageFilter === 'all' ||
        (policyCoverageFilter === 'with-policies' ? policyCount > 0 : policyCount === 0)
      const searchable = [
        agent.name,
        agent.machineName,
        agent.osType,
        agent.version,
        agent.status,
        agent.id,
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase()

      return matchesStatus && matchesOs && matchesPolicyCoverage && (!normalizedQuery || searchable.includes(normalizedQuery))
    })
  }, [agents, normalizedQuery, osFilter, policiesByAgent, policyCoverageFilter, statusFilter])

  const stats = useMemo(
    () => ({
      total: agents.length,
      online: agents.filter((agent) => agent.status === 'online').length,
      stale: agents.filter((agent) => agent.status === 'stale').length,
      offline: agents.filter((agent) => agent.status === 'offline').length,
      policies: policies.length,
    }),
    [agents, policies.length],
  )
  const osOptions = useMemo(() => {
    const values = agents.map((agent) => agent.osType ?? 'Unknown')
    return [...new Set(values)].sort((a, b) => a.localeCompare(b))
  }, [agents])

  function resetFilters() {
    setStatusFilter('all')
    setOsFilter('all')
    setPolicyCoverageFilter('all')
  }

  return (
    <div className="space-y-7">
      <SectionHeading
        eyebrow={t('Infrastructure')}
        title={t('Agents')}
        description={t('A live map of registered machines, their heartbeat health, and the protection policy coverage behind each one.')}
        action={
          <div className="flex items-center gap-3">
            <Badge variant="success">{t('{count} online', { count: stats.online })}</Badge>
            {canInstall ? (
              <Button variant="primary" size="sm" className="gap-2" onClick={() => setInstallOpen(true)}>
                <Download className="h-4 w-4" />
                {t('Install new agent')}
              </Button>
            ) : null}
          </div>
        }
      />

      <div className="grid gap-3 md:grid-cols-4">
        <StatTile icon={<Server className="h-4 w-4" />} label={t('Registered')} value={stats.total} />
        <StatTile icon={<Wifi className="h-4 w-4" />} label={t('Online now')} value={stats.online} tone="success" />
        <StatTile icon={<AlertTriangle className="h-4 w-4" />} label={t('Need review')} value={stats.stale + stats.offline} tone="warning" />
        <StatTile icon={<ShieldCheck className="h-4 w-4" />} label={t('Policies')} value={stats.policies} />
      </div>

      <Card>
        <CardContent className="space-y-4">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
            <div className="relative flex-1">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder={t('Search by agent, machine, OS, status, or id...')}
                className="pl-10"
              />
            </div>
            <Button
              variant={filtersOpen || hasActiveFilters ? 'primary' : 'secondary'}
              onClick={() => setFiltersOpen((value) => !value)}
              title={filtersOpen ? t('Hide filters') : t('Show filters')}
            >
              <SlidersHorizontal className="h-4 w-4" />
              {t('Filters')}
              {hasActiveFilters ? (
                <span className="ml-1 rounded bg-primary-foreground/18 px-1.5 py-0.5 text-[11px]">
                  {t('active')}
                </span>
              ) : null}
            </Button>
          </div>

          {filtersOpen ? (
            <div className="grid gap-3 border-t border-border pt-4 md:grid-cols-[1fr_1fr_1fr_auto] md:items-end">
              <FilterField label={t('Status')}>
                <Select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as StatusFilter)}>
                  <option value="all">{t('All statuses')}</option>
                  <option value="online">{t('Online')}</option>
                  <option value="stale">{t('Stale')}</option>
                  <option value="offline">{t('Offline')}</option>
                </Select>
              </FilterField>

              <FilterField label={t('Operating system')}>
                <Select value={osFilter} onChange={(event) => setOsFilter(event.target.value)}>
                  <option value="all">{t('All systems')}</option>
                  {osOptions.map((os) => (
                    <option key={os} value={os}>{os}</option>
                  ))}
                </Select>
              </FilterField>

              <FilterField label={t('Policy coverage')}>
                <Select
                  value={policyCoverageFilter}
                  onChange={(event) => setPolicyCoverageFilter(event.target.value as PolicyCoverageFilter)}
                >
                  <option value="all">{t('Any coverage')}</option>
                  <option value="with-policies">{t('With policies')}</option>
                  <option value="without-policies">{t('Without policies')}</option>
                </Select>
              </FilterField>

              <Button
                variant="outline"
                onClick={resetFilters}
                disabled={!hasActiveFilters}
                title={t('Reset filters')}
              >
                <X className="h-4 w-4" />
                {t('Reset')}
              </Button>
            </div>
          ) : null}

          <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
            <span>
              {t('Showing {shown} of {total} agents', { shown: filteredAgents.length, total: agents.length })}
            </span>
            {hasActiveFilters ? (
              <Badge variant="neutral">{t('Filtered')}</Badge>
            ) : null}
          </div>
        </CardContent>
      </Card>

      {agentsQuery.isLoading ? (
        <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
          {Array.from({ length: 6 }).map((_, idx) => (
            <SkeletonCard key={idx} className="h-56" />
          ))}
        </div>
      ) : agentsQuery.isError ? (
        <EmptyState
          icon={<AlertTriangle className="h-8 w-8 text-warning" />}
          title={t('Agents could not be loaded')}
          description={t('Check the backend connection and retry this view.')}
          action={
            <Button variant="secondary" onClick={() => agentsQuery.refetch()}>
              {t('Retry')}
            </Button>
          }
        />
      ) : filteredAgents.length ? (
        <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
          {filteredAgents.map((agent) => (
            <AgentCard key={agent.id} agent={agent} policies={getAgentPolicies(agent, policiesByAgent)} />
          ))}
        </div>
      ) : (
        <EmptyState
          title={agents.length ? t('No agents match this search') : t('No agents found')}
          description={
            agents.length
              ? t('Adjust the search or reset filters to widen the result set.')
              : t('Approve pending machines or wait for an agent to register.')
          }
          action={agents.length && hasActiveFilters ? (
            <Button variant="secondary" onClick={resetFilters}>
              {t('Reset filters')}
            </Button>
          ) : undefined}
        />
      )}

      {canInstall ? (
        <InstallAgentDialog open={installOpen} onClose={() => setInstallOpen(false)} />
      ) : null}
    </div>
  )
}

function FilterField({
  label,
  children,
}: {
  label: string
  children: ReactNode
}) {
  return (
    <label className="space-y-2">
      <span className="text-xs font-medium uppercase tracking-[0.14em] text-muted-foreground">
        {label}
      </span>
      {children}
    </label>
  )
}


function AgentCard({ agent, policies }: { agent: Agent; policies: AgentPolicy[] }) {
  const { t } = useI18n()
  const [detailsOpen, setDetailsOpen] = useState(false)
  const [revokeOpen, setRevokeOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const policyCount = policies.length
  const enabledPolicyCount = policies.filter((policy) => policy.isEnabled).length
  const currentUser = useAuthStore((state) => state.user)
  const isAdmin = currentUser?.role === 'admin'
  const queryClient = useQueryClient()

  const revokeMutation = useMutation({
    mutationFn: revokeAgent,
    onSuccess: () => {
      toast.success(t('Agent token revoked'))
      setRevokeOpen(false)
      void queryClient.invalidateQueries({ queryKey: queryKeys.agents })
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Unable to revoke agent'))
    },
  })

  const deleteMutation = useMutation({
    mutationFn: deleteAgent,
    onSuccess: () => {
      toast.success(t('Agent deleted'))
      setDeleteOpen(false)
      void queryClient.invalidateQueries({ queryKey: queryKeys.agents })
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Unable to delete agent'))
    },
  })

  return (
    <>
      <Card className="group overflow-hidden transition duration-200 hover:-translate-y-0.5 hover:shadow-[0_18px_50px_hsl(var(--foreground)/0.08)]">
        <CardHeader>
          <div className="flex items-start justify-between gap-4">
            <div className="flex min-w-0 items-start gap-3">
              <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-secondary text-foreground">
                <Laptop className="h-5 w-5" />
              </div>
              <div className="min-w-0">
                <CardTitle className="truncate text-lg">{agent.name}</CardTitle>
                <CardDescription className="truncate">
                  {agent.machineName ?? agent.osType ?? t('Machine details are not available yet')}
                </CardDescription>
              </div>
            </div>
            <Badge variant={statusTone[agent.status]}>{t(agent.status)}</Badge>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <AgentDetail label={t('OS')} value={agent.osType ?? t('Unknown')} />
            <AgentDetail label={t('Version')} value={agent.version ?? t('Unknown')} />
            <AgentDetail label={t('Policies')} value={t('{enabled}/{total} enabled', { enabled: enabledPolicyCount, total: policyCount })} />
            <AgentDetail
              label={t('Heartbeat')}
              value={agent.lastSeenAt ? formatRelativeTime(agent.lastSeenAt) : t('Never')}
            />
          </div>

          <div className="rounded-lg border border-border bg-secondary/45 p-3">
            <div className="flex items-center gap-2 text-sm font-medium text-foreground">
              <Clock3 className="h-4 w-4 text-muted-foreground" />
              {t('Agent identifier')}
            </div>
            <p className="mt-2 truncate font-mono text-xs text-muted-foreground">{agent.id}</p>
          </div>

          <div className="flex flex-wrap gap-2">
            <Button asChild variant="primary" size="sm" className="flex-1">
              <Link to="/policies">{t('Policies')}</Link>
            </Button>
            <Button variant="outline" size="sm" onClick={() => setDetailsOpen(true)}>
              {t('Details')}
            </Button>
            <DropdownMenu.Root>
              <DropdownMenu.Trigger asChild>
                <Button variant="ghost" size="icon" title={t('More actions')} aria-label={t('More actions')}>
                  <MoreHorizontal className="h-4 w-4" />
                </Button>
              </DropdownMenu.Trigger>
              <DropdownMenu.Portal>
                <DropdownMenu.Content
                  align="end"
                  sideOffset={4}
                  className="z-50 min-w-[176px] rounded-lg border border-border bg-card p-1 shadow-[var(--shadow-lg)] data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95"
                >
                  <DropdownMenu.Item
                    className="flex cursor-default select-none items-center gap-2 rounded-md px-3 py-2 text-sm text-foreground outline-none data-[highlighted]:bg-secondary"
                    onSelect={() => {
                      void navigator.clipboard.writeText(agent.id)
                      toast.success(t('Agent ID copied'))
                    }}
                  >
                    <Copy className="h-4 w-4 text-muted-foreground" />
                    {t('Copy agent ID')}
                  </DropdownMenu.Item>
                  {isAdmin ? (
                    <>
                      <DropdownMenu.Separator className="-mx-1 my-1 h-px bg-border" />
                      <DropdownMenu.Item
                        className="flex cursor-default select-none items-center gap-2 rounded-md px-3 py-2 text-sm text-foreground outline-none data-[highlighted]:bg-secondary"
                        onSelect={() => setRevokeOpen(true)}
                      >
                        <ShieldOff className="h-4 w-4 text-muted-foreground" />
                        {t('Revoke token')}
                      </DropdownMenu.Item>
                      <DropdownMenu.Item
                        className="flex cursor-default select-none items-center gap-2 rounded-md px-3 py-2 text-sm text-destructive outline-none data-[highlighted]:bg-destructive/10"
                        onSelect={() => setDeleteOpen(true)}
                      >
                        <Trash2 className="h-4 w-4" />
                        {t('Delete agent…')}
                      </DropdownMenu.Item>
                    </>
                  ) : null}
                </DropdownMenu.Content>
              </DropdownMenu.Portal>
            </DropdownMenu.Root>
          </div>
        </CardContent>
      </Card>

      <AgentDetailsDialog
        agent={agent}
        policies={policies}
        open={detailsOpen}
        onClose={() => setDetailsOpen(false)}
      />

      <ConfirmDialog
        open={revokeOpen}
        onClose={() => setRevokeOpen(false)}
        onConfirm={() => revokeMutation.mutate(agent.id)}
        title={t('Revoke agent token')}
        description={t('The agent will need to re-enroll. The row stays in the list and history is preserved.')}
        confirmLabel={revokeMutation.isPending ? t('Revoking...') : t('Revoke')}
        variant="danger"
        isLoading={revokeMutation.isPending}
      />

      <ConfirmDialog
        open={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        onConfirm={() => deleteMutation.mutate(agent.id)}
        title={t('Delete agent')}
        description={t('This permanently removes the agent and ALL of its backup jobs, artifacts, policies, and restore history. Stored backup files in object storage will be removed on a best-effort basis. This cannot be undone.')}
        confirmLabel={deleteMutation.isPending ? t('Deleting...') : t('Delete agent')}
        variant="danger"
        isLoading={deleteMutation.isPending}
        requireTypeName={agent.name}
      />
    </>
  )
}

function AgentDetailsDialog({
  agent,
  policies,
  open,
  onClose,
}: {
  agent: Agent
  policies: AgentPolicy[]
  open: boolean
  onClose: () => void
}) {
  const { t } = useI18n()
  return (
    <Dialog
      open={open}
      title={agent.name}
      description={agent.machineName ?? t('Registered RestoreMe agent')}
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            {t('Close')}
          </Button>
          <Button asChild variant="primary">
            <Link to="/policies">{t('Manage policies')}</Link>
          </Button>
        </>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <AgentDetail label={t('Status')} value={t(agent.status)} />
        <AgentDetail label={t('Heartbeat')} value={agent.lastSeenAt ? formatRelativeTime(agent.lastSeenAt) : t('Never')} />
        <AgentDetail label={t('OS')} value={agent.osType ?? t('Unknown')} />
        <AgentDetail label={t('Version')} value={agent.version ?? t('Unknown')} />
      </div>

      <div className="rounded-lg border border-border bg-secondary/35 p-4">
        <p className="text-sm font-medium text-foreground">{t('Agent identifier')}</p>
        <p className="mt-2 break-all font-mono text-xs text-muted-foreground">{agent.id}</p>
      </div>

      <div className="space-y-3">
        <div className="flex items-center justify-between gap-3">
          <p className="text-sm font-medium text-foreground">{t('Assigned policies')}</p>
          <Badge variant={policies.length ? 'success' : 'neutral'}>
            {t('{enabled}/{total} enabled', { enabled: policies.filter((policy) => policy.isEnabled).length, total: policies.length })}
          </Badge>
        </div>

        {policies.length ? (
          <div className="divide-y divide-border rounded-lg border border-border">
            {policies.map((policy) => (
              <div key={policy.id} className="grid gap-3 p-3 sm:grid-cols-[1fr_auto] sm:items-center">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-foreground">{policy.name}</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {formatPolicyType(policy.type)} · every {formatDurationSeconds(policy.intervalSeconds)}
                  </p>
                </div>
                <div className="text-left sm:text-right">
                  <Badge variant={policy.isEnabled ? 'success' : 'neutral'}>
                    {policy.isEnabled ? t('enabled') : t('disabled')}
                  </Badge>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {t('Next')} {formatDateTime(policy.nextRunAt)}
                  </p>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="rounded-lg border border-dashed border-border bg-secondary/25 p-4 text-sm text-muted-foreground">
            {t('No policies are assigned to this agent yet.')}
          </div>
        )}
      </div>
    </Dialog>
  )
}

type AgentPolicy = Pick<BackupPolicy, 'id' | 'name' | 'type' | 'isEnabled' | 'intervalSeconds' | 'nextRunAt'>

function groupPoliciesByAgent(policies: BackupPolicy[]) {
  return policies.reduce((map, policy) => {
    const items = map.get(policy.agentId) ?? []
    items.push(policy)
    map.set(policy.agentId, items)
    return map
  }, new Map<string, AgentPolicy[]>())
}

function getAgentPolicies(agent: Agent, policiesByAgent: Map<string, AgentPolicy[]>) {
  return policiesByAgent.get(agent.id) ?? []
}

function AgentDetail({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0 rounded-lg bg-secondary/45 px-3 py-2">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 truncate text-sm font-medium text-foreground">{value}</p>
    </div>
  )
}
