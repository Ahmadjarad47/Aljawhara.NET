#!/usr/bin/env bash
set -Eeuo pipefail

umask 022
BACKUP_DIR=/var/backups/aljawhara
STATE_DIR=/var/lib/aljawhara-backup
VERIFIED_DIR="$STATE_DIR/verified"
STATUS_FILE="$STATE_DIR/verification.status"
METRICS_FILE="$STATE_DIR/metrics"

install -d -o root -g root -m 0755 "$STATE_DIR"
now=$(date -u +%s)
last_success=0
last_restore=0
verified_count=0
latest_file=
shopt -s nullglob

for marker in "$VERIFIED_DIR"/*.verified; do
    base=$(basename -- "$marker" .verified)
    backup="$BACKUP_DIR/$base"
    [[ -f "$backup" ]] || continue
    verified_count=$((verified_count + 1))
    epoch=$(stat -c %Y -- "$backup")
    restore_epoch=$(awk -F= '$1=="verified_epoch" {print $2}' "$marker")
    if [[ "$restore_epoch" =~ ^[0-9]+$ ]] && (( restore_epoch > last_restore )); then
        last_restore=$restore_epoch
    fi
    if (( epoch > last_success )); then
        last_success=$epoch
        latest_file=$backup
    fi
done

verification_ok=0
if [[ -f "$STATUS_FILE" ]]; then
    verification_ok=$(awk -F= '$1=="verification_ok" {print $2}' "$STATUS_FILE")
fi
[[ "$verification_ok" =~ ^[01]$ ]] || verification_ok=0

missing=1
if (( last_success > 0 && now - last_success <= 97200 )); then missing=0; fi

last_complete=$(journalctl -u aljawhara.service --since '72 hours ago' --no-pager -o short-unix \
    | grep -F 'PostgreSQL backup completed:' | tail -n 1 | awk '{split($1,a,"."); print a[1]}' || true)
last_failure=$(journalctl -u aljawhara.service --since '72 hours ago' --no-pager -o short-unix \
    | grep -F 'PostgreSQL backup failed;' | tail -n 1 | awk '{split($1,a,"."); print a[1]}' || true)
[[ "$last_complete" =~ ^[0-9]+$ ]] || last_complete=0
[[ "$last_failure" =~ ^[0-9]+$ ]] || last_failure=0
if (( last_complete == 0 )); then last_complete=$last_success; fi
job_failure=0
if ! systemctl is-active --quiet aljawhara.service || (( last_failure > last_complete )); then job_failure=1; fi

partials=("$BACKUP_DIR"/*.partial)
completed=("$BACKUP_DIR"/aljawhara-????-??-??.dump)
directory_bytes=$(du -sb "$BACKUP_DIR" 2>/dev/null | awk '{print $1}')
disk_free_bytes=$(df -B1 --output=avail "$BACKUP_DIR" | tail -n 1 | tr -d ' ')
tmp=$(mktemp "$STATE_DIR/.metrics.XXXXXX")
{
    printf 'last_success_epoch=%s\n' "$last_success"
    printf 'backup_job_failure=%s\n' "$job_failure"
    printf 'missing_scheduled=%s\n' "$missing"
    printf 'verification_failure=%s\n' "$((1-verification_ok))"
    printf 'verified_count=%s\n' "$verified_count"
    printf 'completed_count=%s\n' "${#completed[@]}"
    printf 'directory_bytes=%s\n' "${directory_bytes:-0}"
    printf 'disk_free_bytes=%s\n' "${disk_free_bytes:-0}"
    printf 'last_restore_epoch=%s\n' "$last_restore"
    printf 'partial_count=%s\n' "${#partials[@]}"
    printf 'last_backup_log_epoch=%s\n' "$last_complete"
} >"$tmp"
chmod 0644 "$tmp"
mv -f "$tmp" "$METRICS_FILE"

