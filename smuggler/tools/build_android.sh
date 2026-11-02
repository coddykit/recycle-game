#!/bin/sh
# Borderline Empire — Android release bundle (adapted from corrupt-tycoon/tools/build_android.sh + airport's mini_build).
# Gradle build through Godot, signed with the upload key in ~/.android-keys/borderlineempire (never in the repo).
# Usage: sh tools/build_android.sh          → build/android/borderlineempire.aab (signed release, real AdMob units)
#        sh tools/build_android.sh --apk    → build/android/borderlineempire-debug.apk (debug: Google test ad units)
# Upload: Jenkins Games/smuggler-mobile (games/jenkins/run.sh → play_upload.mjs). This script never uploads.
# Never run another Godot process on this project at the same time (the export re-imports).
set -e
cd "$(dirname "$0")/.."
ROOT=$(pwd)
GODOT=${GODOT:-godot}
KEYDIR="$HOME/.android-keys/borderlineempire"
VERSION=$(sed -n 's/^config\/version="\(.*\)"/\1/p' project.godot)
VERSION_CODE=${VERSION_CODE:-$(date +%s)}
export VERSION_CODE
case "$VERSION_CODE" in *[!0-9]*|'') echo "Invalid version code"; exit 1;; esac
[ "$VERSION_CODE" -gt 1791611984 ] || { echo "Refusing the old Play draft version code"; exit 1; }
mkdir -p build/android

MODE=aab
[ "$1" = "--apk" ] && MODE=apk
if [ "$MODE" = aab ]; then
  [ -f "$KEYDIR/keystore.properties" ] && [ -f "$KEYDIR/upload.jks" ] || {
    echo "Android upload key missing: expected $KEYDIR/upload.jks and keystore.properties (never created by this script)."
    echo "Use --apk for a debug build."
    exit 1; }
  export GODOT_ANDROID_KEYSTORE_RELEASE_PATH="$KEYDIR/upload.jks"
  export GODOT_ANDROID_KEYSTORE_RELEASE_USER=$(sed -n 's/^keyAlias=//p' "$KEYDIR/keystore.properties")
  export GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD=$(sed -n 's/^storePassword=//p' "$KEYDIR/keystore.properties")
  # Fail closed on the store configuration a release cannot ship without (RevenueCat keys, real AdMob app + rewarded
  # ids). tools/check_release.py's evidence items (sandbox run, privacy declarations) are release gates, not build gates.
  python3 tools/check_release.py --platform android --configuration-only
fi

# Gradle build template (git-ignored, ~200 MB): (re)install when missing or built by another engine version.
GV=$("$GODOT" --version | sed 's/\.official.*//')
TEMPLATE_FLAG=""
if [ ! -f android/build/build.gradle ] || [ "$(cat android/.build_version 2>/dev/null)" != "$GV" ]; then
  rm -rf android/build android/.build_version
  TEMPLATE_FLAG="--install-android-build-template"
fi

# Stamp the version into the preset for this export only; plugins may rewrite project.godot during an export —
# restore both afterwards.
cp export_presets.cfg build/android/export_presets.cfg.bak
cp project.godot build/android/project.godot.bak
trap 'cp "$ROOT/build/android/export_presets.cfg.bak" "$ROOT/export_presets.cfg"; cp "$ROOT/build/android/project.godot.bak" "$ROOT/project.godot"' EXIT
# Godot 4.7 games crash at launch on many Android phones with Vulkan ("Vulkan device was lost", PowerVR pipeline
# creation). Export Android with gl_compatibility (iOS stays Metal); the trap above restores project.godot afterwards.
# Godot's import can drop the .mobile line, so remove it and re-add it under [rendering], then verify.
awk '/^renderer\/rendering_method\.mobile=/ { next } { print } /^\[rendering\]$/ { print "renderer/rendering_method.mobile=\"gl_compatibility\"" }' \
  project.godot > build/android/project.godot.tmp && mv build/android/project.godot.tmp project.godot
grep -q '^renderer/rendering_method.mobile="gl_compatibility"' project.godot || { echo "could not switch Android to gl_compatibility"; exit 1; }
sed -i.bak -e "s/^version\/code=.*/version\/code=$VERSION_CODE/" -e "s/^version\/name=.*/version\/name=\"$VERSION\"/" export_presets.cfg
grep -q '^gradle_build/target_sdk="3[5-9]"' export_presets.cfg || { echo "Android preset target_sdk must be >= 35"; exit 1; }
if [ "$MODE" = apk ]; then
  OUTFILE=build/android/borderlineempire-debug.apk
  EXPORT=--export-debug
  sed -i.bak -e 's/^gradle_build\/export_format=.*/gradle_build\/export_format=0/' -e "s#^export_path=\"build/android/borderlineempire.aab\"#export_path=\"$OUTFILE\"#" export_presets.cfg
else
  OUTFILE=build/android/borderlineempire.aab
  EXPORT=--export-release
fi

echo "[1/2] Godot + Gradle → $MODE $VERSION ($VERSION_CODE) $TEMPLATE_FLAG"
rm -f "$OUTFILE"
"$GODOT" --headless --path . $TEMPLATE_FLAG $EXPORT "Android" "$ROOT/$OUTFILE" > build/android/export.log 2>&1 || true
if [ ! -f "$OUTFILE" ] && [ -n "$TEMPLATE_FLAG" ] && [ -f android/build/build.gradle ]; then
  echo "  template installed on the first pass, exporting again"
  "$GODOT" --headless --path . $EXPORT "Android" "$ROOT/$OUTFILE" > build/android/export.log 2>&1 || true
fi
[ -f "$OUTFILE" ] || { grep -iE "error|fail|what went wrong" build/android/export.log | tail -25; echo "Android export failed — build/android/export.log"; exit 1; }

if [ "$MODE" = aab ]; then
  # Signed with OUR upload key? (certificate SHA-256 fingerprints are public)
  KT=keytool
  [ -n "$JAVA_HOME" ] && [ -x "$JAVA_HOME/bin/keytool" ] && KT="$JAVA_HOME/bin/keytool"
  KFP=$("$KT" -list -v -keystore "$GODOT_ANDROID_KEYSTORE_RELEASE_PATH" -storepass:env GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD 2>/dev/null | sed -n 's/^[[:space:]]*SHA256: //p' | head -1)
  AFP=$("$KT" -printcert -jarfile "$OUTFILE" 2>/dev/null | sed -n 's/^[[:space:]]*SHA256: //p' | head -1)
  unset GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD
  [ -n "$KFP" ] && [ "$KFP" = "$AFP" ] || { echo "AAB is not signed with the borderlineempire upload key (aab: ${AFP:-unsigned})"; exit 1; }
  # Real ads in release: the AdMob app id must be in the bundle's manifest (Google's sample id = test ads only).
  if command -v unzip >/dev/null && unzip -p "$OUTFILE" base/manifest/AndroidManifest.xml 2>/dev/null | strings | grep -q "3940256099942544"; then
    echo "AAB manifest carries Google's sample AdMob app id"; exit 1
  fi
fi
# W63 item 12: native debug symbols next to the release AAB (<aab>.symbols.zip), attached in the same Play edit
[ "$EXPORT" = --export-release ] && sh "$ROOT/../jenkins/release_symbols.sh" "$ROOT/$OUTFILE"
echo "$VERSION_CODE" > build/android/version_code.txt
if [ "$MODE" = aab ]; then
  python3 - <<'PYMANIFEST'
import hashlib, json, os, re
from pathlib import Path
p=Path('build/android/borderlineempire.aab')
project=Path('project.godot').read_text()
presets=Path('export_presets.cfg').read_text()
p.with_suffix('.release.json').write_text(json.dumps({
 'package':re.search(r'package/unique_name="([^"]+)"',presets).group(1),
 'version_name':re.search(r'config/version="([^"]+)"',project).group(1),
 'version_code':int(os.environ['VERSION_CODE']),
 'sha256':hashlib.sha256(p.read_bytes()).hexdigest()
},indent=2)+'\n')
PYMANIFEST
fi
echo "[2/2] done: $OUTFILE ($(du -h "$OUTFILE" | cut -f1)) · version $VERSION code $VERSION_CODE"
