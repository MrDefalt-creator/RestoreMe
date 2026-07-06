import { describe, expect, it } from 'vitest'

import { interpolate } from './index'

describe('interpolate', () => {
  it('returns the value untouched without params', () => {
    expect(interpolate('Hello {name}')).toBe('Hello {name}')
  })

  it('replaces a single placeholder', () => {
    expect(interpolate('Hello {name}', { name: 'Ivan' })).toBe('Hello Ivan')
  })

  it('replaces every occurrence of a placeholder', () => {
    expect(interpolate('{x} + {x}', { x: 2 })).toBe('2 + 2')
  })

  it('replaces multiple distinct placeholders', () => {
    expect(interpolate('{count} of {total}', { count: 3, total: 10 })).toBe('3 of 10')
  })

  it('leaves unknown placeholders in place', () => {
    expect(interpolate('{known} {unknown}', { known: 'ok' })).toBe('ok {unknown}')
  })
})
