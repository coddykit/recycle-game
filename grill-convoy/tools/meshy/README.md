# games/tools/meshy — Meshy → Blender → Godot asset pipeline

Owner decision 2026-10-23: **Meshy** makes characters, creatures and organic hero props; **Blender** stays for
buildings and modular parts. Plan: 1100 credits / month. Every paid task is logged in `~/.config/meshy/ledger.json`.

| File | What it does |
|---|---|
| `meshy.py` | API client. Price from the published table → cap check against the ledger (`pilot_cap` while the pilot flag is on, else the monthly plan) → POST → ledger entry; `wait` polls (Retry-After aware) and writes the real `consumed_credits` (failed tasks = 0, Meshy refunds them); `download` saves GLB / textures / thumbnail + task json (no URLs). Retries 429 / 5xx; a concurrency 429 (`NoMore…`) is raised, never retried on a timer. |
| `optimize.py` | Blender headless (`/Volumes/BuildSSD/blender/blender`, through `novareka-dev/shot-lock.sh`): weld + drop loose → style edits → pivot bottom centre, +Y up, faces +Z, 1 unit = 1 m → one material named for the game's import script → Decimate per LOD budget → `<name>_lod{0,1,2}.glb` (1024 texture) + `_low.glb` (512) → turntable strip (8 views, LOD0 top row, last LOD bottom row) → `<name>_report.json`. Texture work (recolour, paint-out, posterize, resize) runs in system python (PIL) before Blender, since Blender's python has no PIL. Rigged input (`--keep-armature`) keeps skin + clips, merges extra clip GLBs (`--extra-anims`). |

**Keys (owner 10-27):** `MESHY_KEY=1` (default) = `~/.config/meshy/api_key`, `MESHY_KEY=2` = `~/.config/meshy/api_key_2`.
A task belongs to the key that created it — refine / rig / animate / wait / download run with the same `MESHY_KEY`.
Ledger entries carry `"key"`; the ledger keeps `spent_by_key`, `key_caps` (total cap per key) and `key_plans` (monthly
plan per key). Parallel `wait`s are safe (file lock on the ledger, 10-27: two parallel saves had corrupted it).

**Key:** `~/.config/meshy/api_key` is read inside `meshy.py` only and goes only into the `Authorization` header to
`api.meshy.ai` (never to the asset CDN, a log, a URL or a command line). Do not `cat` it, do not commit it.

## Usage

```sh
export TMPDIR=/Volumes/BuildSSD/tmp/<you>/meshy        # root disk is small
M=games/tools/meshy; OUT=/Volumes/BuildSSD/game-builds/meshy/<game>
python3 $M/meshy.py balance
python3 $M/meshy.py preview rat "<prompt>" --ai_model meshy-t2.5 --polycount 2000        # 5 cr, untextured
python3 $M/meshy.py wait text-to-3d <id>; python3 $M/meshy.py download text-to-3d <id> $OUT/raw/preview
python3 $M/meshy.py refine rat <preview id> "<texture prompt>" --ai_model meshy-6           # 10 cr, 2k texture
python3 $M/optimize.py $OUT/raw/refine/<id>_glb.glb $OUT/optimized --name rat --height 0.5 --lods 1500,700,300 --material paint
# humanoid only: rig the optimised LOD0 (feet at 0, faces +Z) — the rig result already holds Walk + Run clips
python3 $M/meshy.py rig chef - --model_file $OUT/optimized/chef_lod0.glb --texture_file $OUT/optimized/chef_base.png --height 1.72
python3 $M/meshy.py library idle                        # free: animation action ids
python3 $M/meshy.py animate chef <rig id> 0 41          # 3 cr per action (0 Idle, 41 Formal Bow)
python3 $M/optimize.py <anim>.glb $OUT/optimized --name chef_rigged --keep-armature --extra-anims <rig>_walking_glb.glb --lods 3000,1200,500
python3 $M/meshy.py ledger                              # per-asset credits
```

## Figures for skeleton-less games (10-27, Stamp Office citizens)

| File | What it does |
|---|---|
| `pose_figure.py` | Meshy rigged GLB (`meshy.py rig … --model_file <lod0.glb>`, 5 cr) → a STATIC posed figure: `--pose stand` (A-pose arms down to the sides), `--sit-key` (+ a `sit` shape key: thighs forward, shins down, forearms on a counter; `--sit-arms keep` for a figure holding something), welded, smooth normals, feet at 0, `--height`. `--uv-from <lod0.glb>`: Meshy's rig output smears the UVs — every loop takes the exact UV of the reference vertex at the same place (same island). |
| `vc_figure.py` | posed figure → the game's figure: `--legs` cuts the two legs (+ the shoe pieces, bridged) into separate islands for `figure_walk.gd` / FigureRig (prints the hip); `--keep-texture [--texture 512.png]` keeps UV + texture (a walker shader that samples it, e.g. bureaucracy `figure_walk_tex.gdshader`), otherwise the texture is baked into COLOR_0 (`--srgb-numbers` for games whose material decodes sRGB vertex colours). |

Lessons (10-27): a vertex-colour bake at 1.5–1.8k tris loses thin details (straps, buckles, eyes go soft) — keep the
texture where the game's figure shader can take one. The Compatibility renderer sampled a `source_color` uniform
undecoded (washed-out suits): decode by hand (`pow(texel, 2.2)`). Kaiten customers were built (5 × stand + sit) but
NOT shipped: from the 54° top-down camera at 70–90 px they read as dark heads under the counter — worse than the
chunky canonical figures with big hats (the Sky Tower lesson again: check a frame at game size before integrating).

## Budgets (triangles LOD0 / textures)

| Asset class | LOD0 | LOD1 | LOD2 | Texture |
|---|---|---|---|---|
| hero character | 5–8k | ~40 % | ~15 % | 1024 (Low 512) |
| crowd character (drawn many times) | 1.5–3k | ~1.2k | ~500 | 1024 (Low 512) |
| hero prop | 3–6k | ~40 % | ~20 % | 1024 (Low 512) |
| small prop / critter | 0.5–1.5k | ~50 % | ≤ game budget | 1024 (Low 512) |

Check the game's own `ArtLibrary.budget()` too (pest-patrol critters 300, doner kiosks 6000, figures 400) — Meshy
models are 3–7× the hand-built Blender ones, so a swap is a budget decision, not a drop-in.

## Measured credit costs (pilot 2026-10-23, `docs.meshy.ai/api/pricing`)

| Step | Credits | Note |
|---|---|---|
| text-to-3d preview `meshy-t2.5` smart topology (`target_polycount`) | 5 | clean low-poly topology, ~1–2 min; same price `meshy-6-lite` / `meshy-t2`; `meshy-6` / `7.1` = 20 |
| refine (texture, 2k) | 10 | `ai_model meshy-6` → `remove_lighting` works (flat albedo) |
| rigging | 5 | humanoid / biped only; result already contains Walk + Run clips |
| animation | 3 per action | `action_ids` up to 10 in one GLB |
| image-to-3d `meshy-6-lite` 2k textured | 15 | not used in the pilot |

Pilot actuals: chef 31 (2 previews — the first had a hair quiff, no head wrap — + refine + rig + Idle/Bow),
rat 23 (preview + refine + rig + Idle), kiosk 15 (preview + refine). Total 69 of the 150 cap.

**Rollout planning:** static prop / creature ≈ 15 (≈ 20 with the average retry), rigged humanoid with 2 extra clips
≈ 26 (≈ 31 with a retry). 1100 credits ≈ 55 static assets, or ≈ 35 rigged characters, or ~40 for a 50/50 mix.

## Pitfalls found in the pilot

- **Prompts are suggestions:** "no hat" still gave a chef's toque; "no text" still painted "DONER KEBAB" into the
  texture; "doner meat" came out green. Fixes without credits: `--squash-above` (toque → flat cap), `--paint`
  (fill a UV rectangle), `--recolor` (hue range → colour). Always look at the base-colour PNG, not only the thumbnail.
- **Top-down cut-away games:** a Meshy building has a roof and an awning over the interior. `--open-top Z,FRONT`
  removes the roof / ceiling; the awning still hides staff behind the counter (doner-night) — Meshy fits closed props
  and characters better than diorama shells.
- **Rigged meshes come split at every UV seam:** weld before Decimate (done in `--keep-armature`) or LODs tear.
  Keep the Armature modifier (clearing modifiers drops the skin from the export) and put Decimate first.
- **Humanoid rig on a creature:** the biped rat rigs, but the humanoid clips tilt its head back — fine for a test,
  not shippable; quadrupeds are not supported.
- The Blender glTF importer adds a bone-shape "Icosphere" to armatures; it is not in the GLB.
- Godot extracts embedded GLB textures next to the model on import (`<name>_<image>.png` + `.import`); commit them
  with the model when a swap ships.
