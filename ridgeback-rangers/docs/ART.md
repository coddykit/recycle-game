# Ridgeback Rangers — art (P2)

Fleet rules: `LOOKDEV_STANDARD.md`, `NEW_GAME_PLAYBOOK.md` §11, `LESSONS_LEARNED.md` (Meshy lines).
**Meshy** = large creatures and the hero character; **Blender** = buildings, the tram, fences (procedural `MeshKit`), foliage.
Originality (plan #184 §3 / R6): no film-franchise imagery (no torch gate, no black-and-yellow jeeps, no logo), no
"Jurassic", nothing from the Codigames game; species names are public scientific names. `check.sh` greps for the names.

## Meshy set (2026-11-02, cloud session) — 110 credits (owner cap for this game: 150)

| Asset | Use | Pipeline | LOD tris | Texture | Credits |
|---|---|---|---|---|---|
| `dino_horned` | Triceratops, Protoceratops (scale 0.6) | preview t2.5 + refine meshy-6 | 3000 / 1200 / 450 | 1024 (Low 512) | 15 |
| `dino_plated` | Kentrosaurus (0.8), Miragaia | preview + refine | 3000 / 1200 / 450 | 1024 / 512 | 15 |
| `dino_longneck` | Camarasaurus, Saltasaurus (0.85) | preview (1st had a sail, 5) + retry preview + refine | 3000 / 1200 / 450 | 1024 / 512 | 20 |
| `dino_duckbill` | Maiasaura, Parasaurolophus (1.1) | 4 previews (Meshy kept adding a back ridge / sail: t2.5 ×3 = 15, meshy-6 = 20) + refine of the best biped | 3000 / 1200 / 450 | 1024 / 512 | 45 |
| `warden` | the Chief Ranger: coach portrait, intro / curtain art, store art | preview + refine | 6000 / 2400 / 900 | 1024 / 512 | 15 |

Ledger: `~/.config/meshy/ledger.json` (assets `rr_*`). Textures checked by eye: no text, no logos (the warden's shoulder
patches are blank generic emblems). The duckbill keeps a small back ridge (Meshy ignored "no sail" four times) — reads as a
crested biped; replace at P2 polish if the owner wants (image-to-3d from a sketch, ~15 cr).

Files: `unity/Assets/Resources/kit/dino_<family>_lod{1,2}.fbx` + `dino_<family>_base.png` (1024) + `_base_low.png` (512) —
loaded at runtime by `World/Herd.cs` (LOD1 Medium/High, LOD2 + 512 Low; one material per species tinted with
`species.json` `tint` = the colour morph; 8 species from 4 meshes). `unity/Assets/Art/Meshy/*_lod0.fbx` + `warden_base*.png`
are render sources only (`tools/art/render_ui.py`). Pipeline: `tools/meshy/meshy.py` → `optimize.py` (normalised: 1 unit
long for dinosaurs, 1.72 m for the warden, faces +Z) → `tools/art/glb2fbx.py`.

**Crowd lesson applied:** rangers and visitors are ~25 px on screen → chunky canonical figures (`MeshKit.Figure`, rangers with a
wide khaki hat, one swatch per role), GPU-instanced; no Meshy crowd.

## Blender kit + UI renders

`tools/art/kit.py` (Blender 4.0 headless) builds 8 FBX props on the palette UVs: `gate`, `hq`, `barn`, `vet`, `hatchery`,
`station`, `view` (viewpoint tower + feeding trough), `tram_car` → `unity/Assets/Resources/kit/*.fbx` (1 574 tris in total;
drawn at 2.2×), plus the merge bank `props.bytes` (P2 polish, below). `IslandWorld` falls back to procedural `MeshKit` meshes when one is missing. Palette: `unity/Tools/make_palette.py`
(44 swatches, indices = `World.Pal`).

`tools/art/render_ui.py` renders: `Resources/ui/coach_warden.png`, `intro_1..3.png`, `curtain.png`, `Art/Icon/icon_full.png` +
`icon_adaptive_fg.png` (a **placeholder** icon — triceratops; the owner picks the final icon at P7).

## P2 polish (cloud, 2026-11-02) — kit merge bank, foliage, fences, store art

**What changed.** The most placeholder parts of the diorama — box trees, plank ferns, box rocks, the flat water disc,
the box shade sail, the bare mud dig sites, the cone volcano / hills, the box fences — are now Blender kit models on the
palette UVs (`tools/art/kit.py`, bevelled on Medium/High). New: path lamps (Medium/High only), a snack kiosk and a gift
hut by the gate plaza once the café / gift shop is bought, an earth ring + rib bones + tool crate + awning on each dig
site. Palette: +7 swatches (44–50: leaf light, canvas, bone, moss, lamp, bark, sail teal; `Tools/make_palette.py`, `Pal`).

**How (0 extra draws).** `kit.py` also writes `Resources/kit/props.bytes`, a **merge bank**: every static prop as raw
triangles already in Unity space (y/z swap + reversed winding, flat normals, swatch per vertex, 105 KB). `World/KitBank.cs`
reads it once; `MeshKit.Kit()` appends a prop (position, yaw, per-axis scale) into the mesh being built. So trees, rocks,
the volcano, hills, dig sites, lamps, kiosks and — new — **the buildings and viewpoints** go into the ground / zone /
paddock meshes that already exist: the kit adds no renderer, and the 6 buildings + 1–3 viewpoints stop being separate
objects (**−7 to −9 draws on every tier**). No readable-mesh import flag, no FBX axis guess for anything merged. Missing
bank → the old FBX props / procedural shapes (fallback kept, per prop). Rebuilds only on change (zone opened, paddock /
fence / water / shade changed, café / gift bought, tier switched); the rebuild keys are now int hashes, so an idle frame
allocates nothing (they were strings built every frame). Low uses each prop's `_low` twin and keeps the procedural box
fences; lamps are not built on Low.

| Prop (merge bank) | Medium/High tris | Low twin tris | Old procedural it replaces |
|---|---|---|---|
| tree / tree_tall (umbrella conifer) | 164 / 144 | 28 / 28 | box tree 30 |
| tree_fern (cycad crown, 8 fronds) | 161 | 18 | plank fern 50 |
| rock (boulder cluster) | 120 | 10 | box 10 |
| shade_sail (4 poles, 2-tone sagging canvas) | 60 | 20 | 2 boxes 20 |
| water_point (stone rim, pool, pump) | 142 | 13 | disc 10 |
| dig_site (earth ring, ribs, crate, awning, shovel) | 286 | 22 | — (new; mud disc stays) |
| volcano (20 facets, wooded foot, crater, lava seam) | 302 | 43 | cone 42 |
| hill (wooded dome) | 144 | 22 | cone 30 |
| kiosk_cafe / kiosk_gift | 172 / 198 | 22 / 22 | — (new) |
| fence1/2/3 post (wood chamfered / steel pipe / pillar + lamp) | 46 / 40 / 54 | — (Low: boxes) | box posts |
| fence1/2/3 rail (stretched per side) | 88 / 40 / 32 | — (Low: boxes) | box rails |
| lamp (Medium/High only) | 40 | — | — |
| gate / hq / barn / vet / hatchery / station / view | 230 / 134 / 300 / 132 / 186 / 114 / 150 | same mesh | were separate FBX objects |

Low estimate (full island, all props the polish touched): procedural ≈ 1 262 tris → kit ≈ 863 (**−399**, `tools/art/check_kit.py`
prints it); Low draws ≈ ground 1 + 3 zone meshes + paddocks (2–6) + slots 0–1 + rail 1 + tram cars 1–4 + herd / crowd
instanced batches ≈ **25–45** (was +7–9 with the separate buildings / viewpoints; target ≤ 120). Medium/High gain ≈ 1.5 k
tris per paddock (bevelled fences), ≈ 1.2 k for lamps, ≈ 4–8 k for the foliage — no new draws, no new light, no particles.

**Fixes found on the way.** `MeshKit.Quad` still wound its triangles counter-clockwise seen from outside (the fleet note in
`docs/UNITY.md` said it was fixed; this copy was not) → every box, road strip, cone and the sea would have been culled from
the outside. Fixed (`i, i+1, i+2 / i, i+2, i+3`); `Disc` was already right. `IslandWorld.Prop` overwrote the FBX root
rotation (the importer's axis conversion) with the yaw → now an outer pivot carries position + yaw (tram cars turn the pivot).
Check (`tools/art/check_kit.py`, in `check.sh`): format, every prop present, ≤ 700 (building) / ≤ 600 tris, Low twins ≤ the
procedural shape (+3), swatches inside the palette, unit normals, Low scene estimate ≤ +5 %.

**Art check without the editor.** `tools/art/store_art.py -- <game> <dir>` lays the island out like `IslandWorld` (map
JSON, same placement rules, Low vs Medium) from the bank exactly as `KitBank` reads it, rendered with **back-face culling
on** — winding / axis mistakes show as holes. Both tiers rendered clean (scratch renders, not committed).

Meshy this pass: **0 credits** (game total stays 110 of 150).

UI art (`render_ui.py`) not re-rendered: the coach, intro and curtain show the warden, the dinosaurs, the gate, the barn
and the tram car, none of which changed (the FBX lost only their never-seen bottom faces).

### Store art (`tools/art/store_art.py`, Blender EEVEE, own models only, no text)

- `store/feature-graphic.png` (1024 × 500): late afternoon on the island — a mixed herd (2 horned, plated, duckbill, a
  long-neck) in a timber paddock with a drinking pool, the safari tram at the stop, the Chief Warden on the path, tree ferns,
  conifers, the viewpoint tower, the volcano and hills behind. Tagline space top-left / top-right.
- `store/icon-candidates/` (512 × 512, full-bleed, no text, checked at 48 px):
  - **c1** — a long-neck's silhouette against a red sunset volcano, tree ferns in front (reads best at 48 px);
  - **c2** — a plated herbivore in profile, back-lit at dusk, a volcano behind;
  - **c3** — the Chief Warden (waist up, ranger hat) with a long-neck behind her.
  The owner picks one; until then the build keeps the placeholder triceratops in `unity/Assets/Art/Icon`.
- Tone (13+, Families decision pending — plan R4): warm, grown-up lighting and the work of the rangers; no eye close-ups,
  no text, no film-franchise imagery. The Meshy dinosaurs themselves are cartoon-styled (big eyes): if the owner wants the
  store page to read even less child-directed, c1 / c2 (silhouettes) are the safer picks.

Still to do at P2: verify in the editor — the Meshy dinosaurs head first (`Herd` adds no yaw), the tram car's FBX
front, the bank props' facing (known axis, should be right); per-family idle / eat poses (no skeleton: Meshy cannot rig
quadrupeds), lava glow + god rays Medium/High only, a real Low perf_gate pass (target ≤ 120 draws).
