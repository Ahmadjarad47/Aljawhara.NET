#!/usr/bin/env bash
set -Eeuo pipefail

revision="${1:?Supply the Git commit SHA}"
[[ "$revision" =~ ^[a-f0-9]{40}$ ]] || { echo 'Invalid revision'; exit 2; }
base=/var/www/Aljawhara.NET
staged="$base/incoming/$revision"
live="$base/publish"
stamp="$(date -u +%Y%m%dT%H%M%SZ)-$revision"
previous="$base/releases/$stamp-previous"
failed="$base/releases/$stamp-failed"
exec 9>/run/lock/aljawhara-deploy.lock
flock -n 9 || { echo 'Another deployment is running'; exit 1; }
test -f "$staged/Ecom.API.dll"
test -f "$staged/Ecom.API.runtimeconfig.json"
test -d "$live" && test ! -L "$live"
test ! -e "$previous" && test ! -e "$failed"
install -d -m 0755 "$base/releases"

# Runtime configuration stays on the server and is never copied into GitHub artifacts.
for config in "$live"/appsettings*.json; do
    test -f "$config" || continue
    install -o root -g www-data -m 0640 "$config" "$staged/$(basename "$config")"
done
install -d -o www-data -g www-data -m 0700 /var/lib/aljawhara/monitoring
bash "$staged/deploy/install-invoice-browser.sh" "$staged"

stopped=0
rollback() {
    result=$?
    trap - ERR
    if (( stopped )); then
        systemctl stop aljawhara.service || true
        if test -d "$previous"; then
            if test -d "$live"; then mv -- "$live" "$failed"; fi
            mv -- "$previous" "$live"
        fi
        systemctl start aljawhara.service || true
    fi
    echo "Deployment failed; previous release restored when available. Exit=$result" >&2
    exit "$result"
}
trap rollback ERR
stopped=1
systemctl stop aljawhara.service
mv -- "$live" "$previous"
mv -- "$staged" "$live"
systemctl start aljawhara.service
healthy=0
for attempt in {1..30}; do
    if curl --silent --fail --max-time 3 -H 'X-Forwarded-Proto: https' http://127.0.0.1:5000/health >/dev/null; then
        healthy=1
        break
    fi
    sleep 1
done
(( healthy ))
systemctl is-active --quiet aljawhara.service
stopped=0
trap - ERR
echo "Deployed $revision; health verified. Previous release retained at $previous."
