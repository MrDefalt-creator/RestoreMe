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
