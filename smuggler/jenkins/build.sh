#!/bin/sh
# Linux Jenkins adapter (Games/smuggler-mobile, label android-linux; games/jenkins/run.sh → here): headless tests +
# signed Android AAB + native symbols + release.json. BUILD-ONLY: no Play upload stage exists (the Patron uploads the
# archived AAB separately with store-automation gp-upload-aab.ts). No iOS here (games iOS paused, owner 2026-10-18;
# tools/build_ios.sh still runs by hand on the Mac).
#   DRY_RUN=1 sh games/jenkins/run.sh smuggler test|android   validates arguments only
set -eu
cd "$(dirname "$0")/.."
ROOT=$(pwd)
. jenkins/game.env
STAGE=${1:-}
ACTION=${2:-}
case "$STAGE" in
  test) [ -z "$ACTION" ] || { echo 'test takes no action'; exit 2; } ;;
  android) ACTION=${ACTION:-build-only}; [ "$ACTION" = build-only ] || { echo 'Only build-only is supported (Jenkins never uploads; the Patron uploads with gp-upload-aab.ts)'; exit 2; } ;;
  *) echo 'usage: build.sh test|android [build-only]'; exit 2 ;;
esac
if [ "${DRY_RUN:-0}" = 1 ]; then
  echo "DRY_RUN: game=smuggler name=$NAME stage=$STAGE action=$ACTION package=$PLAY_PACKAGE agent=linux-android upload=disabled"
  exit 0
fi
[ "$(uname -s)" = Linux ] || { echo 'Use the Jenkins linux-android agent (Mac jobs are iOS only)'; exit 2; }
export GODOT=${GODOT:-godot}
export JAVA_HOME=${JAVA_HOME:-/usr/lib/jvm/java-17-openjdk-amd64}
export ANDROID_HOME=${ANDROID_HOME:-/opt/android-sdk}
export ANDROID_SDK_ROOT="$ANDROID_HOME"
export PATH="$JAVA_HOME/bin:$ANDROID_HOME/platform-tools:$PATH"
export GRADLE_OPTS=${GRADLE_OPTS:--Dorg.gradle.daemon=false -Dorg.gradle.jvmargs=-Xmx1792m -Dorg.gradle.workers.max=1}
export GAME_SERVICES_MODE=production
export GAME_OUT=${GAME_OUT:-$ROOT/build/jenkins}
"$GODOT" --version | grep -q '^4\.7\.2\.' || { echo 'Godot 4.7.2 required'; exit 1; }
mkdir -p "$GAME_OUT"
VERSION=$(sed -n 's/^config\/version="\(.*\)"/\1/p' project.godot)
# Shared lints run.sh used to run on the Mac (sparse checkout: games/tools may be absent → skipped, never an error)
shared_lints() {
  [ ! -f ../tools/ads/check_admob_init.py ] || python3 ../tools/ads/check_admob_init.py "$ROOT" || { echo 'FAILED: AdMob init lint'; exit 1; }
  [ ! -f ../tools/i18n/check_hardcoded_text.py ] || python3 ../tools/i18n/check_hardcoded_text.py "$ROOT" || { echo 'FAILED: hard-coded text lint'; exit 1; }
}
# release.json next to the AAB (Jenkins archives it): commit + version + both SHA-256s; upload stays a Patron step.
release_json() {
  AAB=$1 VERSION_NAME=$VERSION PLAY_PACKAGE=$PLAY_PACKAGE python3 - <<'PYMETA'
import hashlib, json, os, subprocess
from pathlib import Path
aab = Path(os.environ['AAB']); sym = aab.with_suffix('.symbols.zip')
assert aab.is_file() and aab.stat().st_size > 0, 'AAB missing'
has_sym = sym.is_file() and sym.stat().st_size > 0  # optional: the pinned patched engine has no symbol table (games/jenkins/engine_pin.env)
git = lambda *a: subprocess.run(['git', *a], capture_output=True, text=True).stdout.strip()
meta = {
    'commit': git('rev-parse', 'HEAD'),
    'dirty': bool(git('status', '--porcelain', '--', '.')),
    'package': os.environ['PLAY_PACKAGE'],
    'version_name': os.environ['VERSION_NAME'],
    'version_code': int(Path(aab.parent / 'version_code.txt').read_text().strip()),
    'services_mode': 'production',
    'aab': aab.name, 'aab_sha256': hashlib.sha256(aab.read_bytes()).hexdigest(),
    'symbols': sym.name if has_sym else None, 'symbols_sha256': hashlib.sha256(sym.read_bytes()).hexdigest() if has_sym else None,
    'upload': 'none (Jenkins build-only)', 'smoke': 'pending',
}
(aab.parent / 'release.json').write_text(json.dumps(meta, indent=2) + '\n')
print('release.json:', meta['package'], meta['version_name'], meta['version_code'], meta['commit'][:10])
PYMETA
}
case "$STAGE" in
test)
  shared_lints
  # tests/test_runner.tscn is a scene (Node), not a SceneTree script, so games/jenkins/godot_tests.sh (-s) cannot run
  # it; same gate here: import, run, fail on any failed check OR any script that did not compile.
  "$GODOT" --headless --path . --import > "$GAME_OUT/test-import.log" 2>&1 || { tail -30 "$GAME_OUT/test-import.log"; exit 1; }
  if grep -E "SCRIPT ERROR|Failed to load script|Could not create project data directory|Failed loading resource" "$GAME_OUT/test-import.log"; then
    echo "FAILED: project import"; exit 1
  fi
  set +e
  "$GODOT" --headless --path . res://tests/test_runner.tscn > "$GAME_OUT/tests.log" 2>&1
  rc=$?
  set -e
  grep -E "^ *FAIL|^Checks: |^RESULT: " "$GAME_OUT/tests.log" | tail -15 || true
  if grep -E "Parse Error.*\.gd|\.gd.*Parse Error|Compile Error|Failed to load script" "$GAME_OUT/test-import.log" "$GAME_OUT/tests.log" | head -8 | grep .; then
    echo "FAILED: script parse/compile errors ($GAME_OUT/tests.log)"; exit 1
  fi
  SUMMARY=$(grep -E "^Checks: [0-9]+  Failures: 0$" "$GAME_OUT/tests.log" | tail -1 || true)
  [ "$rc" = 0 ] && [ -n "$SUMMARY" ] && grep -q "^RESULT: PASS" "$GAME_OUT/tests.log" || { tail -30 "$GAME_OUT/tests.log"; echo "FAILED: tests exit $rc ($GAME_OUT/tests.log)"; exit 1; }
  "$GODOT" --headless --path . res://tests/quality_test.tscn > "$GAME_OUT/quality-tests.log" 2>&1 || { tail -30 "$GAME_OUT/quality-tests.log"; exit 1; }
  grep -Eq '^QUALITY checks=[1-9][0-9]* failures=0$' "$GAME_OUT/quality-tests.log" || { tail -30 "$GAME_OUT/quality-tests.log"; exit 1; }
  if grep -E 'SCRIPT ERROR|Compile Error|Failed to load script' "$GAME_OUT/quality-tests.log"; then exit 1; fi
  # quality pass 2026-10-18 … onboarding_hero (2026-10-24, #119); same list as the former Mac adapter
  for T in lifecycle graphics_policy graphics_notice iap touch_scroll rc_ads monkey perf_w60 low_batch ads_sdk_init settings_memory physics_gate soak onboarding_hero; do
    "$GODOT" --headless --path . res://tests/${T}_test.tscn > "$GAME_OUT/$T-tests.log" 2>&1 || { tail -30 "$GAME_OUT/$T-tests.log"; echo "FAILED: $T test"; exit 1; }
    grep -Eq 'checks=[1-9][0-9]* failures=0$' "$GAME_OUT/$T-tests.log" || { tail -30 "$GAME_OUT/$T-tests.log"; echo "FAILED: $T test summary"; exit 1; }
    if grep -E 'SCRIPT ERROR|Compile Error|Failed to load script' "$GAME_OUT/$T-tests.log"; then exit 1; fi
  done
  # W51: Settings → Language leaves nothing drawn in the old language
  "$GODOT" --headless --path . -s tests/language_switch_test.gd > "$GAME_OUT/language-switch.log" 2>&1 || { grep -E "FAIL|ERROR" "$GAME_OUT/language-switch.log" | head -20; echo "FAILED: language switch"; exit 1; }
  grep -Eq '^LANGUAGE checks=[1-9][0-9]* failures=0$' "$GAME_OUT/language-switch.log" || { tail -20 "$GAME_OUT/language-switch.log"; echo "FAILED: language switch summary"; exit 1; }
  # text-overflow gate: every panel / tab / event modal in the longest languages stays inside the 720-wide canvas
  SCAN_LANGS="en de ru pt" "$GODOT" --headless --path . -s tests/tool_layout_scan.gd > "$GAME_OUT/layout-scan.log" 2>&1 || { grep -E "^LAYOUT" "$GAME_OUT/layout-scan.log" | head -30; echo "FAILED: text overflow"; exit 1; }
  grep -q '^LAYOUT-SCAN done: [1-9][0-9]* screens, 0 findings' "$GAME_OUT/layout-scan.log" || { tail -20 "$GAME_OUT/layout-scan.log"; echo "FAILED: layout scan summary"; exit 1; }
  python3 tools/i18n/validate.py > "$GAME_OUT/i18n.log" 2>&1 || { cat "$GAME_OUT/i18n.log"; exit 1; }
  python3 -m unittest discover -s tests -p 'test_release_gate.py' > "$GAME_OUT/release-gate-tests.log" 2>&1 || { cat "$GAME_OUT/release-gate-tests.log"; exit 1; }
  cat "$GAME_OUT/i18n.log"
  grep '^QUALITY ' "$GAME_OUT/quality-tests.log"
  # performance gate (games/CLAUDE.md §0): prints PERF_GATE SKIP without a display (this agent) — run on the Mac
  [ ! -f ../tools/perf/perf_gate.sh ] || sh ../tools/perf/perf_gate.sh . || { echo "FAILED: performance gate"; exit 1; }
  echo "TESTS: $SUMMARY" ;;
android)
  sh jenkins/preflight.sh
  # signed AAB with ~/.android-keys/borderlineempire (version stamp, upload-key fingerprint check, real AdMob id check)
  SYMBOLS_REQUIRED=1 sh tools/build_android.sh
  release_json "$ROOT/build/android/borderlineempire.aab"
  printf 'AAB=%s\nVERSION_NAME=%s\n' "$ROOT/build/android/borderlineempire.aab" "$VERSION" > "$GAME_OUT/android.env"
  echo 'build-only: AAB + symbols + release.json ready; no upload performed' ;;
esac
