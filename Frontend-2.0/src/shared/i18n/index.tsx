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
    Agent: 'Агент',
    'Agent health': 'Здоровье агентов',
    'Agent name': 'Имя агента',
    Agents: 'Агенты',
    Approvals: 'Подтверждения',
    Artifacts: 'Артефакты',
    Backups: 'Копии',
    'Backup activity trend': 'Динамика резервных копий',
    'Backup console': 'Консоль резервного копирования',
    'Backup Policies': 'Политики резервного копирования',
    'Backup protection, at a glance.': 'Защита резервных копий на одном экране.',
    'Calm backup confidence': 'Уверенность в резервных копиях без суеты',
    'Calm backup operations console': 'Спокойная консоль резервного копирования',
    Cancel: 'Отмена',
    'Change password': 'Смена пароля',
    'Completed backups will appear here as recoverable artifacts.': 'Завершенные копии появятся здесь как восстанавливаемые артефакты.',
    Completed: 'Завершено',
    'Confirm new password': 'Подтвердите новый пароль',
    Created: 'Создано',
    'Create backup policy': 'Создать политику резервного копирования',
    'Create policy': 'Создать политику',
    'Create user': 'Создать пользователя',
    'Current session': 'Текущая сессия',
    'Current password': 'Текущий пароль',
    Dark: 'Темная',
    'Dark theme': 'Темная тема',
    Dashboard: 'Панель',
    Delete: 'Удалить',
    'Delete user': 'Удалить пользователя',
    Disabled: 'Отключен',
    'Disabled only': 'Только отключенные',
    Edit: 'Редактировать',
    Enabled: 'Включено',
    'Enabled only': 'Только включенные',
    Failed: 'Ошибка',
    Filters: 'Фильтры',
    'Go back home': 'Вернуться на главную',
    'Identity and password': 'Профиль и пароль',
    Interval: 'Интервал',
    Jobs: 'Задания',
    Language: 'Язык',
    'Language settings': 'Настройки языка',
    Light: 'Светлая',
    'Light theme': 'Светлая тема',
    'Latest activity': 'Последняя активность',
    'Needs attention': 'Требует внимания',
    'New password': 'Новый пароль',
    'No agents found': 'Агенты не найдены',
    'No artifacts yet': 'Артефактов пока нет',
    'No jobs yet': 'Заданий пока нет',
    'No pending requests': 'Заявок нет',
    'No policies found': 'Политики не найдены',
    'No backup policies are available for the current filter.': 'По текущему фильтру политик резервного копирования нет.',
    Operator: 'Оператор',
    Overview: 'Обзор',
    Password: 'Пароль',
    Pending: 'Ожидает',
    'Pending Approvals': 'Заявки на подтверждение',
    Policies: 'Политики',
    Policy: 'Политика',
    'Policy types': 'Типы политик',
    'Protection mix': 'Структура защиты',
    'Recent policies': 'Последние политики',
    'Recoverable backups': 'Восстанавливаемые копии',
    'Recorded runs': 'Записанные запуски',
    Reject: 'Отклонить',
    'Reject agent': 'Отклонить агента',
    'Reject pending agent': 'Отклонить заявку агента',
    Rejected: 'Отклонен',
    'Remember me on this device': 'Запомнить меня на этом устройстве',
    Role: 'Роль',
    Running: 'Выполняется',
    'Run every': 'Запускать каждые',
    'Save changes': 'Сохранить изменения',
    'Save password': 'Сохранить пароль',
    'Search policies...': 'Поиск политик...',
    'Sign in': 'Войти',
    'Sign out': 'Выйти',
    'Signing in...': 'Вход...',
    Stale: 'Устарел',
    Status: 'Статус',
    'Stored data': 'Сохраненные данные',
    'Success ratio': 'Доля успешных',
    Toggle: 'Переключить',
    Type: 'Тип',
    'Update password': 'Обновить пароль',
    'Updating...': 'Обновление...',
    Users: 'Пользователи',
    Username: 'Логин',
    Viewer: 'Наблюдатель',
    'View and manage backup schedules': 'Просмотр и управление расписаниями резервного копирования',
    'Welcome, {username}!': 'Добро пожаловать, {username}!',
    'Everything looks calm': 'Все спокойно',
    'No visible issues require operator attention right now.': 'Сейчас нет видимых проблем, требующих внимания оператора.',
    'RestoreMe keeps the operational view calm: agents, policies, recent jobs and recoverable artifacts in one place.': 'RestoreMe держит операционную картину под контролем: агенты, политики, последние задания и восстанавливаемые артефакты собраны в одном месте.',
    'Use your operator account to manage backup protection.': 'Войдите под учетной записью оператора, чтобы управлять защитой резервных копий.',
    'A focused workspace for agents, policies, backup jobs and recovery artifacts.': 'Рабочее пространство для агентов, политик, заданий резервного копирования и артефактов восстановления.',
    'Designed for quiet, deliberate backup operations.': 'Создано для спокойной и точной работы с резервными копиями.',
    'Access the console': 'Войти в консоль',
    'Access the workspace': 'Войти в рабочее пространство',
    'Unknown agent': 'Неизвестный агент',
    'Unknown policy': 'Неизвестная политика',
    'Unknown user': 'Неизвестный пользователь',
    'Signed in': 'Выполнен вход',
    'Password updated': 'Пароль обновлен',
    'Unable to update password': 'Не удалось обновить пароль',
    'Password must contain at least 8 characters': 'Пароль должен содержать минимум 8 символов',
    'Password is too long': 'Пароль слишком длинный',
    'Passwords do not match': 'Пароли не совпадают',
    'Current password is required': 'Введите текущий пароль',
    'Confirm the new password': 'Подтвердите новый пароль',
    'Username must contain at least 3 characters': 'Логин должен содержать минимум 3 символа',
    'Username is too long': 'Логин слишком длинный',
    'Name is required': 'Введите название',
    'Name is too long': 'Название слишком длинное',
    'Select an agent': 'Выберите агента',
    'Interval must be at least 1': 'Интервал должен быть не меньше 1',
    'Source path is required': 'Укажите исходный путь',
    'Database name is required': 'Укажите имя базы данных',
    'Host is required for MySQL dumps': 'Для дампов MySQL укажите хост',
    'Username is required': 'Введите логин',
    'Password is required': 'Введите пароль',
    'Sign in failed': 'Не удалось войти',
    Infrastructure: 'Инфраструктура',
    Registered: 'Зарегистрировано',
    'Online now': 'Сейчас онлайн',
    'Need review': 'Требуют проверки',
    Online: 'Онлайн',
    Offline: 'Офлайн',
    Filesystem: 'Файловая система',
    Protected: 'Защищено',
    'Ready to set up': 'Готово к настройке',
    'Needs connection': 'Требуется подключение',
    'Job outcomes': 'Результаты заданий',
    '{count} total': 'Всего: {count}',
    '{count} recorded': 'Записано: {count}',
    '{count} offline': 'Офлайн: {count}',
    '{count} artifacts': 'Артефактов: {count}',
    '{count} policies': 'Политик: {count}',
    '{count} waiting': 'В ожидании: {count}',
    '{count} online': 'Онлайн: {count}',
    '{enabled}/{total} enabled': '{enabled}/{total} включено',
    '{offline} offline / {stale} stale': '{offline} офлайн / {stale} без свежего heartbeat',
    '{count} agent request{plural} waiting': 'Заявок агентов на проверке: {count}',
    '{count} agent{plural} not fully healthy': 'Агентов требуют внимания: {count}',
    '{count} active backup issue{plural}': 'Активных проблем с резервным копированием: {count}',
    'API connection needs attention': 'Подключение к API требует внимания',
    'Some live data could not be loaded. Check backend availability.': 'Часть live-данных не загрузилась. Проверьте доступность backend.',
    'Review pending machines before they can run backup policies.': 'Проверьте новые машины, прежде чем разрешить им запуск политик.',
    'Open Jobs to inspect the latest unresolved failure.': 'Откройте раздел заданий, чтобы разобрать последнюю актуальную ошибку.',
    'Agents online': 'Агенты онлайн',
    'Running jobs': 'Задания в работе',
    'None yet': 'Пока нет',
    'Across all known policies': 'По всем известным политикам',
    'Completed jobs': 'Завершенные задания',
    'Backup activity will appear here after policies start running.': 'Активность появится здесь, когда политики начнут выполняться.',
    'Collapse sidebar': 'Свернуть меню',
    'Expand sidebar': 'Развернуть меню',
    'Toggle navigation': 'Переключить навигацию',
    'Switch to light theme': 'Переключить на светлую тему',
    'Switch to dark theme': 'Переключить на темную тему',
    'Know your data is protected before you need it.': 'Будьте уверены в защите данных еще до того, как они понадобятся.',
    'Enter password': 'Введите пароль',
    'Review the active console identity and rotate your own password safely.': 'Проверьте активный профиль консоли и безопасно смените собственный пароль.',
    'Signed in to RestoreMe': 'Вход в RestoreMe выполнен',
    'Enter current password': 'Введите текущий пароль',
    'Choose a stronger password': 'Выберите более надежный пароль',
    'Repeat the new password': 'Повторите новый пароль',
    'Choose the interface language for this browser.': 'Выберите язык интерфейса для этого браузера.',
    'Documents every 15 minutes': 'Документы каждые 15 минут',
    'PostgreSQL dump': 'Дамп PostgreSQL',
    'MySQL dump': 'Дамп MySQL',
    'Accounting workstation': 'Рабочая станция бухгалтерии',
    'Interface preferences': 'Настройки интерфейса',
    'Tune the console to the way you read operational data.': 'Настройте консоль так, чтобы оперативные данные читались быстрее.',
    Appearance: 'Внешний вид',
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
    'Current theme': 'Текущая тема',
    'Session role': 'Роль сессии',
    'Choose how RestoreMe looks and formats operational timestamps on this device.': 'Выберите тему и формат времени для этого устройства.',
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
    'Policy state updated': 'Состояние политики обновлено',
    'Unable to update policy': 'Не удалось обновить политику',
    'Create and manage backup schedules': 'Создавайте расписания резервного копирования и управляйте ими',
    'All policies': 'Все политики',
    enabled: 'включено',
    disabled: 'отключено',
    Path: 'Путь',
    State: 'Состояние',
    'Next Run': 'Следующий запуск',
    'N/A': 'Н/Д',
    'Adjust the filter or create the first backup policy.': 'Измените фильтр или создайте первую политику резервного копирования.',
    'Agent approved': 'Агент подтвержден',
    'Agent rejected': 'Агент отклонен',
    'Failed to approve agent': 'Не удалось подтвердить агента',
    'Failed to reject agent': 'Не удалось отклонить агента',
    'Agent ID': 'ID агента',
    OS: 'ОС',
    Version: 'Версия',
    Approve: 'Подтвердить',
    'Approve agent': 'Подтвердить агента',
    'Approve pending agent': 'Подтвердить заявку агента',
    'Approving...': 'Подтверждение...',
    'Rejecting...': 'Отклонение...',
    'All agents have been approved. New registrations will appear here.': 'Все агенты подтверждены. Новые регистрации появятся здесь.',
    'Review and approve new agent registration requests': 'Проверяйте новые заявки агентов и допускайте их к работе',
    'Assign a readable name before this machine becomes available for backup policies.': 'Задайте понятное имя, прежде чем машина станет доступна для политик.',
    'The agent will be told that this registration request was rejected and will stop waiting for approval.': 'Агент получит отказ по заявке и перестанет ждать подтверждения.',
    'This action keeps the machine out of backup policy assignment until it registers again under a pending request.': 'Машина не сможет получать политики, пока не зарегистрируется заново и не пройдет проверку.',
    'A live map of registered machines, their heartbeat health, and the protection policy coverage behind each one.': 'Живая карта зарегистрированных машин: состояние heartbeat и покрытие политиками защиты.',
    'Search by agent, machine, OS, status, or id...': 'Поиск по агенту, машине, ОС, статусу или id...',
    'Hide filters': 'Скрыть фильтры',
    'Show filters': 'Показать фильтры',
    active: 'активно',
    'All statuses': 'Все статусы',
    'Operating system': 'Операционная система',
    'All systems': 'Все системы',
    'Policy coverage': 'Покрытие политиками',
    'Any coverage': 'Любой вариант',
    'With policies': 'С политиками',
    'Without policies': 'Без политик',
    'Reset filters': 'Сбросить фильтры',
    Reset: 'Сбросить',
    'Showing {shown} of {total} agents': 'Показано {shown} из {total} агентов',
    Filtered: 'Отфильтровано',
    'Loading agents...': 'Загрузка агентов...',
    'Agents could not be loaded': 'Не удалось загрузить агентов',
    'Check the backend connection and retry this view.': 'Проверьте соединение с backend и повторите.',
    Retry: 'Повторить',
    'No agents match this search': 'По этому запросу агенты не найдены',
    'Adjust the search or reset filters to widen the result set.': 'Измените поиск или сбросьте фильтры, чтобы расширить выдачу.',
    'Approve pending machines or wait for an agent to register.': 'Подтвердите ожидающие машины или дождитесь регистрации агента.',
    'Machine details are not available yet': 'Данные машины пока недоступны',
    Unknown: 'Неизвестно',
    Heartbeat: 'Heartbeat',
    Never: 'Никогда',
    'Agent identifier': 'Идентификатор агента',
    Details: 'Детали',
    Close: 'Закрыть',
    'Manage policies': 'Управлять политиками',
    'Registered RestoreMe agent': 'Зарегистрированный агент RestoreMe',
    'Assigned policies': 'Назначенные политики',
    every: 'каждые',
    Next: 'Следующий запуск',
    'No policies are assigned to this agent yet.': 'Этому агенту пока не назначены политики.',
    'Policy updated': 'Политика обновлена',
    'Policy created': 'Политика создана',
    'Policy save failed': 'Не удалось сохранить политику',
    'Edit backup policy': 'Редактировать политику',
    'Choose what should be protected, how often it runs, and which agent owns the work.': 'Выберите объект защиты, частоту запуска и агента, который будет выполнять задачу.',
    'Saving...': 'Сохранение...',
    'Approve at least one agent before creating backup policies.': 'Перед созданием политик подтвердите хотя бы одного агента.',
    'Policy type': 'Тип политики',
    'Filesystem backup': 'Файловая копия',
    Minutes: 'Минуты',
    Hours: 'Часы',
    Days: 'Дни',
    'Enable scheduling immediately': 'Включить расписание сразу',
    'Source path': 'Исходный путь',
    'Database name': 'Имя базы данных',
    Port: 'Порт',
    'Auth mode': 'Режим авторизации',
    'Integrated / local': 'Интегрированная / локальная',
    'Username + password': 'Логин + пароль',
    'Database password': 'Пароль базы данных',
    Execution: 'Выполнение',
    Refresh: 'Обновить',
    'Total runs': 'Всего запусков',
    'Track backup runs as a timeline: what started, what finished, what failed, and where attention is needed.': 'Отслеживайте запуски резервного копирования: что началось, что завершилось, где возникла ошибка и что требует внимания.',
    'Search by job, policy, agent, status, or error...': 'Поиск по заданию, политике, агенту, статусу или ошибке...',
    pending: 'ожидает',
    running: 'выполняется',
    failed: 'ошибка',
    completed: 'завершено',
    'Loading jobs...': 'Загрузка заданий...',
    'Jobs could not be loaded': 'Не удалось загрузить задания',
    'Check the API container and retry the execution timeline.': 'Проверьте API-контейнер и обновите историю запусков.',
    'No jobs match these filters': 'По этим фильтрам заданий нет',
    'Clear the search or switch the status filter.': 'Очистите поиск или смените фильтр статуса.',
    'Execution history will appear here after policies run.': 'История запусков появится здесь после выполнения политик.',
    Duration: 'Длительность',
    'Running now': 'Выполняется сейчас',
    Recovery: 'Восстановление',
    'A clean recovery shelf for every completed backup: inspect type, age, retention, and download the exact artifact you need.': 'Витрина готовых резервных копий: тип, возраст, срок хранения и скачивание нужного артефакта.',
    'Stored size': 'Объем данных',
    Database: 'База данных',
    'Search by artifact, filename, type, job, or id...': 'Поиск по артефакту, файлу, типу, заданию или id...',
    all: 'все',
    'Loading artifacts...': 'Загрузка артефактов...',
    'Artifacts could not be loaded': 'Не удалось загрузить артефакты',
    'Check the backend and object storage containers, then retry this shelf.': 'Проверьте backend и объектное хранилище, затем обновите список.',
    'No artifacts match these filters': 'По этим фильтрам артефактов нет',
    'Clear the search or switch the artifact type.': 'Очистите поиск или смените тип артефакта.',
    'Successful backups will appear here as downloadable recovery artifacts.': 'Успешные резервные копии появятся здесь как артефакты для скачивания.',
    '{name} download started': 'Скачивание {name} началось',
    'Artifact download failed': 'Не удалось скачать артефакт',
    Expires: 'Истекает',
    Expired: 'Истекло',
    Expiring: 'Скоро истечет',
    'Unavailable soon': 'Скоро недоступно',
    Retained: 'Хранится',
    'This artifact is expired': 'Срок хранения артефакта истек',
    Download: 'Скачать',
    'Downloading...': 'Скачивание...',
    'User access': 'Доступ пользователей',
    'Only administrators can create users, rotate passwords and change access roles.': 'Только администраторы могут создавать пользователей, менять пароли и роли доступа.',
    'Sign in as an administrator to manage platform users.': 'Войдите как администратор, чтобы управлять пользователями платформы.',
    'Manage operator, viewer and administrator accounts without leaving the backup workspace.': 'Управляйте аккаунтами операторов, наблюдателей и администраторов прямо из рабочей области.',
    Accounts: 'Аккаунты',
    Current: 'Текущий',
    'Use Account to change your own password': 'Собственный пароль меняется в разделе аккаунта',
    Disable: 'Отключить',
    Enable: 'Включить',
    'User role updated': 'Роль пользователя обновлена',
    'Unable to update role': 'Не удалось обновить роль',
    'User status updated': 'Статус пользователя обновлен',
    'Unable to update status': 'Не удалось обновить статус',
    'Create the first operator, viewer or administrator account.': 'Создайте первый аккаунт оператора, наблюдателя или администратора.',
    'User created': 'Пользователь создан',
    'Unable to create user': 'Не удалось создать пользователя',
    'Add an operator, viewer or administrator account for RestoreMe.': 'Добавьте аккаунт оператора, наблюдателя или администратора RestoreMe.',
    'Creating...': 'Создание...',
    'User deleted': 'Пользователь удален',
    'Unable to delete user': 'Не удалось удалить пользователя',
    'Delete {username}. This removes their RestoreMe access.': 'Удалить {username}. Пользователь потеряет доступ к RestoreMe.',
    'Delete the selected user.': 'Удалить выбранного пользователя.',
    'Deleting...': 'Удаление...',
    'Use deletion for accounts that should no longer exist. For temporary suspension, disable the user instead.': 'Удаляйте только аккаунты, которые больше не нужны. Для временной блокировки лучше отключить пользователя.',
    'Set a new password for {username}.': 'Задайте новый пароль для {username}.',
    'Set a new password for the selected user.': 'Задайте новый пароль для выбранного пользователя.',
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
