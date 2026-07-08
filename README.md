# RestoreMe

🇬🇧 English · [🇷🇺 Русский](README.ru.md)

RestoreMe is a self-hosted backup management system with these main parts:
- `Backup.Server.Api` — ASP.NET Core backend API
- `Backup.Agent.Worker` — agent that registers, synchronizes policies, sends heartbeat and executes backups
- `Frontend-2.0` — React admin panel for operators and administrators

The system uses:
- PostgreSQL for relational data
- MinIO for object storage
- Docker Compose for local full-stack startup

> [!WARNING]
> Read this README and [docker-compose/README.md](docker-compose/README.md) before starting the stack. The repository intentionally includes Docker Compose `.env` and starter secret files to make first setup faster, but all default credentials and tokens must be changed before public, shared or production-like deployment.

## Repository Layout

```text
RestorMe/
  Backup/
    Backup.Server.Api/
    Backup.Server.Application/
    Backup.Server.Domain/
    Backup.Server.Infrastructure/
    Backup.Server.Tests/
    Backup.Agent.Worker/
    Backup.Shared.Contracts/
  Frontend-2.0/
  docker-compose/
    docker-compose.yml
    docker-compose.override.yml
    docker-compose.prod.yml
    .env
    secrets/
  installers/
  .github/workflows/
  README.md
```

## Main Capabilities

### Backend
- layered architecture: API / Application / Domain / Infrastructure / Shared.Contracts
- pending agent registration and approval flow
- heartbeat processing
- policy CRUD for filesystem and logical database backups
- flexible policy scheduling — a fixed interval (optionally confined to a daily backup window, may span midnight) or a 5-field cron expression with an IANA timezone (DST-aware); all next-run computation happens server-side, agents are unaffected
- backup jobs lifecycle: start, fail, complete
- artifact storage in MinIO and artifact download through backend
- automatic EF Core migrations on startup
- file-based secret support through `*_FILE`
- JWT authentication for panel users via HttpOnly cookie
- role model: `admin`, `operator`, `viewer`
- agent bootstrap protection through enrollment token and dedicated agent access tokens
- Production startup guardrails — refuses to boot with dev-default secrets
- multi-channel notifications (Webhook / Telegram / Slack / Discord) with per-event subscriptions, secrets encrypted at rest
- automatic policy auto-disable after repeated consecutive failures
- agent offline / back-online detection via a background health sweep
- retention policies (age / count / total-size budget) with a background cleanup sweep
- artifact integrity verification — SHA256 re-hash on upload, on-demand verify, and a scheduled background scrub
- audit log of critical actions

### Agent
- can receive an `AgentId` after pending registration or reuse a saved one
- stores local state in `state/agent-state.json` (encrypted with ASP.NET Core DataProtection)
- stores backend server address and agent access token locally
- sends heartbeat and periodically synchronizes policies
- executes filesystem backup policies
- executes logical PostgreSQL and MySQL dump policies
- uploads prepared payloads directly to object storage through presigned upload tickets returned by backend

### Frontend
- Apple-like operator console built on Radix UI
- dark and light themes
- dashboard with activity trend, protection mix and attention items
- agents page with filters, policy coverage and details dialog
- install-agent wizard (copy-paste one-liner that pulls installer + binary from the backend)
- pending agent approve and reject flows
- policies, jobs and backups/artifacts views aligned with current backend DTOs
- policies page surfaces the "Auto-disabled" state with a one-click re-enable
- retention controls in the policy form (keep by age / count / total size)
- schedule editor in the policy form — interval or cron mode with daily/weekly/monthly presets, a timezone picker, an optional backup window and a live "next three runs" preview; policy lists render schedules human-readably ("Daily at 03:00 (Europe/Moscow)")
- backups/artifacts page shows a per-artifact integrity badge with a "Verify now" action
- admin-only notification channels page (`/notifications`) — add/edit/test Webhook, Telegram, Slack and Discord channels; also hosts the integrity scrub-schedule settings
- automatic polling and query invalidation
- admin-only audit log view

## Prerequisites

### Local development without Docker
- .NET SDK 10
- Node.js 22+
- Yarn 1.x
- PostgreSQL
- MinIO

### Local full stack with Docker
- Docker Desktop
- Docker Compose

## Recommended Startup Modes

### Option 1. Full stack through Docker Compose

This is the easiest and recommended local startup path.

> [!WARNING]
> Before running Compose outside a private local test environment, replace every checked-in value in `docker-compose/secrets`, rotate the bootstrap administrator password after first login, and replace JWT/enrollment tokens in backend and agent configuration.

```powershell
cd docker-compose
docker compose up --build
```

`docker-compose.override.yml` is auto-loaded for local dev (sets `ASPNETCORE_ENVIRONMENT=Development`, exposes the MinIO admin console). For production-style deployments use the prod overlay:

```powershell
cd docker-compose
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

Required env (or `.env.prod` next to the compose files):
- `CORS_ORIGIN` — public origin of the frontend (e.g. `https://restoreme.example.com`)
- `API_PUBLIC_URL` — public backend URL baked into the Vite bundle and used in the frontend's CSP `connect-src`

Default published addresses:
- frontend: `http://localhost:5173`
- backend: `http://localhost:8080`
- MinIO API: `http://localhost:9000`
- MinIO Console: `http://localhost:9001`
- PostgreSQL: `localhost:5432`

### Option 2. Manual startup

Backend:
```powershell
cd Backup
dotnet run --project .\Backup.Server.Api\Backup.Server.Api.csproj
```

Frontend:
```powershell
cd Frontend-2.0
yarn
yarn dev
```

Agent:
```powershell
cd Backup
dotnet run --project .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
```

## First Deployment Checklist

Use this sequence for a clean local deployment or first workstation setup.

1. Read [docker-compose/README.md](docker-compose/README.md).
2. Replace starter values in [docker-compose/secrets](docker-compose/secrets).
3. Check [docker-compose/.env](docker-compose/.env) if default ports are already occupied.
4. Start the stack with `docker compose up --build`.
5. Wait until backend applies migrations.
6. Open `http://localhost:5173`.
7. Sign in with the bootstrap administrator account.
8. Change the bootstrap administrator password.
9. Create additional users if needed.
10. Start one or more agents separately.
11. Approve pending agents in the panel.
12. Create policies and verify jobs/artifacts.

## Secrets and Sensitive Configuration

### Compose secrets directory

Local Docker startup expects these files in [docker-compose/secrets](docker-compose/secrets):
- `postgres-password.txt`
- `postgres-connection.txt`
- `minio-access-key.txt`
- `minio-secret-key.txt`

> [!WARNING]
> These files are committed only as local starter values. Treat them like templates with working defaults: replace them before pushing a deployed instance to any shared network, demo server or production-like environment.

Examples:

`postgres-password.txt`
```text
my_strong_postgres_password
```

`postgres-connection.txt`
```text
Host=postgres;Port=5432;Database=restoreme_db;Username=restoreme_user;Password=my_strong_postgres_password
```

`minio-access-key.txt`
```text
restoreme_minio_dev
```

`minio-secret-key.txt`
```text
restoreme_minio_dev_ChangeMe_2026!
```

### How backend reads secrets

The backend supports both regular config values and file-based secrets.

Examples:
- `ConnectionStrings:DefaultConnection`
- `ConnectionStrings:DefaultConnection_FILE`
- `Storage:AccessKey`
- `Storage:AccessKey_FILE`
- `Storage:SecretKey`
- `Storage:SecretKey_FILE`

Meaning:
- regular values are convenient for quick local development
- `*_FILE` is the preferred way when Docker mounts secret files into the container

### Important backend config sections

Main backend config file:
- [Backup/Backup.Server.Api/appsettings.json](Backup/Backup.Server.Api/appsettings.json)

A fully-annotated reference with every key and placeholder is available at:
- [Backup/Backup.Server.Api/appsettings.example.json](Backup/Backup.Server.Api/appsettings.example.json)

Important sections:
- `ConnectionStrings:DefaultConnection` / `ConnectionStrings:DefaultConnection_FILE`
- `Storage:Endpoint` (internal MinIO address used by backend)
- `Storage:PublicEndpoint` (external MinIO host baked into agent URLs)
- `Storage:AccessKey` / `Storage:AccessKey_FILE`
- `Storage:SecretKey` / `Storage:SecretKey_FILE`
- `Storage:BucketName`, `Storage:UseSsl`
- `Storage:UseAdaptiveExpiry`, `Storage:AdaptiveBaseSeconds`, `Storage:AdaptivePerGbSeconds` — adaptive presigned URL lifetime (see below)
- `Storage:UploadUrlExpirySeconds`, `Storage:DownloadUrlExpirySeconds` — static expiry fallbacks
- `Storage:VerifyChecksumBeforeComplete`, `Storage:ChecksumVerifyMaxBytes` — artifact integrity gate on upload (see [Artifact integrity verification](#artifact-integrity-verification))
- `Retention:CleanupIntervalHours` — cadence of the background retention cleanup sweep
- `Integrity:CheckIntervalSeconds` — how often the worker checks whether a scheduled integrity scrub is due (the schedule itself is admin-managed at runtime, not in config)
- `Jwt:Issuer`, `Jwt:Audience`
- `Jwt:SigningKey` — user-token signing key
- `Jwt:AgentSigningKey` — optional dedicated key for agent JWTs; rotating it does not invalidate user sessions
- `AgentEnrollment:EnrollmentToken`
- `Cors:AllowedOrigins` — required in Production; backend refuses to start when empty or loopback-only

> [!NOTE]
> Notifications are **no longer configured via `appsettings.json`**. The old single `Notifications:FailureWebhookUrl` has been replaced by admin-managed notification channels stored in the database (see [Notifications](#notifications) below).

### Production-minded note

For local deployment, file-based Docker secrets are a good improvement over plain YAML values.
For real production, a dedicated secret manager or platform secret store is still preferable.

### Production startup guardrails

When the backend starts under `ASPNETCORE_ENVIRONMENT=Production`, it refuses to come up if any of the following holds:
- `Jwt:SigningKey` is a known dev default or shorter than 32 bytes
- `Jwt:AgentSigningKey` is configured but duplicates `Jwt:SigningKey` or is shorter than 32 bytes
- `AgentEnrollment:EnrollmentToken` is empty or a known dev default
- `Cors:AllowedOrigins` is empty
- `Cors:AllowedOrigins` contains any loopback host (localhost / 127.0.0.1 / ::1)

These guards are intentional — they keep dev defaults from silently shipping to a real environment.

Production also enables `UseHsts()` (30-day pin, includes subdomains) and `UseHttpsRedirection()`. RestoreMe is intended to run behind a TLS-terminating reverse proxy (Caddy, Traefik, nginx) — give the backend container an internal network and publish only the reverse proxy on `:443`. If you must expose Kestrel directly, configure it with a certificate via `ASPNETCORE_Kestrel__Endpoints__Https__Certificate__Path` or the `Kestrel:Endpoints` section.

### Adaptive presigned URL expiry

Agents talk to MinIO over presigned URLs. The lifetime of each URL is sized to the payload by default so small jobs get short windows (safer) and large jobs get hours-or-days (still works):

```
expiry = AdaptiveBaseSeconds + sizeGB * AdaptivePerGbSeconds   (capped at 7 days)
```

Defaults — `AdaptiveBaseSeconds=600`, `AdaptivePerGbSeconds=300`:

| Payload | URL lifetime |
|---|---|
| 1 GB | ~15 minutes |
| 10 GB | ~1 hour |
| 100 GB | ~8.5 hours |
| 1 TB | ~3.6 days |

Set `Storage:UseAdaptiveExpiry: false` to fall back to the static `Storage:UploadUrlExpirySeconds`. Set `Storage:DownloadUrlExpirySeconds` (positive integer) to override the restore-download window independently.

### Notifications

RestoreMe ships a multi-channel notification system. Channels are created and managed by admins on the `/notifications` page (admin-only, served by `/api/notification-channels`) — there is no notification config in `appsettings.json`.

**Channel types:** `Webhook` (generic HMAC-signed), `Telegram`, `Slack`, `Discord`.

**Event types** a channel can subscribe to (leaving the subscription empty = receive all):
- `BackupFailed`, `RestoreFailed`, `BackupCompleted`
- `AgentOffline`, `AgentBackOnline`
- `PolicyAutoDisabled`
- `RetentionCleaned`, `IntegrityCheckFailed`

How it works:
- Each channel stores a per-type `Settings` JSON blob (bot token / webhook URL / shared secret). The whole blob is **encrypted at rest** via ASP.NET Core DataProtection — secrets are never returned by the API.
- On an event, the dispatcher fans it out to every enabled channel subscribed to that event type and routes each through the matching adapter. Delivery is **best-effort and isolated per channel**: one broken Slack URL can't suppress Telegram delivery, and a notification failure never blocks the job that triggered it.
- Every delivery attempt is written to the audit log as `notification.sent` / `notification.failed` (the rendered message body and secrets are deliberately excluded).
- The admin "Test channel" button sends a sample event through the real adapter so configuration can be verified.

**Generic webhook signing** — when a `Webhook` channel has a secret set, the request is signed:

```
X-RestoreMe-Signature: sha256=<hex of HMAC-SHA256(body, secret)>
```

Receivers should constant-time compare against the same digest computed over the raw bytes of the request body. Each adapter's HTTP client has a capped timeout so a slow receiver can't block the dispatch path.

### Policy auto-disable

A policy that fails **3 consecutive backups** is automatically disabled (`IsEnabled=false`), stamped with `AutoDisabledAt` and the last failure reason, written to the audit log as `policy.auto_disabled`, and announced through the `PolicyAutoDisabled` notification event — so a broken source or bad credentials stops spamming the audit log and notifications every interval. A successful backup resets the streak. The frontend marks such policies with an "Auto-disabled" badge; re-enabling one (toggle, or saving it enabled) clears the streak so the next failure starts a fresh count.

### Retention

Each policy carries three optional retention knobs: `RetentionDays`, `RetentionMaxCount` (keep the newest N) and `RetentionMaxTotalBytes` (size budget). A background `RetentionCleanupService` runs every `Retention:CleanupIntervalHours` (default 24h) and prunes artifacts that fall outside their policy's rules:

- newest-first, per policy; the newest artifact is **never** deleted — a policy always keeps at least one copy (the "floor")
- **keep-union** — when days and/or count are set, an artifact survives if it is within `RetentionDays` **or** among the newest `RetentionMaxCount`
- **size cap (hard)** — among the survivors, walking newest-first, anything whose cumulative size exceeds `RetentionMaxTotalBytes` is pruned (except the floor)
- a policy with no retention rule configured prunes nothing

Each deletion removes the object from MinIO, then the database row, is written to the audit log as `retention.deleted` (system action, no actor), and fires the `RetentionCleaned` notification event. The retention fields are editable in the policy form on the Policies page.

### Artifact integrity verification

RestoreMe guards against silently corrupted or truncated artifacts at several points:

- **On upload** — when an agent reports a finished upload, the backend re-reads the stored object from MinIO and recomputes its SHA256 (streamed through an incremental hash, so the whole artifact is never buffered) and compares it to the agent-reported checksum. With `Storage:VerifyChecksumBeforeComplete=true` (default) a mismatch fails the job — it never becomes `Completed` with a bad artifact; success is audit-logged as `artifact.verified`. `Storage:ChecksumVerifyMaxBytes` (null = no cap) skips the re-hash for objects larger than the limit — existence + size are still checked and the skip is logged as `artifact.verify_skipped`. Verification is also skipped when the agent reports no checksum.
- **On demand** — operators/admins can trigger `POST /api/backupartifacts/{id}/verify` ("Verify now") from the backups/artifacts page; each artifact shows an integrity badge (verified / failed / unverified).
- **On a schedule** — a background scrub sweep periodically re-verifies stored artifacts. The schedule (enabled, interval, run time, batch size) is **admin-managed at runtime** via `GET/PUT /api/integrity-settings` and the scrub-schedule card on the `/notifications` page — it is *not* hardcoded in config. `Integrity:CheckIntervalSeconds` only controls how often the worker wakes to check whether a run is due. A failed scrub fires the `IntegrityCheckFailed` notification event.
- **Before restore** — the agent re-checks the artifact checksum before applying a restore, so a corrupted copy is never written over a live target.

### Health endpoint

`GET /health` returns `200` only when:
- the backend can reach PostgreSQL (`AddDbContextCheck`)
- the backend can reach MinIO (custom probe via `BucketExistsAsync`)

Docker Compose uses the same endpoint for container healthchecks; the backend waits for both `db` and `minio` to be `service_healthy` before it starts.

## Authentication and Roles

### Bootstrap administrator

In `Development`, the system seeds exactly one initial administrator if the user table is empty.

Current dev credentials:
- `admin / Admin123!`

The seeded admin is created with `MustChangePassword=true`. This is an **advisory nudge, not a server-side gate** — the backend does not block any endpoint while the flag is set. The frontend surfaces it as a login toast, an Account-page banner, and a "Set a personal password" onboarding step so the operator is prompted to rotate the default credential, but nothing forces it. Treat default/temp credentials as a deployment-hardening responsibility, not an API guarantee.

The same flag is set whenever an admin resets another user's password, so the target user is prompted to pick their own password on next sign-in. It clears automatically on the next successful password change.

Source:
- [Backup/Backup.Server.Api/appsettings.Development.json](Backup/Backup.Server.Api/appsettings.Development.json)

Important behavior:
- seeding runs only when there are no users in the database yet
- if users already exist, the seed does not overwrite them
- for an already populated database, manage users through the panel or the database itself

### Panel roles

- `viewer` — read-only access to the workspace
- `operator` — can work with agents, policies, jobs and artifacts
- `admin` — full access, including user management

### User management rules

Implemented safeguards:
- at least one active administrator must remain in the system
- the current signed-in account cannot be deleted from the admin table
- the current signed-in account cannot be disabled from the admin table
- the current signed-in account cannot have its role changed from the admin table
- every signed-in user can change their own password on the `Account` page
- only administrators can create users, change other users' passwords, disable users and delete users

### Session token storage

The access JWT lives in an HTTP-only `access_token` cookie set by the backend. JavaScript on the frontend never sees the token, so an XSS payload cannot exfiltrate it. The cookie is `SameSite=Strict` and gets `Secure` automatically outside Development. The frontend is configured with `withCredentials: true`.

A small profile of the current user (id, username, role, `mustChangePassword` flag) is stored on the frontend so the UI can render the right pages.

### Remember me behavior

The login page allows the user to choose session persistence:
- if `Remember me` is enabled, the cookie carries an explicit `Expires` matching the JWT lifetime
- if `Remember me` is disabled, the cookie is session-only and disappears when the browser closes
- the cached frontend profile follows the same persistence choice (localStorage vs sessionStorage)

### Password and session invalidation

Every user JWT carries a `stamp` claim bound to `AppUser.SecurityStamp`. Whenever the password is changed (self-change, admin reset) the stamp is regenerated server-side; any token issued before the change immediately fails validation on its next call. The check is cached in-memory for 30 seconds to keep it cheap.

### Agent revocation

Admins can revoke an individual agent from the Agents page (only visible to `admin` users). The backend bumps `Agent.TokenVersion`; the agent's JWT carries the previous version as `tokver` and fails on the next call. The agent will need to re-enroll using the enrollment token to get a fresh access token. The action is recorded in the audit log as `agent.revoke`.

### Audit log

The backend writes audit entries for every critical action: user create / delete / status change / role change / password reset, agent approve / reject / revoke, policy auto-disable (`policy.auto_disabled`), artifact integrity outcomes (`artifact.verified` / `artifact.verify_skipped`), retention deletions (`retention.deleted`), and notification delivery outcomes (`notification.sent` / `notification.failed`). Admin-only `GET /api/audit-logs` returns paginated entries with actor username joined server-side. The frontend exposes a read-only `/audit-log` page (admin-only) with filtering by action.

## Installing the Agent

The agent ships as a self-contained, single-file binary for `linux-x64`, `linux-arm64`, and `win-x64`. No .NET runtime is required on the target host. The Frontend-2.0 install wizard prints the right one-liner pointing back at your own backend; the snippets below are the manual equivalents.

### Install agent on Linux

One-shot installer (Debian/Ubuntu/Fedora/etc., any systemd-based distro):

```bash
sudo curl -fsSL https://<your-backend>/installers/install-agent.sh -o /tmp/install-agent.sh
sudo bash /tmp/install-agent.sh \
  --server https://<your-backend> \
  --token <enrollment-token>
```

What it does:
- detects host architecture (`x86_64` → `linux-x64`, `aarch64` → `linux-arm64`)
- downloads the matching agent binary into `/opt/restoreme-agent/restoreme-agent`
- writes `/etc/restoreme-agent/config.env` with `RESTOREME_SERVER`, `RESTOREME_ENROLLMENT_TOKEN`, `RESTOREME_STATE_DIR` (mode `0600`)
- creates state directory `/var/lib/restoreme-agent/state/`
- installs and enables `restoreme-agent.service` via systemd

Useful flags:
- `--state-dir /custom/path` — store agent state somewhere other than `/var/lib/restoreme-agent/state`
- `--service-user restoreme` — run the agent as a dedicated non-root user (creates it on demand). Use `root` for filesystem backups of arbitrary paths.

Verify:

```bash
sudo systemctl status restoreme-agent
sudo journalctl -u restoreme-agent -f
```

Uninstall:

```bash
sudo bash /tmp/install-agent.sh --uninstall          # keeps /var/lib/restoreme-agent/state
sudo bash /tmp/install-agent.sh --uninstall --purge  # also wipes state
```

Manual install (without the script): copy the downloaded binary to `/opt/restoreme-agent/restoreme-agent`, create `/etc/restoreme-agent/config.env`, and adapt [installers/restoreme-agent.service](installers/restoreme-agent.service) — it documents the placeholder set.

### Install agent on Windows

From an elevated PowerShell session:

```powershell
$installer = "$env:TEMP\install-agent.ps1"
Invoke-WebRequest `
  -Uri https://<your-backend>/installers/install-agent.ps1 `
  -OutFile $installer -UseBasicParsing
& $installer -Server https://<your-backend> -Token <enrollment-token>
```

What it does:
- downloads `restoreme-agent-win-x64.exe` to `C:\Program Files\RestoreMe\Agent\restoreme-agent.exe`
- creates state directory `C:\ProgramData\RestoreMe\Agent\state\`
- registers the `RestoreMeAgent` Windows Service (auto-start, restart-on-failure)
- writes `RESTOREME_SERVER` / `RESTOREME_ENROLLMENT_TOKEN` / `RESTOREME_STATE_DIR` to the service's registry environment so SCM injects them at start
- starts the service

Useful parameters:
- `-StateDir 'D:\RestoreMe\state'` — relocate state off `%ProgramData%`

Verify:

```powershell
Get-Service RestoreMeAgent
Get-EventLog -LogName Application -Source 'RestoreMe*' -Newest 20
```

Uninstall:

```powershell
& $installer -Uninstall         # keeps state directory
& $installer -Uninstall -Purge  # also wipes state
```

### Default state-directory locations

When neither `--state-dir` nor `RESTOREME_STATE_DIR` is set, the agent picks an OS-appropriate default and falls back to `<AppContext.BaseDirectory>/state` only if the default isn't writable (typical for `dotnet run` from a developer checkout):

| OS | Default state directory |
|---|---|
| Linux | `/var/lib/restoreme-agent/state` |
| Windows | `%ProgramData%\RestoreMe\Agent\state` |
| macOS | `~/Library/Application Support/RestoreMe/Agent/state` |

The startup log line `state directory: <path> (source: <origin>)` always names the location actually used.

## Agent Security Model

### Bootstrap and regular operation

Agent security works in two phases:

1. The agent uses `Api:EnrollmentToken` for initial registration and access recovery.
2. After approval, the backend issues a dedicated agent access token.
3. The agent stores this token in local state and uses it for:
   - heartbeat
   - policy sync
   - backup job start/finish/fail
   - artifact registration
   - upload ticket requests

### Agent config

Main agent config file:
- [Backup/Backup.Agent.Worker/appsettings.json](Backup/Backup.Agent.Worker/appsettings.json)

Important settings:
- `Api:BaseUrl`
- `Api:EnrollmentToken`
- `Agent:AgentId`
- `Agent:HeartbeatIntervalSeconds`
- `Agent:PolicySyncIntervalSeconds`
- `Agent:PostgreSqlDumpCommand`
- `Agent:MySqlDumpCommand`

> [!WARNING]
> Replace `AgentEnrollment:EnrollmentToken` on the backend and `Api:EnrollmentToken` on every agent before using the system outside local development.

Important note:
- checked-in agent defaults point to the local Docker Compose backend at `http://localhost:8080/`
- for another machine or server, point the agent at the real backend via the `--server` flag, `RESTOREME_SERVER` env var, or by editing `Api:BaseUrl` in `appsettings.json`

### Running the agent against a remote backend

The agent reads its server URL and enrollment token from three sources, in this order of precedence:

1. **CLI flags** — `--server <url>`, `--enrollment-token <token>`
2. **Environment** — `RESTOREME_SERVER`, `RESTOREME_ENROLLMENT_TOKEN`
3. **`appsettings.json`** — `Api:BaseUrl`, `Api:EnrollmentToken`

```powershell
BackupAgent --server http://my-backend:8080 --enrollment-token <token>
```

```bash
RESTOREME_SERVER=http://my-backend:8080 \
RESTOREME_ENROLLMENT_TOKEN=<token> \
  ./BackupAgent
```

The agent persists the resolved URL into `state/agent-state.json` so subsequent runs keep going to the same backend. When you pass `--server` with a different URL, the agent updates the local state and logs a `WARNING` about the change. No state-file hunting required.

### Resetting agent state

If the agent is wedged on an old URL, a revoked token, or you simply want a clean slate:

```powershell
BackupAgent --reset-state
```

or set `RESTOREME_RESET_STATE=1` once. This wipes `state/agent-state.json` and `state/keys/` before the agent starts, so the next run is a fresh enrollment. Combine with `--server` / `--enrollment-token` to redirect at the same time.

### Agent local state

The agent stores local state in:
- `state/agent-state.json` — encrypted with ASP.NET Core DataProtection (contains `AgentId`, `ServerAddress`, `AccessToken`)
- `state/keys/` — DataProtection key ring (persisted across restarts via `PersistKeysToFileSystem` + `SetApplicationName("RestoreMe.Agent")`)

Behavior:
- CLI/ENV overrides win over local state — operator stays in control without editing files
- if an agent already has `AgentId` but no access token, it can recover a new token through the enrollment flow
- when running the agent in Docker, mount `state/` (including `state/keys/`) on a volume so the encrypted state survives container recreation

### Common agent errors and what to do

| Symptom | Likely cause | Fix |
|---|---|---|
| `Cannot reach RestoreMe backend` log line | Wrong URL or backend unreachable | `BackupAgent --server <correct-url> --reset-state` |
| `Backend rejected the agent token` (401 on heartbeat) | Agent revoked from the panel, JWT key rotated, or token version drift | `BackupAgent --server <url> --enrollment-token <token> --reset-state` |
| Agent keeps connecting to localhost after changing config | Old `ServerAddress` persisted in state | Either `--server <url>` to override, or `--reset-state` to wipe |
| `Api:BaseUrl is not configured` | First start without CLI/ENV/config | Pass `--server <url>` or set `RESTOREME_SERVER` |

### Agent resilience

The agent's HTTP clients use the .NET standard resilience handler — retry with exponential backoff, circuit breaker, per-attempt and total timeouts. Transient network blips no longer drop heartbeats or sync attempts permanently.

### Restore safety

Before overwriting any filesystem restore target the agent renames the existing path to `{path}.pre-restore-{utcTimestamp}` so a bad restore can be rolled back manually. ZIP archives are extracted with a per-entry path check (zip-slip guard) — a malicious or corrupted artifact with entries like `../../etc/shadow` is rejected before any file is written.

## Storage Addressing Model

Two storage addresses are important:
- `Storage:Endpoint` — internal MinIO address used by backend
- `Storage:PublicEndpoint` — external address used in upload URLs returned to agents

### Simple deployment

In the common case the agent only needs the backend address.

Example:
- backend: `http://my-server:8080`
- storage: `http://my-server:9000`

In this case the backend can build correct upload URLs for the agent automatically.

### When `Storage:PublicEndpoint` must be set explicitly

Set it explicitly when:
- backend and storage are exposed on different domains
- storage is published through another reverse proxy
- the agent reaches backend through one address, but must reach storage through another address

## Database and Migrations

Migrations live in:
- [Backup/Backup.Server.Infrastructure/Migrations](Backup/Backup.Server.Infrastructure/Migrations)

Behavior:
- backend applies migrations automatically on startup
- empty database is initialized automatically
- up-to-date database continues startup normally

Create a new migration manually:
```powershell
cd Backup
dotnet ef migrations add MigrationName --project .\Backup.Server.Infrastructure\Backup.Server.Infrastructure.csproj --startup-project .\Backup.Server.Api\Backup.Server.Api.csproj --output-dir Migrations
```

## Backend tests (xUnit)

Test project — `Backup/Backup.Server.Tests/`. Uses SQLite + DataProtection.

```powershell
cd Backup
dotnet test BackupSystem.slnx                                       # whole solution
dotnet test .\Backup.Server.Tests\Backup.Server.Tests.csproj        # just the test project
dotnet test --filter "FullyQualifiedName~AgentSelectiveDelete"      # single class/test
```

CI (`.github/workflows/ci.yml`) runs `restore` → `build --configuration Release` → `test --no-build` for the backend on every push, plus `yarn install` → `lint` → `typecheck` → `build` for the frontend.

## Frontend Setup

Frontend folder:
- [Frontend-2.0](Frontend-2.0)

Useful commands:
```powershell
cd Frontend-2.0
yarn
yarn dev
yarn build
yarn preview
```

Typical local environment:
```env
VITE_API_BASE_URL=http://localhost:8080
VITE_API_MODE=live
```

Modes:
- `live` — use real backend API
- `mock` — use local fixtures for offline/demo work

In Docker Compose the frontend is published on `http://localhost:5173`.

## Logical Database Dump Policies

### Required native tools

The agent machine must have the native dump tools installed:
- PostgreSQL: `pg_dump`
- MySQL: `mysqldump`

For predictable behavior across machines, you can set absolute tool paths in agent config:

```json
{
  "Agent": {
    "PostgreSqlDumpCommand": "C:\\Program Files\\PostgreSQL\\18\\bin\\pg_dump.exe",
    "MySqlDumpCommand": "C:\\Program Files\\MySQL\\MySQL Server 8.0\\bin\\mysqldump.exe"
  }
}
```

### PostgreSQL auth modes

PostgreSQL policies support:
- `credentials` — recommended universal mode
- `integrated` — no password is stored in the policy; `pg_dump` must already be able to access the database as the OS user running the agent

Recommended rule:
- for the local Docker Compose PostgreSQL container, use `credentials`
- use `integrated` only for a deliberately configured local PostgreSQL installation

### Manual validation before creating a policy

Credentials mode example:
```powershell
$env:PGPASSWORD = 'your_password'
pg_dump --no-password --host 127.0.0.1 --port 5432 --username restoreme_user --format=plain --file test.sql restoreme_db
```

Integrated mode example:
```powershell
pg_dump --no-password --host 127.0.0.1 --port 5432 --format=plain --file test.sql restoreme_db
```

If the manual command fails, the RestoreMe policy will fail too.

## Typical Operator Workflow

### Approve a new agent
1. Start backend and frontend.
2. Start the worker agent.
3. Open `Pending`.
4. Approve the machine and assign an agent name.
5. The agent continues under the assigned `AgentId` and access token.

### Create a backup policy
1. Open `Policies`.
2. Select an approved agent.
3. Choose a policy type.
4. For `Filesystem`, enter a source path.
5. For `PostgreSQL` or `MySQL`, enter database settings and auth mode.
6. Choose a schedule: a fixed interval (optionally confined to a daily backup window) or a cron expression with a timezone — the form previews the next three runs.
7. Save the policy.

### Execute and inspect a backup
1. The agent synchronizes policies.
2. When a policy is due, the agent starts a backup job.
3. The agent prepares a ZIP archive or logical dump file.
4. The agent requests an upload ticket from backend.
5. Backend returns a presigned MinIO upload URL.
6. The agent uploads payload directly to object storage.
7. The job and artifact become visible in the panel.

## Common Commands

### Backend
```powershell
cd Backup
dotnet build .\Backup.Server.Api\Backup.Server.Api.csproj
dotnet run --project .\Backup.Server.Api\Backup.Server.Api.csproj
dotnet test BackupSystem.slnx
```

### Agent
```powershell
cd Backup
dotnet build .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
dotnet run --project .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
```

### Frontend
```powershell
cd Frontend-2.0
yarn
yarn build
```

### Docker Compose
```powershell
cd docker-compose
docker compose up --build
docker compose down
docker compose logs -f backend
docker compose logs -f frontend-2
docker compose logs -f minio
docker compose logs -f db
```

## Troubleshooting

### Frontend login does not reach backend
Check:
- frontend was rebuilt after the latest login page changes
- backend is actually running on the expected address
- `VITE_API_BASE_URL` points to the correct backend URL

### Only the bootstrap admin should exist, but old users are still present
Reason:
- user seeding only runs when the user table is empty

Fix:
- use a clean database for a fresh first startup
- or delete old users manually through the panel/database if you want to return to a single-admin state

### Agent keeps connecting to the old backend address
Reason:
- `ServerAddress` is already saved in `state/agent-state.json`

Fix:
- `BackupAgent --server <correct-url>` to override, or `--reset-state` to wipe

### Agent can reach backend but cannot upload to MinIO
Check:
- MinIO port is reachable from the agent machine
- backend returned the correct public storage host
- `Storage:PublicEndpoint` is configured if storage host differs from backend host

### PostgreSQL logical dump fails with `no password supplied`
Reason:
- `integrated` mode is used against a database that is not configured for passwordless access

Fix:
- switch the policy to `credentials`
- use `127.0.0.1` instead of `localhost` for local testing if needed

## Additional Documentation

- [docker-compose/README.md](docker-compose/README.md) — [🇷🇺 Русский](docker-compose/README.ru.md)
- [Frontend-2.0/README.md](Frontend-2.0/README.md) — [🇷🇺 Русский](Frontend-2.0/README.ru.md)
- [README.ru.md](README.ru.md) — русский перевод этого файла
