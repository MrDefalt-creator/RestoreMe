import { Home } from 'lucide-react'

import { Button } from '@/shared/ui/Button'

export function NotFoundPage() {
  return (
    <div className="flex min-h-[calc(100vh-8rem)] items-center justify-center">
      <div className="text-center">
        <div className="mb-6 inline-flex h-20 w-20 items-center justify-center rounded-lg bg-secondary">
          <Home className="h-10 w-10 text-muted-foreground" />
        </div>
        <h1 className="text-4xl font-semibold text-foreground">
          404
        </h1>
        <p className="mt-4 text-lg text-muted-foreground">
          Page not found
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          The page you are looking for might have been removed, had its name changed, or is temporarily unavailable.
        </p>
        <Button className="mt-6 gap-2" onClick={() => (window.location.href = '/')}>
          <Home className="h-4 w-4" />
          Go back home
        </Button>
      </div>
    </div>
  )
}
