export type AuthEventType = 'auth:unauthorized'

class AuthEventBus extends EventTarget {
  emitUnauthorized(reason?: string) {
    this.dispatchEvent(new CustomEvent<{ reason?: string }>('auth:unauthorized', { detail: { reason } }))
  }
  onUnauthorized(handler: (event: CustomEvent<{ reason?: string }>) => void) {
    const wrapped = handler as EventListener
    this.addEventListener('auth:unauthorized', wrapped)
    return () => this.removeEventListener('auth:unauthorized', wrapped)
  }
}

export const authEvents = new AuthEventBus()
