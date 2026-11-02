#!/bin/sh
# Borderline Empire — iOS release build (adapted from corrupt-tycoon/tools/build_ios.sh).
# Godot exports an Xcode project (ios/plugins: RevenueCat + Firebase frameworks, not in git — tools/native/setup_ios.sh
# or the Mini's /Volumes/BuildSSD/game-deps/BorderlineEmpire; AdMob comes in via Swift Package Manager), xcodebuild
# archives it with automatic signing (App Store Connect API key from the CoddyApps store-automation .env) and exports
# or uploads the IPA. Needs an UNLOCKED login keychain (the Jenkins job unlocks it).
# Usage: sh tools/build_ios.sh              build + archive + export an IPA locally (no upload)
#        sh tools/build_ios.sh --upload     build + archive + send to App Store Connect / TestFlight
#        sh tools/build_ios.sh --reupload   send the last archive again (e.g. after a network error)
# Never run another Godot process on this project at the same time (the export re-imports).
set -e
cd "$(dirname "$0")/.."
ROOT=$(pwd)
GODOT=${GODOT:-godot}
NAME=BorderlineEmpire
TEAM_ID=68563ZRFVT
ENV_FILE=${ASC_ENV_FILE:-/Users/mehmetcanker/Documents/projects/CoddyApps/store-automation/.env} # Jenkins (games/jenkins/run.sh) passes the Mini's
ASC_KEY_ID=$(grep -E '^ASC_API_KEY_ID=' "$ENV_FILE" 2>/dev/null | head -1 | cut -d= -f2- | tr -d '"'"'"' ')
ASC_ISSUER_ID=$(grep -E '^ASC_API_ISSUER_ID=' "$ENV_FILE" 2>/dev/null | head -1 | cut -d= -f2- | tr -d '"'"'"' ')
ASC_KEY_PATH="$HOME/.appstoreconnect/private_keys/AuthKey_${ASC_KEY_ID}.p8"
[ -n "$ASC_KEY_ID" ] && [ -n "$ASC_ISSUER_ID" ] && [ -f "$ASC_KEY_PATH" ] || { echo "App Store Connect API key not found (store-automation .env + ~/.appstoreconnect/private_keys)"; exit 1; }
VERSION=$(sed -n 's/^config\/version="\(.*\)"/\1/p' project.godot)
XC="$ROOT/build/ios/xcode"
OUT="$ROOT/build/ios/release"

if [ "$1" = "--reupload" ]; then
  [ -d "$OUT/$NAME.xcarchive" ] || { echo "no archive in $OUT to upload"; exit 1; }
  BUILD_NUMBER=$(/usr/libexec/PlistBuddy -c "Print :ApplicationProperties:CFBundleVersion" "$OUT/$NAME.xcarchive/Info.plist")
  echo "[1-2/4] reusing archive $VERSION ($BUILD_NUMBER)"
  set -- --upload
else
  for p in revenue_cat/revenue_cat.gdip firebase_core/firebase_core.gdip firebase_analytics/firebase_analytics.gdip; do
    [ -f "ios/plugins/$p" ] || { echo "ios/plugins/$p missing — run tools/native/setup_ios.sh (Mini: game-deps/$NAME)"; exit 1; }
  done
  BLOCKERS=$(python3 tools/check_release.py 2>&1 | grep -E "^BLOCKED: .*(RevenueCat|AdMob|rewarded|signing team)" || true)
  [ -z "$BLOCKERS" ] || { echo "$BLOCKERS"; echo "release configuration incomplete (tools/check_release.py)"; exit 1; }
  grep -q "^application/app_store_team_id=\"$TEAM_ID\"" export_presets.cfg || { echo "iOS preset team id is not $TEAM_ID"; exit 1; }

  BUILD_NUMBER=$(date +%s)
  rm -rf "$XC" "$OUT" && mkdir -p "$XC" "$OUT"

  # Export plugins may rewrite project.godot / the preset during an export: restore both afterwards.
  cp project.godot "$OUT/project.godot.bak"
  cp export_presets.cfg "$OUT/export_presets.cfg.bak"
  trap 'cp "$OUT/project.godot.bak" "$ROOT/project.godot"; cp "$OUT/export_presets.cfg.bak" "$ROOT/export_presets.cfg"' EXIT

  echo "[1/4] Godot → Xcode project ($VERSION)"
  "$GODOT" --headless --path . --export-release "iOS" "$XC/$NAME.ipa" > "$OUT/godot-export.log" 2>&1 || true
  [ -d "$XC/$NAME.xcodeproj" ] || { tail -30 "$OUT/godot-export.log"; echo "Godot export failed"; exit 1; }
  # Real ads in release: the exported Info.plist must carry our AdMob app id, not Google's sample.
  PL="$XC/$NAME/$NAME-Info.plist"
  APPID=$(sed -n 's/^general\/ios\/app_id="\(.*\)"/\1/p' project.godot)
  [ -f "$PL" ] && grep -q "$APPID" "$PL" || { echo "exported Info.plist lacks GADApplicationIdentifier $APPID"; exit 1; }

  echo "[2/4] archive $VERSION ($BUILD_NUMBER)"
  xcodebuild -project "$XC/$NAME.xcodeproj" -scheme "$NAME" -configuration Release \
    -derivedDataPath "$ROOT/build/ios/dd" -archivePath "$OUT/$NAME.xcarchive" \
    -destination "generic/platform=iOS" archive \
    -allowProvisioningUpdates -authenticationKeyPath "$ASC_KEY_PATH" \
    -authenticationKeyID "$ASC_KEY_ID" -authenticationKeyIssuerID "$ASC_ISSUER_ID" \
    CODE_SIGN_STYLE=Automatic DEVELOPMENT_TEAM="$TEAM_ID" CODE_SIGN_IDENTITY="Apple Development" \
    PROVISIONING_PROFILE_SPECIFIER="" \
    MARKETING_VERSION="$VERSION" CURRENT_PROJECT_VERSION="$BUILD_NUMBER" -quiet > "$OUT/archive.log" 2>&1 || { grep -E "error:" "$OUT/archive.log" | head -20; echo "archive failed — $OUT/archive.log"; exit 1; }
  PV=$(/usr/libexec/PlistBuddy -c "Print :ApplicationProperties:CFBundleVersion" "$OUT/$NAME.xcarchive/Info.plist")
  [ "$PV" = "$BUILD_NUMBER" ] || { echo "archive CFBundleVersion is $PV, expected $BUILD_NUMBER"; exit 1; }
fi

DEST=export
[ "$1" = "--upload" ] && DEST=upload
cat > "$OUT/ExportOptions.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>method</key><string>app-store-connect</string>
  <key>destination</key><string>$DEST</string>
  <key>teamID</key><string>$TEAM_ID</string>
  <key>signingStyle</key><string>automatic</string>
  <key>uploadSymbols</key><true/>
  <key>manageAppVersionAndBuildNumber</key><false/>
</dict></plist>
PLIST

echo "[3/4] export ($DEST)"
ok=0
for try in 1 2 3; do
  rm -rf "$OUT/export"
  xcodebuild -exportArchive -archivePath "$OUT/$NAME.xcarchive" -exportPath "$OUT/export" \
    -exportOptionsPlist "$OUT/ExportOptions.plist" -allowProvisioningUpdates \
    -authenticationKeyPath "$ASC_KEY_PATH" -authenticationKeyID "$ASC_KEY_ID" -authenticationKeyIssuerID "$ASC_ISSUER_ID" > "$OUT/export.log" 2>&1 || true
  grep -E "error|EXPORT|Upload (succeeded|failed)" "$OUT/export.log" || true
  if grep -q "EXPORT SUCCEEDED" "$OUT/export.log"; then ok=1; break; fi
  [ "$try" -lt 3 ] && { echo "  export failed, retrying in 30 s ($try/3)"; sleep 30; }
done
[ "$ok" = 1 ] || { echo "[4/4] FAILED: version $VERSION build $BUILD_NUMBER ($DEST) — see $OUT/export.log"; exit 1; }
echo "[4/4] done: version $VERSION build $BUILD_NUMBER ($DEST)"
ls "$OUT/export" 2>/dev/null || true
