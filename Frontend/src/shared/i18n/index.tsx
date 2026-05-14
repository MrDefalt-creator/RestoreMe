/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, useEffect, useMemo, useState, type PropsWithChildren } from 'react'

export type Language = 'en' | 'ru'
export type DateStyle = 'regional' | 'compact'
export type RefreshInterval = 'manual' | '5s' | '10s' | '30s' | '60s'

const storageKey = 'restoreme-language'
const dateStyleStorageKey = 'restoreme-date-style'
const refreshIntervalStorageKey = 'restoreme-refresh-interval'

const dictionaries: Record<Language, Record<string, string>> = {
  en: {},
  ru: {
    Account: 'Аккаунт',
    Actions: 'Действия',
    Active: 'Активен',
    'Active policies': 'Активные политики',
    'Active users': 'Активные пользователи',
    Admin: 'Администратор',
    Administrator: 'Администратор',
    'Administrator access required': 'Требуется доступ администратора',
    Admissions: 'Допуск',
    Agent: 'Агент',
    'Agent name': 'Имя агента',
    Agents: 'Агенты',
    Artifacts: 'Артефакты',
    'Backup Console': 'Консоль резервного копирования',
    'Backup execution history': 'История резервного копирования',
    'Backup operations': 'Операции резервного копирования',
    'Backup scheduling rules': 'Расписания резервного копирования',
    Cancel: 'Отмена',
    'Change password': 'Смена пароля',
    'Change user password': 'Сменить пароль пользователя',
    Checksum: 'Контрольная сумма',
    Completed: 'Завершено',
    'Confirm new password': 'Подтвердите новый пароль',
    'Confirm password': 'Подтвердите пароль',
    Created: 'Создано',
    'Create platform user': 'Создать пользователя платформы',
    'Create policy': 'Создать политику',
    'Create user': 'Создать пользователя',
    'Current session': 'Текущая сессия',
    'Current password': 'Текущий пароль',
    Dashboard: 'Панель',
    Delete: 'Удалить',
    'Delete user': 'Удалить пользователя',
    Disabled: 'Отключен',
    'Disabled only': 'Только отключенные',
    Download: 'Скачать',
    Edit: 'Редактировать',
    Enabled: 'Включено',
    'Enabled only': 'Только включенные',
    Error: 'Ошибка',
    Failed: 'Ошибка',
    'File name': 'Имя файла',
    Filesystem: 'Файловая система',
    'Go to dashboard': 'К панели',
    Host: 'Хост',
    Identity: 'Профиль',
    'Identity and password': 'Профиль и пароль',
    Interval: 'Интервал',
    Job: 'Задание',
    Jobs: 'Задания',
    Language: 'Язык',
    'Language settings': 'Настройки языка',
    Machine: 'Машина',
    Name: 'Имя',
    'New password': 'Новый пароль',
    'No agents found': 'Агенты не найдены',
    'No artifacts found': 'Артефакты не найдены',
    'No jobs found': 'Задания не найдены',
    'No jobs yet': 'Заданий пока нет',
    'No policies': 'Политик нет',
    'No policies found': 'Политики не найдены',
    'No recent failures': 'Недавних ошибок нет',
    'No user accounts found': 'Пользователи не найдены',
    Operator: 'Оператор',
    'Operations overview': 'Обзор операций',
    Password: 'Пароль',
    Pending: 'Ожидание',
    'Pending agent requests': 'Заявки агентов',
    'Pending approvals': 'Заявки на подтверждение',
    Policies: 'Политики',
    Policy: 'Политика',
    Preparing: 'Подготовка',
    'Protected account': 'Защищенный аккаунт',
    'Recent backup jobs': 'Последние задания',
    'Recent failures': 'Последние ошибки',
    'Recent activity': 'Последняя активность',
    'Registered agents': 'Зарегистрированные агенты',
    'Registered machines': 'Зарегистрированные машины',
    Remember: 'Запомнить',
    'Remember me on this device': 'Запомнить меня на этом устройстве',
    Role: 'Роль',
    'Role model': 'Ролевая модель',
    Running: 'Выполняется',
    Save: 'Сохранить',
    'Save changes': 'Сохранить изменения',
    'Save password': 'Сохранить пароль',
    'Saving...': 'Сохранение...',
    'Search by agent, machine or OS': 'Поиск по агенту, машине или ОС',
    'Search by filename, key, checksum': 'Поиск по имени файла, ключу или сумме',
    'Search by id, error, agent or policy': 'Поиск по id, ошибке, агенту или политике',
    'Search policy, target, type or agent': 'Поиск по политике, цели, типу или агенту',
    Security: 'Безопасность',
    'Selected agent': 'Выбранный агент',
    'Sign in': 'Войти',
    'Sign out': 'Выйти',
    Size: 'Размер',
    Stale: 'Устарел',
    Status: 'Статус',
    'Stored backup objects': 'Сохраненные резервные копии',
    Target: 'Цель',
    Toggle: 'Переключить',
    Type: 'Тип',
    'Update password': 'Обновить пароль',
    Users: 'Пользователи',
    Username: 'Логин',
    Viewer: 'Наблюдатель',
    Version: 'Версия',
    'View and manage backup schedules': 'Просматривайте расписания резервного копирования и управляйте ими',
    'User access management': 'Управление доступом пользователей',
    'Unknown agent': 'Неизвестный агент',
    'Unknown policy': 'Неизвестная политика',
    'Unknown user': 'Неизвестный пользователь',
    'Read only': 'Только чтение',
    'Signed in': 'Выполнен вход',
    'Signed in successfully': 'Вход выполнен',
    'Password updated': 'Пароль обновлен',
    'Unable to update password': 'Не удалось обновить пароль',
    'Password must contain at least 8 characters': 'Пароль должен содержать минимум 8 символов',
    'Password is too long': 'Пароль слишком длинный',
    'Passwords do not match': 'Пароли не совпадают',
    'Current password is required': 'Введите текущий пароль',
    'Confirm the new password': 'Подтвердите новый пароль',
    'Username is required': 'Введите логин',
    'Password is required': 'Введите пароль',
    'Sign in failed': 'Не удалось войти',
    'Operational workspace for agents, policies and backup history.': 'Рабочее пространство для агентов, политик и истории резервных копий.',
    'Local data mode': 'Локальный режим данных',
    'Showing local sample data until the live API connection is available.': 'Показываются локальные тестовые данные, пока live API недоступен.',
    'Secure operator console for RestoreMe backup operations.': 'Безопасная консоль RestoreMe для операторов резервного копирования.',
    'This workspace is reserved for administrators who manage operator and viewer access.': 'Этот раздел доступен администраторам, которые управляют доступом операторов и наблюдателей.',
    'Sign in as an administrator to create users, change roles and disable stale accounts.': 'Войдите как администратор, чтобы создавать пользователей, менять роли и отключать устаревшие аккаунты.',
    'Issue operator and viewer accounts, adjust privileges, rotate passwords and remove stale access without exposing backup agents or policy controls to every user.': 'Создавайте аккаунты операторов и наблюдателей, меняйте права, обновляйте пароли и убирайте лишний доступ без передачи управления агентами всем пользователям.',
    Accounts: 'Аккаунты',
    'Platform identities seeded or created for the control plane.': 'Учетные записи, созданные или добавленные для панели управления.',
    'Accounts that can currently access the administrative workspace.': 'Аккаунты, которые сейчас могут входить в административное пространство.',
    'Admins manage access, operators manage agents and policies, viewers keep read-only visibility.': 'Администраторы управляют доступом, операторы работают с агентами и политиками, наблюдатели видят данные только на чтение.',
    'Use the Account page to change your own password.': 'Используйте страницу аккаунта, чтобы изменить собственный пароль.',
    Disable: 'Отключить',
    Enable: 'Включить',
    'Create the first operator, viewer or administrator account for the secured control plane.': 'Создайте первый аккаунт оператора, наблюдателя или администратора для защищенной консоли.',
    'Policy state updated': 'Состояние политики обновлено',
    'Unable to toggle policy': 'Не удалось переключить политику',
    'Create filesystem backup schedules or logical database dump policies without losing visibility into the target agent and next execution window.': 'Создавайте файловые расписания и логические дампы БД, сохраняя видимость целевого агента и следующего запуска.',
    'All policies': 'Все политики',
    enabled: 'включено',
    disabled: 'отключено',
    'Adjust the filter or create the first filesystem or database backup policy for an approved agent.': 'Измените фильтр или создайте первую политику для файловой системы либо базы данных.',
    Approve: 'Подтвердить',
    'Approve agent': 'Подтвердить агента',
    'Approve pending agent': 'Подтвердить заявку агента',
    'Approving...': 'Подтверждение...',
    'Pending agent approved': 'Заявка агента подтверждена',
    'Approval failed': 'Не удалось подтвердить агента',
    'Accounting workstation': 'Рабочая станция бухгалтерии',
    'Assign the machine a readable agent name before it becomes visible in the operational workspace.': 'Задайте машине понятное имя агента, прежде чем она появится в рабочей области.',
    'Queue is empty': 'Очередь пуста',
    'There are no pending registration requests right now.': 'Сейчас нет заявок на регистрацию.',
    'Review machine registrations before they become manageable backup agents in the admin panel.': 'Проверяйте новые машины до того, как они станут управляемыми агентами.',
    'Review resulting files, object keys and checksums generated by backup jobs before wiring restore flows.': 'Проверяйте файлы, объектные ключи и контрольные суммы перед настройкой сценариев восстановления.',
    'The current dataset has no backup objects matching the active filter.': 'В текущих данных нет объектов резервных копий под активный фильтр.',
    'Secure operator access for agents, backup policies, jobs and stored artifacts.': 'Безопасный доступ к агентам, политикам, заданиям и сохраненным артефактам.',
    'Use your assigned operator or admin account to enter the console.': 'Используйте выданный аккаунт оператора или администратора для входа в консоль.',
    'The development profile can bootstrap demo users through backend configuration.': 'Профиль разработки может создать демо-пользователей через конфигурацию backend.',
    'Enter password': 'Введите пароль',
    'Review the active console identity and change the current password without involving another administrator.': 'Проверьте активный профиль консоли и смените текущий пароль без участия другого администратора.',
    'This view is available to administrators, operators and viewers. Only the current password is accepted for self-service password rotation.': 'Раздел доступен администраторам, операторам и наблюдателям. Для смены пароля нужен текущий пароль.',
    'Change your password': 'Изменить пароль',
    'Enter current password': 'Введите текущий пароль',
    'Choose a stronger password': 'Выберите более надежный пароль',
    'Repeat the new password': 'Повторите новый пароль',
    'Choose the interface language for this browser.': 'Выберите язык интерфейса для этого браузера.',
    'Add an operator, viewer or administrator account for the secure RestoreMe control plane.': 'Добавьте аккаунт оператора, наблюдателя или администратора для защищенной панели RestoreMe.',
    Creating: 'Создание',
    'Creating...': 'Создание...',
    'User created': 'Пользователь создан',
    'Unable to create user': 'Не удалось создать пользователя',
    'Set a new password for {username}.': 'Задайте новый пароль для {username}.',
    'Set a new password for the selected user.': 'Задайте новый пароль для выбранного пользователя.',
    'Edit backup policy': 'Редактировать политику резервного копирования',
    'Policy updated': 'Политика обновлена',
    'Policy created': 'Политика создана',
    'Policy save failed': 'Не удалось сохранить политику',
    'Policies can protect file paths or create logical database dumps through the same scheduling flow.': 'Политики могут защищать файловые пути или создавать логические дампы баз данных в одном расписании.',
    'Policy type': 'Тип политики',
    'Policy name': 'Название политики',
    'Filesystem backup': 'Файловая копия',
    'PostgreSQL logical dump': 'Логический дамп PostgreSQL',
    'MySQL logical dump': 'Логический дамп MySQL',
    'PostgreSQL dump': 'Дамп PostgreSQL',
    'MySQL dump': 'Дамп MySQL',
    'Documents every 15 minutes': 'Документы каждые 15 минут',
    Minutes: 'Минуты',
    Hours: 'Часы',
    Days: 'Дни',
    Seconds: 'Секунды',
    'Source path': 'Исходный путь',
    'Database name': 'Имя базы данных',
    Port: 'Порт',
    'Auth mode': 'Режим авторизации',
    'Integrated / local': 'Интегрированная / локальная',
    'Username + password': 'Логин + пароль',
    'Enter database password': 'Введите пароль базы данных',
    'Enable scheduling immediately': 'Включить расписание сразу',
    'Recommended: use integrated/local auth when pg_dump can access the database without storing a password in the policy. Credentials mode stays available as the universal fallback.': 'Рекомендуется использовать интегрированную или локальную авторизацию, если pg_dump может обращаться к базе без хранения пароля в политике. Режим логина и пароля остается универсальным запасным вариантом.',
    'MySQL uses credentials mode in this first iteration. Make sure mysqldump is installed on the agent machine.': 'В этой версии MySQL использует режим логина и пароля. Убедитесь, что mysqldump установлен на машине агента.',
    'Interface preferences': 'Настройки интерфейса',
    'Tune the console to the way you read operational data.': 'Настройте консоль так, чтобы оперативные данные читались быстрее.',
    'Date and time format': 'Формат даты и времени',
    'Regional format': 'Региональный формат',
    'Compact format': 'Компактный формат',
    'Console status': 'Состояние консоли',
    'Data source': 'Источник данных',
    'Live API': 'Live API',
    'Local demo data': 'Локальные демо-данные',
    'Refresh cadence': 'Частота обновления',
    'Every 10 seconds in live mode': 'Каждые 10 секунд в live-режиме',
    'Manual while using demo data': 'Вручную при демо-данных',
    'Current language': 'Текущий язык',
    'Session role': 'Роль сессии',
    'Data refresh': 'Обновление данных',
    'Auto-refresh cadence': 'Частота автообновления',
    'Choose how often live pages ask the API for fresh data.': 'Выберите, как часто live-страницы будут запрашивать свежие данные.',
    'Applies to dashboard, agents, approvals, policies, jobs, and backups.': 'Применяется к панели, агентам, подтверждениям, политикам, заданиям и копиям.',
    'Manual refresh only': 'Только вручную',
    'Every 5 seconds': 'Каждые 5 секунд',
    'Every 10 seconds': 'Каждые 10 секунд',
    'Every 30 seconds': 'Каждые 30 секунд',
    'Every minute': 'Каждую минуту',
    'Live data': 'Live-данные',
    'Demo data': 'Демо-данные',
    'Track machine identity, heartbeat freshness and the policies attached to every approved agent.': 'Отслеживайте машину, свежесть heartbeat и политики каждого подтвержденного агента.',
    'Last heartbeat': 'Последний heartbeat',
    'Attached policies': 'Назначенные политики',
    'The selected agent does not have backup policies yet.': 'У выбранного агента пока нет политик резервного копирования.',
    'Recent jobs': 'Недавние задания',
    'This agent has not produced any backup history in the current dataset.': 'У этого агента пока нет истории резервного копирования.',
    'Try another search term or approve a pending machine first.': 'Попробуйте другой поисковый запрос или сначала подтвердите ожидающую машину.',
    'Use the dashboard to monitor machine health, pending approvals and the latest backup activity before diving into entity-specific workflows.': 'Используйте панель, чтобы быстро оценить состояние машин, заявки и последнюю активность.',
    '{online} online / {stale} stale / {offline} offline': '{online} онлайн / {stale} без свежего heartbeat / {offline} офлайн',
    'Registration requests waiting for review': 'Заявки на регистрацию ждут проверки',
    'Enabled schedules across all machines': 'Включенные расписания по всем машинам',
    'Latest failed jobs surfaced for quick triage': 'Последние ошибки вынесены для быстрой диагностики',
    'Latest execution attempts across agents and policies.': 'Последние попытки выполнения по агентам и политикам.',
    Started: 'Запущено',
    'Latest backup errors remain visible here for quick review.': 'Последние ошибки остаются здесь, чтобы их было проще разобрать.',
    'The latest jobs completed without backup errors.': 'Последние задания завершились без ошибок резервного копирования.',
    'Loading overview': 'Загрузка обзора',
    'Summary metrics will appear as soon as the first data source responds.': 'Сводные метрики появятся после ответа первого источника данных.',
    'Inspect job status, completion time and the last known failure reason without losing the surrounding queue context.': 'Проверяйте статус, время завершения и последнюю причину ошибки, не теряя контекст очереди.',
    'All jobs': 'Все задания',
    'Job details': 'Детали задания',
    'Job id': 'ID задания',
    'Error message': 'Сообщение об ошибке',
    'No error message recorded for this job.': 'Для этого задания сообщение об ошибке не записано.',
    'Adjust the current filter or wait for the next backup execution to arrive.': 'Измените текущий фильтр или дождитесь следующего запуска резервного копирования.',
  },
}

const languageLabels: Record<Language, string> = {
  en: 'English',
  ru: 'Русский',
}

function normalizeLanguage(value: string | null): Language {
  return value === 'ru' ? 'ru' : 'en'
}

function normalizeDateStyle(value: string | null): DateStyle {
  return value === 'compact' ? 'compact' : 'regional'
}

function normalizeRefreshInterval(value: string | null): RefreshInterval {
  return value === 'manual' || value === '5s' || value === '30s' || value === '60s' ? value : '10s'
}

export function getStoredLanguage(): Language {
  if (typeof window === 'undefined') {
    return 'en'
  }

  return normalizeLanguage(window.localStorage.getItem(storageKey))
}

export function getStoredDateStyle(): DateStyle {
  if (typeof window === 'undefined') {
    return 'regional'
  }

  return normalizeDateStyle(window.localStorage.getItem(dateStyleStorageKey))
}

export function getStoredRefreshInterval(): RefreshInterval {
  if (typeof window === 'undefined') {
    return '10s'
  }

  return normalizeRefreshInterval(window.localStorage.getItem(refreshIntervalStorageKey))
}

export function getRefreshIntervalMs(refreshInterval = getStoredRefreshInterval()): number | false {
  switch (refreshInterval) {
    case 'manual':
      return false
    case '5s':
      return 5_000
    case '30s':
      return 30_000
    case '60s':
      return 60_000
    default:
      return 10_000
  }
}

export function getCurrentLocaleCode() {
  return getStoredLanguage() === 'ru' ? 'ru' : 'en-US'
}

type I18nContextValue = {
  language: Language
  languageLabel: string
  dateStyle: DateStyle
  refreshInterval: RefreshInterval
  refreshIntervalMs: number | false
  setDateStyle: (dateStyle: DateStyle) => void
  setLanguage: (language: Language) => void
  setRefreshInterval: (refreshInterval: RefreshInterval) => void
  t: (key: string, params?: Record<string, string | number>) => string
}

const I18nContext = createContext<I18nContextValue | null>(null)

function interpolate(value: string, params?: Record<string, string | number>) {
  if (!params) {
    return value
  }

  return Object.entries(params).reduce(
    (text, [key, replacement]) => text.split(`{${key}}`).join(String(replacement)),
    value,
  )
}

export function I18nProvider({ children }: PropsWithChildren) {
  const [language, setLanguageState] = useState<Language>(() => getStoredLanguage())
  const [dateStyle, setDateStyleState] = useState<DateStyle>(() => getStoredDateStyle())
  const [refreshInterval, setRefreshIntervalState] = useState<RefreshInterval>(() => getStoredRefreshInterval())

  useEffect(() => {
    document.documentElement.lang = language
    window.localStorage.setItem(storageKey, language)
  }, [language])

  useEffect(() => {
    window.localStorage.setItem(dateStyleStorageKey, dateStyle)
  }, [dateStyle])

  useEffect(() => {
    window.localStorage.setItem(refreshIntervalStorageKey, refreshInterval)
  }, [refreshInterval])

  const value = useMemo<I18nContextValue>(() => ({
    language,
    languageLabel: languageLabels[language],
    dateStyle,
    refreshInterval,
    refreshIntervalMs: getRefreshIntervalMs(refreshInterval),
    setDateStyle: setDateStyleState,
    setLanguage: setLanguageState,
    setRefreshInterval: setRefreshIntervalState,
    t: (key, params) => interpolate(dictionaries[language][key] ?? key, params),
  }), [dateStyle, language, refreshInterval])

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}

export function useI18n() {
  const value = useContext(I18nContext)

  if (!value) {
    throw new Error('useI18n must be used within I18nProvider')
  }

  return value
}

export function formatRoleLabel(role: string | undefined, t: (key: string) => string) {
  switch (role) {
    case 'admin':
      return t('Admin')
    case 'operator':
      return t('Operator')
    default:
      return t('Viewer')
  }
}

export const availableLanguages: { value: Language; label: string }[] = [
  { value: 'en', label: languageLabels.en },
  { value: 'ru', label: languageLabels.ru },
]

export const availableRefreshIntervals: { value: RefreshInterval; label: string }[] = [
  { value: 'manual', label: 'Manual refresh only' },
  { value: '5s', label: 'Every 5 seconds' },
  { value: '10s', label: 'Every 10 seconds' },
  { value: '30s', label: 'Every 30 seconds' },
  { value: '60s', label: 'Every minute' },
]
