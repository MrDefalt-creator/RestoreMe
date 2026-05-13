import { Button } from '@/shared/ui/Button'
import { ArrowLeft } from 'lucide-react'

export function RouterErrorPage() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center">
      <div className="text-center">
        <div className="mb-6 inline-flex h-20 w-20 items-center justify-center rounded-lg bg-destructive/12">
          <svg className="h-10 w-10 text-destructive" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
          </svg>
        </div>
        <h1 className="text-4xl font-semibold text-foreground">
          Oops!
        </h1>
        <p className="mt-4 text-lg text-muted-foreground">
          Something went wrong
        </p>
        <Button className="mt-6 gap-2" onClick={() => window.history.back()}>
          <ArrowLeft className="h-4 w-4" />
          Go back
        </Button>
      </div>
    </div>
  )
}
