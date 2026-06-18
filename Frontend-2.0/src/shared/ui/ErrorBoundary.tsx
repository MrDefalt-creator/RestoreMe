import { Component, type ErrorInfo, type ReactNode } from 'react'
import { AlertOctagon, Home, RotateCcw } from 'lucide-react'

import { Button } from '@/shared/ui/Button'

interface ErrorBoundaryProps {
  children: ReactNode
  resetKey?: string | number
  fallback?: (error: Error, reset: () => void) => ReactNode
}

interface ErrorBoundaryState {
  error: Error | null
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error }
  }

  componentDidUpdate(prevProps: ErrorBoundaryProps) {
    if (this.state.error && prevProps.resetKey !== this.props.resetKey) {
      this.setState({ error: null })
    }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('[ErrorBoundary]', error, info.componentStack)
  }

  private handleReset = () => this.setState({ error: null })

  render() {
    const { error } = this.state
    if (!error) return this.props.children

    if (this.props.fallback) return this.props.fallback(error, this.handleReset)

    return (
      <div className="flex min-h-[60vh] items-center justify-center px-6">
        <div className="max-w-xl text-center">
          <div className="mb-6 inline-flex h-20 w-20 items-center justify-center rounded-lg bg-destructive/10 text-destructive">
            <AlertOctagon className="h-10 w-10" />
          </div>
          <h1 className="text-3xl font-semibold text-foreground">Something broke on this page</h1>
          <p className="mt-4 break-words text-sm text-muted-foreground">{error.message}</p>
          <div className="mt-6 flex flex-wrap justify-center gap-3">
            <Button className="gap-2" onClick={this.handleReset}>
              <RotateCcw className="h-4 w-4" />
              Try again
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
}
