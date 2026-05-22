import axios, { type AxiosInstance, type AxiosError } from 'axios'

import { useAuthStore } from '@/app/store/auth-store'
import { normalizeApiError } from '@/shared/api/errors'
import { env } from '@/shared/config/env'

const apiClient: AxiosInstance = axios.create({
  baseURL: env.apiBaseUrl,
  timeout: 30000,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
})

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response?.status === 401) {
      useAuthStore.getState().clearSession()
      if (window.location.pathname !== '/login') {
        window.location.assign('/login')
      }
    }
    return Promise.reject(normalizeApiError(error))
  },
)

export default apiClient
