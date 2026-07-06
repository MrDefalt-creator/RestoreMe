import apiClient from './client'

export type DashboardPeriod = '7d' | '30d' | '90d'

export interface SuccessRatePoint {
  date: string
  completed: number
  failed: number
}

export interface StorageGrowthPoint {
  date: string
  cumulativeBytes: number
}

export interface TopFailingPolicy {
  policyId: string
  policyName: string
  failureCount: number
}

export interface EngineBreakdown {
  engine: string
  policyCount: number
}

export interface DashboardMetrics {
  period: string
  from: string
  to: string
  successRateTimeseries: SuccessRatePoint[]
  storageGrowthTimeseries: StorageGrowthPoint[]
  topFailingPolicies: TopFailingPolicy[]
  engineBreakdown: EngineBreakdown[]
}

export async function getDashboardMetrics(period: DashboardPeriod): Promise<DashboardMetrics> {
  const response = await apiClient.get('/api/dashboard/metrics', { params: { period } })
  return response.data
}

export interface AgentSummary {
  online: number
  stale: number
  offline: number
  total: number
}

export interface PolicyTypeBreakdown {
  filesystem: number
  postgres: number
  mysql: number
}

export interface PolicySummary {
  active: number
  total: number
  byType: PolicyTypeBreakdown
}

export interface JobsPerDay {
  date: string
  count: number
}

export interface UnresolvedFailure {
  policyId: string
  policyName: string
  errorMessage: string | null
}

export type RecentJobStatus = 'pending' | 'running' | 'completed' | 'failed'

export interface RecentJob {
  id: string
  title: string
  agentName: string
  status: RecentJobStatus
  startedAt: string
}

export interface JobSummary {
  completed: number
  running: number
  failed: number
  total: number
  last7Days: JobsPerDay[]
  unresolvedFailures: UnresolvedFailure[]
  recent: RecentJob[]
}

export interface RecentArtifact {
  id: string
  displayName: string
  size: number
  createdAt: string
}

export interface ArtifactSummary {
  total: number
  totalSize: number
  filesystem: number
  database: number
  recent: RecentArtifact[]
}

export interface DashboardSummary {
  agents: AgentSummary
  pendingAgentsCount: number
  policies: PolicySummary
  jobs: JobSummary
  artifacts: ArtifactSummary
}

export async function getDashboardSummary(): Promise<DashboardSummary> {
  const response = await apiClient.get('/api/dashboard/summary')
  return response.data
}
