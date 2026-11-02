#!/bin/sh
# Volt City P2 look-dev frames (Blender Eevee until the Unity project exists): GPU work under the shared lock, alarm 900
# per Blender run. Output: $OUT/{low,medium,before}/{dusk,night,storm}.png (1080 x 2340), then sheets + pixel check:
#   sh blender/frames.sh [out dir]      (default /Volumes/BuildSSD/game-builds/volt-city/p2)
set -eu
P=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$P"
OUT=${1:-/Volumes/BuildSSD/game-builds/volt-city/p2}
BLENDER=${BLENDER:-/Volumes/BuildSSD/blender/blender}
SHOT_LOCK=${SHOT_LOCK:-/Volumes/BuildSSD/novareka-dev/shot-lock.sh}
for M in dusk night storm ripple; do
  "$SHOT_LOCK" perl -e 'alarm 900; exec @ARGV' "$BLENDER" -b --factory-startup -P blender/render_frames.py -- "$OUT" "$M" low,medium,before
done
python3 blender/sheets.py "$OUT"
