# Bottle Route — art (P2)

Fleet rules: `LOOKDEV_STANDARD.md`, `NEW_GAME_PLAYBOOK.md` §11, `LESSONS_LEARNED.md` (Meshy lines).
**Meshy** = characters and organic hero props only; **Blender** = buildings, machines, trucks, depot modules, district
blocks (modular kit + palette swaps). No real brands, scheme names (DOA, TÜÇA, "Sıfır Atık"), retailer logos or text in
any texture (IP rule of `RECYCLING_GAME_PLAN.md`).

## Meshy set (2026-11-02, cloud session)

| Asset | Use | Pipeline | LOD tris | Texture | Credits |
|---|---|---|---|---|---|
| `operator` | guide coach (portrait in the coach bubble), depot hero at close zoom, store art | preview t2.5 + refine meshy-6 | 6000 / 2400 / 900 | 1024 (Low 512) | 15 |
| `technician` | technician card / jam fix hero, store art | preview + refine | 6000 / 2400 / 900 | 1024 / 512 | 15 |
| `citizen` | onboarding pager + day report art (the bottle bag is a separate Blender prop: Meshy ignored it) | preview + refine | 6000 / 2400 / 900 | 1024 / 512 | 15 |
| `bale_pet` | bale yard stacks (MultiMesh, LOD2 on Low) | refine on the aluminium bale's geometry (the PET preview came out as two pillows; one failed preview refunded) | 1500 / 700 / 300 | 1024 / 512 | 10 (+5 retry preview, unused) |
| `bale_alu` | bale yard stacks | preview + refine | 1500 / 700 / 300 | 1024 / 512 | 15 |

Total **75 credits** (session cap 200; balance after: 1270). Ledger: `~/.config/meshy/ledger.json` on the machine that
ran it (never in git). Textures checked by eye for text / logos: none (the technician's cap badge is an unreadable
generic emblem).

Files: `unity/Assets/Art/Meshy/<name>_lod{0,1,2}.fbx` + `<name>_base.png` (1024) + `<name>_base_low.png` (512).
Made with `tools/meshy/meshy.py` (fleet client: price table, cap check, ledger) → `tools/meshy/optimize.py` (weld, pivot
bottom-centre, +Y up, 1 unit = 1 m, decimate per LOD, 1024 + 512 PNG) → `tools/art/glb2fbx.py` (Unity has no glTF
importer and the project must not get one).

**Crowd lesson applied:** the citizens walking on the map are ~20–30 px — Meshy figures read worse than chunky canonical
figures at that size (Sky Tower 10-23, Kaiten 10-27). The map crowd therefore uses the chunky canonical figure (`MeshKit.Figure`,
GPU-instanced, one palette swatch per archetype, half the walkers at 15 Hz on Low); the Meshy citizen is only used where it is large.

To redo: `export TMPDIR=<scratch>`; key in `~/.config/meshy/api_key` (needs `api.meshy.ai` + `assets.meshy.ai` in the
environment's allowed domains); Blender 4.x with numpy and an EGL/Mesa stack for the turntable renders.

## Blender kit + UI renders (2026-11-02)

`tools/art/kit.py` (Blender 4.0 headless) builds 8 props on the palette UVs: `machine_s`, `machine_m`, `shop`,
`horeca`, `van`, `bike`, `compactor`, `baler` → `unity/Assets/Resources/kit/*.fbx` (1720 tris in total; every prop ≤ 400).
`RegionWorld` loads them from `Resources/kit` and falls back to the procedural `MeshKit` meshes when one is missing.
The Meshy bale LODs 1/2 come from `tools/art/glb2fbx_lod.py` (LOD0 → decimated FBX): Blender 4.0's glTF exporter wrote
empty files for the decimated bale LODs ("Array length mismatch"); the optimizer's LOD0 is fine.

`tools/art/render_ui.py` renders, from the repo's own models only: `Resources/ui/coach_operator.png` (guide portrait),
`intro_1..3.png` (pager), `curtain.png` (boot), `Art/Icon/icon_full.png` + `icon_adaptive_fg.png` — a **placeholder**
icon; the owner picks the final icon from candidates at P7 (the build still needs `icon_adaptive_bg / round / legacy`,
`BottleIcons` fails closed until then). Palette: `unity/Tools/make_palette.py`.

## Polish pass (2026-11-02, cloud): district kit, depot, stadium, store art

**What changed in the diorama.** The procedural district blocks, the stadium box and the depot boxes are replaced by
Blender-kit pieces from `tools/art/kit.py` (bevelled, palette UVs, one material). They are **merged into the meshes that
already existed** (one mesh per district, one depot mesh) by `MeshKit.Append`, so the draw count does not change on any
tier. They ship as `Resources/kit/<name>.bytes` (BRKM v1, read by `World/KitPiece.cs`): Unity-space vertices + flat
normals + palette UVs + clockwise uint16 triangles — no readable-mesh import flag and no FBX axis guess for merged
geometry. Every piece has a `<name>_low` twin (no bevels, no windows) used on **Low** and for locked (grey) districts.
Wall swatch cream → cream / terracotta / blue and roof red → red / grey are swapped per building (palette swaps, no new
material). The procedural boxes stay as the fallback when a piece is missing.

| Piece | Where | Tris (Medium/High) | Low twin |
|---|---|---|---|
| `bld_harbour_a` | Harbour Quarter: two-storey house, gable roof, chimney, planter | 98 | 16 |
| `bld_harbour_b` | Harbour Quarter / Stadium streets: fish hall / shed, awning, bottle crates | 156 | 16 |
| `bld_old_a` | Old Town: tall narrow house, steep roof, balcony | 100 | 16 |
| `bld_old_b` | Old Town / University: corner block, cornice, arcade, roof terrace | 108 | 22 |
| `bld_uni_a` | University Hill / Stadium streets: faculty block, window bands, roof plant, canopy | 110 | 22 |
| `bld_uni_b` | University Hill: library / dorm tower | 90 | 22 |
| `stadium` | Stadium & Fairground: oval bowl of stands, pitch, canopies, 4 floodlights (60 × 40 m) | 220 | 82 |
| `depot_hall` | Depot: hall + yard doors, count-line unit, rooftop solar, baler house + hopper + conveyor, press housing, yard pad, glass bays, sorted skips, gate office | 472 | 84 |

All ≤ 600 tris (`tools/art/kit_check.py`, run by `check.sh`: also UVs on swatch centres, unit normals, winding, Low twin
≤ 40 %). Placement: each street now belongs to the district of its first node (before, a street between two districts got
two overlapping rows).

**Budget (estimate from the map, all four districts built; editor perf_gate still to run):**

| | Before | After |
|---|---|---|
| Low draws | ground 1 + districts 4 + depot 1 + props / instanced | **unchanged** (merged; 0 extra) |
| Low tris, district blocks + stadium + depot | ≈ 4.0k | ≈ 5.0k (+≈ 1k, ≈ +2 % of a ≈ 45k Low scene) |
| Medium/High tris, same | ≈ 6.6k | ≈ 26k |
| Medium/High draws | — | **+1**: the baler press ram (animated while a bale is pressed, no allocation; not created on Low) |

**Fixes found on the way.** `MeshKit.Quad` wound its triangles counter-clockwise seen from outside; Unity's front face is
clockwise, so with back-face culling every box, road strip, the ground quad and the walkers would have rendered inside-out
(`Disc` was already right). Fixed (`t = i, i+1, i+2 / i, i+2, i+3`). The kit machines are life-size (1.4 / 2.0 m) while
the map draws props larger than life (fallback 3.2 / 4.4 m): they get a ×2.2 map scale. `Prop` now keeps the importer's
root rotation under the yaw (FBX axis conversion). District rows use a stable FNV hash (String.GetHashCode is randomised
per process on .NET Core). **Verify in the editor:** the facing of the FBX kit props (machines / shop / café / trucks:
front = Blender −Y) — the merged pieces above have a known axis.

UI art (`render_ui.py`) was not re-rendered: the props it shows (machines, van, bales, Meshy cast) did not change.

### Store art (`tools/art/render_store.py`, Blender EEVEE, own models only, no text)

- `store/feature-graphic.png` (1024 × 500): a harbour street with the new buildings, the deposit corner (machines, shop,
  citizens and the operator), van + compactor on the front road, the depot with bale stacks, the stadium in the back.
  The tagline (`store/screenshots/en.json` `feature`) is composited later.
- Icon candidates (512 × 512, full-bleed, no text; checked at 48 px) — **the owner picks one**; the placeholder in
  `unity/Assets/Art/Icon` stays until then:
  - `store/icon-candidates/c1.png` — the deposit machine (big teal silhouette) with a PET bale, mint field;
  - `store/icon-candidates/c2.png` — the collection van on the road, a machine behind, warm sand background;
  - `store/icon-candidates/c3.png` — a pyramid of PET / aluminium bales on deep green.

Redo: `blender -b --python bottle-route/tools/art/kit.py -- bottle-route/unity [<sheet.png> <merged_sheet.png>]`, then
`blender -b --python bottle-route/tools/art/render_store.py -- bottle-route/unity bottle-route/store`. No Meshy credits
were used in this pass.

Still to do: litter decal art, a real art gate in the editor (Low draws / tris measured by perf_gate), the facing check
above.

**Chapters 5–7 (2026-11-02):** the new districts use the existing kit pieces (Industrial: harbour_b / uni_a; Riviera: harbour_a / uni_b / old_a; Villages + Island: old_a / old_b, more trees). Low merges ALL districts into one mesh (8 districts = 1 draw, was 4) and draws the return points instanced (≤ 4 draws instead of one per point: 38 sites at the end); island ground, piers and the dashed ferry lane are merged into the ground (0 draws). Medium/High: one mesh per district (+4) and the ferry boat (+1). Low estimate, fully built: ≈ 25 draws (was ≈ 46 with chapters 1–4 fully built), ≈ +4.7k tris (≈ 118 buildings + 65 trees, Low twins). Verify in the editor: point-prop facing/scale of the instanced kit meshes on Low (submesh 0, prefab root transform), boat height under a sailing truck.
