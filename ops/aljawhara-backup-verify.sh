#!/usr/bin/env bash
set -Eeuo pipefail

umask 077

BACKUP_DIR=/var/backups/aljawhara
STATE_DIR=/var/lib/aljawhara-backup
VERIFIED_DIR="$STATE_DIR/verified"
STATUS_FILE="$STATE_DIR/verification.status"
LOG_DIR=/var/log/aljawhara-backup
LOCK_FILE=/run/lock/aljawhara-backup-verify.lock
RETENTION_COUNT=3

install -d -o root -g root -m 0755 "$STATE_DIR"
install -d -o root -g root -m 0700 "$VERIFIED_DIR" "$LOG_DIR"
exec 9>"$LOCK_FILE"
flock -n 9 || exit 0

log() {
    printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >>"$LOG_DIR/verification.log"
}

# Coordinate with the application's FileStream lock. If a dump is in progress,
# defer all verification and pruning until the next timer run.
if [[ ! -e "$BACKUP_DIR/.backup.lock" ]]; then
    install -o www-data -g www-data -m 0640 /dev/null "$BACKUP_DIR/.backup.lock"
fi
exec 8<>"$BACKUP_DIR/.backup.lock"
if ! flock -n 8; then
    log "deferred: application backup lock is held"
    exit 0
fi

write_status() {
    local ok=$1 code=$2 backup=${3:-none}
    local tmp
    tmp=$(mktemp "$STATE_DIR/.verification.status.XXXXXX")
    {
        printf 'verification_ok=%s\n' "$ok"
        printf 'verification_code=%s\n' "$code"
        printf 'last_verify_epoch=%s\n' "$(date -u +%s)"
        printf 'last_verified_backup=%s\n' "$backup"
    } >"$tmp"
    chmod 0600 "$tmp"
    mv -f "$tmp" "$STATUS_FILE"
}

cleanup_restore() {
    local db=$1 dump_copy=$2
    sudo -u postgres dropdb --if-exists "$db" >/dev/null 2>&1 || true
    rm -f -- "$dump_copy"
}

verify_backup() {
    local source=$1 base date checksum size marker marker_tmp db dump_copy
    local table_count fk_count invalid_fk important_counts restore_epoch

    base=$(basename -- "$source")
    [[ "$base" =~ ^aljawhara-([0-9]{4}-[0-9]{2}-[0-9]{2})\.dump$ ]] || return 0
    date=${BASH_REMATCH[1]}
    checksum=$(sha256sum -- "$source" | awk '{print $1}')
    size=$(stat -c %s -- "$source")
    marker="$VERIFIED_DIR/$base.verified"

    if [[ -f "$marker" ]] && grep -qx "sha256=$checksum" "$marker"; then
        return 0
    fi

    db="aljawhara_verify_${date//-/}_$$"
    dump_copy="/var/lib/postgresql/$db.dump"
    cleanup_restore "$db" "$dump_copy"
    install -o postgres -g postgres -m 0600 "$source" "$dump_copy"

    if ! sudo -u postgres createdb -T template0 "$db"; then
        cleanup_restore "$db" "$dump_copy"
        return 21
    fi
    if ! sudo -u postgres pg_restore --exit-on-error --single-transaction --no-owner --no-acl \
        -d "$db" "$dump_copy" >>"$LOG_DIR/verification.log" 2>&1; then
        cleanup_restore "$db" "$dump_copy"
        return 22
    fi

    table_count=$(sudo -u postgres psql -XAt -d "$db" -c \
        "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind='r' AND n.nspname NOT IN ('pg_catalog','information_schema');")
    fk_count=$(sudo -u postgres psql -XAt -d "$db" -c \
        "SELECT count(*) FROM pg_constraint WHERE contype='f';")
    invalid_fk=$(sudo -u postgres psql -XAt -d "$db" -c \
        "SELECT count(*) FROM pg_constraint WHERE contype='f' AND NOT convalidated;")

    if (( table_count < 1 || invalid_fk != 0 )); then
        cleanup_restore "$db" "$dump_copy"
        return 23
    fi

    important_counts=$(sudo -u postgres psql -XAt -F, -d "$db" -c \
        "SELECT 'AspNetUsers='||count(*) FROM \"AspNetUsers\" UNION ALL SELECT 'Products='||count(*) FROM \"Products\" UNION ALL SELECT 'Orders='||count(*) FROM \"Orders\" UNION ALL SELECT 'OrderItems='||count(*) FROM \"OrderItems\" UNION ALL SELECT 'PaymentAttempts='||count(*) FROM \"PaymentAttempts\" UNION ALL SELECT 'Transactions='||count(*) FROM \"Transactions\" ORDER BY 1;" | paste -sd, -)
    restore_epoch=$(date -u +%s)
    cleanup_restore "$db" "$dump_copy"

    marker_tmp=$(mktemp "$VERIFIED_DIR/.${base}.XXXXXX")
    {
        printf 'verified=1\n'
        printf 'backup=%s\n' "$source"
        printf 'backup_date=%s\n' "$date"
        printf 'size_bytes=%s\n' "$size"
        printf 'sha256=%s\n' "$checksum"
        printf 'verified_epoch=%s\n' "$restore_epoch"
        printf 'table_count=%s\n' "$table_count"
        printf 'foreign_key_count=%s\n' "$fk_count"
        printf 'unvalidated_foreign_key_count=%s\n' "$invalid_fk"
        printf 'important_counts=%s\n' "$important_counts"
    } >"$marker_tmp"
    chmod 0600 "$marker_tmp"
    mv -f "$marker_tmp" "$marker"
    log "verified backup=$base bytes=$size tables=$table_count foreign_keys=$fk_count"
}

shopt -s nullglob
partials=("$BACKUP_DIR"/*.partial)
if (( ${#partials[@]} > 0 )); then
    log "deferred: backup creation is in progress"
    exit 0
fi

backups=("$BACKUP_DIR"/aljawhara-????-??-??.dump)
if (( ${#backups[@]} == 0 )); then
    write_status 0 10 none
    log "failed: no completed backup files found"
    exit 10
fi
IFS=$'\n' backups=($(printf '%s\n' "${backups[@]}" | sort))
unset IFS

for backup in "${backups[@]}"; do
    if verify_backup "$backup"; then
        :
    else
        rc=$?
        write_status 0 "$rc" "$(basename -- "$backup")"
        log "failed backup=$(basename -- "$backup") code=$rc; retention skipped"
        exit "$rc"
    fi
done

verified=()
for backup in "${backups[@]}"; do
    base=$(basename -- "$backup")
    marker="$VERIFIED_DIR/$base.verified"
    [[ -f "$marker" ]] || continue
    checksum=$(sha256sum -- "$backup" | awk '{print $1}')
    grep -qx "sha256=$checksum" "$marker" || continue
    verified+=("$backup")
done

if (( ${#verified[@]} == 0 )); then
    write_status 0 24 none
    log "failed: no backup passed restore verification"
    exit 24
fi

initial_manifest="$LOG_DIR/retention-initial-dry-run.manifest"
if [[ ! -e "$initial_manifest" ]]; then
    initial_delete_count=$(( ${#verified[@]} > RETENTION_COUNT ? ${#verified[@]} - RETENTION_COUNT : 0 ))
    {
        printf 'mode=initial-dry-run-no-deletion\n'
        printf 'retention_count=%s\n' "$RETENTION_COUNT"
        printf 'verified_count=%s\n' "${#verified[@]}"
        printf 'eligible_deletions=%s\n' "$initial_delete_count"
        for ((i=0; i<initial_delete_count; i++)); do
            backup=${verified[$i]}
            printf 'would_delete=%s size_bytes=%s sha256=%s\n' "$backup" \
                "$(stat -c %s -- "$backup")" "$(sha256sum -- "$backup" | awk '{print $1}')"
        done
    } >"$initial_manifest"
    chmod 0600 "$initial_manifest"
fi

if (( ${#verified[@]} > RETENTION_COUNT )); then
    delete_count=$(( ${#verified[@]} - RETENTION_COUNT ))
    manifest="$LOG_DIR/retention-dry-run-$(date -u +%Y%m%dT%H%M%SZ).manifest"
    {
        printf 'mode=dry-run-before-approved-automatic-cleanup\n'
        printf 'retention_count=%s\n' "$RETENTION_COUNT"
        printf 'verified_before=%s\n' "${#verified[@]}"
        printf 'verified_after=%s\n' "$RETENTION_COUNT"
        for ((i=0; i<delete_count; i++)); do
            backup=${verified[$i]}
            printf 'delete=%s size_bytes=%s sha256=%s\n' "$backup" \
                "$(stat -c %s -- "$backup")" "$(sha256sum -- "$backup" | awk '{print $1}')"
        done
    } >"$manifest"
    chmod 0600 "$manifest"

    partials=("$BACKUP_DIR"/*.partial)
    (( ${#partials[@]} == 0 )) || { log "retention aborted: backup creation began"; exit 0; }
    for ((i=0; i<delete_count; i++)); do
        backup=${verified[$i]}
        base=$(basename -- "$backup")
        rm -f -- "$backup" "$VERIFIED_DIR/$base.verified"
        log "pruned backup=$base manifest=$(basename -- "$manifest")"
    done
fi

latest=${verified[${#verified[@]}-1]}
write_status 1 0 "$(basename -- "$latest")"
log "completed verified_count=${#verified[@]} retention=$RETENTION_COUNT"

