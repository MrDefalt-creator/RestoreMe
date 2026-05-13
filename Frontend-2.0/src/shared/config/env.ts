export const env = {
  apiMode: import.meta.env.VITE_API_MODE || 'live',
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL || '/api',
  isLive: (import.meta.env.VITE_API_MODE || 'live') === 'live',
}
