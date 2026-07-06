# Security Policy

## Supported Versions

RestoreMe has no tagged releases yet. Security fixes land on the `preview`
branch first; run the latest `preview` (or the most recent release once
tags exist) to receive them.

## Reporting a Vulnerability

**Please do not open a public issue for security problems.**

Use [GitHub Security Advisories](../../security/advisories/new) to report
privately ("Report a vulnerability" on the repository's Security tab). Include:

- Affected component (backend API, agent worker, frontend, installers, compose stack)
- Reproduction steps or a proof of concept
- Impact assessment as you see it (what an attacker gains)
- Version/commit you tested against

You can expect an acknowledgement within a few days. Please allow a
reasonable disclosure window for a fix to land before publishing details.

## Scope notes

- Dev-default secrets in `docker-compose/secrets/*.txt` and
  `appsettings.json` are **intentional** for local development. The backend
  refuses to start in `Production` with dev-default JWT keys, enrollment
  tokens, or loopback-only CORS — reports about the dev defaults themselves
  are out of scope; bypasses of the production guardrails are very much in
  scope.
- The threat model assumes the backend runs behind a TLS-terminating
  reverse proxy and MinIO is not exposed to untrusted networks.
