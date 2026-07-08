import { interpolate } from '@/shared/i18n'

import { formatDurationSeconds } from './format'

type Translate = (key: string, params?: Record<string, string | number>) => string

// Default translator for callers without an i18n context (unit tests): fall
// back to the English key and interpolate, matching the previous output.
const identity: Translate = (key, params) => interpolate(key, params)

// 2023-01-01 was a Sunday, so day 1+weekday lands on the target weekday.
function weekdayName(weekday: number, locale: string): string {
  return new Intl.DateTimeFormat(locale, { weekday: 'short' }).format(
    new Date(Date.UTC(2023, 0, 1 + weekday)),
  )
}

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
  if (dayOfMonth === '*' && isPlainNumber(weekday)) return { preset: 'weekly', time, weekday: Number(weekday) % 7 }
  if (isPlainNumber(dayOfMonth) && weekday === '*') return { preset: 'monthly', time, dayOfMonth: Number(dayOfMonth) }
  return null
}

export function describeSchedule(
  p: {
    scheduleKind: 'interval' | 'cron'
    intervalSeconds: number
    cronExpression: string | null
    timeZoneId: string | null
    windowStartMinutes: number | null
    windowEndMinutes: number | null
  },
  t: Translate = identity,
  locale = 'en-US',
): string {
  if (p.scheduleKind === 'cron' && p.cronExpression) {
    const tz = p.timeZoneId ? ` (${p.timeZoneId})` : ''
    const preset = parseCronPreset(p.cronExpression)
    if (preset?.preset === 'daily') return t('Daily at {time}{tz}', { time: preset.time, tz })
    if (preset?.preset === 'weekly')
      return t('Weekly on {weekday} at {time}{tz}', {
        weekday: weekdayName(preset.weekday!, locale),
        time: preset.time,
        tz,
      })
    if (preset?.preset === 'monthly')
      return t('Monthly on day {day} at {time}{tz}', { day: preset.dayOfMonth!, time: preset.time, tz })
    return t('Cron: {expression}{tz}', { expression: p.cronExpression, tz })
  }

  const duration = formatDurationSeconds(p.intervalSeconds)
  if (p.windowStartMinutes != null && p.windowEndMinutes != null) {
    const tz = p.timeZoneId ? ` (${p.timeZoneId})` : ''
    return t('Every {duration}, {start}–{end}{tz}', {
      duration,
      start: minutesToTime(p.windowStartMinutes),
      end: minutesToTime(p.windowEndMinutes),
      tz,
    })
  }
  return t('Every {duration}', { duration })
}
