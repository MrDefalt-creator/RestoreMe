import axios from 'axios'

export type ApiError = {
  message: string
  status: number | null
  details?: unknown
}

export class UnsupportedApiError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'UnsupportedApiError'
  }
}

export function normalizeApiError(error: unknown): ApiError {
  if (axios.isAxiosError(error)) {
    if (!error.response) {
      return {
        message:
          'Network error. Ensure the API is running and the development certificate for https://localhost:7104 is trusted.',
        status: null,
        details: error.message,
      }
    }

    return {
      message:
        (error.response?.data as { message?: string } | undefined)?.message ??
        (typeof error.response?.data === 'string' ? error.response.data : undefined) ??
        error.message,
      status: error.response?.status ?? null,
      details: error.response?.data,
    }
  }

  if (error instanceof Error) {
    return {
      message: error.message,
      status: null,
    }
  }

  return {
    message: 'Unexpected error',
    status: null,
    details: error,
  }
}
