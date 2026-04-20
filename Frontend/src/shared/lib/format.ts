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
