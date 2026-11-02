#!/bin/sh
# Volt City P2 models (Unity target): one Blender process under the shared GPU lock (alarm 900) ->
# unity/Assets/Art/Models/{world,plants,grid,props}/<id>.{fbx,glb,json} + layout.json + manifest.json.
# Unity imports them through Assets/Art/Editor/VcArtImport.cs (import settings + axis check against the sidecars).
# Deterministic: two runs give the same manifest sha256 (build.sh twice; validate_art.py checks every file hash).
set -eu
P=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$P"
BLENDER=${BLENDER:-/Volumes/BuildSSD/blender/blender}
SHOT_LOCK=${SHOT_LOCK:-/Volumes/BuildSSD/novareka-dev/shot-lock.sh}
"$SHOT_LOCK" perl -e 'alarm 900; exec @ARGV' "$BLENDER" -b --factory-startup -t 4 -P blender/build_art.py
