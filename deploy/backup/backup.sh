#!/bin/sh
# Online backups of the Otium SQLite database. sqlite3's .backup takes a consistent snapshot while the server keeps
# writing (WAL mode); copying otium.db with cp would not.
#
#   otium-backup        back up now, then every BACKUP_INTERVAL_HOURS
#   otium-backup once   back up once and exit
#
# Settings (environment):
#   BACKUP_INTERVAL_HOURS    hours between backups (default 6)
#   BACKUP_KEEP_DAYS         local backups older than this are deleted (default 14)
#   BACKUP_RCLONE_REMOTE     optional rclone destination for off-site copies, e.g. offsite:otium-backups
#   BACKUP_RCLONE_KEEP_DAYS  optional: also delete off-site copies older than this (default: keep them all; prefer the
#                            storage provider's lifecycle rules and credentials that cannot delete)
set -eu

DB="${OTIUM_DB:-/data/otium.db}"
DIR="${BACKUP_DIR:-/backups}"
INTERVAL_HOURS="${BACKUP_INTERVAL_HOURS:-6}"
KEEP_DAYS="${BACKUP_KEEP_DAYS:-14}"
REMOTE="${BACKUP_RCLONE_REMOTE:-}"
REMOTE_KEEP_DAYS="${BACKUP_RCLONE_KEEP_DAYS:-}"

log() {
    echo "$(date -u +%Y-%m-%dT%H:%M:%SZ) $*"
}

backup_once() {
    if [ ! -f "$DB" ]; then
        log "no database at $DB yet; nothing to back up"
        return 0
    fi

    name="otium-$(date -u +%Y%m%dT%H%M%SZ).db"
    tmp="$DIR/.$name.partial"
    rm -f "$tmp" "$tmp.gz"

    sqlite3 -cmd ".timeout 30000" "$DB" ".backup '$tmp'"
    check="$(sqlite3 "$tmp" "PRAGMA integrity_check;")"
    if [ "$check" != "ok" ]; then
        log "backup failed its integrity check: $check"
        rm -f "$tmp"
        return 1
    fi
    gzip "$tmp"
    mv "$tmp.gz" "$DIR/$name.gz"
    log "backed up to $DIR/$name.gz ($(du -h "$DIR/$name.gz" | cut -f1))"

    find "$DIR" -maxdepth 1 -type f -name 'otium-*.db.gz' -mtime +"$KEEP_DAYS" -print -delete

    if [ -n "$REMOTE" ]; then
        rclone copy "$DIR/$name.gz" "$REMOTE"
        log "copied to $REMOTE"
        if [ -n "$REMOTE_KEEP_DAYS" ]; then
            rclone delete "$REMOTE" --min-age "${REMOTE_KEEP_DAYS}d" --include 'otium-*.db.gz'
        fi
    fi
}

case "${1:-loop}" in
    once)
        backup_once
        ;;
    loop)
        log "backing up $DB every $INTERVAL_HOURS h, keeping $KEEP_DAYS days${REMOTE:+, off-site to $REMOTE}"
        while true; do
            backup_once || log "backup failed; retrying at the next interval"
            sleep $((INTERVAL_HOURS * 3600))
        done
        ;;
    *)
        echo "usage: otium-backup [once|loop]" >&2
        exit 2
        ;;
esac
