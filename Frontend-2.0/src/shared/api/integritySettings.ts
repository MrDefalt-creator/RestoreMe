import apiClient from './client'

export interface IntegrityScrubSettings {
  isEnabled: boolean
  intervalDays: number
  runAtMinutesUtc: number
  batchSize: number
  lastRunAt?: string | null
  nextRunAt: string
}

export type UpdateIntegrityScrubSettings = Pick<
  IntegrityScrubSettings,
  'isEnabled' | 'intervalDays' | 'runAtMinutesUtc' | 'batchSize'
>

export async function getIntegritySettings(): Promise<IntegrityScrubSettings> {
  const response = await apiClient.get('/api/integrity-settings')
  return response.data
}

export async function updateIntegritySettings(body: UpdateIntegrityScrubSettings): Promise<IntegrityScrubSettings> {
  const response = await apiClient.put('/api/integrity-settings', body)
  return response.data
}
