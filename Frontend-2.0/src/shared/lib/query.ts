export const queryKeys = {
  dashboard: ['dashboard'] as const,
  agents: ['agents'] as const,
  pendingAgents: ['agents', 'pending'] as const,
  policies: ['policies'] as const,
  jobs: ['jobs'] as const,
  artifacts: ['artifacts'] as const,
  users: ['users'] as const,
  notificationChannels: ['notification-channels'] as const,
  auditLogs: (page: number, pageSize: number, action?: string) =>
    ['audit-logs', page, pageSize, action ?? null] as const,
  restoreStatus: (id: string) => ['restore', id] as const,
}
