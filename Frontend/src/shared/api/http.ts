import axios from 'axios'

import { env } from '@/shared/config/env'
import { normalizeApiError } from '@/shared/api/errors'

export const http = axios.create({
  baseURL: env.apiBaseUrl,
  timeout: 10_000,
})

http.interceptors.response.use(
  (response) => response,
  (error) => Promise.reject(normalizeApiError(error)),
)
