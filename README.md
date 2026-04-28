# RestoreMe

RestoreMe is a backup management system with three main parts:
- `Backup.Server.Api` - ASP.NET Core backend API
- `Backup.Agent.Worker` - agent that registers, synchronizes policies, sends heartbeat and executes backups
- `Frontend` - React admin panel for operators and administrators

The system uses:
- PostgreSQL for relational data
- MinIO for object storage
- Docker Compose for local full-stack startup

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

### Frontend
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

```powershell
cd D:\projects\RestorMe\docker-compose
docker compose up --build
```

Default published addresses:
- frontend: `http://localhost:5173`
- backend: `http://localhost:8080`
- MinIO API: `http://localhost:9000`
- MinIO Console: `http://localhost:9001`
- PostgreSQL: `localhost:5432`

### Option 2. Manual startup

Backend:
```powershell
cd D:\projects\RestorMe\Backup
dotnet run --project .\Backup.Server.Api\Backup.Server.Api.csproj
```

Frontend:
```powershell
cd D:\projects\RestorMe\Frontend
yarn
yarn dev
```

Agent:
```powershell
cd D:\projects\RestorMe\Backup
dotnet run --project .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
```

## First Deployment Checklist

Use this sequence for a clean local deployment or first workstation setup.

1. Fill secret files in [D:\projects\RestorMe\docker-compose\secrets](D:\projects\RestorMe\docker-compose\secrets).
2. Check [D:\projects\RestorMe\docker-compose\.env](D:\projects\RestorMe\docker-compose\.env) if default ports are already occupied.
3. Start the stack with `docker compose up --build`.
4. Wait until backend applies migrations.
5. Open `http://localhost:5173`.
6. Sign in with the bootstrap administrator account.
7. Create additional users if needed.
8. Start one or more agents separately.
9. Approve pending agents in the panel.
10. Create policies and verify jobs/artifacts.

## Secrets and Sensitive Configuration

### Compose secrets directory

Local Docker startup expects these files in [D:\projects\RestorMe\docker-compose\secrets](D:\projects\RestorMe\docker-compose\secrets):
- `postgres-password.txt`
- `postgres-connection.txt`
- `minio-access-key.txt`
- `minio-secret-key.txt`

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
minioadmin
```

`minio-secret-key.txt`
```text
strong_minio_secret
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
- [D:\projects\RestorMe\Backup\Backup.Server.Api\appsettings.json](D:\projects\RestorMe\Backup\Backup.Server.Api\appsettings.json)

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

For diploma and local deployment, file-based Docker secrets are a good improvement over plain YAML values.
For real production, a dedicated secret manager or platform secret store is still preferable.

## Authentication and Roles

### Bootstrap administrator

In `Development`, the system seeds exactly one initial administrator if the user table is empty.

Current dev credentials:
- `admin / Admin123!`

Source:
- [D:\projects\RestorMe\Backup\Backup.Server.Api\appsettings.Development.json](D:\projects\RestorMe\Backup\Backup.Server.Api\appsettings.Development.json)

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
- only administrators can create users, change чужие passwords, disable users and delete users

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
- [D:\projects\RestorMe\Backup\Backup.Agent.Worker\appsettings.json](D:\projects\RestorMe\Backup\Backup.Agent.Worker\appsettings.json)

Important settings:
- `Api:BaseUrl`
- `Api:EnrollmentToken`
- `Agent:AgentId`
- `Agent:HeartbeatIntervalSeconds`
- `Agent:PolicySyncIntervalSeconds`
- `Agent:PostgreSqlDumpCommand`
- `Agent:MySqlDumpCommand`

Important note:
- current checked-in agent `appsettings.json` still contains a legacy local `https://localhost:7104/` base URL as a placeholder
- for the current Docker stack, point the agent to `http://localhost:8080/` or to the real server address you want it to use

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
- [D:\projects\RestorMe\Backup\Backup.Server.Infrastructure\Migrations](D:\projects\RestorMe\Backup\Backup.Server.Infrastructure\Migrations)

Behavior:
- backend applies migrations automatically on startup
- empty database is initialized automatically
- up-to-date database continues startup normally

Create a new migration manually:
```powershell
cd D:\projects\RestorMe\Backup
dotnet ef migrations add MigrationName --project .\Backup.Server.Infrastructure\Backup.Server.Infrastructure.csproj --startup-project .\Backup.Server.Api\Backup.Server.Api.csproj --output-dir Migrations
```

## Frontend Setup

Frontend folder:
- [D:\projects\RestorMe\Frontend](D:\projects\RestorMe\Frontend)

Useful commands:
```powershell
cd D:\projects\RestorMe\Frontend
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
cd D:\projects\RestorMe\Backup
dotnet build .\Backup.Server.Api\Backup.Server.Api.csproj
dotnet run --project .\Backup.Server.Api\Backup.Server.Api.csproj
```

### Agent
```powershell
cd D:\projects\RestorMe\Backup
dotnet build .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
dotnet run --project .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
```

### Frontend
```powershell
cd D:\projects\RestorMe\Frontend
yarn
yarn build
```

### Docker Compose
```powershell
cd D:\projects\RestorMe\docker-compose
docker compose up --build
docker compose down
docker compose logs -f backend
docker compose logs -f frontend
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

- [D:\projects\RestorMe\docker-compose\README.md](D:\projects\RestorMe\docker-compose\README.md)
- [D:\projects\RestorMe\Frontend\README.md](D:\projects\RestorMe\Frontend\README.md)
