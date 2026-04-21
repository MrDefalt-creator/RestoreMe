import { env } from '@/shared/config/env'
import { getAgents, getPendingAgents } from '@/entities/agent/api'
import { getJobs } from '@/entities/job/api'
import { getPolicies } from '@/entities/policy/api'
import { getDashboardSummary as getDashboardSummaryMock } from '@/shared/api/mockDb'

export async function getDashboardSummary() {
  if (env.apiMode === 'mock') {
    return getDashboardSummaryMock()
  }

  const [agents, pendingAgents, policies, jobs] = await Promise.all([
    getAgents(),
    getPendingAgents(),
    getPolicies(),
    getJobs('all'),
  ])

  const recentJobs = [...jobs]
    .sort((left, right) => right.startedAt.localeCompare(left.startedAt))
    .slice(0, 5)

  const recentErrors = [...jobs]
    .filter((job) => job.status === 'failed')
    .sort((left, right) =>
      (right.completedAt ?? right.startedAt).localeCompare(
        left.completedAt ?? left.startedAt,
      ),
    )
    .slice(0, 5)

  return {
    totalAgents: agents.length,
    onlineAgents: agents.filter((agent) => agent.status === 'online').length,
    staleAgents: agents.filter((agent) => agent.status === 'stale').length,
    offlineAgents: agents.filter((agent) => agent.status === 'offline').length,
    pendingAgents: pendingAgents.length,
    activePolicies: policies.filter((policy) => policy.isEnabled).length,
    recentJobs,
    recentErrors,
  }
}
