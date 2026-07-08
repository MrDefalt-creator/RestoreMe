# Changelog

All notable changes to RestoreMe are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
No versions have been tagged yet — everything below is unreleased.

## [Unreleased]

### Added

- **Database dump compression**: logical PostgreSQL/MySQL dumps are streamed
  through zstd on the agent (per-policy toggle, default on), so `pg_dump`/
  `mysqldump` output is compressed straight to the artifact — a full plain-SQL
  copy never touches the agent's temp disk. Restore auto-detects compressed
  artifacts by their zstd magic bytes, so legacy plain-`.sql` backups keep
  restoring unchanged. Uses the pure-managed `ZstdSharp.Port` — the agent
  binary gains no native dependency. Filesystem policies are unaffected
  (archives are already compressed).
- **Cron schedules + backup windows**: policies can now run on a cron
  schedule (5-field expression, IANA timezone, DST-aware) instead of a
  fixed interval, and interval policies can be confined to a daily backup
  window (e.g. 22:00–06:00, may span midnight). All computation stays
  server-side — agents are unchanged. The policy form offers daily /
  weekly / monthly presets or a custom cron expression, a timezone picker
  and a live "next three runs" preview (`POST
  /api/policies/schedule-preview`); policy lists show human-readable
  schedules ("Daily at 03:00 (Europe/Moscow)").
- **Retention strategies** per policy: `RetentionDays`, `RetentionMaxCount`
  (keep newest N) and `RetentionMaxTotalBytes` (size budget) with a pure,
  heavily-tested evaluator; a background cleanup service deletes pruned
  artifacts from storage and DB, audit-logging each deletion. The newest
  artifact of a policy is never deleted.
- **Artifact integrity verification**: uploads are re-hashed (streaming
  SHA-256) against the agent-reported checksum before a job may complete;
  a scheduled integrity scrub re-verifies stored artifacts on an
  admin-configurable cadence, with per-artifact integrity state, a
  verify-now action and an `IntegrityCheckFailed` notification event.
  Restores verify the artifact checksum before applying.
- **Multi-channel notifications**: admin-managed channels (generic webhook,
  Telegram, Slack, Discord) with per-channel event subscriptions, encrypted
  settings at rest, best-effort fan-out and a test-send action. Covers
  backup/restore failures, completions, agent offline/online, policy
  auto-disable and retention cleanup.
- **Policy auto-disable** after 3 consecutive backup failures, with audit
  trail and notification; manual re-enable resets the failure streak.
- **Selective agent delete**: remove an agent while choosing what happens
  to its jobs, artifacts and policies.
- **Audit log** for critical actions (user/agent lifecycle, notifications,
  retention, integrity) with an admin-only UI.
- **Install-agent wizard**: one-liner installers for Linux/Windows served
  by the backend itself; agent binaries built on demand via the
  `agent-builder` compose profile — no GitHub dependency.
- **Live updates over SSE**: the backend streams change events
  (`GET /api/events`) for jobs, artifacts, restores, agents, policies,
  users and notification channels; the admin panel applies them instantly
  and suspends interval polling while the stream is up (polling returns
  automatically as a fallback when it drops).
- **Control-plane self-backup (DR)**: a compose sidecar dumps the
  PostgreSQL metadata and the DataProtection key ring to the host on a
  schedule with rotation, plus a written disaster-recovery runbook
  (`docs/DR-RUNBOOK.md`).
- Server-side pagination and sorting for Jobs, Backups and Agents lists;
  entity deep links (`?id=…`) open their target on Agents/Policies/Backups.
- Frontend test infrastructure: vitest + Testing Library with seed tests.
- CI hardening: frontend tests in CI, backend coverage artifact, CodeQL
  scanning, Dependabot, run-concurrency cancellation.

### Changed

- **Frontend 2.0 UI overhaul**: Feature-Sliced Design, Radix primitives,
  dark/light theme, command palette, empty states, URL-synced list filters
  (deep links like `/jobs?status=failed` now apply), EN/RU i18n.
- Error responses from the exception filter and rate limiter now use
  RFC 7807 `application/problem+json`; rate-limited responses carry
  `Retry-After`.
- Auth DTOs validate input server-side (`400 ValidationProblemDetails`
  on malformed bodies).
- Docker stack: pinned MinIO image, frontend healthcheck, backend container
  runs as non-root, memory limits on long-running services.

### Security

- HttpOnly cookie-based user sessions (`SameSite=Strict`); JS never sees
  the JWT. Security-stamp claim invalidates tokens on password change.
- Production startup guardrails: refuse to boot with dev-default JWT
  signing keys, dev enrollment tokens, or loopback-only CORS; HSTS and
  HTTPS redirection enabled in Production.
- Login rate limiting + account lockout; per-agent rate limits keyed off
  the JWT subject; agent token revocation via token-version bump.
- Notification channel secrets encrypted at rest via DataProtection.
- Agent hardening: zip-slip guard on restore extraction, pre-restore
  snapshot rename of overwritten targets.

### Removed

- Legacy `Frontend/` app — `Frontend-2.0/` is the only UI.
- Legacy single `Notifications:FailureWebhookUrl` config, superseded by
  DB-backed notification channels.
