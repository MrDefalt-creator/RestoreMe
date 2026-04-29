import { isRouteErrorResponse, useRouteError } from 'react-router-dom'

import { NotFoundPage } from '@/pages/errors/NotFoundPage'
import { Card } from '@/shared/ui/Card'

export function RouterErrorPage() {
  const error = useRouteError()

  if (isRouteErrorResponse(error) && error.status === 404) {
    return <NotFoundPage />
  }

  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-10">
      <Card className="mx-auto max-w-2xl space-y-4">
        <div className="space-y-3">
          <p className="text-sm uppercase tracking-[0.18em] text-ink-800/65">Application error</p>
          <h1 className="text-3xl font-semibold text-ink-950">Unable to open the requested page</h1>
          <p className="text-sm leading-6 text-ink-800/75">
            The route could not be rendered correctly. Try returning to the dashboard or reloading the page.
          </p>
        </div>

        {error instanceof Error ? (
          <pre className="overflow-x-auto rounded-2xl border border-surface-200 bg-surface-50 p-4 text-xs leading-6 text-ink-800/80">
            {error.message}
          </pre>
        ) : null}
      </Card>
    </div>
  )
}
