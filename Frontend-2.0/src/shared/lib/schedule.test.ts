import { describe, expect, it } from 'vitest'
import {
  buildCronExpression,
  describeSchedule,
  minutesToTime,
  parseCronPreset,
  timeToMinutes,
} from './schedule'

describe('buildCronExpression', () => {
  it('builds daily', () => expect(buildCronExpression({ kind: 'daily' }, '03:00')).toBe('0 3 * * *'))
  it('builds weekly', () => expect(buildCronExpression({ kind: 'weekly', weekday: 1 }, '04:30')).toBe('30 4 * * 1'))
  it('builds monthly', () => expect(buildCronExpression({ kind: 'monthly', dayOfMonth: 15 }, '23:05')).toBe('5 23 15 * *'))
})

describe('parseCronPreset', () => {
  it('round-trips daily', () =>
    expect(parseCronPreset('0 3 * * *')).toEqual({ preset: 'daily', time: '03:00' }))
  it('round-trips weekly', () =>
    expect(parseCronPreset('30 4 * * 1')).toEqual({ preset: 'weekly', time: '04:30', weekday: 1 }))
  it('round-trips monthly', () =>
    expect(parseCronPreset('5 23 15 * *')).toEqual({ preset: 'monthly', time: '23:05', dayOfMonth: 15 }))
  it('returns null for non-preset shapes', () => {
    expect(parseCronPreset('*/15 * * * *')).toBeNull()
    expect(parseCronPreset('0 3 * * 1-5')).toBeNull()
    expect(parseCronPreset('garbage')).toBeNull()
  })
  it('normalizes weekday 7 (Cronos Sunday) to 0', () =>
    expect(parseCronPreset('0 4 * * 7')).toEqual({ preset: 'weekly', time: '04:00', weekday: 0 }))
})

describe('time/minutes conversion', () => {
  it('converts both ways', () => {
    expect(timeToMinutes('22:00')).toBe(1320)
    expect(minutesToTime(1320)).toBe('22:00')
    expect(minutesToTime(5)).toBe('00:05')
  })
})

describe('describeSchedule', () => {
  const base = { intervalSeconds: 0, cronExpression: null, timeZoneId: null, windowStartMinutes: null, windowEndMinutes: null }

  it('describes plain interval', () =>
    expect(describeSchedule({ ...base, scheduleKind: 'interval', intervalSeconds: 900 })).toBe('Every 15m 0s'))

  it('describes interval with window', () =>
    expect(describeSchedule({
      ...base, scheduleKind: 'interval', intervalSeconds: 3600,
      timeZoneId: 'Europe/Moscow', windowStartMinutes: 1320, windowEndMinutes: 360,
    })).toBe('Every 1h 0m, 22:00–06:00 (Europe/Moscow)'))

  it('describes daily cron', () =>
    expect(describeSchedule({
      ...base, scheduleKind: 'cron', cronExpression: '0 3 * * *', timeZoneId: 'Europe/Moscow',
    })).toBe('Daily at 03:00 (Europe/Moscow)'))

  it('falls back to raw expression for custom cron', () =>
    expect(describeSchedule({
      ...base, scheduleKind: 'cron', cronExpression: '*/15 22-5 * * *', timeZoneId: 'Etc/UTC',
    })).toBe('Cron: */15 22-5 * * * (Etc/UTC)'))

  it('describes weekly cron with weekday 7 as Sunday (no "undefined")', () =>
    expect(describeSchedule({
      ...base, scheduleKind: 'cron', cronExpression: '0 4 * * 7', timeZoneId: 'Etc/UTC',
    })).toBe('Weekly on Sun at 04:00 (Etc/UTC)'))
})
