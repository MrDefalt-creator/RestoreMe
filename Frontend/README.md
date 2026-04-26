# RestoreMe Frontend

Frontend admin panel for the RestoreMe backup system.

It is built as a Vite + React + TypeScript application and communicates with the ASP.NET Core backend in `live` mode or with local fixtures in `mock` mode.

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
- Sonner for toasts
- Lucide React for icons

## Project Goals

The frontend provides an operator-facing admin panel for:
- agent overview
- pending agent approval
- backup policy management
- job monitoring
- artifact inspection and download

## Install

```powershell
cd D:\projects\RestorMe\Frontend
yarn
```

## Run in Development

```powershell
cd D:\projects\RestorMe\Frontend
yarn dev
```

By default Vite starts on:
- `http://localhost:5173`

## Build and Checks

```powershell
cd D:\projects\RestorMe\Frontend
yarn typecheck
yarn lint
yarn build
yarn preview
```

## Environment

Create `.env` from `.env.example`.

Current example:
```env
VITE_API_BASE_URL=http://localhost:8080
VITE_API_MODE=live
```

### Modes
- `live` — use the ASP.NET Core backend
- `mock` — use local in-memory fixtures for offline/demo work

### Notes
- in local manual development the recommended backend URL is `http://localhost:8080`
- in Docker Compose the frontend build also points to `http://localhost:8080` by default
- if you rebuild or redeploy the frontend while a browser tab stays open, Vite chunk hashes can change and the old tab may need one reload

## Main Scripts

Defined in [package.json](D:\projects\RestorMe\Frontend\package.json):
- `yarn dev` — run Vite dev server
- `yarn build` — run TypeScript build and production bundle
- `yarn lint` — run ESLint
- `yarn typecheck` — run TypeScript without emitting files
- `yarn preview` — preview the production bundle locally

## Project Structure

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

## Implemented UI

- responsive app shell with sidebar and header
- dashboard with summary cards, recent jobs and failure visibility
- agents list with search and detail area
- pending agents page with approval dialog
- policies page with search, filtering, create, edit and toggle
- jobs page with history table
- artifacts page with search, list and download action
- polling in live mode so data refreshes without manual browser reload

## API Behavior

The frontend uses:
- Axios for HTTP
- TanStack Query for server state and polling
- Zustand for UI/shared client state

In `live` mode the main pages refetch automatically on an interval, so backend changes appear without pressing `F5`.

## Backend Endpoints Used

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

## Typical Local Workflow

1. Start backend.
2. Start frontend with `yarn dev`.
3. Open `http://localhost:5173`.
4. Approve a pending agent in `Pending`.
5. Create or edit a policy in `Policies`.
6. Observe jobs in `Jobs`.
7. Download uploaded artifacts in `Artifacts`.

## Notes for Docker Compose

In Docker Compose the frontend is built into a static production bundle and served by Apache.

Important:
- frontend API URL is injected during image build from `API_PORT`
- SPA routes like `/jobs` or `/policies` are handled by the container rewrite rules
- after a fresh frontend rebuild, an old browser tab may still reference outdated chunk names and need one manual reload

## Related Documentation

- [Root README](D:\projects\RestorMe\README.md)
- [Docker Compose README](D:\projects\RestorMe\docker-compose\README.md)
