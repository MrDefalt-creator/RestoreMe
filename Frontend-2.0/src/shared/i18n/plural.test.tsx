import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { I18nProvider, useI18n } from './index'

function Probe({ count }: { count: number }) {
  const { tp } = useI18n()
  return <span>{tp('{count} artifacts', count, { count })}</span>
}

describe('tp', () => {
  it('falls back to the key form when no dictionary entry (en other)', () => {
    // default language is en; en dict has no plural entry -> uses key literally
    render(<I18nProvider><Probe count={2} /></I18nProvider>)
    expect(screen.getByText('2 artifacts')).toBeInTheDocument()
  })
})
