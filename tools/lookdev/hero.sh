#!/bin/bash
# W67 look-dev hero frames (Compatibility renderer, windowed; run under novareka-dev/shot-lock.sh, one Godot at a time).
# Usage: tools/lookdev/hero.sh <out_dir> [game_dir] — Low + Medium, 1080x2340, with the game's own QA args
# (scripts/core/debug_tools.gd; isolated QA save, HUD hidden). LOOKDEV_SHOTS="start empire" limits the shots.
set -u
OUT=${1:?out dir}
GAME=${2:-$(cd "$(dirname "$0")/../.." && pwd)}
G=${GODOT:-/Volumes/BuildSSD/godot/4.7.2/Godot.app/Contents/MacOS/Godot}
mkdir -p "$OUT"
FLEET="--notut --unlock=8 --garage=4 --buy=small_van,cargo_van,pickup,box_truck,minibus --auto=3 --sim=60 --nomodal --nohud=all"
args() { case $1 in
  start)  echo "--notut --nomodal --nohud=all --wait=2.5" ;;
  empire) echo "--notut --unlock=3 --garage=4 --buy=small_van,cargo_van,pickup,box_truck,minibus --auto=3 --sim=40 --nomodal --nohud=all --focus=hq --zoom=30 --wait=2.5" ;;
  map)    echo "--notut --unlock=5 --garage=3 --buy=small_van,pickup,cargo_van --auto=3 --sim=40 --nomodal --nohud=all --focus=2 --zoom=115 --wait=3" ;;
  coast)  echo "$FLEET --focus=2 --zoom=45 --wait=3" ;;
  frost)  echo "$FLEET --focus=5 --zoom=50 --wait=3" ;;
  neon)   echo "$FLEET --focus=7 --zoom=50 --wait=3" ;;
esac; }
for q in low medium; do
  mkdir -p "$OUT/$q"
  for s in ${LOOKDEV_SHOTS:-start empire map coast frost neon}; do
    timeout 240 "$G" --path "$GAME" --rendering-method gl_compatibility --audio-driver Dummy --resolution 1080x2340 -- \
      --qa --fresh --winsize=1080x2340 --lang=en --quality=$q --shot="$OUT/$q/$s.png" --delay=2.5 $(args $s) > "$OUT/$q/$s.log" 2>&1
    grep -qE "SHADER ERROR|SCRIPT ERROR" "$OUT/$q/$s.log" && { echo "LOOKDEV_FAIL $q/$s"; grep -m3 -E "SHADER ERROR|SCRIPT ERROR" -A2 "$OUT/$q/$s.log"; exit 1; }
    [ -f "$OUT/$q/$s.png" ] && echo "LOOKDEV_SHOT $q/$s" || echo "LOOKDEV_MISSING $q/$s"
  done
done
