#!/bin/sh
# @TITLE@ (Unity) — Android build-only on the Linux agent (label linux-android). NOT WIRED until the Unity licence exists
# on the agent (games/volt-city/docs/UNITY.md §7). Never uploads anything: the AAB + symbols go to the Patron's upload step.
#
#   sh games/@SLUG@/jenkins/build_unity_android.sh [test|build]      (default: build = test + AAB)
#
# Env (Jenkins credentials -> env; NOTHING here is printed):
#   UNITY_EDITOR           path to the Linux editor; default /opt/unity/editors/<ProjectVersion>/Editor/Unity
#   licence, one of:
#     UNITY_SERIAL + UNITY_EMAIL + UNITY_PASSWORD   (Plus / Pro serial; returned in the exit trap)
#     UNITY_LICENSE_FILE (+ UNITY_EMAIL/PASSWORD)   (Personal: the Unity_lic.ulf activated once with Unity Hub)
#   @PREFIX@_RELEASE       production | internal | dev (default dev: development build, no signing needed)
#   @PREFIX@_KEYSTORE / @PREFIX@_KEYSTORE_PASS / @PREFIX@_KEY_ALIAS / @PREFIX@_KEY_PASS   (upload key; release only)
#   @PREFIX@_VERSION_NAME  default: PlayerSettings bundleVersion;  @PREFIX@_VERSION_CODE default: date +%s
#   DRY_RUN=1              print the plan, run nothing heavy
# Output: games/@SLUG@/unity/Build/android/{@SLUG@.aab, @SLUG@.symbols.zip, version_code.txt, release.json,
#         preflight.txt, editmode.xml, build.log}
set -eu
MODE=${1:-build}
HERE=$(cd "$(dirname "$0")" && pwd)
GAME=$(dirname "$HERE")
UNITY_PROJ="$GAME/unity"
OUT="$UNITY_PROJ/Build/android"
RELEASE=${@PREFIX@_RELEASE:-dev}
mkdir -p "$OUT"
# GNU timeout on Linux; gtimeout (coreutils) or none on the Mini
TIMEOUT=timeout; command -v timeout >/dev/null 2>&1 || { command -v gtimeout >/dev/null 2>&1 && TIMEOUT=gtimeout || TIMEOUT=""; }
limit() { if [ -n "$TIMEOUT" ]; then "$TIMEOUT" "$@"; else shift; "$@"; fi; }

PV="$UNITY_PROJ/ProjectSettings/ProjectVersion.txt"
if [ ! -f "$PV" ]; then
    echo "BLOCKED: $PV missing — open the project once in Unity 6 LTS on the Mini and commit ProjectSettings (games/volt-city/docs/UNITY.md §3)"
    exit 10
fi
VER=$(sed -n 's/^m_EditorVersion: //p' "$PV" | head -1)
if [ "$(uname)" = "Darwin" ]; then DEF_EDITOR=/Volumes/BuildSSD/unity/editors/$VER/Unity.app/Contents/MacOS/Unity; else DEF_EDITOR=/opt/unity/editors/$VER/Editor/Unity; fi
UNITY_EDITOR=${UNITY_EDITOR:-$DEF_EDITOR}
echo "@TITLE@ Unity build: mode=$MODE release=$RELEASE editor=$VER commit=$(git -C "$GAME" rev-parse --short HEAD 2>/dev/null || echo '?')"

if [ "${DRY_RUN:-0}" = "1" ]; then
    echo "DRY_RUN: preflight -> licence -> EditMode tests -> @NS@AndroidBuild.Run -> symbols -> release.json"
    exit 0
fi
[ -x "$UNITY_EDITOR" ] || { echo "BLOCKED: no Unity editor at $UNITY_EDITOR (install $VER + Android modules, games/volt-city/docs/UNITY.md §7)"; exit 11; }

# ---------------------------------------------------------------- licence (never echoed)
ULF_DIR="$HOME/.local/share/unity3d/Unity"
ULF_PLACED=0
cleanup() {
    if [ -n "${UNITY_SERIAL:-}" ]; then
        "$UNITY_EDITOR" -batchmode -nographics -quit -returnlicense -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
            -logFile "$OUT/return-licence.log" >/dev/null 2>&1 || true
    fi
    if [ "$ULF_PLACED" = "1" ]; then rm -f "$ULF_DIR/Unity_lic.ulf"; fi
    # the build writes signing fields into ProjectSettings: restore the committed file — on CI only (a local run must
    # never throw away the owner's editor changes)
    if [ -n "${JENKINS_HOME:-}${CI:-}" ]; then git -C "$GAME" checkout -- unity/ProjectSettings 2>/dev/null || true; fi
}
trap cleanup EXIT INT TERM
if [ -n "${UNITY_SERIAL:-}" ]; then
    "$UNITY_EDITOR" -batchmode -nographics -quit -serial "$UNITY_SERIAL" -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
        -logFile "$OUT/activate.log" >/dev/null 2>&1 || { echo "BLOCKED: serial activation failed (see activate.log, secrets are not in it)"; exit 12; }
elif [ -n "${UNITY_LICENSE_FILE:-}" ]; then
    mkdir -p "$ULF_DIR"
    cp "$UNITY_LICENSE_FILE" "$ULF_DIR/Unity_lic.ulf"
    chmod 600 "$ULF_DIR/Unity_lic.ulf"
    ULF_PLACED=1
elif [ "$(uname)" = "Darwin" ]; then
    echo "local Mac run: the editor's own activated licence (games/volt-city/docs/UNITY.md section 7a)"
else
    echo "BLOCKED: no Unity licence (UNITY_SERIAL or UNITY_LICENSE_FILE) - NOT WIRED yet"
    exit 13
fi

# ---------------------------------------------------------------- Google package tarballs (Play Review, Firebase; gitignored; P4 adds Tools/fetch_packages.sh)
if [ -f "$UNITY_PROJ/Tools/fetch_packages.sh" ]; then sh "$UNITY_PROJ/Tools/fetch_packages.sh" || { echo "BLOCKED: package tarballs (Tools/fetch_packages.sh)"; exit 14; }; fi

# ---------------------------------------------------------------- preflight + tests
if [ "$RELEASE" = "production" ] || [ "$RELEASE" = "internal" ]; then
    python3 "$HERE/preflight.py" --production
else
    python3 "$HERE/preflight.py"
fi

if [ "${SKIP_TESTS:-0}" != "1" ]; then
    set +e
    limit "${UNITY_TEST_TIMEOUT_S:-2400}" "$UNITY_EDITOR" -batchmode -nographics -projectPath "$UNITY_PROJ" -buildTarget Android \
        -runTests -testPlatform EditMode -testResults "$OUT/editmode.xml" -logFile "$OUT/editmode.log"
    RC=$?
    set -e
    [ "$RC" -eq 0 ] || { echo "TESTS FAILED (exit $RC): $OUT/editmode.xml"; exit 20; }
    echo "EditMode tests: PASS"
fi
[ "$MODE" = "test" ] && exit 0

# ---------------------------------------------------------------- build
export @PREFIX@_RELEASE="$RELEASE"
export @PREFIX@_VERSION_CODE=${@PREFIX@_VERSION_CODE:-$(date +%s)}
export @PREFIX@_OUT="$OUT"
limit "${UNITY_BUILD_TIMEOUT_S:-5400}" "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$UNITY_PROJ" -buildTarget Android \
    -executeMethod @NS@.EditorTools.@NS@AndroidBuild.Run -logFile "$OUT/build.log"
grep -q "BUILD_OK" "$OUT/build.log" || { echo "BUILD FAILED: $OUT/build.log"; exit 30; }
# app icon: the bundle's launcher icons must be OUR art, never Unity's default (owner 2026-10-29)
if [ -f "$UNITY_PROJ/Tools/icon_check.py" ]; then python3 "$UNITY_PROJ/Tools/icon_check.py" --aab "$OUT/@SLUG@.aab" || { echo "ICON CHECK FAILED (Unity default icon?)"; exit 32; }; fi

# Unity names the symbols "<product>-<version>-v<code>[-IL2CPP].symbols.zip" next to the AAB: one fixed name for play_upload.mjs
SYM=$(ls -t "$OUT"/*.symbols.zip 2>/dev/null | grep -v '^.*/@SLUG@.symbols.zip$' | head -1 || true)
[ -n "$SYM" ] && mv -f "$SYM" "$OUT/@SLUG@.symbols.zip"
[ -f "$OUT/@SLUG@.symbols.zip" ] || { echo "SYMBOLS MISSING (games/CLAUDE.md §10: same Play edit)"; exit 31; }

sha() { if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1"; else shasum -a 256 "$1"; fi | cut -d' ' -f1; }
AAB_SHA=$(sha "$OUT/@SLUG@.aab")
SYM_SHA=$(sha "$OUT/@SLUG@.symbols.zip")
cat > "$OUT/release.json" <<JSON
{"game": "@SLUG@", "engine": "unity", "unity": "$VER", "release": "$RELEASE",
 "commit": "$(git -C "$GAME" rev-parse HEAD)", "version_code": $(cat "$OUT/version_code.txt"),
 "aab": "@SLUG@.aab", "aab_sha256": "$AAB_SHA", "symbols": "@SLUG@.symbols.zip", "symbols_sha256": "$SYM_SHA"}
JSON
echo "AAB READY: $OUT/@SLUG@.aab (vc $(cat "$OUT/version_code.txt")) — build-only, no upload"
