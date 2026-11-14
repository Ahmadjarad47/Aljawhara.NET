#!/usr/bin/env bash
set -euo pipefail

key=${1:-}
case "$key" in
    last_success_epoch|backup_job_failure|missing_scheduled|verification_failure|verified_count|completed_count|directory_bytes|disk_free_bytes|last_restore_epoch|partial_count|last_backup_log_epoch) ;;
    *) exit 1 ;;
esac
awk -F= -v key="$key" '$1 == key { print $2; found=1 } END { if (!found) exit 1 }' /var/lib/aljawhara-backup/metrics

