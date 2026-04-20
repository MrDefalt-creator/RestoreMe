import { z } from 'zod'

const envSchema = z.object({
  VITE_API_BASE_URL: z.url().default('https://localhost:7104'),
  VITE_API_MODE: z.enum(['mock', 'live']).default('live'),
})

const parsed = envSchema.safeParse({
  VITE_API_BASE_URL: import.meta.env.VITE_API_BASE_URL,
  VITE_API_MODE: import.meta.env.VITE_API_MODE,
})

const fallback = {
  VITE_API_BASE_URL: 'https://localhost:7104',
  VITE_API_MODE: 'live' as const,
}

const values = parsed.success ? parsed.data : fallback

export const env = {
  apiBaseUrl: values.VITE_API_BASE_URL,
  apiMode: values.VITE_API_MODE,
  isProduction: import.meta.env.PROD,
}
