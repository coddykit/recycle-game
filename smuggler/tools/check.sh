#!/bin/sh
# Headless QA gate (W64): kit sync + §16 static gate + i18n + every test scene + the perf gate. Fails on engine
# script errors as well as on test failures. Never launches an emulator, export or build service.
#   sh tools/check.sh            (from games/smuggler; the perf gate needs a display and prints SKIP without one)
set -eu
P=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
GODOT=${GODOT:-/Volumes/BuildSSD/godot/4.7.2/Godot.app/Contents/MacOS/Godot}
"$GODOT" --version | grep -q '^4\.7\.2\.' || { echo 'Godot 4.7.2 required'; exit 1; }
OUT=$(mktemp -d)
cp "$P/project.godot" "$OUT/project.godot"
cp "$P/export_presets.cfg" "$OUT/export_presets.cfg"
trap 'cp "$OUT/project.godot" "$P/project.godot"; cp "$OUT/export_presets.cfg" "$P/export_presets.cfg"; rm -rf "${OUT:?}"' EXIT HUP INT TERM
# Patron 10-27 (#157-7): parallel check.sh runs (agents, slaves, Jenkins) never share user:// saves / settings — a
# throw-away HOME inside $OUT (removed with it); Python keeps the real user site-packages (PIL), git the real config
REAL_HOME=${REAL_HOME:-$HOME}
PYTHONUSERBASE=${PYTHONUSERBASE:-$(HOME="$REAL_HOME" python3 -m site --user-base 2>/dev/null || true)}
GIT_CONFIG_GLOBAL=${GIT_CONFIG_GLOBAL:-$REAL_HOME/.gitconfig}
HOME="$OUT/home"; XDG_DATA_HOME="$HOME/.local/share"; XDG_CONFIG_HOME="$HOME/.config"
export REAL_HOME PYTHONUSERBASE GIT_CONFIG_GLOBAL HOME XDG_DATA_HOME XDG_CONFIG_HOME
mkdir -p "$HOME"
# run <godot args...>: exit code + engine errors fail; the log is kept for the marker grep
run() {
  if ! "$GODOT" --headless --audio-driver Dummy --path "$P" "$@" > "$OUT/run.log" 2>&1; then
    cat "$OUT/run.log"; exit 1
  fi
  cat "$OUT/run.log"
  # Patron 10-29: "resources still in use at exit" fails again (headless runs are silent, tests/audio_exit_test.gd)
  if grep "^ERROR:" "$OUT/run.log" | grep -vE "RID allocations|ObjectDB"; then exit 1; fi
  if grep -E 'SCRIPT ERROR|Parse Error|Compile Error' "$OUT/run.log"; then exit 1; fi
}
# #62: real input -> content-ready headless frame, desktop and Low, <=100 ms.
if [ "${1:-}" = "--tab-latency" ]; then
  run --editor --import --quit
  run res://tests/tab_open_probe.tscn --   --limit 100
  sh "$P/addons/coddy_ui/tests/tab_budget.sh" "$OUT/run.log" || { echo "TAB_BUDGET retry once (host load)"; run res://tests/tab_open_probe.tscn --   --limit 100; sh "$P/addons/coddy_ui/tests/tab_budget.sh" "$OUT/run.log" || exit 1; }   # #183 tap → first row ≤ 150 ms
  grep -q "TAB_OPEN_PROBE:.* 0 failures" "$OUT/run.log"
  run res://tests/tab_open_probe.tscn --  --phone --limit 100
  sh "$P/addons/coddy_ui/tests/tab_budget.sh" "$OUT/run.log" || { echo "TAB_BUDGET retry once (host load)"; run res://tests/tab_open_probe.tscn --  --phone --limit 100; sh "$P/addons/coddy_ui/tests/tab_budget.sh" "$OUT/run.log" || exit 1; }   # #183 tap → first row ≤ 150 ms
  grep -q "TAB_OPEN_PROBE:.* 0 failures" "$OUT/run.log"
  echo 'TAB_LATENCY_CHECK OK'
  exit 0
fi

# scene <name> <marker>: res://tests/<name>.tscn must print the marker line
scene() { run "res://tests/$1.tscn"; grep -Eq "$2" "$OUT/run.log" || { tail -30 "$OUT/run.log"; echo "check.sh: $1 did not report '$2'"; exit 1; }; }
[ ! -f "$P/../tools/perf/sync.sh" ] || bash "$P/../tools/perf/sync.sh" --check "$P"
# W64 UI kit (games/tools/ui): the game's copy matches the kit; §16 static gate — every ScrollContainer is TouchScroll-wired.
[ ! -f "$P/../tools/ui/sync.sh" ] || bash "$P/../tools/ui/sync.sh" --check "$P"
[ ! -f "$P/../tools/ui/check_safe_overlays.py" ] || python3 "$P/../tools/ui/check_safe_overlays.py" "$P"   # 10-27: edge-anchored overlays keep off the system bars
[ ! -f "$P/../tools/anim/sync.sh" ] || bash "$P/../tools/anim/sync.sh" --check "$P"   # W68 coddy_anim copy
[ ! -f "$P/../tools/ui/check_touch_scroll.py" ] || python3 "$P/../tools/ui/check_touch_scroll.py" "$P"
python3 "$P/tools/i18n/validate.py"
run --headless --import --quit
run -s res://addons/coddy_ui/tests/run.gd
grep -q "CODDY_UI_TESTS:.* 0 failures" "$OUT/run.log"
# Patron 10-28 (S1 banner audit): no HUD text / button under the status bar, cutout or navigation bar; the toast never on the tabs (4 phones)
run -s res://addons/coddy_ui/tests/safe_layout_test.gd
grep -q "SAFE_LAYOUT:.* 0 failures" "$OUT/run.log"
scene touch_scroll "TOUCH_SCROLL_TESTS:.* 0 failures"
# Each locale gets a fresh scene tree; all four original language cases remain gates.
for locale in en tr de ja; do
  run res://tests/touch_scroll_test.tscn -- "--lang=$locale"
  grep -Eq 'TOUCHSCROLL checks=[0-9]+ failures=0' "$OUT/run.log" || { echo "check.sh: touch_scroll_test $locale failed"; exit 1; }
done
scene money_review_test 'MONEY_REVIEW:.* 0 failures'
scene test_runner 'RESULT: PASS'
scene early_pacing_test 'EARLY_PACING checks=[0-9]+ failures=0'
# Review #74: real game bot, first 15 min forced-idle gap <=30 s; retain 1 h economy gates.
GODOT="$GODOT" python3 "$P/tools/qa_pacing.py" --out "$OUT/pacing.json"
scene quality_test 'QUALITY checks=[0-9]+ failures=0'
scene lifecycle_test 'LIFECYCLE checks=[0-9]+ failures=0'
# Patron 10-29: headless runs are silent and keep no audio stream at exit (was "2 resources still in use" in every run)
scene audio_exit_test 'AUDIOEXIT checks=[0-9]+ failures=0'
scene tap_refresh_test 'TAPREFRESH checks=[0-9]+ failures=0'
# touch audit 10-27: one phone tap (touch + emulated click) opens a HUD chip's sheet once — it no longer opens and shuts
scene phone_tap_once_test 'PHONETAP checks=[0-9]+ failures=0'
# review 10-30 (D3): player ×1 / ×2 speed — hidden in the tutorial, one tap toggles, persists, economy ×2, pause stops it
scene game_speed_test 'GAMESPEED checks=[0-9]+ failures=0'
# #113: tap a bottom tab -> first row drawn (input path as on Android); red above 100 ms in both profiles
scene tab_open_probe 'TAB_OPEN_PROBE:.* 0 failures'
sh "$P/addons/coddy_ui/tests/tab_budget.sh" "$OUT/run.log" || { echo "TAB_BUDGET retry once (host load)"; scene tab_open_probe 'TAB_OPEN_PROBE:.* 0 failures'; sh "$P/addons/coddy_ui/tests/tab_budget.sh" "$OUT/run.log" || exit 1; }   # #183 tap → first row ≤ 150 ms
run res://tests/tab_open_probe.tscn -- --phone --limit 100; grep -Eq 'TAB_OPEN_PROBE:.* 0 failures' "$OUT/run.log" || { tail -30 "$OUT/run.log"; echo "check.sh: tab_open_probe --phone failed"; exit 1; }
sh "$P/addons/coddy_ui/tests/tab_budget.sh" "$OUT/run.log" || { echo "TAB_BUDGET retry once (host load)"; run res://tests/tab_open_probe.tscn -- --phone --limit 100; grep -Eq 'TAB_OPEN_PROBE:.* 0 failures' "$OUT/run.log" || { tail -30 "$OUT/run.log"; echo "check.sh: tab_open_probe --phone failed"; exit 1; }; sh "$P/addons/coddy_ui/tests/tab_budget.sh" "$OUT/run.log" || exit 1; }   # #183 tap → first row ≤ 150 ms
scene graphics_notice_test 'GFXNOTICE checks=[0-9]+ failures=0'
scene graphics_policy_test 'GFX checks=[0-9]+ failures=0'
scene settings_memory_test 'SETTINGS-MEM checks=[0-9]+ failures=0'
scene iap_test 'IAP checks=[0-9]+ failures=0'
scene iap_matrix_test 'IAPMATRIX checks=[0-9]+ failures=0'
scene rc_ads_test 'RC ADS checks=[0-9]+ failures=0'
scene analytics_consent_test 'ANALYTICS CONSENT checks=[0-9]+ failures=0'
scene ads_sdk_init_test 'ADS SDK INIT checks=[0-9]+ failures=0'
scene camera_frame_test 'CAMERA_FRAME checks=[0-9]+ failures=0'
scene desert_redo_test 'DESERT_REDO checks=[0-9]+ failures=0'
# W66 10-29: HD models (tools/blender/build_w66.py) keep the canonical nodes / box; Low never loads assets/models/hd
scene w66_hd_test 'W66_HD checks=[0-9]+ failures=0'
scene low_batch_test 'LOWBATCH checks=[0-9]+ failures=0'
scene physics_gate_test 'PHYSICS GATE checks=[0-9]+ failures=0'
scene perf_w60_test 'PERF checks=[0-9]+ failures=0'
scene traffic_test 'TRAFFIC checks=[0-9]+ failures=0'
# W68 10-30: the whole map at once (fleet on contracts + ambient cars + HQ crew) — an ambient car never overlaps a fleet vehicle
scene fleet_overlap_test 'FLEET_OVERLAP checks=[0-9]+ failures=0'
scene onboarding_hero_test 'ONBOARDING checks=[0-9]+ failures=0'
scene banner_test 'BANNER checks=[0-9]+ failures=0'
SCAN_LANGS="en de ru" run -s tests/tool_layout_scan.gd
grep -q "LAYOUT-SCAN done: .* 0 findings" "$OUT/run.log" || { grep "^LAYOUT " "$OUT/run.log" | cut -c1-400; echo "check.sh: layout scan found overflow"; exit 1; }
# Performance gate (owner rule 2026-10-22, games/CLAUDE.md §0): Low profile, windowed; prints SKIP where there is no display
# the Linux job checks out only games/<game> + games/jenkins (no games/tools): the gate runs locally
[ ! -f "$P/../tools/perf/perf_gate.sh" ] || sh "$P/../tools/perf/perf_gate.sh" "$P"

# #70: source-kit font chain + bannerless house-card glyph fallback, all store locales.
GODOT="$GODOT" sh "$P/addons/coddy_ui/tests/check_fonts.sh" "$P"
