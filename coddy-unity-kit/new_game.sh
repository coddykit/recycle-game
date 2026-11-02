#!/bin/bash
# New Unity game skeleton (owner 2026-11-02: new games are Unity 6 LTS + URP, built one by one; U0 of the Unity factory).
#
#   bash games/tools/unity/new_game.sh <slug> <package> <Namespace> <portrait|landscape>
#   e.g. bash games/tools/unity/new_game.sh checkout-crunch com.coddykit.checkoutcrunch CheckoutCrunch portrait
#
# Optional env: TITLE="Checkout Crunch" (product name until P6; default = slug in title case), BG=0f1a24 (splash /
# palette base colour, 6 hex; P2 changes it).
# Writes games/<slug>/{unity,jenkins} from games/tools/unity/template (placeholders @SLUG@ @PACKAGE@ @NS@ @PREFIX@ @TITLE@
# @ORIENT@ @LANDSCAPE@ @BG@ @BG_RGB@ @SLUGNODASH@), copies the kit (sync.sh) and generates the game's CoddyTokens.uss.
# Then (see games/NEW_GAME_PLAYBOOK.md "Unity path"): open once in batchmode (ProjectSetup.Apply), EditMode, check.sh,
# dev AAB, and commit everything the editor generated (ProjectSettings + every .meta) in ONE commit.
# Refuses to touch an existing games/<slug>/unity.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
GAMES="$(dirname "$(dirname "$HERE")")"
[ "$#" -eq 4 ] || { sed -n '2,12p' "$0"; exit 2; }
SLUG=$1; PACKAGE=$2; NS=$3; ORIENT=$4
[[ "$SLUG" =~ ^[a-z][a-z0-9-]+$ ]] || { echo "slug must be lowercase-dashed: $SLUG"; exit 2; }
[[ "$PACKAGE" =~ ^com\.coddykit\.[a-z][a-z0-9]+$ ]] || { echo "package must be com.coddykit.<lowercase>: $PACKAGE"; exit 2; }
[[ "$NS" =~ ^[A-Z][A-Za-z0-9]+$ ]] || { echo "Namespace must be PascalCase: $NS"; exit 2; }
case "$ORIENT" in portrait) LANDSCAPE=false ;; landscape) LANDSCAPE=true ;; *) echo "orientation: portrait|landscape"; exit 2 ;; esac
BG=${BG:-0f1a24}
[[ "$BG" =~ ^[0-9a-fA-F]{6}$ ]] || { echo "BG must be 6 hex digits"; exit 2; }
BG=$(echo "$BG" | tr 'A-F' 'a-f')
TITLE=${TITLE:-$(echo "$SLUG" | awk -F- '{for(i=1;i<=NF;i++) $i=toupper(substr($i,1,1)) substr($i,2)} 1' OFS=' ')}
PREFIX=$(echo "$NS" | tr '[:lower:]' '[:upper:]')
SLUGNODASH=${SLUG//-/}
BG_RGB=$(printf '%d / 255f, %d / 255f, %d / 255f' 0x${BG:0:2} 0x${BG:2:2} 0x${BG:4:2})
GAME="$GAMES/$SLUG"
[ ! -e "$GAME/unity" ] || { echo "REFUSED: $GAME/unity exists (the generator never overwrites a game)"; exit 1; }
[ ! -e "$GAME/jenkins" ] || { echo "REFUSED: $GAME/jenkins exists"; exit 1; }

export SLUG PACKAGE NS PREFIX TITLE ORIENT LANDSCAPE BG BG_RGB SLUGNODASH
python3 - "$HERE/template" "$GAME" <<'PY'
import os, sys
from pathlib import Path
src, dst = Path(sys.argv[1]), Path(sys.argv[2])
subs = {f"@{k}@": os.environ[k] for k in ("SLUG", "PACKAGE", "NS", "PREFIX", "TITLE", "ORIENT", "LANDSCAPE", "BG", "BG_RGB", "SLUGNODASH")}
def sub(t):
    for k, v in subs.items():
        t = t.replace(k, v)
    return t
n = 0
for p in sorted(src.rglob("*")):
    if p.is_dir() or p.name == ".DS_Store":
        continue
    out = dst / sub(p.relative_to(src).as_posix())
    out.parent.mkdir(parents=True, exist_ok=True)
    data = p.read_bytes()
    try:
        text = data.decode("utf-8")
        out.write_text(sub(text), encoding="utf-8")
        left = [k for k in ("@SLUG@", "@NS@", "@PREFIX@", "@TITLE@") if k in out.read_text(encoding="utf-8")]
        assert not left, (out, left)
    except UnicodeDecodeError:
        out.write_bytes(data)
    if os.access(p, os.X_OK) or out.suffix == ".sh":
        out.chmod(0o755)
    n += 1
print(f"template: {n} files -> {dst}")
PY
bash "$HERE/sync.sh" "$GAME" | tail -1
python3 "$GAME/unity/Tools/gen_uss_tokens.py"
if ! grep -q "\"$SLUG\"" "$HERE/CoddyKit/Core/Ads/HouseRelatedTable.g.cs"; then
  echo "NOTE: $SLUG has no house-ads row yet: add a PENDING entry to games/tools/unity/gen_house_related.py (or the fleet"
  echo "      games/tools/ads/house_related.py), regenerate, then bash games/tools/unity/sync.sh <every Unity game>"
fi
cat <<MSG
NEW_GAME_OK $SLUG ($PACKAGE, $NS, $ORIENT)
next (games/NEW_GAME_PLAYBOOK.md "Unity path"):
  U=/Volumes/BuildSSD/unity/editors/6000.3.27f1/Unity.app/Contents/MacOS/Unity
  \$U -batchmode -nographics -quit -projectPath games/$SLUG/unity -buildTarget Android -executeMethod $NS.EditorTools.${NS}ProjectSetup.Apply -logFile -
  \$U -batchmode -nographics -projectPath games/$SLUG/unity -runTests -testPlatform EditMode -testResults /tmp/em.xml -logFile -
  UNITY_EDITOR=\$U sh games/$SLUG/unity/Tools/check.sh
  sh games/$SLUG/jenkins/build_unity_android.sh build      # dev AAB
  then ONE commit: ProjectSettings/*, Packages/packages-lock.json, Assets/Settings/*, Assets/Scenes/Boot.unity, every .meta
MSG
