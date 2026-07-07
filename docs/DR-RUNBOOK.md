# Disaster Recovery Runbook — RestoreMe control plane

RestoreMe backs up your machines; this runbook is about backing up — and
restoring — **RestoreMe itself**. Read it before you need it, and rehearse
the restore at least once on a scratch host.

## What the control plane consists of

| Asset | Where it lives | Lost if not backed up |
|---|---|---|
| PostgreSQL metadata | `db_data` volume | Agents, policies, job history, artifact index, users, audit log, notification channels |
| DataProtection key ring | `backend_keys` volume | Ability to decrypt notification-channel secrets stored in the database |
| Backup artifacts | `minio_data` volume | The actual backup data your agents uploaded |
| Compose config + secrets | `docker-compose/` directory (incl. `secrets/*.txt`) | Stack definition, DB/MinIO credentials, JWT signing keys |

The database and the key ring **belong together**: notification-channel
settings (bot tokens, webhook URLs) are encrypted at rest with the key
ring. A database restored without the matching keys starts fine, but every
notification channel's secret is unreadable and must be re-entered.

## What the sidecar backs up automatically

The `control-plane-backup` service in `docker-compose/docker-compose.yml`
runs on every stack (dev and prod). Every `BACKUP_INTERVAL_HOURS`
(default 24 h) it writes into `docker-compose/backups/` on the host:

- `db-<UTC-stamp>.dump` — `pg_dump --format=custom` of `restoreme_db`
  (compressed, restorable with `pg_restore`)
- `keys-<UTC-stamp>.tar.gz` — archive of the DataProtection key ring

and keeps the newest `BACKUP_KEEP` copies of each (default 14). A
`.last-success` marker drives the container healthcheck: the service goes
**unhealthy** when the newest backup is older than two cycles.

Tuning (set in `.env` next to the compose file):

```env
BACKUP_INTERVAL_HOURS=24
BACKUP_KEEP=14
```

### What the sidecar does NOT cover — your responsibility

1. **Off-host shipping.** `docker-compose/backups/` sits on the same
   machine as the database it protects. Sync it elsewhere on a schedule
   (`rsync`/`rclone`/`restic` to another host, NAS, or object storage).
   A control-plane backup that dies with the host is only half a backup.
2. **MinIO artifact data.** Artifact volumes are typically orders of
   magnitude larger than the metadata, so they are not part of the dump.
   Options, by increasing durability:
   - filesystem-level snapshot/backup of the `minio_data` volume while
     MinIO is stopped (crash-consistent otherwise);
   - continuous `mc mirror --watch` of the `backups` bucket to a second
     MinIO/S3 endpoint;
   - versioned bucket replication if you run a multi-node MinIO.
   Without artifact data you can still restore the control plane; job
   history will reference artifacts that no longer exist.
3. **Compose config and secrets.** `docker-compose/secrets/*.txt` and any
   `.env` are not in the dump. Keep a copy in your password
   manager / configuration management. Restoring with *different* secrets
   works (they authenticate infra, they don't encrypt data) — except
   `Jwt:SigningKey`, whose rotation just logs everyone out.

## Restore procedure

Scenario: the host (or a volume) is gone; you have the
`docker-compose/` directory (or the repo checkout), the secret files, and
an off-host copy of `backups/`.

### 0. Pick the artifacts

```sh
ls backups/
# choose the newest matching PAIR — db and keys from the same stamp
DB_DUMP=backups/db-20260707T120000Z.dump
KEYS_TAR=backups/keys-20260707T120000Z.tar.gz
```

### 1. Start only the database (fresh volume)

```sh
cd docker-compose
docker compose up -d db
docker compose ps db   # wait for healthy
```

A fresh `db_data` volume initializes an empty `restoreme_db` owned by
`restoreme_user` — exactly what `pg_restore` needs. **Do not start the
backend yet**: on an empty database it would run migrations and seed a
bootstrap admin, and the restore below would collide with those rows.

### 2. Restore the database dump

```sh
docker compose cp "$DB_DUMP" db:/tmp/restore.dump
docker compose exec db sh -c 'pg_restore -U restoreme_user -d restoreme_db --no-owner --exit-on-error /tmp/restore.dump && rm /tmp/restore.dump'
```

If the target database is NOT empty (e.g. re-restoring after a bad
attempt), recreate it first:

```sh
docker compose exec db psql -U restoreme_user -d postgres \
  -c 'DROP DATABASE restoreme_db;' -c 'CREATE DATABASE restoreme_db OWNER restoreme_user;'
```

### 3. Restore the DataProtection key ring

```sh
# Populate the backend_keys volume before the backend first starts.
docker compose create backend           # creates the volume without starting
docker run --rm -v restoreme_backend_keys:/keys \
  -v "$(pwd)/backups:/backups:ro" postgres:18.3 \
  sh -c 'tar -xzf /backups/'"$(basename "$KEYS_TAR")"' -C /keys'
```

(The volume name is `<project>_backend_keys`; with the default project
name that is `restoreme_backend_keys` — check `docker volume ls`.)

### 4. Start the rest of the stack

```sh
docker compose up -d
```

Migrations run idempotently on top of the restored schema. The bootstrap
admin is **not** re-seeded because the user table is not empty.

### 5. Verify

- `GET /health` returns 200; log in with a pre-disaster account.
- Agents/Policies/Jobs pages show pre-disaster data.
- Open **Notifications** and use *Test channel* on one channel — this
  proves the key ring matches the database (decryption works).
- Agents reconnect on their next heartbeat; their tokens survive the
  restore (signing key unchanged). If you rotated `Jwt:AgentSigningKey`,
  revoke and re-enroll agents.
- If MinIO data was lost, artifact downloads 404 — the metadata is
  intact, the objects are not. Communicate this before anyone needs a
  restore.

## Rehearsal

Run the restore on a scratch machine (or a second compose project name:
`docker compose -p restoreme-drill …`) at least once, and after any
change to this file. An unrehearsed runbook is a hypothesis, not a plan.
