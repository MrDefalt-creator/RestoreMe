import axios from 'axios'

export class ApiError extends Error {
  status: number | null
  details?: unknown

  constructor(message: string, status: number | null, details?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.details = details
  }
}

export function normalizeApiError(error: unknown): ApiError {
  if (axios.isAxiosError(error)) {
    if (!error.response) {
      return new ApiError(messageFor('network'), null, error.message)
    }

    const status = error.response.status ?? null
    return new ApiError(readResponseMessage(error.response.data) ?? messageForStatus(status), status, error.response.data)
  }

  if (error instanceof Error) {
    return new ApiError(error.message, null)
  }

  return new ApiError(messageFor('unexpected'), null, error)
}

function readResponseMessage(data: unknown): string | null {
  if (!data) {
    return null
  }

  if (typeof data === 'string') {
    return data.trim() || null
  }

  if (typeof data !== 'object') {
    return null
  }

  const payload = data as {
    message?: unknown
    title?: unknown
    detail?: unknown
    errors?: Record<string, string[] | string> | string[]
  }

  const directMessage = firstText(payload.message) ?? firstText(payload.detail) ?? firstText(payload.title)
  if (directMessage) {
    return directMessage
  }

  if (Array.isArray(payload.errors)) {
    return payload.errors.map(firstText).find(Boolean) ?? null
  }

  if (payload.errors && typeof payload.errors === 'object') {
    return Object.values(payload.errors)
      .flatMap((value) => (Array.isArray(value) ? value : [value]))
      .map(firstText)
      .find(Boolean) ?? null
  }

  return null
}

function firstText(value: unknown): string | null {
  return typeof value === 'string' && value.trim() ? value.trim() : null
}

function messageForStatus(status: number | null): string {
  switch (status) {
    case 400:
      return messageFor('badRequest')
    case 401:
      return messageFor('unauthorized')
    case 403:
      return messageFor('forbidden')
    case 404:
      return messageFor('notFound')
    case 409:
      return messageFor('conflict')
    case 422:
      return messageFor('validation')
    case 500:
    case 502:
    case 503:
    case 504:
      return messageFor('server')
    default:
      return messageFor('unexpected')
  }
}

function messageFor(key: keyof typeof messages.en): string {
  const language = typeof window !== 'undefined' && window.localStorage.getItem('restoreme-language') === 'ru' ? 'ru' : 'en'
  return messages[language][key]
}

const messages = {
  en: {
    badRequest: 'Check the entered data and try again.',
    conflict: 'The operation conflicts with the current server state. Refresh the data and try again.',
    forbidden: 'You do not have permission to perform this action.',
    network: 'Cannot reach the RestoreMe API. Check that the server is running and reachable.',
    notFound: 'The requested object was not found. Refresh the page and try again.',
    server: 'The server could not complete the request. Check backend logs if the problem repeats.',
    unauthorized: 'Sign in again or check the entered credentials.',
    unexpected: 'Something went wrong while contacting the RestoreMe API.',
    validation: 'Check the highlighted fields and try again.',
  },
  ru: {
    badRequest: 'Проверьте введенные данные и повторите попытку.',
    conflict: 'Операция конфликтует с текущим состоянием сервера. Обновите данные и попробуйте снова.',
    forbidden: 'У этой учетной записи нет прав на выполнение действия.',
    network: 'Не удалось подключиться к API RestoreMe. Проверьте, что сервер запущен и доступен.',
    notFound: 'Запрошенный объект не найден. Обновите страницу и попробуйте снова.',
    server: 'Сервер не смог выполнить запрос. Если ошибка повторится, проверьте логи backend.',
    unauthorized: 'Войдите заново или проверьте введенные учетные данные.',
    unexpected: 'При обращении к API RestoreMe произошла ошибка.',
    validation: 'Проверьте заполненные поля и попробуйте снова.',
  },
}
