export const queryKeys = {
  dashboard: ['dashboard'] as const,
  agents: ['agents'] as const,
  pendingAgents: ['agents', 'pending'] as const,
  policies: ['policies'] as const,
  jobs: ['jobs'] as const,
  // Prefixed with 'jobs' so invalidateQueries({ queryKey: queryKeys.jobs })
  // also refreshes every cached page.
  jobsPage: (page: number, sort: string, status?: string) =>
    ['jobs', 'page', page, sort, status ?? null] as const,
  artifacts: ['artifacts'] as const,
  artifactsPage: (page: number, sort: string) =>
    ['artifacts', 'page', page, sort] as const,
  users: ['users'] as const,
  notificationChannels: ['notification-channels'] as const,
  auditLogs: (page: number, pageSize: number, action?: string) =>
    ['audit-logs', page, pageSize, action ?? null] as const,
  restoreStatus: (id: string) => ['restore', id] as const,
}
