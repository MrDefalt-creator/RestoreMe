# RestoreMe Frontend 2.0

[🇬🇧 English](README.md) · 🇷🇺 Русский

RestoreMe Frontend 2.0 — админ-панель RestoreMe. Это Vite + React + TypeScript SPA на Radix UI, общающаяся с ASP.NET Core бэкендом через HttpOnly-куку `access_token`.

## Назначение

Фронтенд фокусируется на:

- спокойном премиальном дашборде
- тёмной и светлой темах
- понятных empty-states и операционных алертах
- видимости политик, агентов, задач и артефактов
- быстрой операторской обратной связи через polling и инвалидацию кэша
- контрактах бэкенда RestoreMe

## Стек

- React 19
- TypeScript
- Vite 8
- Yarn 1.x
- React Router 7
- TanStack Query 5 (+ devtools)
- Zustand
- React Hook Form 7
- Zod 4
- Tailwind CSS 4
- Sonner 2
- Lucide React 1.x
- Radix UI primitives (`@radix-ui/react-dialog`, `react-select`, `react-toast` и т.д.)

## Раскладка (Feature-Sliced Design)

```
src/
  app/          providers, router, zustand-сторы (auth-store, ui-store)
  entities/     доменные модели + API-хуки (agent, artifact, audit-log, auth, job, policy, user)
  features/     самодостаточные фича-модули (approve-agent, install-agent, policy-form, user-management, notification-channel-form)
  pages/        route-level компоненты, собранные из features/widgets
  widgets/      app-shell, header, side-bar
  shared/       api (axios-клиент), config (env.ts), i18n, lib, ui (примитивы)
```

Алиас `@/` указывает на `src/`.

## Реализованные области

- логин и аутентифицированный app-shell
- дашборд: статус защиты, тренды, attention-items, recent activity
- страница агентов: фильтры, покрытие политиками, диалог деталей
- **мастер установки агента** на странице Agents — admin/operator копирует one-liner, который ставит и регистрирует агента на Linux или Windows; URL сервера берётся из панели, enrollment-токен — из `GET /api/agents/enrollment-info`
- страница pending-агентов: approve и reject
- страница политик: создание, редактирование, тоггл, плюс бейдж «Auto-disabled» и повторное включение в один клик для политик, остановленных после серии провалов
- страница задач: устойчивые лейблы, основанные на lookup'е агента/политики
- страница backups/artifacts: download
- страница пользователей (управление доступом для admin)
- **страница каналов уведомлений** (только для admin) — создание/редактирование/тест каналов Webhook, Telegram, Slack, Discord с подпиской по типам событий
- страница аккаунта (смена пароля)
- переключатель тёмной/светлой темы
- SPA-роутинг для прямых ссылок: `/backups`, `/jobs`, `/policies`
- audit-log (только для admin)

## API бэкенда

Основные API-группы:

- `GET /api/agents`
- `GET /api/agents/pending`
- `POST /api/agents/approve/{pendingId}`
- `POST /api/agents/reject/{pendingId}`
- `GET /api/policies`
- `POST /api/policies/create_policy/{agentId}`
- `PUT /api/policies/{policyId}`
- `PATCH /api/policies/{policyId}/toggle`
- `GET /api/backupjobs`
- `GET /api/backupartifacts`
- `GET /api/backupartifacts/{artifactId}/download`
- `GET /api/users`
- `GET/POST/PUT/DELETE /api/notification-channels`, `POST /api/notification-channels/{id}/test` (только для admin)
- `GET /api/audit-logs` (только для admin)

## HTTP-клиент и аутентификация

`src/shared/api/client.ts` — Axios-инстанс с `withCredentials: true`. Бэкенд выставляет HttpOnly-куку `access_token` (`SameSite=Strict`, `Secure` вне Development), которую JS не читает. При 401 клиент очищает сессию и эмитит `auth-events.emitUnauthorized('session_expired')`.

В Zustand `auth-store` хранится только маленький профиль (id, username, role, флаг `mustChangePassword`). Storage переключается между `localStorage` (Remember me = true) и `sessionStorage` (Remember me = false).

## Поведение обновления данных

В `live` приложение настроено под operator-console:

- данные считаются stale через 5 секунд
- активные запросы refetch'атся каждые 10 секунд
- страницы делают refetch при монтировании
- refetch при reconnect и focus окна
- изменения политик инвалидируют политики и данные агентов

Это держит дашборд, страницы агентов и покрытие политик близко к текущему состоянию бэкенда без ручных обновлений браузера.

## Локальная разработка

Установка зависимостей:

```powershell
cd D:\projects\RestorMe\Frontend-2.0
yarn
```

Запуск Vite:

```powershell
cd D:\projects\RestorMe\Frontend-2.0
yarn dev
```

Vite выберет свободный локальный порт. В Docker Compose фронт публикуется на:

- `http://localhost:5173`

## Окружение

`.env` нужен только если хотите перебить дефолты:

```env
VITE_API_BASE_URL=http://localhost:8080
VITE_API_MODE=live
```

Важно:

- `VITE_API_BASE_URL` указывает на API RestoreMe бэкенда.
- `VITE_API_MODE=live` включает polling для реальных бэкенд-данных.
- Фронт рассчитан на работу с живым бэкендом, не на fixture-heavy mock-демо.

## Скрипты

```powershell
yarn typecheck
yarn lint
yarn build
yarn preview
```

Смысл:

- `yarn typecheck` — TypeScript-проверки без emit
- `yarn lint` — ESLint
- `yarn build` — production-бандл
- `yarn preview` — локальный preview production-бандла

## Docker Compose

Корневой стек включает этот фронт как `frontend-2`.

```powershell
cd D:\projects\RestorMe\docker-compose
docker compose up --build frontend-2
```

Дефолтный адрес:

- `http://localhost:5173`

Production-образ собирает Vite-бандл и раздаёт через Apache с SPA rewrite-правилами — прямой переход по вложенным маршрутам работает.

## Рекомендованный smoke-test

1. Поднять backend, БД, MinIO и фронт.
2. Войти как администратор.
3. Approve или reject pending-агента.
4. Создать политику.
5. Дать агенту выполнить политику.
6. Убедиться, что job и артефакт появились на странице backups.
7. Скачать артефакт.

## Заметки

- Если вкладка браузера была открыта во время пересборки — обновите её один раз, чтобы избежать stale-чанков Vite.

## Связанные документы

- [Root README (RU)](../README.ru.md) — [🇬🇧 English](../README.md)
- [Docker Compose README (RU)](../docker-compose/README.ru.md) — [🇬🇧 English](../docker-compose/README.md)
- [README.md](README.md) — английская версия этого файла
