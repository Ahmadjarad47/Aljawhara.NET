#!/usr/bin/env bash
set -euo pipefail
publish_dir="${1:?Supply the absolute API publish directory}"
export PLAYWRIGHT_BROWSERS_PATH="${PLAYWRIGHT_BROWSERS_PATH:-/opt/aljawhara-playwright}"
driver="$publish_dir/.playwright/node/linux-x64/node"
cli="$publish_dir/.playwright/package/cli.js"
test -f "$driver" && test -f "$cli"
# SCP from Windows can leave this executable readable but non-executable for www-data.
chmod 0755 "$driver"
install -d -o www-data -g www-data -m 0750 "$PLAYWRIGHT_BROWSERS_PATH"
# Full Chromium's crash handler also needs a writable HOME/XDG configuration directory.
install -d -o www-data -g www-data -m 0700 /var/lib/aljawhara /var/lib/aljawhara/.config /var/lib/aljawhara/.cache
"$driver" "$cli" install-deps chromium
runuser -u www-data -- env PLAYWRIGHT_BROWSERS_PATH="$PLAYWRIGHT_BROWSERS_PATH" "$driver" "$cli" install chromium
runuser -u www-data -- test -x "$driver"
# No service restart and no invoice state changes. Run the Linux smoke test before recovery.
