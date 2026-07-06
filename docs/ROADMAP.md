# RestoreMe Roadmap

Outcome of a full-project audit (July 2026) against the standards of
commercial backup products (Proxmox Backup Server, Veeam) and professional
engineering practice. Tier 1 (quick wins: URL-synced filters, frontend test
infra, DTO validation, RFC-7807 errors, CI/CodeQL/Dependabot, editorconfig,
compose hardening, community docs) shipped on `preview` — see `CHANGELOG.md`.

Tiers are ordered by value-for-effort. Items within a tier are independent
unless noted.

## Tier 2 — engineering depth and UX (0.5–2 days each)

### Engineering

- **Observability**: Serilog (JSON console) + OpenTelemetry metrics/traces,
  `/metrics` endpoint, correlation IDs; split `/health` into `/health/live`
  vs `/health/ready`.
- **HTTP-level integration tests** via `WebApplicationFactory`: login,
  lockout, rate limiting, cookie attributes, authorization policies. There
  are currently zero tests that exercise the HTTP pipeline.
- **Agent worker tests**: `BackupExecuter`, `RestoreExecuter` (zip-slip
  guard, pre-restore snapshot), `ArchiveService`.
- **ProblemDetails sweep**: ~19 bespoke `{ message }` response sites across
  8 controllers still bypass RFC 7807 (Tier 1 converted only the exception
  filter and rate limiter).
- **Background service hygiene**: `PeriodicTimer` + graceful-shutdown hooks
  in `RetentionCleanupService` / `AgentHealthSweepService` / scrub sweep.

### UX / UI

- **Live job progress** via SSE (or WebSocket) — replaces 2-second polling.
- **Server-side pagination + column sorting** for Jobs / Artifacts / Agents
  (only AuditLog paginates today).
- **Entity deep links**: `/agents?id=…`, `/policies?id=…` are emitted by the
  command palette and JobDrawer but never read by the target pages
  (known bug).
- **Session-expiry warning + sliding session** (expiry is reactive-only
  today); proper Russian 3-form plurals in i18n.
- **Bulk actions** (row selection) on Jobs/Artifacts; CSV export (Jobs,
  AuditLog); explicit timezone label on timestamps.
- **Lint depth**: ESLint strict + `jsx-a11y`; resolve the
  `tailwind.config.ts` vs `@theme` token duplication.

### Release engineering

- **GHCR image publishing** (backend + frontend), first git tag, and a
  release workflow for the server (only agent binaries have a release
  pipeline today, and it has never fired — zero tags).
- **DR for RestoreMe's own control plane**: pg_dump sidecar/cron, backup of
  the `backend_keys` DataProtection volume, and a written restore runbook.
  A backup product must have a story for backing up itself.
- **Installer hardening**: sha256 verification of the downloaded agent
  binary; systemd unit hardening (`ProtectSystem=strict`,
  `NoNewPrivileges`, …).
- **Frontend container non-root** (httpd needs `Listen 8080` + pid/log
  rewiring — deferred from Tier 1).
- **Playwright e2e smoke**: login → dashboard → create policy.

## Tier 3 — product features (3+ days each; need design specs first)

- **Cron schedules + backup windows** (e.g. Cronos): today scheduling is
  interval-seconds only — the biggest functional gap vs commercial tools.
- **Client-side artifact encryption** (per-policy key, age/AES-GCM) and
  TLS to MinIO enforced in production examples (`UseSsl: false` today).
- **Compression for DB dumps** (plain SQL today) — small enough that it
  could be pulled into Tier 2.
- **Incremental / differential backups** (every run is a full ZIP today).
- **Restore-test verification**: scheduled scratch-restore proof, not just
  checksum verification.
- **Auth depth**: 2FA, refresh-token/session model.
- **Fleet features**: agent auto-update, bandwidth limiting.
- **Notification reliability**: outbox pattern, in-app notification inbox.
