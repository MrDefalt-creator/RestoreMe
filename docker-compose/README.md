# RestoreMe Docker Compose

This folder is the single local entry point for starting the full RestoreMe stack.

Contents:
- `docker-compose.yml` — full stack definition
- `.env` — non-secret ports and frontend mode
- `secrets/` — local secret files mounted into containers

## Services

Current stack includes:
- `db` — PostgreSQL 18
- `minio` — object storage
- `backend` — ASP.NET Core API
- `frontend` — Vite production build served by Apache

## Start the Stack

```powershell
cd D:\projects\RestorMe\docker-compose
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
- frontend: `http://localhost:5173`
- backend: `http://localhost:8080`
- MinIO API: `http://localhost:9000`
- MinIO Console: `http://localhost:9001`
- PostgreSQL: `localhost:5432`

You can change these in `.env`.

## Important Compose Behavior

- frontend API URL is derived from `API_PORT` during the frontend image build
- backend CORS in `Development` accepts localhost and loopback origins on any port
- backend runs EF Core migrations automatically on startup
- backend talks to MinIO internally via `minio:9000`
- agents usually need only the backend address in simple deployments
- local Docker PostgreSQL is best tested through `credentials` mode for logical dump policies

## Secrets

Expected secret files in [secrets](D:\projects\RestorMe\docker-compose\secrets):
- `postgres-password.txt`
- `postgres-connection.txt`
- `minio-access-key.txt`
- `minio-secret-key.txt`

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
minioadmin
```

`minio-secret-key.txt`
```text
strong_minio_secret
```

## Storage Addressing in Compose

Compose uses two different storage addresses:
- internal backend-to-MinIO address: `minio:9000`
- external/public address for agents: usually `http://localhost:9000` in the local scenario

### Simple scenario

If the agent runs on the same machine and reaches backend on `http://localhost:8080`, the backend can usually return upload URLs that also point to `http://localhost:9000`.

### More complex scenario

If the agent runs on another machine and the server is reached by a LAN IP or domain, you should make sure the public storage address matches that real external address.

Examples:
- backend: `http://192.168.1.50:8080`
- storage: `http://192.168.1.50:9000`

If storage must be reached through another host or proxy, set `Storage__PublicEndpoint` explicitly.

## Useful Commands

Show service status:
```powershell
docker compose ps
```

Show logs:
```powershell
docker compose logs -f backend
docker compose logs -f frontend
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

## First Start Checklist

1. Fill files in `secrets/`.
2. Check `.env` ports if the defaults are occupied.
3. Run `docker compose up --build`.
4. Wait until backend applies migrations.
5. Open the frontend.
6. Start the worker agent separately if you want to test the real registration flow.

## Logical database dump testing with Compose

For the bundled local PostgreSQL container, the recommended first test is:
- `Policy type`: `PostgreSQL logical dump`
- `Auth mode`: `credentials`
- `Host`: `127.0.0.1`
- `Port`: `5432`
- `Database`: `restoreme_db`
- `Username`: the PostgreSQL user from your secret or connection string
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

### Frontend opens but API requests fail
Check:
- backend container is running
- `API_PORT` is correct
- browser is using the correct frontend URL

### Agent can reach backend but cannot upload archives
Check:
- MinIO port is reachable from the agent machine
- backend returned an upload URL with the correct external host
- `Storage__PublicEndpoint` is configured if your storage host differs from the backend host

### PostgreSQL logical dump fails without a password
This usually means the policy is using `integrated` mode against the compose PostgreSQL container, which is not the recommended test scenario. Switch the policy to `credentials` and use `127.0.0.1:5432`.

### Agent cannot find `pg_dump` or `mysqldump`
Install the matching native dump tool on the agent machine or configure an absolute path in agent settings.

### Frontend route returns Not Found in Docker
This should already be handled by the frontend container rewrite rules. If you still see it, rebuild the frontend image.

## Related Documentation

- [Root README](D:\projects\RestorMe\README.md)
- [Frontend README](D:\projects\RestorMe\Frontend\README.md)
