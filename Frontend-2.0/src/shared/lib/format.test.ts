import { describe, expect, it } from 'vitest'

import { formatDurationSeconds, formatFileSize, formatPolicyType, formatTarget, formatUtcOffset } from './format'

describe('formatDurationSeconds', () => {
  it('renders sub-minute durations as minutes and seconds', () => {
    expect(formatDurationSeconds(42)).toBe('0m 42s')
  })

  it('renders minutes with a seconds remainder', () => {
    expect(formatDurationSeconds(125)).toBe('2m 5s')
  })

  it('renders hours with a minutes remainder', () => {
    expect(formatDurationSeconds(3660)).toBe('1h 1m')
  })

  it('renders days with an hours remainder', () => {
    expect(formatDurationSeconds(90000)).toBe('1d 1h')
  })
})

describe('formatFileSize', () => {
  it('handles zero bytes', () => {
    expect(formatFileSize(0)).toBe('0 B')
  })

  it('uses binary units', () => {
    expect(formatFileSize(1024)).toBe('1 KB')
    expect(formatFileSize(1536)).toBe('1.5 KB')
    expect(formatFileSize(5 * 1024 * 1024)).toBe('5 MB')
  })

  it('caps at gigabytes', () => {
    expect(formatFileSize(2.5 * 1024 ** 3)).toBe('2.5 GB')
  })
})

describe('formatTarget', () => {
  it('falls back for empty targets', () => {
    expect(formatTarget('')).toBe('Unknown')
  })

  it('splits database targets on @', () => {
    expect(formatTarget('appdb@db.internal')).toBe('appdb @ db.internal')
  })

  it('passes filesystem paths through', () => {
    expect(formatTarget('/var/lib/data')).toBe('/var/lib/data')
  })
})

describe('formatPolicyType', () => {
  it('maps known engine types', () => {
    expect(formatPolicyType('postgres')).toBe('PostgreSQL')
    expect(formatPolicyType('mysql')).toBe('MySQL')
  })

  it('defaults to filesystem', () => {
    expect(formatPolicyType('filesystem')).toBe('Filesystem')
    expect(formatPolicyType('anything-else')).toBe('Filesystem')
  })
})

describe('formatUtcOffset', () => {
  it('formats a whole positive offset', () => {
    // getTimezoneOffset returns minutes WEST of UTC (negative for east)
    const d = { getTimezoneOffset: () => -180 } as Date // UTC+3
    expect(formatUtcOffset(d)).toBe('UTC+3')
  })
  it('formats a whole negative offset', () => {
    const d = { getTimezoneOffset: () => 300 } as Date // UTC-5
    expect(formatUtcOffset(d)).toBe('UTC-5')
  })
  it('formats a half-hour offset', () => {
    const d = { getTimezoneOffset: () => -330 } as Date // UTC+5:30
    expect(formatUtcOffset(d)).toBe('UTC+5:30')
  })
  it('labels zero offset as UTC', () => {
    const d = { getTimezoneOffset: () => 0 } as Date
    expect(formatUtcOffset(d)).toBe('UTC')
  })
})
