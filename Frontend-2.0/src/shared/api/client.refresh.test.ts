import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import axios, { type AxiosAdapter, type AxiosResponse } from 'axios'

import apiClient from './client'
import { useAuthStore } from '@/app/store/auth-store'
import type { User } from './auth'

const testUser: User = {
  id: 'u1',
  username: 'tester',
  role: 'admin',
  isActive: true,
}

function okResponse(config: Parameters<AxiosAdapter>[0], data: unknown): AxiosResponse {
  return {
    data,
    status: 200,
    statusText: 'OK',
    headers: {},
    config,
    request: {},
  }
}

function unauthorized(config: Parameters<AxiosAdapter>[0]): never {
  throw new axios.AxiosError('Unauthorized', 'ERR_BAD_REQUEST', config, {}, {
    data: {},
    status: 401,
    statusText: 'Unauthorized',
    headers: {},
    config,
  } as AxiosResponse)
}

let originalAdapter: AxiosAdapter | undefined

beforeEach(() => {
  originalAdapter = apiClient.defaults.adapter as AxiosAdapter | undefined
  useAuthStore.setState({ user: testUser })
})

afterEach(() => {
  apiClient.defaults.adapter = originalAdapter
  useAuthStore.setState({ user: null })
  localStorage.clear()
  sessionStorage.clear()
})

describe('client 401 refresh interceptor', () => {
  it('refreshes once for N concurrent 401s, then retries each request', async () => {
    let refreshCount = 0
    let refreshed = false

    apiClient.defaults.adapter = async (config) => {
      const url = config.url ?? ''
      if (url.includes('/api/auth/refresh')) {
        refreshCount += 1
        refreshed = true
        return okResponse(config, { refreshed: true })
      }
      if (!refreshed) {
        unauthorized(config)
      }
      return okResponse(config, { url })
    }

    const [a, b, c] = await Promise.all([
      apiClient.get('/api/jobs'),
      apiClient.get('/api/agents'),
      apiClient.get('/api/policies'),
    ])

    expect(refreshCount).toBe(1)
    expect(a.data).toEqual({ url: '/api/jobs' })
    expect(b.data).toEqual({ url: '/api/agents' })
    expect(c.data).toEqual({ url: '/api/policies' })
    expect(useAuthStore.getState().user).not.toBeNull()
  })

  it('clears the session when the retry still 401s after a successful refresh', async () => {
    apiClient.defaults.adapter = async (config) => {
      const url = config.url ?? ''
      if (url.includes('/api/auth/refresh')) {
        return okResponse(config, {})
      }
      unauthorized(config) // protected endpoint never recovers
    }

    await expect(apiClient.get('/api/jobs')).rejects.toBeDefined()
    expect(useAuthStore.getState().user).toBeNull()
  })

  it('clears the session when the refresh itself 401s', async () => {
    apiClient.defaults.adapter = async (config) => {
      unauthorized(config) // both the protected call and the refresh fail
    }

    await expect(apiClient.get('/api/jobs')).rejects.toBeDefined()
    expect(useAuthStore.getState().user).toBeNull()
  })

  it('does not attempt a refresh when there is no session', async () => {
    useAuthStore.setState({ user: null })
    let refreshCount = 0

    apiClient.defaults.adapter = async (config) => {
      const url = config.url ?? ''
      if (url.includes('/api/auth/refresh')) {
        refreshCount += 1
        return okResponse(config, {})
      }
      unauthorized(config)
    }

    await expect(apiClient.get('/api/jobs')).rejects.toBeDefined()
    expect(refreshCount).toBe(0)
  })
})
