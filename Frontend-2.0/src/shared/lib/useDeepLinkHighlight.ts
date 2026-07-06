import { useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'

/**
 * Reads the `?id=` entity deep link (emitted by the command palette and
 * JobDrawer) and scrolls the matching element into view once the list has
 * rendered. Matching elements must carry `data-deep-link-id={entityId}`.
 * Pages that render the same entity twice for responsive layouts (table +
 * stacked cards) can tag both — the first visible one wins.
 *
 * Returns the raw id so callers can add a visual highlight to the match.
 */
export function useDeepLinkHighlight(ready: boolean): string | null {
  const [searchParams] = useSearchParams()
  const id = searchParams.get('id')

  useEffect(() => {
    if (!id || !ready) return
    const nodes = document.querySelectorAll<HTMLElement>(
      `[data-deep-link-id="${CSS.escape(id)}"]`,
    )
    const target = [...nodes].find((node) => node.offsetParent !== null) ?? nodes[0]
    target?.scrollIntoView({ block: 'center', behavior: 'smooth' })
  }, [id, ready])

  return id
}
