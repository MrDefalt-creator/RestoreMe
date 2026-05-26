import type { AuditLogEntry } from '@/entities/audit-log'

export type AuditCategory =
  | 'Users'
  | 'Agents'
  | 'Policies'
  | 'Backups'
  | 'Restores'
  | 'Security'
  | 'Other'

export function categorize(action: string): AuditCategory {
  const prefix = action.split('.')[0]
  switch (prefix) {
    case 'user': return 'Users'
    case 'agent': return 'Agents'
    case 'policy': return 'Policies'
    case 'job':
    case 'artifact': return 'Backups'
    case 'restore': return 'Restores'
    case 'auth': return 'Security'
    default: return 'Other'
  }
}

export function renderAuditMessage(entry: AuditLogEntry): string {
  const actor = entry.actorUsername ?? 'System'
  const target = entry.targetId ? entry.targetId.slice(0, 8) : ''
  const details = entry.details ?? ''

  switch (entry.action) {
    case 'user.create': return `${actor} created user ${target}${details ? ` (${details})` : ''}`
    case 'user.delete': return `${actor} deleted user ${target}`
    case 'user.update': return `${actor} updated user ${target}`
    case 'user.role_change': return `${actor} changed role for ${target}${details ? ` → ${details}` : ''}`
    case 'agent.approve': return `${actor} approved agent ${target}`
    case 'agent.reject': return `${actor} rejected agent ${target}`
    case 'agent.delete': return `${actor} deleted agent ${target}`
    case 'agent.deleted': return `${actor} deleted agent ${target}${details ? ` (${details})` : ''}`
    case 'agent.revoke': return `${actor} revoked token for agent ${target}`
    case 'policy.create': return `${actor} created policy ${target}${details ? ` "${details}"` : ''}`
    case 'policy.update': return `${actor} updated policy ${target}${details ? ` (${details})` : ''}`
    case 'policy.toggle': return `${actor} toggled policy ${target}${details ? ` (${details})` : ''}`
    case 'policy.delete': return `${actor} deleted policy ${target}${details ? ` (${details})` : ''}`
    case 'job.started': return `Backup job ${target} started${details ? ` (${details})` : ''}`
    case 'job.completed': return `Backup job ${target} completed${details ? ` (${details})` : ''}`
    case 'job.failed': return `Backup job ${target} failed${details ? ` — ${details}` : ''}`
    case 'artifact.added': return `Artifact ${target} uploaded${details ? ` (${details})` : ''}`
    case 'restore.request': return `${actor} requested restore for artifact ${target}`
    case 'restore.complete': return `Restore job ${target} completed`
    case 'restore.failed': return `Restore job ${target} failed${details ? `: ${details}` : ''}`
    case 'auth.login': return `${actor} signed in`
    case 'auth.failed': return `Failed login attempt${target ? ` for ${target}` : ''}${details ? ` from ${details}` : ''}`
    case 'auth.logout': return `${actor} signed out`
    default: return `${actor}: ${entry.action}${target ? ` on ${target}` : ''}${details ? ` — ${details}` : ''}`
  }
}

function hashCode(str: string): number {
  let hash = 0
  for (let i = 0; i < str.length; i++) {
    hash = ((hash << 5) - hash) + str.charCodeAt(i)
    hash |= 0
  }
  return Math.abs(hash)
}

export function actorHue(username: string): number {
  return hashCode(username) % 360
}
