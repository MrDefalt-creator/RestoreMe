import { describe, expect, it } from 'vitest'

import { cn } from './cn'

describe('cn', () => {
  it('joins class values', () => {
    expect(cn('a', 'b')).toBe('a b')
  })

  it('drops falsy values', () => {
    const disabled = false as boolean
    expect(cn('a', disabled && 'b', undefined, null, 'c')).toBe('a c')
  })

  it('lets the last conflicting tailwind class win', () => {
    expect(cn('p-2', 'p-4')).toBe('p-4')
    expect(cn('text-sm text-muted-foreground', 'text-lg')).toBe('text-muted-foreground text-lg')
  })

  it('supports conditional objects', () => {
    expect(cn('base', { active: true, hidden: false })).toBe('base active')
  })
})
