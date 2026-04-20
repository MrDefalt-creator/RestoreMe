# RestorMe Frontend Prototype

Admin panel prototype for the `RestorMe / Backup` diploma project.

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

## Install

```bash
yarn
```

## Run

```bash
yarn dev
```

## Checks

```bash
yarn typecheck
yarn lint
yarn build
```

## Environment

Create `.env` from `.env.example`.

```env
VITE_API_BASE_URL=https://localhost:7104
VITE_API_MODE=live
```

Modes:

- `live` — use the ASP.NET Core backend
- `mock` — use local fixtures for demo/offline work

If you use `https://localhost:7104`, make sure the ASP.NET Core development certificate is trusted in the browser, otherwise requests from the frontend will fail as network errors.

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

- App shell with sidebar, header and responsive layout
- Dashboard with aggregated summary, recent jobs and failures
- Agents list with search and side detail panel
- Pending agents page with approve dialog
- Policies page with search, filtering, create/edit and toggle
- Jobs page with filters and detail panel
- Artifacts page with search and table

## Backend Endpoints Used

### Agents

- `GET /api/agents`
- `GET /api/agents/{id}`
- `GET /api/agents/pending`
- `POST /api/agents/approve/{pendingId}`
- `GET /api/agents/status/{pendingId}`
- `POST /api/agents/register_pending`
- `POST /api/agents/heartbeat/{agentId}`

### Policies

- `GET /api/policies`
- `GET /api/policies/{id}`
- `GET /api/policies/agent/{agentId}`
- `POST /api/policies/create_policy/{agentId}`
- `PUT /api/policies/{id}`
- `PATCH /api/policies/{id}/toggle`
- `POST /api/policies/mark_policy_executed/{policyId}`

### Jobs

- `GET /api/backupjobs`
- `GET /api/backupjobs/{id}`
- `GET /api/backupjobs/agent/{agentId}`
- `GET /api/backupjobs/policy/{policyId}`
- `POST /api/backupjobs/start`
- `POST /api/backupjobs/complete/{jobId}`
- `POST /api/backupjobs/failed`
- `POST /api/backupjobs/add_artifact`
- `POST /api/backupjobs/upload_ticket`

### Artifacts

- `GET /api/backupartifacts`
- `GET /api/backupartifacts/job/{jobId}`

## Notes

- Dashboard live mode is currently aggregated on the client via `agents`, `pending agents`, `policies` and `jobs` endpoints.
- A dedicated `GET /api/dashboard/summary` endpoint would still be a good next step to reduce client requests.
- The frontend can now run against the updated backend in `live` mode without relying on mock data for the main admin pages.
