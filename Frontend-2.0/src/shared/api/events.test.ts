import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import {
  connectServerEvents,
  isServerEventsConnected,
  SERVER_EVENT_TOPICS,
  subscribeServerEventsState,
} from './events'

class MockEventSource {
  static instances: MockEventSource[] = []

  url: string
  withCredentials: boolean
  closed = false
  onopen: (() => void) | null = null
  onerror: (() => void) | null = null
  listeners = new Map<string, () => void>()

  constructor(url: string, init?: { withCredentials?: boolean }) {
    this.url = url
    this.withCredentials = init?.withCredentials ?? false
    MockEventSource.instances.push(this)
  }

  addEventListener(type: string, listener: () => void) {
    this.listeners.set(type, listener)
  }

  close() {
    this.closed = true
  }

  emit(type: string) {
    this.listeners.get(type)?.()
  }
}

let disconnect: (() => void) | null = null

beforeEach(() => {
  MockEventSource.instances = []
  vi.stubGlobal('EventSource', MockEventSource)
})

afterEach(() => {
  // Closing resets the module-level connection flag between tests.
  disconnect?.()
  disconnect = null
  vi.unstubAllGlobals()
})

describe('connectServerEvents', () => {
  it('opens the stream against /api/events with credentials', () => {
    disconnect = connectServerEvents(() => {})

    const source = MockEventSource.instances[0]
    expect(source.url).toMatch(/\/api\/events$/)
    expect(source.withCredentials).toBe(true)
  })

  it('subscribes to every server topic and forwards it to the callback', () => {
    const received: string[] = []
    disconnect = connectServerEvents((topic) => received.push(topic))

    const source = MockEventSource.instances[0]
    for (const topic of SERVER_EVENT_TOPICS) {
      source.emit(topic)
    }

    expect(received).toEqual(SERVER_EVENT_TOPICS)
  })

  it('ignores event types it did not subscribe to', () => {
    const onTopic = vi.fn()
    disconnect = connectServerEvents(onTopic)

    MockEventSource.instances[0].emit('unknown-topic')

    expect(onTopic).not.toHaveBeenCalled()
  })

  it('closes the underlying stream on cleanup', () => {
    disconnect = connectServerEvents(() => {})
    const source = MockEventSource.instances[0]

    disconnect()
    disconnect = null

    expect(source.closed).toBe(true)
  })
})

describe('connection state', () => {
  it('starts disconnected', () => {
    expect(isServerEventsConnected()).toBe(false)
  })

  it('flips on open, off on error, and notifies subscribers once per change', () => {
    const listener = vi.fn()
    const unsubscribe = subscribeServerEventsState(listener)

    disconnect = connectServerEvents(() => {})
    const source = MockEventSource.instances[0]

    source.onopen?.()
    expect(isServerEventsConnected()).toBe(true)
    expect(listener).toHaveBeenCalledTimes(1)

    // Duplicate open must not re-notify (useSyncExternalStore contract).
    source.onopen?.()
    expect(listener).toHaveBeenCalledTimes(1)

    source.onerror?.()
    expect(isServerEventsConnected()).toBe(false)
    expect(listener).toHaveBeenCalledTimes(2)

    unsubscribe()
  })

  it('reports disconnected after cleanup even if the stream was open', () => {
    disconnect = connectServerEvents(() => {})
    MockEventSource.instances[0].onopen?.()
    expect(isServerEventsConnected()).toBe(true)

    disconnect()
    disconnect = null

    expect(isServerEventsConnected()).toBe(false)
  })

  it('stops notifying an unsubscribed listener', () => {
    const listener = vi.fn()
    const unsubscribe = subscribeServerEventsState(listener)
    unsubscribe()

    disconnect = connectServerEvents(() => {})
    MockEventSource.instances[0].onopen?.()

    expect(listener).not.toHaveBeenCalled()
  })
})
