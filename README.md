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

Important sections:
- `ConnectionStrings:DefaultConnection`
- `ConnectionStrings:DefaultConnection_FILE`
- `Storage:Endpoint`
- `Storage:PublicEndpoint`
- `Storage:AccessKey`
- `Storage:AccessKey_FILE`
- `Storage:SecretKey`
- `Storage:SecretKey_FILE`
- `Storage:BucketName`
- `Storage:UseSsl`
- `Storage:UploadUrlExpirySeconds`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:SigningKey`
- `AgentEnrollment:EnrollmentToken`

### Production-minded note

For local deployment, file-based Docker secrets are a good improvement over plain YAML values.
For real production, a dedicated secret manager or platform secret store is still preferable.

## Authentication and Roles

### Bootstrap administrator

In `Development`, the system seeds exactly one initial administrator if the user table is empty.

Current dev credentials:
- `admin / Admin123!`

> [!WARNING]
> Change the bootstrap administrator password immediately after the first login. The checked-in value is public development bootstrap data, not a secret.

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

### Remember me behavior

The login page allows the user to choose session persistence:
- if `Remember me` is enabled, the session is stored in `localStorage`
- if `Remember me` is disabled, the session lives only in `sessionStorage`
- a non-persistent session disappears when the browser session ends

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
- `state/agent-state.json`

That state can contain:
- `AgentId`
- `ServerAddress`
- `AccessToken`

Behavior:
- if a saved `ServerAddress` exists, it has priority over config `Api:BaseUrl`
- if an agent already has `AgentId` but no access token, it can recover a new token through enrollment flow
- if the agent still connects to an old backend after changing config, update or delete `state/agent-state.json`

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


