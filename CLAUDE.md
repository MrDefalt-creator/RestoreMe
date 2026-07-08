# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

RestoreMe is a self-hosted backup management system. The monorepo contains:
- `Backup/` — ASP.NET Core 10 backend + agent worker + xUnit test project (solution file: `Backup/BackupSystem.slnx`)
- `Frontend-2.0/` — React admin panel (Radix UI, dark/light theme)
- `docker-compose/` — full-stack local startup, neutral baseline + dev override + prod overlay
- `installers/` — Linux (`install-agent.sh`, systemd unit) and Windows (`install-agent.ps1`) installers for the self-contained agent binary
- `.github/workflows/` — `ci.yml` (build+test backend, lint/typecheck/build the frontend) and `release-agent.yml`

## Commands

### Full stack (recommended)
```powershell
cd docker-compose
docker compose up --build
```
Default ports: frontend `:5173`, backend `:8080`, MinIO API `:9000`, MinIO Console `:9001`, PostgreSQL `:5432`.

Production-style startup (skips dev override, adds prod overlay; requires `CORS_ORIGIN` + `API_PUBLIC_URL`):
```powershell
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

Build self-hosted agent binaries (linux-x64, linux-arm64, win-x64) into the shared `agent_binaries` volume — run once after `compose up`, and again any time the agent code changes:
```powershell
docker compose --profile build-agents up agent-builder
```

### Backend
```powershell
cd Backup
dotnet run --project .\Backup.Server.Api\Backup.Server.Api.csproj
dotnet run --project .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
dotnet build .\Backup.Server.Api\Backup.Server.Api.csproj
```

### Tests (xUnit)
```powershell
cd Backup
dotnet test BackupSystem.slnx                                       # whole solution
dotnet test .\Backup.Server.Tests\Backup.Server.Tests.csproj        # just the test project
dotnet test --filter "FullyQualifiedName~AgentSelectiveDelete"      # single test or class
```
CI runs `dotnet restore` → `dotnet build --configuration Release` → `dotnet test --no-build` on every push.

### EF Core migrations (run from `Backup/`)
```powershell
dotnet ef migrations add MigrationName --project .\Backup.Server.Infrastructure\Backup.Server.Infrastructure.csproj --startup-project .\Backup.Server.Api\Backup.Server.Api.csproj --output-dir Migrations
```
Migrations auto-apply on startup.

### Frontend-2.0
```powershell
cd Frontend-2.0
yarn                # install
yarn dev            # dev server
yarn build          # tsc -b && vite build
yarn lint           # eslint
yarn typecheck      # tsc --noEmit
yarn test           # vitest run (single run)
yarn test:watch     # vitest watch mode
yarn preview        # preview build
```

## Backend architecture

Clean architecture with strict layer isolation:

```
Backup.Shared.Contracts      DTOs shared between backend and agent
Backup.Server.Domain         Entities, Enums, Options (no deps)
Backup.Server.Application    Repository interfaces + application services
Backup.Server.Infrastructure EF Core (AppDbContext, repositories, migrations), MinIO StorageAccessService
Backup.Server.Api            Controllers, Program.cs (DI root), JWT + AgentEnrollment auth
Backup.Server.Tests          xUnit integration tests (SQLite + DataProtection)
Backup.Agent.Worker          Standalone worker: heartbeat, policy sync, backup execution, upload
```

`Program.cs` is the DI composition root — services, options, CORS, auth schemes, EF context all wired there.

### Auth model

Two JWT token types, distinguished by a `token_type` claim:
- **`user`** tokens — issued after login, carry the user's role (`viewer`, `operator`, `admin`)
- **`agent`** tokens — issued after agent approval, used for heartbeat/jobs/artifacts/upload-tickets

Authorization policies:
- `AdminReadPolicy` — viewer/operator/admin, user token
- `AdminWritePolicy` — operator/admin, user token
- `UserManagementPolicy` — admin only, user token
- `AgentPolicy` — agent token
- `AgentEnrollmentPolicy` — separate `AgentEnrollment` header scheme (enrollment token)

**User tokens live in an HttpOnly `access_token` cookie** issued by `AuthController` (`SameSite=Strict`, `Secure` outside Development, `Path=/`). JavaScript never sees the JWT — the frontend just sends `withCredentials: true`. Cookie deletion mirrors the same attributes (otherwise some browsers ignore it). When "Remember me" is on, the cookie carries an explicit `Expires` matching JWT lifetime; off → session-only.

The user JWT carries a `stamp` claim bound to `AppUser.SecurityStamp`. Any password change (self-change or admin reset) regenerates the stamp and invalidates all previously-issued tokens on next request. Check is in-memory-cached for 30s.

`mustChangePassword` is an advisory nudge, **not** a server-side gate. The flag is set on bootstrap-admin seed and on admin password reset, cleared on the next successful change. It rides in `CurrentUserResponse`/`auth-store` so the UI can prompt for rotation — Frontend-2.0 surfaces a login toast, an Account-page banner, and a "Set a personal password" onboarding step — but the backend does **not** block any endpoint while the flag is set. (An earlier `MustChangePasswordFilter` enforced a hard `403`; it was dropped and the flow is intentionally soft-nudge now.) Treat default/temp credentials as a deployment-hardening responsibility, not an API guarantee.

Agent revocation: bumping `Agent.TokenVersion` invalidates the agent's JWT (`tokver` claim mismatches). Agent must re-enroll using the enrollment token. Recorded as `agent.revoke` in the audit log.

### Audit log

Backend logs critical actions (user create/delete/role-change/status-change/password-reset, agent approve/reject/revoke, policy auto-disable, notification send/failure). Admin-only `GET /api/audit-logs` returns paginated entries with actor username joined server-side. The frontend exposes a read-only `/audit-log` page (admin-only) with filtering.

### Config secrets pattern

Backend resolves config values by checking a `*_FILE` sibling first; if it points to a file it reads from there, otherwise falls back to the plain value:
- `ConnectionStrings:DefaultConnection` / `ConnectionStrings:DefaultConnection_FILE`
- `Storage:AccessKey` / `Storage:AccessKey_FILE`
- `Storage:SecretKey` / `Storage:SecretKey_FILE`

Docker Compose mounts Docker secrets at `/run/secrets/` and uses the `_FILE` variants. Local dev uses plain values. The fully-annotated key reference is `Backup/Backup.Server.Api/appsettings.example.json`.

### Production startup guardrails

Under `ASPNETCORE_ENVIRONMENT=Production` the backend refuses to start if any of:
- `Jwt:SigningKey` is a known dev default or shorter than 32 bytes
- `Jwt:AgentSigningKey` (optional dedicated agent key) duplicates `Jwt:SigningKey` or is shorter than 32 bytes
- `AgentEnrollment:EnrollmentToken` is empty or a known dev default
- `Cors:AllowedOrigins` is empty or only contains loopback hosts (localhost / 127.0.0.1 / ::1)

Production also enables `UseHsts()` (30-day, includes subdomains) and `UseHttpsRedirection()`. Intended to run behind a TLS-terminating reverse proxy.

### Storage addressing and presigned URLs

- `Storage:Endpoint` — internal MinIO address (backend → MinIO)
- `Storage:PublicEndpoint` — external address baked into presigned URLs returned to agents

Adaptive presigned URL expiry (default on): `expiry = AdaptiveBaseSeconds + sizeGB * AdaptivePerGbSeconds`, capped at 7 days. Defaults `AdaptiveBaseSeconds=600`, `AdaptivePerGbSeconds=300`. Disable with `Storage:UseAdaptiveExpiry=false` to fall back to static `Storage:UploadUrlExpirySeconds`. `Storage:DownloadUrlExpirySeconds` overrides the restore-download window.

### Artifact integrity verification

When an agent reports a finished upload (`AddArtifact` in `BackupJobsService`), the backend gates job completion on more than size. With `Storage:VerifyChecksumBeforeComplete=true` (default) it streams the stored object back out of MinIO through an `IncrementalHash` (`StorageAccessService.ComputeObjectSha256Async` — never buffers the whole artifact) and compares the recomputed SHA256 to the agent-reported checksum. A mismatch throws, so `AddArtifact` fails and the job is marked **Failed** — it never becomes `Completed` with a silently-corrupted/truncated artifact that happened to match on size. Success is audit-logged as `artifact.verified`.

Re-hashing is costly for huge backups: `Storage:ChecksumVerifyMaxBytes` (null = no cap) skips the re-hash for objects larger than the limit — existence + size are still checked, and the skip is audit-logged as `artifact.verify_skipped`. Verification is also skipped when the agent reports no checksum.

### Retention

Policies carry three optional retention knobs (`BackupPolicy`): `RetentionDays`, `RetentionMaxCount` (keep newest N), `RetentionMaxTotalBytes` (size budget). `RetentionEvaluator.SelectForDeletion` is **pure, DB-free** decision logic (heavily unit-tested in `Backup.Server.Tests/Retention`):
- Per policy, newest-first; the newest artifact is **never** deleted (a policy always keeps ≥1 copy — the "floor").
- **Keep-union**: when days and/or count are set, an artifact survives if it is within `RetentionDays` **OR** among the newest `RetentionMaxCount`. Pruned ones are attributed reason `Age`/`Count`.
- **Size cap (hard)**: among survivors, walking newest-first, any artifact whose cumulative size exceeds `RetentionMaxTotalBytes` is pruned (reason `Size`), except the floor.
- A policy with no retention rule configured prunes nothing.

`RetentionCleanupService` (`BackgroundService`, `Retention:CleanupIntervalHours` cadence, default 24h) pulls candidates via `IBackupArtifactRepository.GetArtifactsForRetentionAsync` (only artifacts of policies that have at least one rule, with Job+Policy loaded), runs the evaluator, deletes each from MinIO then DB, audit-logs each as `retention.deleted` (`ActorId=null`, system action), and fires `NotifyRetentionCleanedAsync`.

### Notifications (multi-channel)

The old single `Notifications:FailureWebhookUrl` config has been replaced by an admin-managed, DB-backed notification system (no notification config in `appsettings.json` anymore).

- **Channels** are `NotificationChannel` rows (admin-managed via `NotificationChannelsController` → `/notifications` page). Each has a `Type` (`Webhook`, `Telegram`, `Slack`, `Discord`), a per-type `Settings` JSON blob (encrypted at rest via DataProtection — every type carries a secret: bot token, webhook URL, or HMAC secret), and a comma-separated `SubscribedEvents` filter. `SubscribedEvents = NULL` means "all events" (the trivial upgrade path from the legacy single webhook).
- **Event types** (`NotificationEventType`): `BackupFailed`, `RestoreFailed`, `BackupCompleted`, `AgentOffline`, `AgentBackOnline`, `PolicyAutoDisabled`, `RetentionCleaned`.
- **`NotificationDispatcher`** (`INotificationService`) builds a channel-neutral `NotificationEvent`, fans it out to every enabled+subscribed channel, and routes each through its `INotificationChannelAdapter` (`GenericWebhookAdapter`/`TelegramAdapter`/`SlackAdapter`/`DiscordAdapter`, registered as typed `HttpClient`s with a capped timeout). Delivery is **best-effort**: a failing adapter (or even a DB error enumerating channels) is swallowed per-channel and logged, so one broken Slack URL can't suppress Telegram or block the failing backup job that triggered it. Every attempt is audit-logged as `notification.sent` / `notification.failed` (rendered message body and secrets deliberately excluded).
- **Test send**: `SendTestAsync` bypasses the `SubscribedEvents` filter so the admin "Test channel" button works even on channels that haven't opted into the test event.

### Policy auto-disable

A policy that fails `AutoDisableThreshold` (= **3**) consecutive backups is flipped `IsEnabled = false` (`BackupJobsService`), stamped with `AutoDisabledAt` + `LastFailureReason`, audit-logged, and fires `NotifyPolicyAutoDisabledAsync`. A successful backup resets the streak. Manual re-enable (toggle or update with `isEnabled=true`) is treated as the operator's "I fixed it" signal and clears `ConsecutiveFailureCount`/`LastFailureReason`/`AutoDisabledAt` so the next failure starts a fresh streak.

`AgentHealthSweepService` (`BackgroundService`, 30s cadence) polls heartbeat freshness and fires `AgentOffline`/`AgentBackOnline` transitions via `AgentHealthService.SweepAsync`. The first tick on startup is a non-notifying baseline pass.

### Live updates (SSE)

`GET /api/events` (user token, `AdminReadPolicy`) is a Server-Sent Events stream of **coarse topic events without payload** — `jobs`, `artifacts`, `restores`, `agents`, `policies`, `users`, `notification-channels`. Services publish through `IAdminEventBroadcaster` (`AdminEventBroadcaster` singleton: bounded channel of 64 per subscriber, `DropOldest`, publisher never blocks). The frontend (`shared/api/events.ts` + `ServerEventsBridge`) opens one `EventSource` and invalidates the matching TanStack Query groups with 250ms coalescing. While the stream is connected, `useLiveQueryOptions` disables interval polling; on disconnect, polling resumes automatically. Known gaps (intentional): audit log and in-drawer restore progress still poll; "Manual refresh only" UI mode disables SSE entirely; agent heartbeats are not published (offline/online transitions come from the health sweep); direct SQL edits bypass event publication.

### Health endpoint

`GET /health` returns `200` only when the backend can reach both PostgreSQL (`AddDbContextCheck`) and MinIO (`BucketExistsAsync` probe). Docker Compose uses this for container healthchecks; backend waits for `db` and `minio` to be `service_healthy`.

### Server-side pagination

Jobs, Backups (artifacts) and Agents list endpoints take `page`/`pageSize`/`sortBy`/`sortDir` and return a paged envelope; the corresponding Frontend-2.0 pages drive pagination and column sorting from URL query params (which also serve as deep links — `?id=…` opens the target entity on Agents/Policies/Backups).

### Control-plane self-backup (DR)

The `control-plane-backup` compose sidecar (in the base `docker-compose.yml`, no profile) runs `docker-compose/scripts/control-plane-backup.sh`: `pg_dump -Fc` of the metadata DB + tar of the `backend_keys` DataProtection volume into the bind-mounted `./backups/` every `BACKUP_INTERVAL_HOURS` (default 24), keeping `BACKUP_KEEP` (default 14) copies. Healthcheck = freshness of `.last-success`. Full restore procedure lives in `docs/DR-RUNBOOK.md`. Off-host copying of `./backups/` and MinIO object data replication are operator responsibilities (documented in the runbook). Note: DB table names are EF case-sensitive (`"AppUsers"`, not `Users`) — quote them in manual SQL.

### Dev credentials

- Bootstrap admin: `admin / Admin123!` (seeded only when user table is empty; `MustChangePassword=true` on first login)
- JWT signing key / enrollment token in `appsettings.json` are development defaults — refuse to boot in Production

## Frontend architecture

Frontend-2.0 follows **Feature-Sliced Design (FSD)**:

```
src/
  app/          providers, router, zustand stores (auth-store, ui-store)
  entities/     domain models and API query hooks (agent, artifact, auth, job, policy, user)
  features/     self-contained feature modules (policy-form, user-management, approve-agent, install-agent, notification-channel-form)
  pages/        route-level components assembled from features/widgets
  widgets/      app-shell, header, side-bar
  shared/       api (axios client), config (env.ts), i18n, lib, ui (primitives)
```

`@/` alias maps to `src/`. Radix UI primitives (`@radix-ui/*`) cover dialog/select/dropdown/toast. Dark/light theme is managed via `ui-store`.

### HTTP client and auth flow

`src/shared/api/client.ts` — Axios with `withCredentials: true` so the HttpOnly `access_token` cookie is sent automatically. JS never reads the JWT. 401 response triggers `clearSession()` + an `auth-events.emitUnauthorized` notification.

A small profile (id, username, role, `mustChangePassword`) lives in the Zustand `auth-store` so the UI can render the right pages without exposing the token. Storage backend switches between `localStorage` (remember me = true) and `sessionStorage` (remember me = false).

### Environment variables

Validated with Zod at boot (`shared/config/env.ts`):
- `VITE_API_BASE_URL` — backend base URL (default `http://localhost:8080`)
- `VITE_API_MODE` — `live` (real API) or `mock` (local fixtures)

Both values are **baked at build time**. In Docker Compose they are passed as build args. The prod overlay uses `API_PUBLIC_URL` for both the bundle and the CSP `connect-src`.

### i18n

`shared/i18n/index.tsx` — dictionary context, supports `en` and `ru`. Default falls back to `en` (no-op).

### Install-agent wizard

On the Agents page, admins/operators copy a one-liner that installs and enrols an agent on Linux or Windows. The installer script + agent binary are served from the backend itself (no GitHub dependency) at `/installers/binaries/*`; agent binaries are produced on demand by the `agent-builder` compose profile.

## Agent worker

`Worker.cs` is the main hosted service. On startup it enrolls or recovers the agent (using `EnrollmentToken`), then runs a loop:
- Heartbeat every `HeartbeatIntervalSeconds` (default 60s)
- Policy sync every `PolicySyncIntervalSeconds` (default 30s)
- Executes due backup jobs: filesystem ZIP or logical DB dump (`pg_dump` / `mysqldump`)
- Requests presigned upload ticket from backend, uploads artifact directly to MinIO

### State and key ring

- `state/agent-state.json` — encrypted with ASP.NET Core DataProtection (contains `AgentId`, `ServerAddress`, `AccessToken`)
- `state/keys/` — DataProtection key ring (persisted via `PersistKeysToFileSystem` + `SetApplicationName("RestoreMe.Agent")`)

In Docker mount `state/` (including `state/keys/`) on a volume so encrypted state survives container recreation.

### Configuration precedence (CLI > env > config file)

| Source | Server URL | Enrollment token |
|---|---|---|
| CLI | `--server <url>` | `--enrollment-token <token>` |
| Env | `RESTOREME_SERVER` | `RESTOREME_ENROLLMENT_TOKEN` |
| File | `Api:BaseUrl` | `Api:EnrollmentToken` |

Additional flags/env: `--reset-state` / `RESTOREME_RESET_STATE=1` wipes `state/agent-state.json` and `state/keys/` for a clean enrollment. `RESTOREME_STATE_DIR` relocates state. Persisted `ServerAddress` overrides `Api:BaseUrl` (logs a WARNING when CLI changes it).

### Resilience and restore safety

- HTTP clients use the .NET standard resilience handler (exponential backoff retry, circuit breaker, per-attempt + total timeouts) — transient blips don't drop heartbeats permanently.
- Before overwriting any filesystem restore target, the agent renames the existing path to `{path}.pre-restore-{utcTimestamp}` so a bad restore is recoverable.
- ZIP extraction uses a per-entry path check (zip-slip guard) — entries like `../../etc/shadow` are rejected before any file is written.

## Memory / branch status

- `preview` is the active branch — all current work lands there.
- `main` is frozen — coordinate before pushing.
- Legacy `Frontend/` was removed; the only UI is `Frontend-2.0/`.
- `docs/ROADMAP.md` is the prioritized backlog (Tier 2/3 of a July 2026 full-project audit); shipped items are struck through. `CHANGELOG.md` tracks delivered batches.
- On Windows hosts, port 8080 may fall into the OS reserved-port range (`netsh interface ipv4 show excludedportrange protocol=tcp`); set `API_PORT` in `docker-compose/.env` (e.g. `8200`) to remap — the frontend bundle bakes the API URL from it at build time.
