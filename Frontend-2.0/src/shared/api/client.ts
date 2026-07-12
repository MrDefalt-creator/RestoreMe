import axios, { type AxiosInstance, type AxiosError, type InternalAxiosRequestConfig } from 'axios'

import { useAuthStore } from '@/app/store/auth-store'
import { normalizeApiError } from '@/shared/api/errors'
import { authEvents } from '@/shared/lib/auth-events'
import { env } from '@/shared/config/env'

const apiClient: AxiosInstance = axios.create({
  baseURL: env.apiBaseUrl,
  timeout: 30000,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
})

// Single-flight refresh: many requests can 401 at once when the short-lived
// access token expires, but they must trigger exactly ONE POST /api/auth/refresh
// and then all retry. Concurrent callers await the same in-flight promise.
let refreshPromise: Promise<void> | null = null

function refreshOnce(): Promise<void> {
  if (!refreshPromise) {
    refreshPromise = apiClient
      .post('/api/auth/refresh')
      .then(() => undefined)
      .finally(() => {
        refreshPromise = null
      })
  }
  return refreshPromise
}

// The auth endpoints that must never themselves be retried-after-refresh:
// refreshing on a failed login/refresh would loop.
function isAuthBypass(url?: string): boolean {
  if (!url) return false
  return url.includes('/api/auth/refresh') || url.includes('/api/auth/login')
}

function failSession() {
  useAuthStore.getState().clearSession()
  authEvents.emitUnauthorized('session_expired')
}

type RetryableConfig = InternalAxiosRequestConfig & { _retry?: boolean }

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetryableConfig | undefined
    const status = error.response?.status
    const hasSession = Boolean(useAuthStore.getState().user)

    // A 401 on a normal endpoint while we believe we have a session: try a
    // single refresh, then replay the original request exactly once.
    if (status === 401 && hasSession && config && !config._retry && !isAuthBypass(config.url)) {
      config._retry = true
      try {
        await refreshOnce()
      } catch {
        // Refresh itself failed — the session is genuinely gone.
        failSession()
        return Promise.reject(normalizeApiError(error))
      }
      return apiClient(config)
    }

    // A 401 that survived a retry (refresh succeeded but we're still
    // unauthorized) is a terminal session expiry. Auth-bypass endpoints are
    // left to their callers; refresh failures are handled in the branch above.
    if (status === 401 && hasSession && config?._retry) {
      failSession()
    }

    return Promise.reject(normalizeApiError(error))
  },
)

export default apiClient
