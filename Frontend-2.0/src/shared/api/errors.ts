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
    const responseMessage = readResponseMessage(error.response.data)
    return new ApiError(responseMessage ? localizeServerMessage(responseMessage) : messageForStatus(status), status, error.response.data)
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
  const language = getCurrentLanguage()
  return messages[language][key]
}

function getCurrentLanguage(): keyof typeof messages {
  return typeof window !== 'undefined' && window.localStorage.getItem('restoreme-language') === 'ru' ? 'ru' : 'en'
}

function localizeServerMessage(message: string): string {
  if (getCurrentLanguage() !== 'ru') {
    return message
  }

  const exactMessage = serverMessagesRu[message]
  if (exactMessage) {
    return exactMessage
  }

  if (/^Job with id .+ does not exist$/i.test(message)) {
    return 'Задание не найдено.'
  }

  if (/^Agent with id .+ does not exist$/i.test(message)) {
    return 'Агент не найден.'
  }

  if (/^Policy with id .+ does not exist$/i.test(message)) {
    return 'Политика не найдена.'
  }

  if (/^Artifact with id .+ does not exist$/i.test(message)) {
    return 'Артефакт не найден.'
  }

  if (/^Unsupported user role/i.test(message)) {
    return 'Эта роль пользователя не поддерживается.'
  }

  if (/^Unsupported policy type/i.test(message)) {
    return 'Этот тип политики не поддерживается.'
  }

  return message
}

const serverMessagesRu: Record<string, string> = {
  'Invalid username or password.': 'Неверный логин или пароль.',
  'Current password is incorrect.': 'Текущий пароль указан неверно.',
  'User with the same username already exists.': 'Пользователь с таким логином уже существует.',
  'User not found.': 'Пользователь не найден.',
  'At least one active administrator must remain in the system.': 'В системе должен остаться хотя бы один активный администратор.',
  'You cannot change the role of the current signed-in account.': 'Нельзя изменить роль текущей учетной записи.',
  'You cannot change the status of the current signed-in account.': 'Нельзя изменить статус текущей учетной записи.',
  'You cannot delete the current signed-in account.': 'Нельзя удалить текущую учетную запись.',
  'Agent already exists': 'Агент уже существует.',
  'Agent not found': 'Агент не найден.',
  'Pending agent not found': 'Заявка агента не найдена.',
  "This agent doesn't exist": 'Агент не найден.',
  'Approved agent requests cannot be rejected.': 'Уже подтвержденную заявку агента нельзя отклонить.',
  'Policy with the same name already exists for this agent.': 'У этого агента уже есть политика с таким названием.',
  'Policy not found': 'Политика не найдена.',
  'Source path is required for filesystem policies.': 'Для файловой политики нужно указать исходный путь.',
  'Database settings are required for logical database backup policies.': 'Для политики дампа базы данных нужно заполнить настройки базы.',
  'Database name is required for logical database backup policies.': 'Для политики дампа базы данных нужно указать имя базы.',
  'MySQL logical backups currently require credentials authentication mode.': 'Для MySQL-дампов сейчас требуется режим логина и пароля.',
  'Username is required when credentials authentication mode is selected.': 'Для выбранного режима авторизации нужно указать логин.',
  'Password is required when credentials authentication mode is selected.': 'Для выбранного режима авторизации нужно указать пароль.',
  'PostgreSQL policy type requires PostgreSQL database settings.': 'Для PostgreSQL-политики нужны настройки PostgreSQL.',
  'MySQL policy type requires MySQL database settings.': 'Для MySQL-политики нужны настройки MySQL.',
  'Backup job not found.': 'Задание резервного копирования не найдено.',
  'Backup job does not belong to policy.': 'Задание не относится к этой политике.',
  'Backup job is not running.': 'Задание сейчас не выполняется.',
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
