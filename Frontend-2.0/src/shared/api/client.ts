import axios, { type AxiosInstance, type AxiosError } from 'axios'

import { useAuthStore } from '@/app/store/auth-store'
import { normalizeApiError } from '@/shared/api/errors'

const apiClient: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
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
