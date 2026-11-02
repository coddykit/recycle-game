#!/bin/bash
# CoddyUnityKit: the ONE kit source is games/tools/unity/CoddyKit (owner 2026-11-02, U0). A game never edits its kit copy;
# fix the kit here, then sync every Unity game (LESSONS rule 0). Same pattern as games/tools/ads/sync.sh (Godot kits).
#
#   bash games/tools/unity/sync.sh <game>...           # copy the kit into <game>/unity/Assets/CoddyKit
#   bash games/tools/unity/sync.sh --check <game>...   # drift = FAIL (exit 1), nothing is written
#
# <game> = a folder name under games/ (checkout-crunch) or a path to the game folder.
# Kit-owned: every file under CoddyKit/ (code, asmdefs, USS, theme, icons, androidlib, the generated HouseRelatedTable.g.cs).
# Game-owned (never copied, never checked): UI/CoddyTokens.uss (the game's palette, Tools/gen_uss_tokens.py) and
# UI/Resources/CoddyPanelSettings.asset (+ its font text settings, written by ProjectSetup / the game's fonts step).
# .meta files: copied only when the game has none yet (a GUID never changes under existing references); --check ignores
# their content (LastDigit / Silent Evidence carry their own GUIDs) but reports a missing one.
set -eu
HERE="$(cd "$(dirname "$0")" && pwd)"
GAMES="$(dirname "$(dirname "$HERE")")"
KIT="$HERE/CoddyKit"
CHECK=0
if [ "${1:-}" = "--check" ]; then CHECK=1; shift; fi
[ "$#" -gt 0 ] || { echo "usage: sync.sh [--check] <game>..."; exit 2; }

python3 "$HERE/gen_house_related.py" --check >/dev/null || { echo "FAIL kit table stale: python3 games/tools/unity/gen_house_related.py"; exit 1; }

game_owned() {
  case "$1" in
    UI/CoddyTokens.uss|UI/CoddyTokens.uss.meta|UI/Resources/CoddyPanelSettings.asset|UI/Resources/CoddyPanelSettings.asset.meta) return 0 ;;
  esac
  return 1
}

RC=0
for g in "$@"; do
  if [ -d "$g/unity" ]; then GAME="$g"; elif [ -d "$GAMES/$g/unity" ]; then GAME="$GAMES/$g"; else echo "FAIL not a Unity game: $g"; RC=1; continue; fi
  DST="$GAME/unity/Assets/CoddyKit"
  name="$(basename "$GAME")"
  diffs=0; copied=0; metas=0; extra=0; files=0
  while IFS= read -r -d '' src; do
    rel="${src#"$KIT"/}"
    game_owned "$rel" && continue
    dst="$DST/$rel"
    case "$rel" in
      *.meta)
        if [ ! -f "$dst" ]; then
          if [ "$CHECK" = 1 ]; then echo "  missing meta: $rel"; metas=$((metas + 1)); else mkdir -p "$(dirname "$dst")"; cp "$src" "$dst"; copied=$((copied + 1)); fi
        fi ;;
      *)
        files=$((files + 1))
        if ! cmp -s "$src" "$dst"; then
          if [ "$CHECK" = 1 ]; then
            if [ -f "$dst" ]; then echo "  differs: $rel"; else echo "  missing: $rel"; fi
            diffs=$((diffs + 1))
          else
            mkdir -p "$(dirname "$dst")"; cp "$src" "$dst"; copied=$((copied + 1)); echo "  synced $rel"
          fi
        fi ;;
    esac
  done < <(find "$KIT" -type f ! -name '.DS_Store' -print0 | sort -z)
  # files in the game's CoddyKit that the kit does not have (a game-local kit edit)
  if [ -d "$DST" ]; then
    while IFS= read -r -d '' f; do
      rel="${f#"$DST"/}"
      game_owned "$rel" && continue
      [ -f "$KIT/$rel" ] && continue
      case "$rel" in *.meta) [ -e "$KIT/${rel%.meta}" ] || [ -e "$DST/${rel%.meta}" ] && continue ;; esac
      echo "  extra (not in the kit): $rel"; extra=$((extra + 1))
    done < <(find "$DST" -type f ! -name '.DS_Store' -print0 | sort -z)
  fi
  if [ "$CHECK" = 1 ]; then
    if [ $((diffs + metas + extra)) -eq 0 ]; then echo "UNITY_KIT_SYNC $name: OK ($files kit files)"
    else echo "UNITY_KIT_SYNC $name: FAIL ($diffs differ/missing, $extra extra, $metas metas missing of $files kit files)"; RC=1; fi
  else
    echo "UNITY_KIT_SYNC $name: $copied files written"
    [ "$extra" -eq 0 ] || { echo "  WARN $extra extra files in the game's CoddyKit (move them into the kit or the game's own folders)"; RC=1; }
  fi
done
exit "$RC"
