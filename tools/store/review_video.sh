#!/bin/bash
# App Review screen recording (ASC review attachment): launch → title → animated intro → tutorial start →
# Business → Shop (Empire Pass with the price the store returns) → Restore Purchases scrolled into view.
# Godot movie maker renders every frame of the real game (isolated QA save), ffmpeg encodes H.264.
# Usage: tools/store/review_video.sh <out.mp4>   (run alone: parallel Godot instances share the QA save dir)
set -e
cd "$(dirname "$0")/../.."
OUT=${1:-store/borderlineempire-review.mp4}
T=$(mktemp -d)
taps="wait3,at360x900,wait2.5"
for i in 1 2 3 4 5 6; do taps="$taps,intro_continue,wait1.6"; done
taps="$taps,intro_name_ok,wait4,nav_business,wait2.5,business_shop,wait3.5,show_shop_restore,wait3"
godot --path . --always-on-top --write-movie "$T/review.avi" --fixed-fps 30 --resolution 720x1280 -- \
  --qa --fresh --lang=en --intro --shot="$T/end.png" --delay=0.5 --store_price='$3.99' --taps="$taps" --wait=1.5 > "$T/log.txt" 2>&1 || true
grep -E "QA (tap|show)|SCRIPT ERROR" "$T/log.txt" || true
ffmpeg -y -loglevel error -i "$T/review.avi" -c:v libx264 -pix_fmt yuv420p -crf 23 -preset slow -movflags +faststart -an "$OUT"
ffprobe -v error -show_entries format=duration:stream=width,height -of default=nw=1 "$OUT"
rm -rf "$T"
