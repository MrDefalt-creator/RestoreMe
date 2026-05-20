# RestoreMe

RestoreMe is a backup management system with these main parts:
- `Backup.Server.Api` - ASP.NET Core backend API
- `Backup.Agent.Worker` - agent that registers, synchronizes policies, sends heartbeat and executes backups
- `Frontend` - stable React admin panel for operators and administrators
- `Frontend-2.0` - flagship next-generation UI prototype built on the same backend contracts

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
    Backup.Agent.Worker/
    Backup.Shared.Contracts/
  Frontend/
  Frontend-2.0/
  docker-compose/
    docker-compose.yml
    .env
    secrets/
  README.md
```

## Main Capabilities

### Backend
- layered architecture: API / Application / Domain / Infrastructure / Shared.Contracts
- pending agent registration and approval flow
- heartbeat processing
- policy CRUD for filesystem and logical database backups
- backup jobs lifecycle: start, fail, complete
- artifact storage in MinIO and artifact download through backend
- automatic EF Core migrations on startup
- file-based secret support through `*_FILE`
- JWT authentication for panel users
- role model: `admin`, `operator`, `viewer`
- agent bootstrap protection through enrollment token and dedicated agent access tokens

### Agent
- can receive an `AgentId` after pending registration or reuse a saved one
- stores local state in `state/agent-state.json`
- stores backend server address and agent access token locally
- sends heartbeat and periodically synchronizes policies
- executes filesystem backup policies
- executes logical PostgreSQL and MySQL dump policies
- uploads prepared payloads directly to object storage through upload tickets returned by backend

### Frontend v1
- secure login page with `Remember me`
- dashboard
- agents page
- pending agents approval page
- policies page
- jobs page
- artifacts page
- account page for self-service password change
- users page for administrator access management
- automatic polling in live mode

### Frontend 2.0
- Apple-like flagship UI prototype for the same RestoreMe backend
- dark and light themes
- refined dashboard with activity trend, protection mix and attention items
- agents page with filters, policy coverage and details dialog
- pending agent approve and reject flows
- policies, jobs and backups/artifacts views aligned with current backend DTOs
- automatic polling and query invalidation tuned for live operational use

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
- `API_PUBLIC_URL` — public backend URL baked into the Vite bundle and used in the CSP `connect-src` of both frontends

Default published addresses:
- frontend v1: `http://localhost:5173`
- frontend 2.0: `http://localhost:5174`
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
cd Frontend
yarn
yarn dev
```

Frontend 2.0:
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
6. Open `http://localhost:5173` for the stable frontend, or `http://localhost:5174` for Frontend 2.0.
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
- `Jwt:Issuer`, `Jwt:Audience`
- `Jwt:SigningKey` — user-token signing key
- `Jwt:AgentSigningKey` — optional dedicated key for agent JWTs; rotating it does not invalidate user sessions
- `AgentEnrollment:EnrollmentToken`
- `Notifications:FailureWebhookUrl`, `Notifications:WebhookSecret` — backup/restore failure webhook (HMAC-SHA256 signing when secret is set)
- `Cors:AllowedOrigins` — required in Production; backend refuses to start when empty or loopback-only

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

### Failure webhook

When `Notifications:FailureWebhookUrl` is set the backend POSTs a JSON body to it on every failed backup or restore job. Pair with `Notifications:WebhookSecret` to enable HMAC-SHA256 signing:

```
X-RestoreMe-Signature: sha256=<hex of HMAC-SHA256(body, WebhookSecret)>
```

Receivers should constant-time compare against the same digest computed over the raw bytes of the request body. The webhook HTTP client has a 10-second timeout; slow receivers will not block the failure-reporting path.

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

The seeded admin is created with `MustChangePassword=true`. On the very first sign-in, both frontends will redirect to the Account page and lock the rest of the workspace until the operator picks their own password. The API enforces this server-side too: every request other than `/api/auth/me`, `/api/auth/change-password`, `/api/auth/logout` returns `403 { "code": "must_change_password" }` while the flag is set.

The same flag is set whenever an admin resets another user's password — the target user signs in once with the temporary value and is forced to rotate.

Source:
- [Backup/Backup.Server.Api/appsettings.Development.json](Backup/Backup.Server.Api/appsettings.Development.json)

Important behavior:
- seeding runs only when there are no users in the database yet
- if users already exist, the seed does not overwrite them
- for an already populated database, you should manage users through the panel or the database itself

### Panel roles

- `viewer` - read-only access to the workspace
- `operator` - can work with agents, policies, jobs and artifacts
- `admin` - full access, including user management

### User management rules

Implemented safeguards:
- at least one active administrator must remain in the system
- the current signed-in account cannot be deleted from the admin table
- the current signed-in account cannot be disabled from the admin table
- the current signed-in account cannot have its role changed from the admin table
- every signed-in user can change their own password on the `Account` page
- only administrators can create users, change other users' passwords, disable users and delete users

### Session token storage

The access JWT lives in an HTTP-only `access_token` cookie set by the backend. JavaScript on the frontend never sees the token, so an XSS payload cannot exfiltrate it. The cookie is `SameSite=Strict` and gets `Secure` automatically outside Development. Both frontends are configured with `withCredentials: true`.

A small profile of the current user (id, username, role, `mustChangePassword` flag) is stored on the frontend so the UI can render the right pages.

### Remember me behavior

The login page allows the user to choose session persistence:
- if `Remember me` is enabled, the cookie carries an explicit `Expires` matching the JWT lifetime
- if `Remember me` is disabled, the cookie is session-only and disappears when the browser closes
- the cached frontend profile follows the same persistence choice (localStorage vs sessionStorage)

### Password and session invalidation

Every user JWT carries a `stamp` claim bound to `AppUser.SecurityStamp`. Whenever the password is changed (self-change, admin reset) the stamp is regenerated server-side; any token issued before the change immediately fails validation on its next call. The check is cached in-memory for 30 seconds to keep it cheap.

### Agent revocation

Admins can revoke an individual agent from the Agents page in either frontend (only visible to `admin` users). The backend bumps `Agent.TokenVersion`; the agent's JWT carries the previous version as `tokver` and fails on the next call. The agent will need to re-enroll using the enrollment token to get a fresh access token. The action is recorded in the audit log as `agent.revoke`.

### Audit log

The backend writes audit entries for every critical action: user create / delete / status change / role change / password reset, agent approve / reject / revoke. Admin-only `GET /api/audit-logs` returns paginated entries with actor username joined server-side. Both frontends expose a read-only `/audit-log` page (admin-only) with filtering by action.

## Agent Security Model

### Bootstrap and regular operation

Agent security now works in two phases:

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
- for another machine or server, change `Api:BaseUrl` to the real backend address before starting the agent

### Agent local state

The agent stores local state in:
- `state/agent-state.json` — encrypted with ASP.NET Core DataProtection
- `state/keys/` — DataProtection key ring (persisted across restarts via `PersistKeysToFileSystem` + `SetApplicationName("RestoreMe.Agent")`)

That state can contain:
- `AgentId`
- `ServerAddress`
- `AccessToken`

Behavior:
- if a saved `ServerAddress` exists, it has priority over config `Api:BaseUrl`
- if an agent already has `AgentId` but no access token, it can recover a new token through enrollment flow
- if the agent still connects to an old backend after changing config, update or delete `state/agent-state.json`
- when running the agent in Docker, mount `state/` (including `state/keys/`) on a volume so the encrypted state survives container recreation

### Agent resilience

The agent's HTTP clients use the .NET standard resilience handler — retry with exponential backoff, circuit breaker, per-attempt and total timeouts. Transient network blips no longer drop heartbeats or sync attempts permanently.

### Restore safety

Before overwriting any filesystem restore target the agent renames the existing path to `{path}.pre-restore-{utcTimestamp}` so a bad restore can be rolled back manually. ZIP archives are extracted with a per-entry path check (zip-slip guard) — a malicious or corrupted artifact with entries like `../../etc/shadow` is rejected before any file is written.

## Storage Addressing Model

Two storage addresses are important:
- `Storage:Endpoint` - internal MinIO address used by backend
- `Storage:PublicEndpoint` - external address used in upload URLs returned to agents

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

## Frontend Setup

Stable frontend folder:
- [Frontend](Frontend)

Useful commands:
```powershell
cd Frontend
yarn
yarn dev
yarn build
yarn preview
```

Typical local frontend environment:
```env
VITE_API_BASE_URL=http://localhost:8080
VITE_API_MODE=live
```

Modes:
- `live` - use real backend API
- `mock` - use local fixtures for offline/demo work

## Frontend 2.0 Setup

Frontend 2.0 folder:
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

Notes:
- Frontend 2.0 is the flagship UI prototype, not the primary diploma baseline.
- It uses the same backend and database as the original frontend.
- Data created in one frontend should be visible in the other after refetch/polling.
- In Docker Compose it is published on `http://localhost:5174`.

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
- `credentials` - recommended universal mode
- `integrated` - no password is stored in the policy; `pg_dump` must already be able to access the database as the OS user running the agent

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
6. Set interval.
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
```

### Agent
```powershell
cd Backup
dotnet build .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
dotnet run --project .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
```

### Frontend
```powershell
cd Frontend
yarn
yarn build
```

### Frontend 2.0
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
docker compose logs -f frontend
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
- update `ServerAddress` manually
- or delete the state file and restart the agent

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

- [docker-compose/README.md](docker-compose/README.md)
- [Frontend/README.md](Frontend/README.md)
- [Frontend-2.0/README.md](Frontend-2.0/README.md)


