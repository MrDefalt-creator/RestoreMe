import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { EmptyState } from './EmptyState'

describe('EmptyState', () => {
  it('renders the title', () => {
    render(<EmptyState title="No jobs yet" />)
    expect(screen.getByRole('heading', { name: 'No jobs yet' })).toBeInTheDocument()
  })

  it('renders description and action when provided', () => {
    render(
      <EmptyState
        title="Nothing here"
        description="Come back later."
        action={<button type="button">Retry</button>}
      />,
    )
    expect(screen.getByText('Come back later.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })

  it('omits description when not provided', () => {
    render(<EmptyState title="Bare" />)
    expect(screen.queryByText('Come back later.')).not.toBeInTheDocument()
  })
})
