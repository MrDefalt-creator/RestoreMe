# RestoreMe Frontend 2.0

🇬🇧 English · [🇷🇺 Русский](README.ru.md)

RestoreMe Frontend 2.0 is the admin panel for RestoreMe. It is a Vite + React + TypeScript SPA built on Radix UI primitives, talking to the ASP.NET Core backend over the HttpOnly `access_token` cookie.

## Purpose

The frontend focuses on:

- a calm, premium dashboard experience
- dark and light themes
- clear empty states and operational alerts
- policy, agent, job and artifact visibility
- fast operator feedback through polling and query invalidation
- the RestoreMe backend contracts

## Stack

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
- Radix UI primitives (`@radix-ui/react-dialog`, `react-select`, `react-toast`, etc.)

## Layout (Feature-Sliced Design)

```
src/
  app/          providers, router, zustand stores (auth-store, ui-store)
  entities/     domain models + API hooks (agent, artifact, audit-log, auth, job, policy, user)
  features/     self-contained feature modules (approve-agent, install-agent, policy-form, user-management, notification-channel-form)
  pages/        route-level components assembled from features/widgets
  widgets/      app-shell, header, side-bar
  shared/       api (axios http client), config (env.ts), i18n, lib, ui (primitives)
```

`@/` alias maps to `src/`.

## Implemented Areas

- login and authenticated app shell
- dashboard with protection status, trends, attention items and recent activity
- agents page with filtering, policy coverage and details dialog
- **install-agent wizard** on the Agents page — admins/operators copy a one-liner that installs and enrols an agent on Linux or Windows; server URL is taken from the panel, enrollment token from `GET /api/agents/enrollment-info`
- pending agents page with approve and reject flows
- policies page with create, edit and toggle, plus an "Auto-disabled" badge and one-click re-enable for policies stopped after repeated failures; the policy form includes retention controls (keep by age / count / total size)
- jobs page with resilient labels based on agent/policy lookup
- backups/artifacts page with download flow, a per-artifact integrity badge and a "Verify now" action
- users page for administrator access management
- **notification channels page** (admin-only) — create/edit/test Webhook, Telegram, Slack and Discord channels with per-event subscriptions; also hosts the admin-only integrity scrub-schedule settings
- account page for password change
- dark/light theme toggle
- SPA routing for direct links such as `/backups`, `/jobs` and `/policies`

## Backend API surface

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
- `POST /api/backupartifacts/{artifactId}/verify` (operator/admin — verify integrity now)
- `GET/PUT /api/integrity-settings` (admin-only — scrub schedule)
- `GET /api/users`
- `GET/POST/PUT/DELETE /api/notification-channels`, `POST /api/notification-channels/{id}/test` (admin-only)
- `GET /api/audit-logs` (admin-only)

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

- `http://localhost:5173`

## Environment

Create `.env` if you need to override defaults:

```env
VITE_API_BASE_URL=http://localhost:8080
VITE_API_MODE=live
```

Important:

- `VITE_API_BASE_URL` points to the RestoreMe backend API.
- `VITE_API_MODE=live` enables polling behavior for real backend data.
- The frontend is intended for live backend use, not as a fixture-heavy mock demo.

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

- `http://localhost:5173`

The production image builds the Vite bundle and serves it through Apache with SPA rewrite rules, so direct navigation to nested routes works.

## Recommended Smoke Test

1. Start backend, database, MinIO and the frontend.
2. Sign in as an administrator.
3. Approve or reject a pending agent.
4. Create a policy.
5. Let the agent execute the policy.
6. Confirm the job and artifact appear on the backups page.
7. Download an artifact.

## Notes

- If a browser tab was open during a rebuild, reload it once to avoid stale Vite chunk references.

## Related Documentation

- [Root README](../README.md) — [🇷🇺 Русский](../README.ru.md)
- [Docker Compose README](../docker-compose/README.md) — [🇷🇺 Русский](../docker-compose/README.ru.md)
- [README.ru.md](README.ru.md) — русский перевод этого файла
