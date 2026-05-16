# RestoreMe Frontend 2.0

RestoreMe Frontend 2.0 - флагманский прототип admin panel RestoreMe.

Оригинальная папка `Frontend` остается основным стабильным UI для diploma/demo baseline. Эта версия - более polished Apple-like operational console, построенная на том же backend API и database model, поэтому data, созданные в одном frontend, видны в другом.

## Purpose

Frontend 2.0 фокусируется на:

- более спокойном, premium dashboard experience
- dark и light themes
- более понятных empty states и operational alerts
- улучшенной видимости policy, agent, job и artifact
- более быстрой обратной связи для operator через polling и query invalidation
- совместимости с существующими RestoreMe backend contracts

## Stack

- React 19
- TypeScript
- Vite 6
- Yarn 1.x
- React Router 6
- TanStack Query
- Zustand
- React Hook Form
- Zod
- Tailwind CSS
- Sonner
- Lucide React

## Реализованные области

- login и authenticated app shell
- dashboard с protection status, trends, attention items и recent activity
- agents page с filtering, policy coverage и details dialog
- pending agents page с approve и reject flows
- policies page с create, edit и toggle
- jobs page с resilient labels на основе agent/policy lookup
- backups/artifacts page с download flow
- users page для administrator access management
- account page для password change
- dark/light theme toggle
- SPA routing для direct links вроде `/backups`, `/jobs` и `/policies`

## Backend Compatibility

Этот frontend использует тот же backend, что и original frontend.

Основные API groups:

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

## Data Refresh Behavior

В `live` mode приложение настроено как operator console:

- data считается stale через 5 секунд
- active queries refetch-ятся каждые 10 секунд
- pages refetch-ятся при mount
- data refetch-ится при reconnect и window focus
- policy changes invalidates policy и agent data

Это держит dashboard, agents и policy coverage views близкими к текущему backend state без manual browser refresh.

## Local Development

Установить dependencies:

```powershell
cd D:\projects\RestorMe\Frontend-2.0
yarn
```

Запустить Vite:

```powershell
cd D:\projects\RestorMe\Frontend-2.0
yarn dev
```

Vite выберет доступный локальный port. Docker Compose setup публикует этот frontend на:

- `http://localhost:5174`

## Environment

Создайте `.env`, если нужно переопределить defaults:

```env
VITE_API_BASE_URL=http://localhost:8080
VITE_API_MODE=live
```

Важно:

- `VITE_API_BASE_URL` указывает на RestoreMe backend API.
- `VITE_API_MODE=live` включает polling behavior для real backend data.
- Frontend 2.0 предназначен для live backend use; в отличие от original frontend, он не поддерживается как fixture-heavy mock demo surface.

## Scripts

```powershell
yarn typecheck
yarn lint
yarn build
yarn preview
```

Значение:

- `yarn typecheck` запускает TypeScript checks без emitting files
- `yarn lint` запускает ESLint
- `yarn build` создает production bundle
- `yarn preview` раздает production bundle локально

## Docker Compose

Корневой Compose stack включает этот frontend как `frontend-2`.

```powershell
cd D:\projects\RestorMe\docker-compose
docker compose up --build frontend-2
```

Default address:

- `http://localhost:5174`

Production image собирает Vite bundle и раздает его через Apache с SPA rewrite rules, поэтому direct navigation к nested routes работает.

## Рекомендуемый Smoke Test

1. Запустите backend, database, MinIO и оба frontend.
2. Войдите как administrator.
3. Подтвердите или отклоните pending agent.
4. Создайте policy в одном frontend.
5. Убедитесь, что она появилась в другом frontend.
6. Дайте agent выполнить policy.
7. Убедитесь, что job и artifact появились в обоих frontends.
8. Скачайте artifact со страницы backups.

## Notes

- Оригинальный `Frontend` следует считать stable diploma baseline.
- `Frontend-2.0` следует представлять как next-generation RestoreMe UI prototype.
- Оба frontend должны оставаться совместимыми с одними backend contracts.
- Если browser tab была открыта во время rebuild, reload ее один раз, чтобы избежать stale Vite chunk references.

## Связанная документация

- [Root README](../README.ru.md)
- [Original Frontend README](../Frontend/README.ru.md)
- [Docker Compose README](../docker-compose/README.ru.md)
