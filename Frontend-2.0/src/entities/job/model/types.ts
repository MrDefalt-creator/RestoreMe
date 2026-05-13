export interface Job {
  id: string
  name: string
  status: 'completed' | 'failed' | 'running'
  policyId: string
  policyName: string
  agentId: string
  agentName: string
  startedAt: string
  completedAt?: string
  errorMessage?: string
}
