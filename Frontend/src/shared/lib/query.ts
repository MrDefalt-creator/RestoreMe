export const queryKeys = {
  dashboard: ['dashboard'] as const,
  agents: ['agents'] as const,
  agentDetails: (agentId: string) => ['agents', agentId] as const,
  pendingAgents: ['pending-agents'] as const,
  policies: ['policies'] as const,
  jobs: ['jobs'] as const,
  jobDetails: (jobId: string) => ['jobs', jobId] as const,
  artifacts: ['artifacts'] as const,
  users: ['users'] as const,
}
