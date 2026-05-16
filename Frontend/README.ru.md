# RestoreMe Frontend

Frontend admin panel для системы резервного копирования RestoreMe.

Он собран как Vite + React + TypeScript приложение и взаимодействует с ASP.NET Core backend в `live` mode или с локальными fixtures в `mock` mode.

## Stack

- React 19
- TypeScript
- Vite
- Yarn
- React Router
- Axios
- TanStack Query
- Zustand
- React Hook Form
- Zod
- Tailwind CSS
- Sonner для toasts
- Lucide React для icons

## Цели проекта

Frontend предоставляет operator-facing admin panel для:
- agent overview
- pending agent approval
- backup policy management
- job monitoring
- artifact inspection and download

## Установка

```powershell
cd D:\projects\RestorMe\Frontend
yarn
```

## Запуск в Development

```powershell
cd D:\projects\RestorMe\Frontend
yarn dev
```

По умолчанию Vite запускается на:
- `http://localhost:5173`

## Build и Checks

```powershell
cd D:\projects\RestorMe\Frontend
yarn typecheck
yarn lint
yarn build
yarn preview
```

## Environment

Создайте `.env` на основе `.env.example`.

Текущий пример:
```env
VITE_API_BASE_URL=http://localhost:8080
VITE_API_MODE=live
```

### Modes
- `live` - использовать ASP.NET Core backend
- `mock` - использовать локальные in-memory fixtures для offline/demo работы

### Notes
- в локальной ручной разработке рекомендуемый backend URL: `http://localhost:8080`
- в Docker Compose frontend build также по умолчанию указывает на `http://localhost:8080`
- если вы пересобрали или redeploy frontend, пока browser tab оставалась открытой, Vite chunk hashes могли измениться, и старой вкладке может понадобиться один reload

## Основные Scripts

Определены в [package.json](D:\projects\RestorMe\Frontend\package.json):
- `yarn dev` - запустить Vite dev server
- `yarn build` - выполнить TypeScript build и production bundle
- `yarn lint` - запустить ESLint
- `yarn typecheck` - запустить TypeScript без emitting files
- `yarn preview` - preview production bundle локально

## Структура проекта

```text
src/
  app/
    providers/
    router/
    store/
  pages/
    agents/
    artifacts/
    dashboard/
    jobs/
    pending-agents/
    policies/
  widgets/
    app-shell/
  features/
    approve-agent/
    policy-form/
  entities/
    agent/
    artifact/
    job/
    policy/
  shared/
    api/
    config/
    lib/
    ui/
```

## Реализованный UI

- responsive app shell с sidebar и header
- dashboard с summary cards, recent jobs и failure visibility
- agents list с search и detail area
- pending agents page с approval dialog
- policies page с search, filtering, create, edit и toggle
- jobs page с history table
- artifacts page с search, list и download action
- polling в live mode, чтобы data обновлялись без manual browser reload

## API Behavior

Frontend использует:
- Axios для HTTP
- TanStack Query для server state и polling
- Zustand для UI/shared client state

В `live` mode основные pages автоматически refetch-ятся по интервалу, поэтому backend changes появляются без нажатия `F5`.

## Используемые Backend Endpoints

### Agents
- `GET /api/agents`
- `GET /api/agents/agent/{id}`
- `GET /api/agents/pending`
- `POST /api/agents/approve/{pendingId}`
- `GET /api/agents/status/{pendingId}`

### Policies
- `GET /api/policies`
- `GET /api/policies/{id}`
- `GET /api/policies/agent/{agentId}`
- `POST /api/policies/create_policy/{agentId}`
- `PUT /api/policies/{id}`
- `PATCH /api/policies/{id}/toggle`

### Jobs
- `GET /api/backupjobs`
- `GET /api/backupjobs/{id}`
- `GET /api/backupjobs/agent/{agentId}`
- `GET /api/backupjobs/policy/{policyId}`

### Artifacts
- `GET /api/backupartifacts`
- `GET /api/backupartifacts/job/{jobId}`
- `GET /api/backupartifacts/{artifactId}/download`

## Типовой Local Workflow

1. Запустите backend.
2. Запустите frontend через `yarn dev`.
3. Откройте `http://localhost:5173`.
4. Подтвердите pending agent в `Pending`.
5. Создайте или отредактируйте policy в `Policies`.
6. Наблюдайте jobs в `Jobs`.
7. Скачайте uploaded artifacts в `Artifacts`.

## Notes для Docker Compose

В Docker Compose frontend собирается в static production bundle и раздается Apache.

Важно:
- frontend API URL inject-ится во время image build из `API_PORT`
- SPA routes вроде `/jobs` или `/policies` обрабатываются container rewrite rules
- после fresh frontend rebuild старая browser tab может все еще ссылаться на outdated chunk names и требовать один manual reload

## Связанная документация

- [Root README](../README.ru.md)
- [Docker Compose README](../docker-compose/README.ru.md)
