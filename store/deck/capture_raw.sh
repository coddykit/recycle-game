#!/bin/bash
# capture_raw.sh <device iphone|ipad|playphone|playtab> <langs...>  -> $S/work/raw_<device>/<lang>/<id>.png (upscaled to store size)
# playphone 1080x2340 / playtab 1200x1920 = native frames for the coddy circuit Play set (tools/store/render.mjs, README there);
# each frame also writes <id>.state.json (debug_tools.gd --store_state: banner held off, no modal/toast, store on, nothing owned).
# Run it inside the fleet banner wrapper + the shared shot lock, one Godot at a time, e.g.
#   WORK=<scratch> GODOT=<4.7.2> /Volumes/BuildSSD/novareka-dev/shot-lock.sh python3 ../tools/store/capture.py --game . -- \
#     bash store/deck/capture_raw.sh playphone en tr de es fr ja ko pt ru zh
S=${WORK:?set WORK=<scratch dir>}   # raw frames land in $WORK/work/raw_<device>/<lang>/
cd "$(dirname "$0")/../.."          # the game root (repo-relative)
DEV=$1; shift
case $DEV in iphone) R=1157x2514; T=1320x2868;; ipad) R=1886x2514; T=2064x2752;; playphone) R=1080x2340; T=1080x2340;; playtab) R=1200x1920; T=1200x1920;;
  *) echo "unknown device $DEV"; exit 2;; esac
# a run that hangs fails after N seconds: coreutils timeout (Linux), gtimeout (Homebrew coreutils) or a perl alarm (macOS
# has neither by default — the 10-26 re-shoot stopped on "timeout: command not found")
run_limited() { local s=$1; shift
  if command -v timeout >/dev/null 2>&1; then timeout "$s" "$@"
  elif command -v gtimeout >/dev/null 2>&1; then gtimeout "$s" "$@"
  else perl -e 'my $s=shift; my $p=fork(); if(!$p){exec @ARGV; exit 127} $SIG{ALRM}=sub{kill "TERM",$p; sleep 2; kill "KILL",$p; exit 124}; alarm $s; waitpid($p,0); alarm 0; exit($?>>8)' "$s" "$@"; fi; }
# Clean store frames (10-27 circuit re-shoot): every Play shot runs with --paywall (desktop otherwise owns every product,
# and the paid Crisis Manager auto-resolves the event) and --nomodal, which also hides the kit toast (hud.ui_toast), again
# right before the frame (debug_tools.gd); each shot's --goal is one its state has NOT met yet, so no "Goal complete"
# toast/bar fires; --clean_late resolves a road event that fired while the shot waited (not on 03, the event shot). Mid/late-game states only: 02 owns a few vans so the dispatch sheet offers several vehicles;
# 07_contracts = a mid-game Contracts list (--autoplay=dispatch delivers the TUTORIAL-tagged first contract);
# 08_frost = a late-game empire, all 8 regions (stage 9/10), framed on snowy Frostland. 07_mentor (onboarding intro, early game) is kept only
# for the old rays deck.
SHOTS=${SHOTS:-01_empire 02_dispatch 03_event 04_fleet 05_upgrades 06_map 07_contracts 08_frost}
P="--paywall --store_state --notut --nomodal --clean_late"
args() { case $1 in
 01_empire) echo "$P --unlock=4 --garage=4 --buy=small_van,cargo_van,pickup,box_truck,minibus --auto=3 --sim=40 --focus=hq --zoom=30 --setcash=64820 --goal=19 --wait=2.5";;
 02_dispatch) echo "$P --unlock=3 --garage=3 --buy=small_van,pickup --rep=40 --sim=905 --setcash=12840 --goal=12 --panel=dispatch --wait=2.5";;
 03_event) echo "${P% --clean_late} --unlock=3 --garage=3 --buy=small_van,pickup --autoplay=dispatch,event --event_id=rival --setcash=9420 --goal=12 --wait=2.5";;
 04_fleet) echo "$P --unlock=4 --garage=4 --buy=small_van,cargo_van,pickup,box_truck --auto=3 --sim=60 --setcash=48250 --goal=19 --panel=fleet --wait=2.5";;
 05_upgrades) echo "$P --unlock=4 --garage=4 --buy=small_van,cargo_van,pickup,box_truck --auto=3 --sim=60 --setcash=486000 --goal=21 --panel=upgrades --wait=2.5";;
 06_map) echo "$P --unlock=5 --garage=3 --buy=small_van,pickup,cargo_van --auto=3 --sim=40 --focus=2 --zoom=115 --setcash=186500 --goal=20 --wait=3";;
 07_contracts) echo "$P --unlock=3 --garage=3 --buy=small_van,pickup --autoplay=dispatch --biz=contract_office:2 --rep=40 --sim=70 --setcash=12840 --goal=12 --panel=contracts --wait=2.5";;
 08_frost) echo "$P --unlock=8 --garage=5 --buy=small_van,cargo_van,pickup,box_truck,minibus,heavy_truck --auto=3 --sim=60 --focus=5 --zoom=50 --stage=8 --earned=9400000 --setcash=2860400 --goal=19 --wait=3";;
 07_mentor) echo "--intro --gating --taps=wait3 --wait=0.1";;
esac; }
for L in "$@"; do for id in $SHOTS; do
  O=$S/work/raw_$DEV/$L; mkdir -p $O; rm -f $O/$id.png
  run_limited 200 "${GODOT:-godot}" --audio-driver Dummy --path . --resolution $R -- --qa --shot=$O/_$id.png --delay=2.5 --notitle --fresh --lang=$L --quality=high --winsize=$R $(args $id) > $O/$id.log 2>&1
  grep -m1 "^STORE_STATE " $O/$id.log | cut -d" " -f2- > $O/$id.state.json
  if grep -q "SCRIPT ERROR" $O/$id.log || [ ! -f $O/_$id.png ]; then echo "FAIL $DEV $L $id"; continue; fi
  python3 -c "
import sys;from PIL import Image
im=Image.open(sys.argv[1]).convert('RGB'); w,h=map(int,sys.argv[3].split('x'))
assert abs(im.size[0]/im.size[1]-w/h)<0.01 and im.size[1]>=1920, im.size
im.resize((w,h),Image.LANCZOS).save(sys.argv[2])" $O/_$id.png $O/$id.png $T $R && rm $O/_$id.png && echo "ok $DEV $L $id"
done; done
