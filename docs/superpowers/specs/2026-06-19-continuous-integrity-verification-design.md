# Continuous Integrity Verification — Design

**Date:** 2026-06-19
**Branch:** `feature/retention-and-integrity`
**Status:** Approved (design), pending spec review

## Goal

Close the integrity loop of RestoreMe so it is comparable to flagship backup
products (Proxmox Backup Server, restic, Borg). Today integrity is verified only
**on upload** (`BackupJobsService.AddArtifact` re-hashes the stored object before
the job is allowed to complete). Two gaps remain:

1. **At-rest bit-rot** — a stored artifact can silently degrade in MinIO weeks or
   months after upload; nothing re-checks it.
2. **Restore-path integrity** — `RestoreExecuter` downloads an artifact and applies
   it directly with no checksum check, so a corrupted/truncated download can
   overwrite live data.

This cycle delivers a full vertical slice (backend + agent + minimal UI) that
detects at-rest corruption proactively and refuses to apply a corrupt restore.

Out of scope for this cycle (future cycles): verified restore-drills, at-rest
encryption of artifact content, incremental/dedup backups, Prometheus metrics.

## Existing primitives reused

- `IStorageAccessService.ComputeObjectSha256Async(objectKey, ct)` — streaming
  SHA256 of a stored object (incremental hash, never buffers the whole artifact).
- `BackupArtifact.Checksum` — agent-reported SHA256, already persisted.
- Agent `IChecksumService.ComputeSha256Async(filePath, ct)`.
- `StorageOptions.ChecksumVerifyMaxBytes` — size cap above which re-hashing is
  skipped (reused as the cap for scrub and manual verify).
- Background-service + options pattern from `RetentionCleanupService` /
  `RetentionOptions`.
- Notification fan-out pattern (`INotificationService` + `NotificationEventType`).
- Audit-log pattern (`IAuditLogRepository`, system actions with `ActorId = null`).

## Architecture decision

**Stateful scrub with per-artifact integrity columns** (chosen over stateless
full-rescan and over restore-only verification). Per-artifact state lets the
sweep prioritise least-recently-verified objects, spread I/O over time, and lets
the UI show a real status and last-verified timestamp — matching how flagships
expose verify state per snapshot.

## 1. Data model

Add to `BackupArtifact`:

- `IntegrityStatus` (new enum `ArtifactIntegrityStatus`: `Unverified = 0`,
  `Verified = 1`, `Failed = 2`).
- `LastVerifiedAt` (`DateTime?`).

Failure detail (expected vs. actual hash) is **not** stored on the entity; it
goes to the audit log. One EF migration (modeled on `AddRetentionStrategies`).
Existing rows default to `Unverified` / `null`.

## 2. Scrub sweep (backend)

New `IntegrityScrubService : BackgroundService` (in `Backup.Server.Api/Services`,
alongside `RetentionCleanupService`).

New options section `Integrity` (`RetentionOptions`-style):

- `ScrubIntervalHours` (default `168` = weekly) — sweep cadence.
- `ScrubBatchSize` (default `50`) — max artifacts re-hashed per tick (throttles
  MinIO I/O).
- Reuses `Storage:ChecksumVerifyMaxBytes` as the per-object size cap.

Per tick:

1. Select up to `ScrubBatchSize` artifacts ordered by `LastVerifiedAt` ascending
   (`null` first) — least-recently-verified first.
2. For each artifact:
   - If `SizeBytes > ChecksumVerifyMaxBytes` (when set): skip re-hash, audit
     `artifact.scrub_skipped`, leave status unchanged.
   - Else compute `ComputeObjectSha256Async(ObjectKey)` and compare to `Checksum`:
     - **match** → `IntegrityStatus = Verified`, `LastVerifiedAt = now`. Info log
       only (no per-OK audit row — avoids audit-log spam).
     - **mismatch** → `IntegrityStatus = Failed`. Audit `artifact.scrub_failed`
       with `objectKey`, expected and actual checksum. **Not deleted** — a corrupt
       artifact is evidence; retention/operator decides its fate.
     - **object missing in storage** → `IntegrityStatus = Failed`. Audit
       `artifact.scrub_missing`.
3. If the run produced ≥1 failure → `NotifyIntegrityCheckFailedAsync(failedCount,
   ...)`.

New `NotificationEventType.IntegrityCheckFailed = 7` + `INotificationService`
method `NotifyIntegrityCheckFailedAsync` + dispatcher wiring (matches the
`RetentionCleaned` precedent).

New repository method on `IBackupArtifactRepository`:
`GetArtifactsForScrubAsync(int batchSize, CancellationToken)` returning the
prioritised batch (Job/Policy not required — only `ObjectKey`, `Checksum`,
`SizeBytes`, `Id`).
Plus `UpdateIntegrityAsync(Guid id, ArtifactIntegrityStatus, DateTime? verifiedAt, CancellationToken)`.

DI registration of `IntegrityScrubService` + `Integrity` options binding in
`Program.cs`.

## 3. Verify-on-restore (agent + backend)

- Backend: add `Checksum` (expected SHA256) to `PendingRestoreResponse`. The
  pending-restore endpoint already loads the artifact, so this is a field add.
- Agent `RestoreExecuter.ExecutePendingAsync`: after `DownloadFileAsync` into the
  temp file and **before** `ApplyRestoreAsync`:
  - If `pending.Checksum` is non-empty: `ComputeSha256Async(temp)` and compare.
    - **mismatch** → `FailRestoreJobAsync(jobId, "Artifact checksum verification
      failed: downloaded data does not match the expected SHA256.")`, delete the
      temp file, return. The restore target is **never touched**.
    - **match** → proceed to `ApplyRestoreAsync`.
  - If `pending.Checksum` is empty (legacy artifact): skip with a warning log
    (backward compatible).

The existing restore-failure path already surfaces a `RestoreFailed` notification
and marks the job failed, so no new notification type is needed here.

## 4. Minimal UI (Frontend-2.0)

- Artifacts table: integrity badge (`✓ Verified` / `Unverified` / `✗ Failed`) and
  last-verified timestamp, sourced from new fields on the artifact DTO
  (`BackupArtifactDto` gains `integrityStatus`, `lastVerifiedAt`).
- **Verify now** action (operator/admin) → `POST /api/backup-artifacts/{id}/verify`
  on `BackupArtifactsController`: synchronous re-hash via `ComputeObjectSha256Async`
  (subject to the same `ChecksumVerifyMaxBytes` cap; returns `verify_skipped` if
  over cap), updates the row, returns the new status. Audited as
  `artifact.verify_manual`.
- i18n keys (en/ru), a small badge component, and an artifact API/query hook
  update under `entities/artifact`.

## 5. Testing (TDD — tests first)

- Unit: scrub batch selection (prioritises `null`/oldest `LastVerifiedAt`,
  respects `ScrubBatchSize` and size cap) — pure-logic style like
  `RetentionEvaluatorTests`.
- Integration (SQLite + fake/stub storage), modeled on
  `ArtifactChecksumVerificationTests`:
  - scrub marks `Verified` on match, `Failed` on mismatch and on missing object,
    and emits the audit actions / notification on failure.
  - manual verify endpoint updates status and respects the size cap.
- Agent: verify-on-restore fails the job and leaves the target untouched on
  mismatch; proceeds on match; skips on empty checksum.

## Config additions (`appsettings.example.json`)

```jsonc
"Integrity": {
  "_": "Background scrub re-hashes stored artifacts to detect at-rest bit-rot. ScrubBatchSize throttles MinIO I/O per tick; objects larger than Storage:ChecksumVerifyMaxBytes are skipped.",
  "ScrubIntervalHours": 168,
  "ScrubBatchSize": 50
}
```

## Branch & sequencing

- Continues `feature/retention-and-integrity`. `main` is frozen — do not touch.
- The branch currently holds **uncommitted** retention + upload-verify work.
  Before implementing this cycle, commit that existing work as its own atomic
  commit (subject to user confirmation), then build this cycle as separate
  commits on top.
- Untracked root-level `.docx`/`.pptx` presentation files must not be swept into
  any commit (gitignore or leave unstaged).

## Risks / honest trade-offs

- **Scrub I/O**: re-hashing streams whole artifacts through the backend. Mitigated
  by `ScrubBatchSize`, weekly default cadence, size cap, and least-recently-
  verified prioritisation. No external storage costs (self-hosted MinIO).
- **Synchronous manual verify** can block on a large object; bounded by the size
  cap. If it proves painful, a later cycle can move it to a queued job.
- **No auto-remediation**: a `Failed` artifact is flagged, not repaired. True
  remediation needs a second copy (3-2-1 replication), which is a future cycle.
