import { formatDurationSeconds } from './format'

const WEEKDAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']

export type CronPresetSpec =
  | { kind: 'daily' }
  | { kind: 'weekly'; weekday: number }
  | { kind: 'monthly'; dayOfMonth: number }

export function timeToMinutes(time: string): number {
  const [hours, minutes] = time.split(':').map(Number)
  return hours * 60 + minutes
}

export function minutesToTime(minutes: number): string {
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`
}

export function buildCronExpression(preset: CronPresetSpec, time: string): string {
  const [hours, minutes] = time.split(':').map(Number)
  switch (preset.kind) {
    case 'daily':
      return `${minutes} ${hours} * * *`
    case 'weekly':
      return `${minutes} ${hours} * * ${preset.weekday}`
    case 'monthly':
      return `${minutes} ${hours} ${preset.dayOfMonth} * *`
  }
}

export function parseCronPreset(
  expression: string,
): { preset: 'daily' | 'weekly' | 'monthly'; time: string; weekday?: number; dayOfMonth?: number } | null {
  const parts = expression.trim().split(/\s+/)
  if (parts.length !== 5) return null
  const [minute, hour, dayOfMonth, month, weekday] = parts
  const isPlainNumber = (s: string) => /^\d+$/.test(s)
  if (!isPlainNumber(minute) || !isPlainNumber(hour) || month !== '*') return null

  const time = minutesToTime(Number(hour) * 60 + Number(minute))

  if (dayOfMonth === '*' && weekday === '*') return { preset: 'daily', time }
  if (dayOfMonth === '*' && isPlainNumber(weekday)) return { preset: 'weekly', time, weekday: Number(weekday) }
  if (isPlainNumber(dayOfMonth) && weekday === '*') return { preset: 'monthly', time, dayOfMonth: Number(dayOfMonth) }
  return null
}

export function describeSchedule(p: {
  scheduleKind: 'interval' | 'cron'
  intervalSeconds: number
  cronExpression: string | null
  timeZoneId: string | null
  windowStartMinutes: number | null
  windowEndMinutes: number | null
}): string {
  if (p.scheduleKind === 'cron' && p.cronExpression) {
    const tz = p.timeZoneId ? ` (${p.timeZoneId})` : ''
    const preset = parseCronPreset(p.cronExpression)
    if (preset?.preset === 'daily') return `Daily at ${preset.time}${tz}`
    if (preset?.preset === 'weekly') return `Weekly on ${WEEKDAY_NAMES[preset.weekday!]} at ${preset.time}${tz}`
    if (preset?.preset === 'monthly') return `Monthly on day ${preset.dayOfMonth} at ${preset.time}${tz}`
    return `Cron: ${p.cronExpression}${tz}`
  }

  const every = `Every ${formatDurationSeconds(p.intervalSeconds)}`
  if (p.windowStartMinutes != null && p.windowEndMinutes != null) {
    const tz = p.timeZoneId ? ` (${p.timeZoneId})` : ''
    return `${every}, ${minutesToTime(p.windowStartMinutes)}–${minutesToTime(p.windowEndMinutes)}${tz}`
  }
  return every
}
