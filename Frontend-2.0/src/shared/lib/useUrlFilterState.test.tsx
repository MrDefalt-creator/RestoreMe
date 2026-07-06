import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useSearchParams } from 'react-router-dom'
import { describe, expect, it } from 'vitest'

import { useUrlFilterState } from './useUrlFilterState'

type Status = 'all' | 'failed' | 'completed'

function Probe() {
  const [status, setStatus] = useUrlFilterState<Status>('status', 'all', ['all', 'failed', 'completed'])
  const [params] = useSearchParams()
  return (
    <div>
      <span data-testid="value">{status}</span>
      <span data-testid="search">{params.toString()}</span>
      <button type="button" onClick={() => setStatus('failed')}>fail</button>
      <button type="button" onClick={() => setStatus('all')}>reset</button>
    </div>
  )
}

function renderAt(url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Probe />
    </MemoryRouter>,
  )
}

describe('useUrlFilterState', () => {
  it('reads the filter from the URL (deep link)', () => {
    renderAt('/jobs?status=failed')
    expect(screen.getByTestId('value')).toHaveTextContent('failed')
  })

  it('falls back to the default when the param is absent', () => {
    renderAt('/jobs')
    expect(screen.getByTestId('value')).toHaveTextContent('all')
  })

  it('falls back to the default for unknown values', () => {
    renderAt('/jobs?status=banana')
    expect(screen.getByTestId('value')).toHaveTextContent('all')
  })

  it('writes the filter to the URL and preserves sibling params', async () => {
    const user = userEvent.setup()
    renderAt('/jobs?id=job-123')
    await user.click(screen.getByRole('button', { name: 'fail' }))
    expect(screen.getByTestId('value')).toHaveTextContent('failed')
    expect(screen.getByTestId('search').textContent).toContain('id=job-123')
    expect(screen.getByTestId('search').textContent).toContain('status=failed')
  })

  it('removes the param from the URL when reset to the default', async () => {
    const user = userEvent.setup()
    renderAt('/jobs?status=failed&id=job-123')
    await user.click(screen.getByRole('button', { name: 'reset' }))
    expect(screen.getByTestId('search').textContent).not.toContain('status=')
    expect(screen.getByTestId('search').textContent).toContain('id=job-123')
  })
})
