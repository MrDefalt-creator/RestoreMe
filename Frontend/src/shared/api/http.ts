import axios from 'axios'

import { useAuthStore } from '@/app/store/auth-store'
import { env } from '@/shared/config/env'
import { normalizeApiError } from '@/shared/api/errors'

export const http = axios.create({
  baseURL: env.apiBaseUrl,
  timeout: 10_000,
})

http.interceptors.request.use((config) => {
  const accessToken = useAuthStore.getState().accessToken

  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`
  }

  return config
})

http.interceptors.response.use(
  (response) => response,
  (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      useAuthStore.getState().clearSession()

      if (typeof window !== 'undefined' && window.location.pathname !== '/login') {
        window.location.assign('/login')
      }
    }

    return Promise.reject(normalizeApiError(error))
  },
)
