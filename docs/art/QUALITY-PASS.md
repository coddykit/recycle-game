# Blender quality proposal — smuggler

Office, apartment and shop gain side/rear windows, frames, sills, plinths and facade bands. Existing front facades and palette remain.

| Model | Before tris | LOD1 | LOD0 | Materials |
|---|---:|---:|---:|---:|
| apartment | 188 | 772 | 880 | 1 |
| office | 140 | 516 | 564 | 1 |
| shop | 140 | 292 | 316 | 1 |

Canonical resources are LOD1; `assets/models/lod0` contains authored close-up LOD0 variants, not a runtime quality switch. No gameplay code changes. Bounds and ground/attachment origins are checked against the committed baseline; the maximum allowed size difference is 12%, with a 2.5cm tolerance for small details. Current material count does not increase; textures are at most 512px.

Rebuild: Blender 5.2.2, background, four threads, under the shared shot-lock. Run `tools/blender/build_quality_pass.py`. Validate with `python3 tools/blender/check_quality_pass.py` (Pillow).

Evidence (local, kept outside Git): `/Volumes/BuildSSD/game-builds/blender-pass/smuggler`. `before/models` and `after/models` contain identical-camera Low/Medium model images; `before` and `after` contain the game hook scenes. Aircraft and Ski Resort also have `before-front` / `after-front` views. `geometry.json` records baseline and both LODs, including materials, textures and bounds. `check.log` and per-game QA logs contain test results.

Validation: geometry PASS. tools/check.sh: PASS, including kit/i18n/game/quality/layout tests and windowed Low perf_gate: 3 scenes, 0 failures. Patron visual approval pending.
