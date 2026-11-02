#!/bin/bash
# Captures the raw store screenshots (EN + TR) with the QA args in scripts/core/debug_tools.gd,
# then composes captions, icons and the feature graphic.
# Usage: GODOT=/path/to/godot tools/store/shoot_screens.sh [raw_dir]
set -e
G=${GODOT:-godot}
OUT=${1:-/tmp/store_raw}
mkdir -p "$OUT"
cd "$(dirname "$0")/../.."
shot() { n=$1; l=$2; r=$3; shift 3
  timeout 200 xvfb-run -a -s "-screen 0 ${r}x24" "$G" --rendering-driver opengl3 --resolution $r -- --qa --shot="$OUT/${n}_${l}.png" --delay=2.5 --notitle --fresh --lang=$l --quality=high "$@" > "$OUT/${n}_${l}.log" 2>&1
}
for l in en tr; do
  shot 1_mentor $l 1080x1920 --intro --gating --taps=wait3 --wait=0.1
  shot 2_empire $l 1080x1920 --notut --unlock=3 --garage=4 --buy=small_van,cargo_van,pickup,box_truck,minibus --auto=3 --sim=40 --nomodal --focus=hq --zoom=30 --setcash=48250 --goal=10 --wait=2.5
  wait
  shot 3_contracts $l 1080x1920 --notut --unlock=3 --garage=3 --biz=contract_office:2 --rep=40 --sim=70 --nomodal --setcash=12840 --goal=9 --panel=contracts --wait=1.5
  shot 4_dispatch $l 1080x1920 --notut --unlock=3 --rep=40 --sim=70 --nomodal --setcash=12840 --goal=9 --panel=dispatch --wait=2
  wait
  shot 5_event $l 1080x1920 --notut --garage=2 --buy=small_van --autoplay=dispatch,event --event_id=rival --setcash=3420 --goal=5 --nomodal --wait=1.5
  shot 6_map $l 1080x1920 --notut --unlock=5 --garage=3 --buy=small_van,pickup,cargo_van --auto=3 --sim=40 --nomodal --focus=2 --zoom=115 --setcash=186500 --goal=15 --wait=3
  wait
done
shot feature en 1024x500 --notut --unlock=3 --garage=3 --buy=small_van,pickup --auto=3 --sim=30 --nomodal --nohud=all --focus=hq --zoom=42 --pan=560x0 --wait=2.5
python3 tools/store/make_store_art.py "$OUT"
