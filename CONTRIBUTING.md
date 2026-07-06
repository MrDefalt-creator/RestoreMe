# Contributing to RestoreMe

Thanks for your interest in improving RestoreMe.

## Branching

- **Target `preview` with your PRs.** `main` is frozen and only receives
  coordinated merges.
- Use conventional commit messages (`feat:`, `fix:`, `refactor:`, `docs:`,
  `test:`, `ci:`, `chore:`), scoped where it helps: `feat(integrity): …`,
  `fix(frontend): …`.

## Getting the stack running

Full stack (recommended):

```powershell
cd docker-compose
docker compose up --build
```

Frontend `:5173`, backend `:8080`, MinIO `:9000`/`:9001`, PostgreSQL `:5432`.
Bootstrap admin: `admin / Admin123!` (local dev only).

Agent binaries for the install wizard (run once, and after agent changes):

```powershell
docker compose --profile build-agents up agent-builder
```

## Backend (`Backup/`)

```powershell
cd Backup
dotnet build .\Backup.Server.Api\Backup.Server.Api.csproj
dotnet test BackupSystem.slnx
```

- Clean architecture: `Api` → `Application` → `Domain`; EF Core and MinIO
  live in `Infrastructure`; DTOs shared with the agent live in
  `Shared.Contracts`. Keep layer boundaries intact.
- EF migrations (from `Backup/`):

  ```powershell
  dotnet ef migrations add MigrationName --project .\Backup.Server.Infrastructure\Backup.Server.Infrastructure.csproj --startup-project .\Backup.Server.Api\Backup.Server.Api.csproj --output-dir Migrations
  ```

## Frontend (`Frontend-2.0/`)

```powershell
cd Frontend-2.0
yarn
yarn dev
```

Before pushing, make sure all four gates pass — CI runs the same:

```powershell
yarn lint
yarn typecheck
yarn test
yarn build
```

- Feature-Sliced Design: `app/` → `pages/` → `widgets/` → `features/` →
  `entities/` → `shared/`. Imports flow downward only.
- Uses yarn 1 (classic) — commit `yarn.lock` changes together with
  `package.json`.

## Tests

- Backend: xUnit in `Backup.Server.Tests` (SQLite-backed integration style)
  and `Backup.Agent.Worker.Tests`. New service logic should come with tests —
  see `Backup.Server.Tests/Retention` for the house style.
- Frontend: vitest + Testing Library. Co-locate `*.test.ts(x)` next to the
  unit under test.

## Pull requests

- Keep PRs focused; one concern per PR.
- Fill in the PR template (what/why/how verified).
- CI (build + tests for both stacks, CodeQL) must be green.
