#!/bin/bash
# Play (phone 1284x2568) + tablet (2064x2752, used for 7"/10") slides in the fleet 'coddy' store style, from raw frames.
#   WORK=<scratch dir> bash store/deck/capture_raw.sh iphone en tr ...     (and: ipad ...)   -> $WORK/work/raw_<device>/<lang>/<id>.png
#   WORK=<scratch dir> bash store/deck/fleet_render.sh [langs...]                              -> $WORK/work/out/{play,ipad}/<lang>/NN-*.png
# Nothing is uploaded; play_images.mjs does that (owner decision). SKILL = the store-deck scripts dir.
set -e
WORK=${WORK:?set WORK=<scratch dir>}
GAME=$(cd "$(dirname "$0")/../.." && pwd)
SKILL=${SKILL:-$GAME/../../base/.claude/skills/store-deck/scripts}
mkdir -p "$WORK/work"
cp "$GAME/store/icons/icon_1024.png" "$WORK/work/icon.png"
sed -e "s|@GAME@|$GAME|g" -e "s|@WORK@|$WORK|g" "$GAME/store/deck/store-deck.json" > "$WORK/work/store-deck.json"
CFG=$WORK/work/store-deck.json
python3 "$SKILL/theme.py" "$CFG"
python3 - "$CFG" <<'PY'
import json, os, sys
cfg = json.load(open(sys.argv[1])); W = cfg["workDir"]
for dev in ("iphone", "ipad"):
    man = {}
    for lang in cfg["langs"]:
        rows = []
        for s in cfg["deck"]["slots"]:
            f = f"{W}/raw_{dev}/{lang}/{s['from']}.png"
            if not os.path.exists(f): f = f"{W}/raw_{dev}/en/{s['from']}.png"   # same screen in English, text stays in the language
            rows.append({"slot": s["slot"], "file": f, "dropped": not os.path.exists(f)})
        man[lang] = rows
    json.dump(man, open(f"{W}/manifest_{dev}.json", "w"), indent=1)
PY
LANGS=${*:-$(python3 -c "import json,sys;print(' '.join(json.load(open(sys.argv[1]))['langs']))" "$CFG")}
for dev in play ipad; do bash "$SKILL/render.sh" "$CFG" $dev $LANGS; done
