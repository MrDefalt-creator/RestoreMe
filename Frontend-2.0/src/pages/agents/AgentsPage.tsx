import { useMemo, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  AlertTriangle,
  Clock3,
  Laptop,
  Search,
  Server,
  ShieldCheck,
  SlidersHorizontal,
  Wifi,
  X,
} from 'lucide-react'

import { getAgents, type Agent } from '@/shared/api/agents'
import { getPolicies, type BackupPolicy } from '@/shared/api/policies'
import { queryKeys } from '@/shared/lib/query'
import { formatDateTime, formatDurationSeconds, formatPolicyType, formatRelativeTime } from '@/shared/lib/format'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/Card'
import { Dialog } from '@/shared/ui/Dialog'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'
import { SectionHeading } from '@/shared/ui/SectionHeading'
import { Select } from '@/shared/ui/Select'
import { Spinner } from '@/shared/ui/Spinner'
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
  const liveQueryOptions = useLiveQueryOptions()
  const [query, setQuery] = useState('')
  const [filtersOpen, setFiltersOpen] = useState(false)
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
        action={<Badge variant="success">{t('{count} online', { count: stats.online })}</Badge>}
      />

      <div className="grid gap-3 md:grid-cols-4">
        <AgentMetric icon={<Server />} label={t('Registered')} value={stats.total} />
        <AgentMetric icon={<Wifi />} label={t('Online now')} value={stats.online} tone="success" />
        <AgentMetric icon={<AlertTriangle />} label={t('Need review')} value={stats.stale + stats.offline} tone="warning" />
        <AgentMetric icon={<ShieldCheck />} label={t('Policies')} value={stats.policies} />
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
        <Card>
          <CardContent className="flex min-h-64 items-center justify-center gap-3 text-muted-foreground">
            <Spinner />
            {t('Loading agents...')}
          </CardContent>
        </Card>
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

function AgentMetric({
  icon,
  label,
  value,
  tone = 'neutral',
}: {
  icon: ReactNode
  label: string
  value: number
  tone?: 'neutral' | 'success' | 'warning'
}) {
  return (
    <Card className="overflow-hidden">
      <CardContent className="flex items-center justify-between gap-3">
        <div>
          <p className="text-sm text-muted-foreground">{label}</p>
          <p className="mt-1 text-2xl font-semibold text-foreground">{value}</p>
        </div>
        <div
          className={
            tone === 'success'
              ? 'flex h-11 w-11 items-center justify-center rounded-lg bg-success/12 text-success'
              : tone === 'warning'
                ? 'flex h-11 w-11 items-center justify-center rounded-lg bg-warning/12 text-warning'
                : 'flex h-11 w-11 items-center justify-center rounded-lg bg-secondary text-muted-foreground'
          }
        >
          {icon}
        </div>
      </CardContent>
    </Card>
  )
}

function AgentCard({ agent, policies }: { agent: Agent; policies: AgentPolicy[] }) {
  const { t } = useI18n()
  const [detailsOpen, setDetailsOpen] = useState(false)
  const policyCount = policies.length
  const enabledPolicyCount = policies.filter((policy) => policy.isEnabled).length

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

          <div className="flex gap-2">
            <Button asChild variant="primary" size="sm" className="flex-1">
              <Link to="/policies">{t('Policies')}</Link>
            </Button>
            <Button variant="outline" size="sm" onClick={() => setDetailsOpen(true)}>
              {t('Details')}
            </Button>
          </div>
        </CardContent>
      </Card>

      <AgentDetailsDialog
        agent={agent}
        policies={policies}
        open={detailsOpen}
        onClose={() => setDetailsOpen(false)}
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
