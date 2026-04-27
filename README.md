# RestoreMe

RestoreMe is a backup management system consisting of three main parts:
- `Backup.Server.Api` — ASP.NET Core backend API
- `Backup.Agent.Worker` — worker agent that registers, synchronizes policies, sends heartbeat and executes backups
- `Frontend` — React admin panel for operators

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

## What Is Already Implemented

### Backend
- layered architecture: API / Application / Domain / Infrastructure / Shared.Contracts
- agent pending registration and approval flow
- heartbeat processing
- policy CRUD for the admin panel
- backup jobs lifecycle: start, fail, complete
- artifact storage in MinIO and artifact download through backend
- automatic EF Core migrations on application startup
- database indexes for the main read paths
- support for config values and file-based secrets

### Agent
- can use a configured `AgentId` or receive one after pending registration
- stores local state in `state/agent-state.json`
- stores both `AgentId` and the resolved backend server address
- resolves backend URL from local state first, then from config
- sends heartbeat and periodically synchronizes policies
- packs source directories into archives and uploads them to object storage
- can execute logical database dump policies for PostgreSQL and MySQL

### Frontend
- app shell with sidebar and responsive layout
- dashboard with aggregate metrics
- agents page
- pending agents page with approval dialog
- policies page with create, edit and toggle
- jobs page
- artifacts page with download action
- automatic polling in live mode, so data refreshes without manual page reload

## Prerequisites

### For local development without Docker
- .NET SDK 10
- Node.js 22+
- Yarn 1.x
- PostgreSQL
- MinIO

### For Docker startup
- Docker Desktop
- Docker Compose

## Quick Start

### Option 1. Run the whole stack with Docker Compose

This is the easiest way to start the project.

```powershell
cd D:\projects\RestorMe\docker-compose
docker compose up --build
```

After startup the services are available at:
- frontend: `http://localhost:5173`
- backend: `http://localhost:8080`
- MinIO API: `http://localhost:9000`
- MinIO Console: `http://localhost:9001`
- PostgreSQL: `localhost:5432`

### Option 2. Run services manually

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

## Docker Compose Setup

The centralized Docker entry point is:
- [docker-compose/docker-compose.yml](D:\projects\RestorMe\docker-compose\docker-compose.yml)

The folder contains:
- `docker-compose.yml` — full stack startup
- `.env` — non-secret ports and frontend build mode
- `secrets/` — local secret files mounted into containers

### Current Compose Services
- `db` — PostgreSQL 18
- `minio` — object storage
- `backend` — ASP.NET Core API
- `frontend` — built Vite app served through Apache

### Compose Secrets

Current compose expects these files in [docker-compose/secrets](D:\projects\RestorMe\docker-compose\secrets):
- `postgres-password.txt`
- `postgres-connection.txt`
- `minio-access-key.txt`
- `minio-secret-key.txt`

Example `postgres-password.txt`:
```text
my_strong_postgres_password
```

Example `postgres-connection.txt`:
```text
Host=postgres;Port=5432;Database=restoreme_db;Username=restoreme_user;Password=my_strong_postgres_password
```

Example `minio-access-key.txt`:
```text
minioadmin
```

Example `minio-secret-key.txt`:
```text
strong_minio_secret
```

### Compose Notes
- frontend API URL is derived from `API_PORT`
- frontend SPA routes such as `/agents`, `/policies` and `/jobs` are handled inside the frontend container
- backend runs migrations automatically on startup
- backend CORS in `Development` accepts localhost and loopback origins on any port
- backend talks to MinIO through the internal Docker address `minio:9000`

## Backend Configuration

Main backend config file:
- [Backup.Server.Api/appsettings.json](D:\projects\RestorMe\Backup\Backup.Server.Api\appsettings.json)

Important configuration sections:
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

### File-based secrets

The backend supports both regular config values and `*_FILE` secrets. This means:
- local development can use values from `appsettings.json`
- Docker can override sensitive values through mounted secret files

## Database and Migrations

EF Core migrations live in:
- [Backup.Server.Infrastructure/Migrations](D:\projects\RestorMe\Backup\Backup.Server.Infrastructure\Migrations)

Current behavior:
- migrations are applied automatically on backend startup
- if the database is empty, schema is created automatically
- if the database is already up to date, startup continues normally

Create a new migration manually:
```powershell
cd D:\projects\RestorMe\Backup
dotnet ef migrations add MigrationName --project .\Backup.Server.Infrastructure\Backup.Server.Infrastructure.csproj --startup-project .\Backup.Server.Api\Backup.Server.Api.csproj --output-dir Migrations
```

## Frontend Setup

Frontend project folder:
- [Frontend](D:\projects\RestorMe\Frontend)

Install dependencies:
```powershell
cd D:\projects\RestorMe\Frontend
yarn
```

Run in development mode:
```powershell
yarn dev
```

Useful commands:
```powershell
yarn typecheck
yarn lint
yarn build
yarn preview
```

Frontend environment example:
```env
VITE_API_BASE_URL=http://localhost:8080
VITE_API_MODE=live
```

Modes:
- `live` — use the real backend API
- `mock` — use local fixtures for offline/demo work

## Agent Setup

Agent project folder:
- [Backup.Agent.Worker](D:\projects\RestorMe\Backup\Backup.Agent.Worker)

Main agent config file:
- [Backup.Agent.Worker/appsettings.json](D:\projects\RestorMe\Backup\Backup.Agent.Worker\appsettings.json)

Default agent configuration shape:
```json
{
  "Api": {
    "BaseUrl": "https://localhost:7104/"
  },
  "Agent": {
    "AgentId": "",
    "HeartbeatIntervalSeconds": 60,
    "PolicySyncIntervalSeconds": 30
  }
}
```

### What each setting means
- `Api:BaseUrl` — default backend URL used before a stored override exists
- `Agent:AgentId` — optional fixed agent identifier
- `Agent:HeartbeatIntervalSeconds` — heartbeat interval
- `Agent:PolicySyncIntervalSeconds` — policy synchronization interval

### Local agent state

The worker stores local state in a file named:
- `state/agent-state.json`

That file contains:
- saved `AgentId`
- saved `ServerAddress`

Important behavior:
- if `ServerAddress` already exists in local state, the agent uses it first
- if local state does not contain a server address, the agent falls back to `Api:BaseUrl`

This means that if you change the server later and the agent still connects to the old address, you should either:
1. update `ServerAddress` inside `state/agent-state.json`
2. or delete the state file and start the agent again

### Logical backup prerequisites

For logical database dump policies the agent machine must have the native dump tools installed:
- PostgreSQL: `pg_dump`
- MySQL: `mysqldump`

The agent first tries the configured command name, then the process `PATH`, and on Windows also checks common install folders such as `C:\Program Files\PostgreSQL\<version>\bin`.

If you want predictable behavior across different machines, set the tool paths explicitly in agent config. Example:
```json
{
  "Agent": {
    "PostgreSqlDumpCommand": "C:\\Program Files\\PostgreSQL\\18\\bin\\pg_dump.exe",
    "MySqlDumpCommand": "C:\\Program Files\\MySQL\\MySQL Server 8.0\\bin\\mysqldump.exe"
  }
}
```

### PostgreSQL auth modes

PostgreSQL policies support two modes:
- `credentials` ? universal mode; provide host, port, database, username and password
- `integrated` ? no password is stored in the policy; `pg_dump` must already be able to access the database as the OS user running the agent

Recommended practical rule:
- for Docker Compose PostgreSQL use `credentials`
- use `integrated` only for a specially configured local PostgreSQL installation where the agent process is already trusted by the database

Why Docker Compose usually needs credentials:
- the bundled PostgreSQL container is exposed over TCP
- `pg_dump` from the agent reaches it as a network client
- without a dedicated trust or equivalent auth rule, PostgreSQL will ask for a password

### How to prepare PostgreSQL for passwordless agent access

Use this only if you really want the `integrated` scenario and you control the database host.

Typical idea:
1. Create a dedicated database role for backups.
2. Run the agent under a dedicated OS user.
3. Configure PostgreSQL auth so that this user can connect locally without putting a password into the policy.
4. Verify that `pg_dump` works from the same user account before testing the policy in RestoreMe.

On Linux this is usually implemented through a local socket auth rule such as `peer`, `ident`, or another trusted local rule in `pg_hba.conf`.

On Windows there is no direct Linux-style `peer` equivalent for the common local TCP scenario. In practice you normally choose one of these approaches:
- keep using `credentials`
- configure an environment-specific trusted local auth flow supported by your PostgreSQL installation
- prepare another local secret mechanism outside the policy itself

Important limitation:
- the current Docker Compose PostgreSQL setup is not the recommended environment for testing passwordless `integrated` mode
- use a locally installed and intentionally configured PostgreSQL instance for that scenario

### How to validate PostgreSQL access before creating a policy

Run the same dump tool manually under the same OS account that starts the agent.

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

### How to connect a new agent
1. Set `Api:BaseUrl` to the backend URL you want to use.
2. Start the agent.
3. The agent sends pending registration to the backend.
4. Open the frontend and go to `Pending`.
5. Approve the agent and assign a readable name.
6. After approval, the agent starts working under its assigned `AgentId`.

## Storage Addressing Model

Storage configuration has two roles:
- `Storage:Endpoint` — internal MinIO address used by the backend itself
- `Storage:PublicEndpoint` — optional external storage address used for upload URLs returned to agents

### Simple scenario

In the common scenario the agent only needs the backend address.

Example:
- agent connects to `http://my-server:8080`
- backend receives the upload ticket request on `my-server`
- backend builds the public MinIO upload URL on the same host with the MinIO port
- agent uploads directly to MinIO without separate storage configuration

This keeps deployment simple: in the normal case you update only the backend address for the agent.

### When `Storage:PublicEndpoint` should be set explicitly

Set `Storage:PublicEndpoint` manually only when the external storage address differs from the backend host, for example:
- backend and storage are published on different domains
- MinIO is exposed through another proxy
- the agent reaches backend through one address, but storage must be reached through another address

## Frontend Endpoints Used

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
- `POST /api/backupjobs/upload_ticket`

### Artifacts
- `GET /api/backupartifacts`
- `GET /api/backupartifacts/job/{jobId}`
- `GET /api/backupartifacts/{artifactId}/download`

## Typical Operator Workflow

### Approve a new agent
1. Start backend and frontend.
2. Start the worker agent.
3. Open the `Pending` page.
4. Approve the machine and assign a readable agent name.
5. The worker continues with the assigned `AgentId`.

### Create a backup policy
1. Open `Policies`.
2. Select an approved agent.
3. Choose a policy type.
4. For `Filesystem`, enter a source path.
5. For `PostgreSQL` or `MySQL`, enter database connection settings and the auth mode supported by that engine.
6. Set interval in days, hours, minutes and seconds.
7. Save the policy.

### Execute and inspect a backup
1. The agent synchronizes policies.
2. When a policy is due, the agent starts a backup job.
3. For filesystem policies the agent uploads a file directly or first creates a ZIP archive from a directory.
4. For database policies the agent runs `pg_dump` or `mysqldump` and creates a temporary `.sql` dump file.
5. The agent requests an upload ticket from the backend.
6. The backend returns a presigned MinIO upload URL.
7. The agent uploads the prepared payload directly to object storage.
8. The job and artifact become visible in the admin panel.

### Download an artifact
1. Open `Artifacts`.
2. Find the needed record.
3. Click `Download`.
4. The frontend downloads the file through the backend artifact endpoint.

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
yarn dev
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

### Agent keeps connecting to the old backend address
Reason:
- `ServerAddress` is already saved in the agent state file and has higher priority than `Api:BaseUrl`

Fix:
- edit `state/agent-state.json` and update `ServerAddress`
- or delete the state file and start the agent again

### Agent can reach backend but cannot upload to MinIO
Check:
- whether MinIO is reachable from the agent machine
- whether backend returned the correct public upload host
- whether `Storage:PublicEndpoint` must be set explicitly in your network topology

### PostgreSQL logical dump fails with `no password supplied`
Reason:
- the policy uses `integrated` mode, but the target PostgreSQL instance is not configured for passwordless access from the agent process

Fix:
- for the local Docker Compose PostgreSQL setup, switch the policy to `credentials`
- use `127.0.0.1` instead of `localhost` if you want to avoid extra IPv6 attempts during local testing
- reserve `integrated` mode for a specially configured local PostgreSQL installation

### Database dump job looks stuck
Current behavior should fail fast instead of waiting for interactive password input. If a job still appears stuck, check:
- whether the running agent binary was restarted after the latest changes
- whether the dump tool itself can be executed manually from the same OS account
- whether the agent is blocked by antivirus or another local security tool

### Agent cannot find `pg_dump` or `mysqldump`
Check:
- whether the native dump tool is installed on the agent machine
- whether the tool is visible in the agent process environment
- whether you should set `Agent:PostgreSqlDumpCommand` or `Agent:MySqlDumpCommand` to an absolute path

### Frontend shows old chunk import errors after rebuild
Reason:
- Vite chunk hashes changed after rebuild, but the browser still uses an old tab/runtime

Fix:
- reload the page once after deploy or rebuild

### Docker dev uses HTTP instead of HTTPS
This is expected for the local compose setup.
For production, TLS should be terminated by a reverse proxy or ingress.

## Additional Documentation

More focused local docs:
- [Frontend README](D:\projects\RestorMe\Frontend\README.md)
- [Docker Compose README](D:\projects\RestorMe\docker-compose\README.md)

## Status

The project is ready as an MVP/prototype foundation:
- frontend is integrated with backend
- backend routes support the admin panel
- agent can register, synchronize and execute policies
- compose stack starts from a single folder
- migrations are automatic
- secrets can be passed through file-based Docker mounts
- in simple deployments an agent usually needs only the backend address
