#!/bin/sh
# Volt City (Unity) — local / CI check. Runs every gate that exists today and, when a Unity editor is present
# (UNITY_EDITOR=/path/to/Unity), the EditMode tests in batch mode.
#   sh games/volt-city/unity/Tools/check.sh
# Gates without the editor: static source gates, IP denylist (+ proof), generated-file freshness, engine-free core tests
# under a plain .NET SDK (skipped with a note when no dotnet is installed).
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
UNITY=$(dirname "$HERE")
FAIL=0
step() { printf '\n== %s\n' "$1"; }

step "P7 engine-free save corruption (5,000 cases), daily reload/determinism and retained-memory soak"
sh "$HERE/robustness_check.sh" || FAIL=1

step "source gates"
python3 "$HERE/check_unity_sources.py" || FAIL=1

step "IP denylist"
python3 "$HERE/ip_denylist.py" || FAIL=1

step "engine-free core (dotnet)"
if [ -n "${DOTNET:-}" ] || command -v dotnet >/dev/null 2>&1 || [ -x /Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet ] || [ -x /Volumes/BuildSSD/tmp/dotnet-sdk/dotnet ]; then
    OUT=$(sh "$HERE/dotnet_check.sh" 2>&1) || FAIL=1
    echo "$OUT"
    echo "$OUT" | grep -q "Passed!" || FAIL=1
else
    echo "SKIP: no .NET SDK (set DOTNET=) — the Unity EditMode run below covers the same tests"
fi

step "P1 sim + EditMode sim tests + balance gates (dotnet)"
if [ -n "${DOTNET:-}" ] || command -v dotnet >/dev/null 2>&1 || [ -x /Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet ] || [ -x /Volumes/BuildSSD/tmp/dotnet-sdk/dotnet ]; then
    OUT=$(sh "$HERE/sim_dotnet_check.sh" 2>&1) || FAIL=1
    echo "$OUT" | tail -3
    echo "$OUT" | grep -q "Passed!  - Failed:     0" || FAIL=1
else
    echo "SKIP: no .NET SDK — the Unity EditMode run below covers the same tests"
fi

step "P3 generated data fresh (road graph from the art layout, UI icons) + strings table (10 languages)"
python3 "$HERE/road_graph.py" --check || FAIL=1
python3 "$HERE/gen_ui_icons.py" --check || FAIL=1
python3 "$HERE/strings_merge.py" --check | tail -1 || FAIL=1
python3 "$HERE/strings_merge.py" --check >/dev/null 2>&1 || FAIL=1

step "kit icons fresh + no unicode symbol / emoji as a button face (gen_kit_icons.py --check, games/tools/ui/symbol_button_gate.py)"
python3 "$HERE/gen_kit_icons.py" "$(dirname "$HERE")" --check || FAIL=1
python3 "$(dirname "$HERE")/../../tools/ui/symbol_button_gate.py" "$(dirname "$HERE")" || FAIL=1

step "P5 bundled fonts cover every shown character (Noto subsets, Tools/subset_fonts.py)"
FT_PY=""
for c in "${FONTTOOLS_PY:-}" /Volumes/BuildSSD/lego-fonts-2026-10-09/venv/bin/python python3; do
    [ -n "$c" ] && command -v "$c" >/dev/null 2>&1 && "$c" -c "import fontTools" 2>/dev/null && { FT_PY=$c; break; }
done
if [ -n "$FT_PY" ]; then "$FT_PY" "$HERE/subset_fonts.py" --check || FAIL=1; else echo "SKIP: no fontTools (set FONTTOOLS_PY=)"; fi

step "app icon: every slot set, never Unity's default icon (Tools/icon_check.py)"
python3 "$HERE/icon_check.py" || FAIL=1

step "copy gate: no free / trial claim in player-facing text (10 languages)"
python3 "$HERE/copy_gate.py" || FAIL=1

step "P4 IAP catalogue (iap.json, store/iap/rc-catalog.json + catalog.json, strings) fresh + rules"
python3 "$HERE/build_iap.py" --check || FAIL=1

step "P6 store listing (10 locales) + screenshot copy"
OUT=$(python3 "$UNITY/../store/validate.py" 2>&1) || FAIL=1
echo "$OUT" | grep -E "^FAIL|STORE_LISTING" | tail -20

step "P3/P4 game layer + game EditMode tests (dotnet) + C# 9 syntax of every script"
if [ -n "${DOTNET:-}" ] || command -v dotnet >/dev/null 2>&1 || [ -x /Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet ] || [ -x /Volumes/BuildSSD/tmp/dotnet-sdk/dotnet ]; then
    OUT=$(sh "$HERE/game_dotnet_check.sh" 2>&1) || FAIL=1
    echo "$OUT" | tail -2
    echo "$OUT" | grep -q "Passed!  - Failed:     0" || FAIL=1
    sh "$HERE/cs_parse_check.sh" | tail -1 || FAIL=1
else
    echo "SKIP: no .NET SDK — the Unity EditMode run below covers the same tests"
fi

if [ -n "${UNITY_EDITOR:-}" ] && [ -x "$UNITY_EDITOR" ]; then
    step "Unity EditMode tests"
    mkdir -p "$UNITY/Build"
    # alarm: an editor that hangs on import must not block the agent forever (fleet rule for long runs)
    ( "$UNITY_EDITOR" -batchmode -nographics -projectPath "$UNITY" -runTests -testPlatform EditMode \
        -testResults "$UNITY/Build/editmode.xml" -logFile "$UNITY/Build/editmode.log" ) &
    PID=$!
    ( sleep "${UNITY_TEST_TIMEOUT_S:-1800}" && kill "$PID" 2>/dev/null ) &
    WATCH=$!
    if wait "$PID"; then echo "EDITMODE: pass ($UNITY/Build/editmode.xml)"; else echo "EDITMODE: FAIL (see Build/editmode.log)"; FAIL=1; fi
    kill "$WATCH" 2>/dev/null || true
else
    step "Unity EditMode tests"
    echo "SKIP: UNITY_EDITOR not set / no editor installed yet — UNCOMPILED: CoddyKit.Runtime, bridges, editor scripts (incl. P4 VoltAudioImport), VoltCity.App, VoltCity.World, VoltCity.UI (incl. P4 VoltAudio / shop / settings wiring), PlayMode smoke"
fi

[ "$FAIL" -eq 0 ] && echo "\nVOLT_UNITY_CHECK: PASS" || echo "\nVOLT_UNITY_CHECK: FAIL"
exit "$FAIL"
