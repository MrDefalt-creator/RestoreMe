#!/bin/sh
# Control-plane self-backup loop for the RestoreMe stack.
#
# Every BACKUP_INTERVAL_HOURS it writes two artifacts into /backups:
#   db-<utc-stamp>.dump     pg_dump custom format (compressed, pg_restore-able)
#   keys-<utc-stamp>.tar.gz DataProtection key ring (backend_keys volume)
# and prunes each kind down to the newest BACKUP_KEEP copies.
#
# The key ring is as critical as the database: NotificationChannel settings
# are encrypted at rest with these keys, so a database restored without the
# matching key ring loses every configured notification secret.
#
# /backups is a host bind mount — ship it off-host (rsync, rclone, object
# storage) to survive loss of the whole machine. See docs/DR-RUNBOOK.md.
set -eu

INTERVAL_HOURS="${BACKUP_INTERVAL_HOURS:-24}"
KEEP="${BACKUP_KEEP:-14}"
PGHOST="${BACKUP_PGHOST:-db}"
PGUSER="${BACKUP_PGUSER:-restoreme_user}"
PGDATABASE="${BACKUP_PGDATABASE:-restoreme_db}"
OUT=/backups

PGPASSWORD="$(cat /run/secrets/postgres-password)"
export PGPASSWORD PGHOST PGUSER PGDATABASE

# Keep the newest $KEEP files matching $1*$2; delete the rest. Timestamped
# names sort chronologically, so lexicographic sort is enough.
prune() {
  ls -1 "$OUT"/"$1"*"$2" 2>/dev/null | sort | head -n "-$KEEP" | while read -r old; do
    rm -f "$old"
    echo "pruned $old"
  done
}

# Explicit `|| return 1` on every step: `set -e` is suspended inside a
# function called from an `if` condition, so without these a failed
# pg_dump would fall through to the success marker.
backup_once() {
  stamp="$(date -u +%Y%m%dT%H%M%SZ)"

  # Write to a dot-file first and rename: a backup interrupted mid-write
  # must never be mistaken for a restorable artifact.
  pg_dump -Fc -f "$OUT/.tmp-db.dump" || return 1
  mv "$OUT/.tmp-db.dump" "$OUT/db-$stamp.dump" || return 1

  tar -czf "$OUT/.tmp-keys.tar.gz" -C /keys . || return 1
  mv "$OUT/.tmp-keys.tar.gz" "$OUT/keys-$stamp.tar.gz" || return 1

  prune db- .dump
  prune keys- .tar.gz

  # Healthcheck watches this file's mtime.
  date -u +%Y-%m-%dT%H:%M:%SZ > "$OUT/.last-success"
  echo "backup complete: db-$stamp.dump keys-$stamp.tar.gz (keeping $KEEP)"
}

echo "control-plane backup: every ${INTERVAL_HOURS}h, keeping $KEEP copies"
while :; do
  if backup_once; then
    :
  else
    # Don't exit: a transient DB hiccup shouldn't kill the schedule. The
    # healthcheck flips unhealthy once .last-success is older than 2 cycles.
    echo "backup FAILED, retrying in 15m" >&2
    sleep 900
    continue
  fi
  sleep "$((INTERVAL_HOURS * 3600))"
done
