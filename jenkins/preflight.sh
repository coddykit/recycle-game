#!/bin/sh
# Read-only production configuration gate (Jenkins "Production configuration" stage). Never prints service ids.
# RevenueCat keys, real AdMob app + unit ids, package / Firebase config — the game's own fail-closed check.
set -eu
cd "$(dirname "$0")/.."
python3 tools/check_release.py --platform android --configuration-only
