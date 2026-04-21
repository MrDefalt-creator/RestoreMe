import { format, formatDistanceToNowStrict } from 'date-fns'

export function formatDateTime(value?: string | null) {
  if (!value) {
    return 'Not available'
  }

  return format(new Date(value), 'dd MMM yyyy, HH:mm')
}

export function formatRelativeTime(value?: string | null) {
  if (!value) {
    return 'No heartbeat yet'
  }

  return `${formatDistanceToNowStrict(new Date(value), { addSuffix: true })}`
}

export function formatBytes(value: number) {
  if (value < 1024) {
    return `${value} B`
  }

  const units = ['KB', 'MB', 'GB', 'TB']
  let size = value / 1024
  let unitIndex = 0

  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024
    unitIndex += 1
  }

  return `${size.toFixed(size >= 10 ? 0 : 1)} ${units[unitIndex]}`
}

export function formatDurationSeconds(totalSeconds: number) {
  const remaining = Math.max(0, Math.floor(totalSeconds))
  const days = Math.floor(remaining / 86_400)
  const hours = Math.floor((remaining % 86_400) / 3_600)
  const minutes = Math.floor((remaining % 3_600) / 60)
  const seconds = remaining % 60

  const parts = [
    days ? `${days}d` : null,
    hours ? `${hours}h` : null,
    minutes ? `${minutes}m` : null,
    seconds ? `${seconds}s` : null,
  ].filter(Boolean)

  return parts.length ? parts.join(' ') : '0s'
}
