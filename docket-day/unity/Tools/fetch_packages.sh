#!/bin/sh
# Docket Day — Google's Unity package tarballs (not on any UPM registry): Play In-App Review (+ Play Core / Common) and
# Firebase App + Analytics, pinned by sha256, into Packages/tarballs (gitignored: Firebase App alone is 61 MB). The
# manifest references them as "file:tarballs/<name>.tgz". Run before the first editor open and in CI before the build
# (the build script does). Offline + already present = no-op.
#   sh docket-day/unity/Tools/fetch_packages.sh
set -eu
HERE=$(cd "$(dirname "$0")/.." && pwd)
DIR="$HERE/Packages/tarballs"
BASE=https://dl.google.com/games/registry/unity
mkdir -p "$DIR"
fail=0
while read -r sum name; do
  [ -n "$name" ] || continue
  pkg=${name%-*}
  f="$DIR/$name.tgz"
  if [ ! -f "$f" ] || [ "$(shasum -a 256 "$f" | cut -d' ' -f1)" != "$sum" ]; then
    curl -fsSL -m 600 -o "$f.part" "$BASE/$pkg/$name.tgz" && mv "$f.part" "$f" || { echo "FETCH FAILED: $name"; fail=1; continue; }
  fi
  [ "$(shasum -a 256 "$f" | cut -d' ' -f1)" = "$sum" ] || { echo "SHA256 MISMATCH: $name"; rm -f "$f"; fail=1; }
done <<'LIST'
be7396c8479c8929747adbb08995b2b2c73326679ac0694382c00957f361efcc com.google.play.review-1.8.3
2de4e5a9139308cc264448071d5aa3c88704a315d3b6b86caf65d2e68c07e048 com.google.play.core-1.8.5
b5e553317f0607e03baee0eb7614cd8b3acfda6c1d84723829853047d2d074d6 com.google.play.common-1.9.2
934c52d39cac8465d13f530cab4ebe2e1e336ed4b35286691a08c6854574cb6f com.google.firebase.app-14.1.0
d2b242d7bbf1862aa226f5861d16cfdf5cd368bdd5d7977686a9933fb50450c7 com.google.firebase.analytics-14.1.0
LIST
[ $fail = 0 ] && echo "PACKAGES_OK ($DIR)"
exit $fail
