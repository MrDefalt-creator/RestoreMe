import { AlertOctagon, Home, RotateCcw } from 'lucide-react'
import { isRouteErrorResponse, useRouteError } from 'react-router-dom'

import { Button } from '@/shared/ui/Button'

function describe(error: unknown): { title: string; detail: string } {
  if (isRouteErrorResponse(error)) {
    return {
      title: `${error.status} ${error.statusText}`.trim(),
      detail: typeof error.data === 'string' && error.data ? error.data : 'The router reported an HTTP error.',
    }
  }
  if (error instanceof Error) {
    return { title: 'Unexpected error', detail: error.message }
  }
  return { title: 'Unexpected error', detail: 'Something went wrong while rendering this page.' }
}

export function ErrorPage() {
  const error = useRouteError()
  const { title, detail } = describe(error)

  return (
    <div className="flex min-h-[calc(100vh-8rem)] items-center justify-center px-6">
      <div className="max-w-xl text-center">
        <div className="mb-6 inline-flex h-20 w-20 items-center justify-center rounded-lg bg-destructive/10 text-destructive">
          <AlertOctagon className="h-10 w-10" />
        </div>
        <h1 className="text-3xl font-semibold text-foreground">{title}</h1>
        <p className="mt-4 break-words text-sm text-muted-foreground">{detail}</p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <Button className="gap-2" onClick={() => window.location.reload()}>
            <RotateCcw className="h-4 w-4" />
            Reload
          </Button>
          <Button variant="secondary" className="gap-2" onClick={() => (window.location.href = '/')}>
            <Home className="h-4 w-4" />
            Back to dashboard
          </Button>
        </div>
      </div>
    </div>
  )
}
