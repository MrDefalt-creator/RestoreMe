import { format, formatDistanceToNow, parseISO } from 'date-fns'
import { enUS, ru } from 'date-fns/locale'

import { getStoredDateStyle, getStoredLanguage } from '@/shared/i18n'

function getDateLocale() {
  return getStoredLanguage() === 'ru' ? ru : enUS
}

export function formatDateTime(dateString: string): string {
  const date = parseISO(dateString)
  const pattern = getStoredDateStyle() === 'compact'
    ? 'yyyy-MM-dd HH:mm'
    : getStoredLanguage() === 'ru' ? 'd MMM yyyy, HH:mm' : 'MMM d, yyyy \'at\' HH:mm'

  return format(date, pattern, {
    locale: getDateLocale(),
  })
}

export function formatDurationSeconds(seconds: number): string {
  const mins = Math.floor(seconds / 60)
  const hours = Math.floor(mins / 60)
  const days = Math.floor(hours / 24)

  if (days > 0) {
    return `${days}d ${hours % 24}h`
  }
  if (hours > 0) {
    return `${hours}h ${mins % 60}m`
  }
  return `${mins}m ${seconds % 60}s`
}

export function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

export function formatRelativeTime(dateString: string): string {
  const date = parseISO(dateString)
  return formatDistanceToNow(date, { addSuffix: true, locale: getDateLocale() })
}

export function formatTarget(target: string): string {
  if (!target) return 'Unknown'
  // Handle database targets
  if (target.includes('@')) {
    const [database, host] = target.split('@')
    return `${database} @ ${host}`
  }
  return target
}

export function formatPolicyType(type: string): string {
  switch (type) {
    case 'postgres':
      return 'PostgreSQL'
    case 'mysql':
      return 'MySQL'
    default:
      return 'Filesystem'
  }
}
