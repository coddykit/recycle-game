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

`tools/art/kit.py` (Blender 4.0 headless) builds 8 props on the palette UVs: `gate`, `hq`, `barn`, `vet`, `hatchery`,
`station`, `view` (viewpoint tower + feeding trough), `tram_car` → `unity/Assets/Resources/kit/*.fbx` (1 628 tris in total;
drawn at 2.2×). `IslandWorld` falls back to procedural `MeshKit` meshes when one is missing. Palette: `unity/Tools/make_palette.py`
(44 swatches, indices = `World.Pal`).

`tools/art/render_ui.py` renders: `Resources/ui/coach_warden.png`, `intro_1..3.png`, `curtain.png`, `Art/Icon/icon_full.png` +
`icon_adaptive_fg.png` (a **placeholder** icon — triceratops; the owner picks the final icon at P7).

Still to do at P2: verify FBX axes in the editor (kit fronts −Z, dinosaurs head +Z), per-family idle / eat poses (no skeleton:
Meshy cannot rig quadrupeds), lava glow + god rays Medium/High only, a real Low perf_gate pass (target ≤ 120 draws).
