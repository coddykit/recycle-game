#!/bin/sh
# Focused gate for tests/test_house_fonts.gd. Import first; never use the player's save.
set -eu
P=${1:?game project directory required}
GODOT=${GODOT:-/Volumes/BuildSSD/godot/4.7.2/Godot.app/Contents/MacOS/Godot}
OUT=$(mktemp -d)
if [ -f "$P/override.cfg" ]; then cp "$P/override.cfg" "$OUT/override.cfg"; fi
cleanup() {
  if [ -f "$OUT/override.cfg" ]; then cp "$OUT/override.cfg" "$P/override.cfg"; else rm -f "$P/override.cfg"; fi
  rm -rf "$OUT"
}
trap cleanup EXIT HUP INT TERM
printf '[application]\nconfig/use_custom_user_dir=true\nconfig/custom_user_dir_name="coddy-font-audit/%s"\n' "$(basename "$OUT")" > "$P/override.cfg"
if ! perl -e 'alarm 90; exec @ARGV' "$GODOT" --headless --audio-driver Dummy --path "$P" -s res://tests/test_house_fonts.gd > "$OUT/log" 2>&1; then
  cat "$OUT/log"; exit 1
fi
cat "$OUT/log"
if grep -E 'SCRIPT ERROR|Parse Error|Compile Error|^FAIL' "$OUT/log"; then exit 1; fi
grep -q 'HOUSE_FONTS: 10 locales, 0 failures' "$OUT/log"
