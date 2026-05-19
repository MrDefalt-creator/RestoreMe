# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

RestoreMe is a backup management system. The monorepo contains:
- `Backup/` — ASP.NET Core 10 backend + agent worker (solution file: `Backup/BackupSystem.slnx`)
- `Frontend/` — stable React admin panel (operators and admins)
- `Frontend-2.0/` — next-generation UI prototype with dark/light theme and Radix UI
- `docker-compose/` — full-stack local startup

## Commands

### Full stack (recommended)
```powershell
cd docker-compose
docker compose up --build
```
Default ports: frontend v1 `:5173`, frontend 2.0 `:5174`, backend `:8080`, MinIO API `:9000`, MinIO Console `:9001`, PostgreSQL `:5432`.

### Backend
```powershell
cd Backup
dotnet run --project .\Backup.Server.Api\Backup.Server.Api.csproj
dotnet run --project .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
dotnet build .\Backup.Server.Api\Backup.Server.Api.csproj
```

### EF Core migrations (run from `Backup/`)
```powershell
dotnet ef migrations add MigrationName --project .\Backup.Server.Infrastructure\Backup.Server.Infrastructure.csproj --startup-project .\Backup.Server.Api\Backup.Server.Api.csproj --output-dir Migrations
```
Migrations auto-apply on startup.

### Frontend / Frontend-2.0
```powershell
cd Frontend         # or Frontend-2.0
yarn                # install
yarn dev            # dev server
yarn build          # tsc -b && vite build
yarn lint           # eslint
yarn typecheck      # tsc --noEmit
yarn preview        # preview build
```

## Backend architecture

Clean architecture with strict layer isolation:

```
Backup.Shared.Contracts   ← DTOs shared between backend and agent
Backup.Server.Domain      ← Entities, Enums, Options (no deps)
Backup.Server.Application ← Repository interfaces + application services
Backup.Server.Infrastructure ← EF Core (AppDbContext, repositories, migrations), MinIO StorageAccessService
Backup.Server.Api         ← Controllers, Program.cs (DI root), JWT + AgentEnrollment auth
Backup.Agent.Worker       ← Standalone worker: heartbeat, policy sync, backup execution, upload
```

`Program.cs` is the DI composition root — all services, options, CORS, auth schemes, and EF context are registered there.

### Auth model

Two JWT token types, distinguished by a custom `token_type` claim:
- **`user`** tokens — issued after login, carry the user's role (`viewer`, `operator`, `admin`)
- **`agent`** tokens — issued after agent approval, used for heartbeat/jobs/artifacts/upload-tickets

Authorization policies (defined in `Program.cs`):
- `AdminReadPolicy` — viewer/operator/admin, user token
- `AdminWritePolicy` — operator/admin, user token
- `UserManagementPolicy` — admin only, user token
- `AgentPolicy` — agent token
- `AgentEnrollmentPolicy` — separate `AgentEnrollment` header scheme (enrollment token)

### Config secrets pattern

The backend resolves config values by checking a `*_FILE` sibling first; if it points to a file it reads from there, otherwise falls back to the plain value. Example pairs:
- `ConnectionStrings:DefaultConnection` / `ConnectionStrings:DefaultConnection_FILE`
- `Storage:AccessKey` / `Storage:AccessKey_FILE`

Docker Compose mounts Docker secrets at `/run/secrets/` and uses the `_FILE` variants. Local dev uses plain values in `appsettings.json`.

### Dev credentials
- Bootstrap admin: `admin / Admin123!` (seeded only when user table is empty, defined in `appsettings.Development.json`)
- JWT signing key / enrollment token in `appsettings.json` are development defaults — replace before any shared deployment

### Storage addressing
- `Storage:Endpoint` — internal MinIO address (used by backend → MinIO)
- `Storage:PublicEndpoint` — external address baked into presigned upload URLs returned to agents

## Frontend architecture (both frontends)

Both frontends follow **Feature-Sliced Design (FSD)**:

```
src/
  app/          ← providers, router, zustand stores (auth-store, ui-store)
  entities/     ← domain models and API query hooks (agent, artifact, auth, job, policy, user)
  features/     ← self-contained feature modules (policy-form, user-management, approve-agent)
  pages/        ← route-level components assembled from features/widgets
  widgets/      ← app-shell (layout + nav), header, sidebar
  shared/       ← api (axios http client), config (env.ts), i18n, lib, ui (primitives)
```

`@/` alias maps to `src/`.

### HTTP client and auth flow
`shared/api/http.ts` — Axios instance that reads the access token from Zustand `auth-store` on every request. A 401 response clears the session and redirects to `/login`.

### Environment variables
Validated with Zod at boot (`shared/config/env.ts`):
- `VITE_API_BASE_URL` — backend base URL (default `http://localhost:8080`)
- `VITE_API_MODE` — `live` (real API) or `mock` (local fixtures from `shared/api/mockDb.ts`)

Both values are **baked at build time**. In Docker Compose they are passed as build args.

### Remember me
`auth-store.ts` uses a custom storage that switches between `localStorage` (remember me = true) and `sessionStorage` (remember me = false).

### i18n
`shared/i18n/index.tsx` — simple dictionary context, supports `en` and `ru`. The dictionary maps English string keys to Russian translations. Default language falls back to `en` (no-op translation).

## Frontend-2.0 differences

- Radix UI primitives (`@radix-ui/*`) instead of custom headless components
- Dark / light theme support (managed via `ui-store`)
- Additional widgets: `widgets/header`, `widgets/side-bar`
- Radix `react-dialog`, `react-dropdown-menu`, `react-select`, `react-toast` replace custom implementations in v1
- `react-router-dom` v6 (v1 uses v7)

## Agent worker

`Worker.cs` is the main hosted service. On startup it enrolls or recovers the agent (using `EnrollmentToken`), then runs a loop:
- Heartbeat every `HeartbeatIntervalSeconds` (default 60s)
- Policy sync every `PolicySyncIntervalSeconds` (default 30s)
- Executes due backup jobs: filesystem ZIP or logical DB dump (`pg_dump` / `mysqldump`)
- Requests presigned upload ticket from backend, uploads artifact directly to MinIO

Local state persists in `state/agent-state.json` (`AgentId`, `ServerAddress`, `AccessToken`). If `ServerAddress` is saved, it overrides `Api:BaseUrl` in config.
