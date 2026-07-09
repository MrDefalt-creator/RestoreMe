import { describe, expect, it } from 'vitest'
import { selectPlural } from './plural'

describe('selectPlural ru', () => {
  it.each([
    [1, 'one'], [2, 'few'], [4, 'few'], [5, 'many'], [11, 'many'],
    [21, 'one'], [22, 'few'], [25, 'many'], [101, 'one'], [111, 'many'],
  ])('count %i -> %s', (count, expected) => {
    expect(selectPlural('ru', count)).toBe(expected)
  })
})

describe('selectPlural en', () => {
  it('1 -> one, 2 -> other', () => {
    expect(selectPlural('en', 1)).toBe('one')
    expect(selectPlural('en', 2)).toBe('other')
  })
})
