# RestoreMe Docker Compose

This folder is the single local entry point for starting the full RestoreMe stack.

> [!WARNING]
> Read this file before running the stack. The repository includes `.env` and starter files in `secrets/` for convenience, but the values are public development defaults and must be replaced before any shared, demo or production-like deployment.

Contents:
- `docker-compose.yml` - full stack definition
- `.env` - non-secret ports and frontend mode
- `secrets/` - local secret files mounted into containers

## Services

Current stack includes:
- `db` - PostgreSQL 18
- `minio` - object storage
- `backend` - ASP.NET Core API
- `frontend` - stable RestoreMe frontend served by Apache
- `frontend-2` - flagship Frontend 2.0 prototype served by Apache

## First-Time Startup

Use this order when you deploy the stack on a clean workstation.

1. Open [.env](.env) and check whether the default ports are free.
2. Replace the starter secret files inside [secrets](secrets).
3. Run `docker compose up --build`.
4. Wait until backend applies migrations.
5. Open the stable frontend on `http://localhost:5173`, or Frontend 2.0 on `http://localhost:5174`.
6. Sign in with the bootstrap administrator account.
7. Change the bootstrap administrator password.
8. Create additional users if required.
9. Start one or more agents separately.

## Bootstrap Administrator

On the first backend startup in `Development`, the system seeds one administrator account only if the user table is empty.

Current dev credentials:
- `admin / Admin123!`

> [!WARNING]
> Change this password after the first login. The checked-in bootstrap account is included only to make initial local setup possible.

Important behavior:
- if users already exist in the database, seed does not overwrite them
- if you want a truly clean first-start state, use a clean database volume

## Start and Stop

Start the stack:
```powershell
cd docker-compose
docker compose up --build
```

Run in background:
```powershell
docker compose up -d --build
```

Stop the stack:
```powershell
docker compose down
```

## Default Ports

By default the stack publishes:
- stable frontend: `http://localhost:5173`
- frontend 2.0: `http://localhost:5174`
- backend: `http://localhost:8080`
- MinIO API: `http://localhost:9000`
- MinIO Console: `http://localhost:9001`
- PostgreSQL: `localhost:5432`

You can change these in `.env`.

## Secrets

Expected secret files in [secrets](secrets):
- `postgres-password.txt`
- `postgres-connection.txt`
- `minio-access-key.txt`
- `minio-secret-key.txt`

> [!WARNING]
> Do not reuse the checked-in starter values for a deployed instance. Replace PostgreSQL password, PostgreSQL connection string, MinIO access key and MinIO secret key together before exposing the stack.

### Example values

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

### Why there are two PostgreSQL secret files

`postgres-password.txt` is used by the PostgreSQL container itself.

`postgres-connection.txt` is used by the backend, because the backend reads a full connection string from `ConnectionStrings__DefaultConnection_FILE`.

This keeps the container startup and backend startup independent and explicit.

## How Compose Passes Secrets into the Application

### PostgreSQL container

The database container reads:
- `POSTGRES_PASSWORD_FILE=/run/secrets/postgres-password`

The secret file must contain only the password.

### Backend container

The backend reads:
- `ConnectionStrings__DefaultConnection_FILE=/run/secrets/postgres-connection`
- `Storage__AccessKey_FILE=/run/secrets/minio-access-key`
- `Storage__SecretKey_FILE=/run/secrets/minio-secret-key`

This means the backend does not need hardcoded database or MinIO secrets in `docker-compose.yml`.

## Important Compose Behavior

- frontend API URLs are derived from `API_PORT` during the frontend image builds
- backend CORS in `Development` accepts localhost and loopback origins on any port
- backend runs EF Core migrations automatically on startup
- backend talks to MinIO internally via `minio:9000`
- backend returns public upload URLs based on `Storage__PublicEndpoint` or the incoming backend host
- agents usually need only the backend address in simple deployments
- local Docker PostgreSQL is best tested through `credentials` mode for logical dump policies

## Storage Addressing in Compose

Compose uses two different storage addresses:
- internal backend-to-MinIO address: `minio:9000`
- external/public address for agents: usually `http://localhost:9000` in the local scenario

### Simple scenario

If the agent runs on the same machine and reaches backend on `http://localhost:8080`, the backend can usually return upload URLs that also point to `http://localhost:9000`.

### Another machine in the LAN

If the agent runs on another machine, then `localhost` is no longer correct for that agent.
You should expose the backend and MinIO through the real LAN IP or domain.

Example:
- backend: `http://192.168.1.50:8080`
- storage: `http://192.168.1.50:9000`

In that case update:
- the agent backend address
- `Storage__PublicEndpoint` in Compose if needed

## Agent Setup Against the Compose Stack

The agent is started separately from this Compose stack.

Recommended local values for the current stack:
- backend URL: `http://localhost:8080/`
- enrollment token: `restoreme-agent-enrollment-dev-token`

> [!WARNING]
> Replace the enrollment token in backend and agent configuration before using agents on any shared network. The default token is public repository data.

Important note:
- the checked-in agent appsettings still contains an older placeholder base URL
- before testing, point the agent to the actual backend URL you want to use

Agent state file:
- `state/agent-state.json`

If the agent keeps using an old server address, update or delete that state file.

## User Login and Session Behavior

The frontend login page supports two modes:
- `Remember me` enabled - the session is persisted in `localStorage`
- `Remember me` disabled - the session is stored only for the current browser session

This does not change backend security rules; it only changes frontend session persistence.

## Frontend Versions in Compose

The Compose stack runs both UI versions against the same backend, database and object storage:

- `frontend` on `http://localhost:5173` is the stable diploma baseline.
- `frontend-2` on `http://localhost:5174` is the next-generation UI prototype.

Both frontends use the same API and should show the same agents, policies, jobs and artifacts after polling/refetch.

Useful comparison flow:
1. Create or update a policy in one frontend.
2. Open the other frontend.
3. Confirm the same policy appears there.
4. Let the agent execute the policy.
5. Confirm the resulting job and artifact appear in both frontends.

## Useful Commands

Show service status:
```powershell
docker compose ps
```

Show logs:
```powershell
docker compose logs -f backend
docker compose logs -f frontend
docker compose logs -f frontend-2
docker compose logs -f minio
docker compose logs -f db
```

Rebuild only backend:
```powershell
docker compose up -d --build backend
```

Rebuild only frontend:
```powershell
docker compose up -d --build frontend
```

Rebuild only Frontend 2.0:
```powershell
docker compose up -d --build frontend-2
```

Remove containers but keep named volumes:
```powershell
docker compose down
```

Remove containers and named volumes too:
```powershell
docker compose down -v
```

Use the last command only when you intentionally want to reset PostgreSQL and MinIO data.

## Logical Database Dump Testing with Compose

For the bundled local PostgreSQL container, the recommended first test is:
- `Policy type`: `PostgreSQL logical dump`
- `Auth mode`: `credentials`
- `Host`: `127.0.0.1`
- `Port`: `5432`
- `Database`: `restoreme_db`
- `Username`: the PostgreSQL user from your connection string
- `Password`: the PostgreSQL password from your secret

Why this is the recommended path:
- the compose PostgreSQL instance is reached over TCP
- passwordless local auth is not the default for this setup
- `integrated` mode is intended for a deliberately configured local PostgreSQL installation, not for the default compose database container

Before creating a logical dump policy, also make sure the agent machine has the required native dump tool installed:
- PostgreSQL: `pg_dump`
- MySQL: `mysqldump`

If needed, set the absolute tool path in the agent config.

## Troubleshooting

### Frontend opens but login does not work
Check:
- backend container is running
- frontend image was rebuilt after the latest login-related changes
- frontend is pointing to the correct backend URL
- you are using the current seeded admin credentials on a clean or expected database

### Frontend 2.0 is not available on port 5174
Check:
- `.env` contains `FRONTEND_2_PORT=5174`
- `frontend-2` container exists in `docker compose ps`
- the image was rebuilt with `docker compose up -d --build frontend-2`
- another local process is not already using the selected port

### There should be only one bootstrap admin, but more users exist
Reason:
- the database was already populated before the latest seed rules

Fix:
- use a clean database volume for a fresh first startup
- or delete extra users through the panel/database manually

### Agent can reach backend but cannot upload archives
Check:
- MinIO port is reachable from the agent machine
- backend returned an upload URL with the correct external host
- `Storage__PublicEndpoint` is correct for your topology

### PostgreSQL logical dump fails without a password
This usually means the policy is using `integrated` mode against the compose PostgreSQL container. Switch the policy to `credentials` and use `127.0.0.1:5432`.

### Agent cannot find `pg_dump` or `mysqldump`
Install the matching native dump tool on the agent machine or configure an absolute path in the agent settings.

### Frontend route returns Not Found in Docker
This should already be handled by the frontend container rewrite rules. If you still see it, rebuild the frontend image.

## Related Documentation

- [../README.md](../README.md)
- [../Frontend/README.md](../Frontend/README.md)
- [../Frontend-2.0/README.md](../Frontend-2.0/README.md)

