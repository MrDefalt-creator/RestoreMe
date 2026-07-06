import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { useDeepLinkHighlight } from './useDeepLinkHighlight'

function Probe({ ready }: { ready: boolean }) {
  const id = useDeepLinkHighlight(ready)
  return (
    <div>
      <span data-testid="value">{id ?? 'none'}</span>
      <div data-deep-link-id="entity-1">row one</div>
      <div data-deep-link-id="entity-2">row two</div>
    </div>
  )
}

function renderAt(url: string, ready = true) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Probe ready={ready} />
    </MemoryRouter>,
  )
}

describe('useDeepLinkHighlight', () => {
  const scrollIntoView = vi.fn()

  beforeEach(() => {
    scrollIntoView.mockClear()
    Element.prototype.scrollIntoView = scrollIntoView
  })

  it('returns the id from the URL and scrolls the matching element', () => {
    renderAt('/policies?id=entity-2')
    expect(screen.getByTestId('value')).toHaveTextContent('entity-2')
    expect(scrollIntoView).toHaveBeenCalledTimes(1)
  })

  it('returns null and does not scroll without an id param', () => {
    renderAt('/policies')
    expect(screen.getByTestId('value')).toHaveTextContent('none')
    expect(scrollIntoView).not.toHaveBeenCalled()
  })

  it('waits for ready before scrolling', () => {
    const { rerender } = renderAt('/policies?id=entity-1', false)
    expect(scrollIntoView).not.toHaveBeenCalled()

    rerender(
      <MemoryRouter initialEntries={['/policies?id=entity-1']}>
        <Probe ready />
      </MemoryRouter>,
    )
    expect(scrollIntoView).toHaveBeenCalledTimes(1)
  })

  it('does not scroll for an id that matches nothing', () => {
    renderAt('/policies?id=missing')
    expect(screen.getByTestId('value')).toHaveTextContent('missing')
    expect(scrollIntoView).not.toHaveBeenCalled()
  })
})
