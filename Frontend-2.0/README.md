# RestoreMe Frontend 2.0

RestoreMe Frontend 2.0 is the flagship prototype of the RestoreMe admin panel.

The original `Frontend` folder remains the primary stable UI for the diploma/demo baseline. This version is a more polished Apple-like operational console built on the same backend API and database model, so data created in one frontend is visible in the other.

## Purpose

Frontend 2.0 focuses on:

- a calmer, more premium dashboard experience
- dark and light themes
- clearer empty states and operational alerts
- improved policy, agent, job and artifact visibility
- faster operator feedback through polling and query invalidation
- compatibility with the existing RestoreMe backend contracts

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

## Implemented Areas

- login and authenticated app shell
- dashboard with protection status, trends, attention items and recent activity
- agents page with filtering, policy coverage and details dialog
- pending agents page with approve and reject flows
- policies page with create, edit and toggle
- jobs page with resilient labels based on agent/policy lookup
- backups/artifacts page with download flow
- users page for administrator access management
- account page for password change
- dark/light theme toggle
- SPA routing for direct links such as `/backups`, `/jobs` and `/policies`

## Backend Compatibility

This frontend uses the same backend as the original frontend.

Main API groups:

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

In `live` mode the app is tuned for an operator console:

- data is considered stale after 5 seconds
- active queries refetch every 10 seconds
- pages refetch when mounted
- data refetches on reconnect and window focus
- policy changes invalidate policy and agent data

This keeps the dashboard, agents and policy coverage views close to the current backend state without requiring manual browser refreshes.

## Local Development

Install dependencies:

```powershell
cd D:\projects\RestorMe\Frontend-2.0
yarn
```

Run Vite:

```powershell
cd D:\projects\RestorMe\Frontend-2.0
yarn dev
```

Vite will choose an available local port. The Docker Compose setup publishes this frontend on:

- `http://localhost:5174`

## Environment

Create `.env` if you need to override defaults:

```env
VITE_API_BASE_URL=http://localhost:8080
VITE_API_MODE=live
```

Important:

- `VITE_API_BASE_URL` points to the RestoreMe backend API.
- `VITE_API_MODE=live` enables polling behavior for real backend data.
- Frontend 2.0 is intended for live backend use; unlike the original frontend, it is not maintained as a fixture-heavy mock demo surface.

## Scripts

```powershell
yarn typecheck
yarn lint
yarn build
yarn preview
```

Meaning:

- `yarn typecheck` runs TypeScript checks without emitting files
- `yarn lint` runs ESLint
- `yarn build` creates a production bundle
- `yarn preview` serves the production bundle locally

## Docker Compose

The root Compose stack includes this frontend as `frontend-2`.

```powershell
cd D:\projects\RestorMe\docker-compose
docker compose up --build frontend-2
```

Default address:

- `http://localhost:5174`

The production image builds the Vite bundle and serves it through Apache with SPA rewrite rules, so direct navigation to nested routes works.

## Recommended Smoke Test

1. Start backend, database, MinIO and both frontends.
2. Sign in as an administrator.
3. Approve or reject a pending agent.
4. Create a policy in one frontend.
5. Confirm it appears in the other frontend.
6. Let the agent execute the policy.
7. Confirm the job and artifact appear in both frontends.
8. Download an artifact from the backups page.

## Notes

- The original `Frontend` should be treated as the stable diploma baseline.
- `Frontend-2.0` should be presented as the next-generation RestoreMe UI prototype.
- Both frontends should remain compatible with the same backend contracts.
- If a browser tab was open during a rebuild, reload it once to avoid stale Vite chunk references.

## Related Documentation

- [Root README](../README.md)
- [Original Frontend README](../Frontend/README.md)
- [Docker Compose README](../docker-compose/README.md)
