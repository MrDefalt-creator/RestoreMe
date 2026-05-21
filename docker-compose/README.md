# RestoreMe Docker Compose

This folder is the single local entry point for starting the full RestoreMe stack.

> [!WARNING]
> Read this file before running the stack. The repository includes `.env` and starter files in `secrets/` for convenience, but the values are public development defaults and must be replaced before any shared, demo or production-like deployment.

Contents:
- `docker-compose.yml` - full stack definition (neutral baseline; no environment)
- `docker-compose.override.yml` - **auto-loaded** for local dev; sets `ASPNETCORE_ENVIRONMENT=Development` and exposes the MinIO admin console
- `docker-compose.prod.yml` - opt-in overlay for production-style deploys
- `.env` - non-secret ports and frontend mode
- `secrets/` - local secret files mounted into containers (`*.example.txt` templates are tracked, real `*.txt` are git-ignored by default)

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
5. Open Frontend 2.0 on `http://localhost:5173` (primary), or the deprecated legacy frontend on `http://localhost:5174`.
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

Start the stack (development — uses the auto-loaded override):
```powershell
cd docker-compose
docker compose up --build
```

Run in background:
```powershell
docker compose up -d --build
```

Production-style startup (skips the dev override, adds prod overlay):
```powershell
cd docker-compose
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

Required env (export or place in `.env.prod` next to the files):
- `CORS_ORIGIN` — public origin of the frontend, e.g. `https://restoreme.example.com`
- `API_PUBLIC_URL` — public backend URL baked into the Vite bundle and into the frontends' CSP `connect-src`

The backend will refuse to start in Production when `Cors:AllowedOrigins` is empty or only contains loopback hosts — make sure `CORS_ORIGIN` is set before bringing the stack up.

Stop the stack:
```powershell
docker compose down
```

## Default Ports

By default the stack publishes:
- frontend 2.0: `http://localhost:5173` (primary)
- legacy frontend: `http://localhost:5174` (deprecated, see [Frontend/README.md](../Frontend/README.md))
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

Each one has a matching `*.example.txt` template in the same folder. The current `.txt` files ship with dev-default values to make first-time `docker compose up` work without any setup step; the `.gitignore` rule blocks any *new* `.txt` you drop into `secrets/` so real production secrets cannot be accidentally committed.

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

- frontend API URLs are derived from `API_PORT` during the frontend image builds (override via `API_PUBLIC_URL` in prod)
- backend CORS in `Development` accepts localhost and loopback origins on any port; in Production the backend refuses to start without an explicit non-loopback `Cors:AllowedOrigins`
- CORS only affects **browser** traffic (the admin panel). Agent → backend traffic is a plain HTTP client, no `Origin` header, no preflight — so an agent on a different machine reaches the backend regardless of the CORS allowlist as long as the network/firewall allows it
- all services share the `restoreme-internal` Docker network declared in `docker-compose.yml`; inter-service hostnames are the service names (`db`, `minio`, `backend`)
- backend runs EF Core migrations automatically on startup
- backend talks to MinIO internally via `minio:9000`
- backend returns public upload/download URLs based on `Storage__PublicEndpoint` or the incoming backend host
- agents usually need only the backend address in simple deployments
- local Docker PostgreSQL is best tested through `credentials` mode for logical dump policies
- the backend persists ASP.NET Core DataProtection keys to a named `backend_keys` volume so cookie-bound JWTs survive `docker compose up --build`
- `/health` is wired into the backend healthcheck and requires both PostgreSQL and MinIO to be reachable

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

### Building agent binaries

The install wizard generates a command that pulls **both** the installer
script and the agent binary from the backend itself (no GitHub dependency
— this is the self-hosted path). The installer scripts are baked into the
backend image, but agent binaries are produced on-demand by a one-shot
service so the backend image stays slim and so backend/agent versions can
be patched independently.

Run it once after a fresh `compose up` (and again any time the agent code
changes):

```powershell
cd docker-compose
docker compose --profile build-agents up agent-builder
```

This publishes `linux-x64`, `linux-arm64`, and `win-x64` self-contained
single-file binaries into a shared volume (`agent_binaries`) that the
backend mounts read-only at `/app/wwwroot/installers/binaries/`. The
binaries become reachable via the install wizard immediately — no backend
restart needed.

If an operator skips this step, the install wizard URL still resolves
(the installer script downloads fine), but the script will fail on the
agent-binary download with a hint pointing back at this section.

> [!WARNING]
> Replace the enrollment token in backend and agent configuration before using agents on any shared network. The default token is public repository data.

### Why a remote agent doesn't need to be in the CORS allowlist

CORS is a browser security feature. Browsers refuse to deliver cross-origin XHR responses to a page if the server's `Access-Control-Allow-Origin` doesn't list the page's origin. **Agents are not browsers** — the worker uses `HttpClient` to POST/GET against the backend; no `Origin` header is sent, no preflight is performed, the server doesn't apply CORS to the response.

So:

- Adding a new agent on `192.168.1.50` while the backend's CORS allowlist contains only `http://localhost:5173` is **fine** — the agent connects regardless.
- What needs to be reachable across the LAN/Internet is the backend's TCP port (`API_PORT`, default `8080`) and the MinIO endpoint exposed via `Storage__PublicEndpoint`.
- CORS only matters when an operator opens the admin panel from a different host than what's listed — that's the case where you extend `Cors:AllowedOrigins`.

Important note:
- the checked-in agent appsettings already points to the local Compose backend at `http://localhost:8080/`
- before testing against another host, point the agent to the actual backend URL you want it to use

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

- `frontend-2` on `http://localhost:5173` is the primary RestoreMe admin panel.
- `frontend` on `http://localhost:5174` is the deprecated legacy UI, kept available during the burn-in period.

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

### Frontend 2.0 is not available on port 5173
Check:
- `.env` contains `FRONTEND_2_PORT=5173`
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

