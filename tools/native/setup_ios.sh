#!/usr/bin/env bash
set -euo pipefail
# Downloads use the team's Mac Mini network; no app credentials are needed.
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
HOST="${BUILD_HOST:-mehmet@100.113.101.52}"
ssh "$HOST" 'set -e
archive=/tmp/smuggler-rc-ios.zip
if [ ! -f "$archive" ]; then
  curl -fL --retry 2 -o "$archive" https://github.com/godot-x/revenuecat/releases/download/2.4.0/godotx_revenue_cat.zip
fi
actual=$(shasum -a 256 "$archive" | cut -d " " -f1)
[ "$actual" = c9e266c4a989f4648222fcd026b203dc892beacb1b6be064e7d458071324c308 ]
mkdir -p /tmp/smuggler-rc-unpack
cd /tmp/smuggler-rc-unpack
unzip -qo "$archive" "godotx_revenue_cat/ios/*"'
rsync -az "$HOST:/tmp/smuggler-rc-unpack/godotx_revenue_cat/ios/" "$ROOT/ios/"
printf 'RevenueCat 2.4.0 iOS payload installed and checksum verified.\n'

# Firebase Core + Analytics native frameworks (shared pinned installer).
bash "$ROOT/../tools/firebase/install_godotx_firebase.sh" "$ROOT"

# Upstream Firebase and RevenueCat ship the same script UID; regenerate locally.
printf 'uid://cse86oj1r4glr\n' > "$ROOT/addons/godotx_firebase/export_plugin.gd.uid"
