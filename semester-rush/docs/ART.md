# Semester Rush — art (P2)

Fleet rules: `LOOKDEV_STANDARD.md`, `NEW_GAME_PLAYBOOK.md` §11, `LESSONS_LEARNED.md` (Meshy lines). **Meshy** = large /
close-up characters and organic hero props; **Blender** = buildings and modular parts. Nothing from the reference game
(names, art, UI, text); no real university names, crests or colours; no text or logos in any texture.

## Meshy set (2026-11-02, cloud session) — 60 credits

| Asset | Use | Pipeline | Tris | Texture | Credits |
|---|---|---|---|---|---|
| `dean` | guide portrait (`coach_dean.png`), pager, curtain, placeholder icon | preview t2.5 + refine meshy-6 | 6000 (LOD0 FBX) | 1024 / 512 | 15 |
| `professor` | pager 3, curtain | preview + refine | 6000 | 1024 / 512 | 15 |
| `student` | pager 2 / 3, curtain | preview + refine; **a brand-like script mark on the hoodie was painted out** (`optimize.py --paint 0.398,0.786,0.460,0.849:12,128,133`) | 6000 | 1024 / 512 | 15 |
| `oak` | the hero oak on the college green (Medium / High only, one instance) | preview + refine; autumn-yellow leaves recoloured green (`--recolor 30-70:86,150,62:0.35`) | 2500 (LOD0) | 1024 / 512 | 15 |

Files: `unity/Assets/Art/Meshy/{dean,professor,student}_lod0.fbx` + `_base.png` / `_base_low.png` (UI renders only),
`unity/Assets/Resources/kit/oak.fbx` + `oak_base(_low).png`. Pipeline: `tools/meshy/meshy.py` (ledger) →
`tools/meshy/optimize.py` → `tools/art/glb2fbx.py`. Decimated LOD1/2 GLBs hit Blender 4.0's "Array length mismatch"
again (Bottle Route note); only LOD0 is used. Every base-colour PNG checked by eye: no remaining text / logos.

**Crowd lesson applied:** the bell-change students on the map are ~20–30 px — chunky canonical figures in the faculty
colour (`MeshKit.Figure`, instanced, ≤ 30 animated, Low ≤ 15 at 15 Hz) + instanced dots for the rest; the Meshy student
is used only where it is large (pager, curtain).

## Blender kit + UI renders

`tools/art/kit.py` (Blender 4.0 headless) builds 11 props on the palette UVs: `hall_s`, `hall_m`, `hall_l`
(amphitheatre with portico), `lab`, `library` (columns), `canteen`, `dorm`, `admin` (clock tower, no digits), `plot`,
`scaffold`, `bus_stop` → `unity/Assets/Resources/kit/*.fbx` (3,104 tris in total, every prop ≤ 600) + the 7 campus
dressing props in `kit/merge/` (polish pass below; 18 props, 4,516 tris in total). `CampusWorld`
loads them from `Resources/kit` (×1.6) and falls back to the procedural `MeshKit` buildings when one is missing.
Paths, lawns, ribbons and figures are procedural (one palette material); trees and gates are kit models on Medium /
High (merged, see below) and procedural on Low.

`tools/art/render_ui.py` renders, from the repo's own models only: `Resources/ui/coach_dean.png`, `intro_1..3.png`,
`curtain.png`, `Art/Icon/icon_full.png` + `icon_adaptive_fg.png` — a **placeholder** icon; the owner picks the final icon
at P7. Palette (warm collegiate: brick, ivy, slate, maroon brand, 7 faculty colours): `unity/Tools/make_palette.py`.

## Polish pass (2026-11-02, cloud) — campus dressing + store art (Blender only, 0 Meshy credits)

The most placeholder parts of the diorama were the zone trees (three stacked boxes) and the shortcut gates (six plain
boxes, one GameObject = one draw each). `tools/art/kit.py` now also builds **7 dressing props** on the palette UVs
(bevelled, one palette material) into `unity/Assets/Resources/kit/merge/`:

| Prop | Tris | Where (Medium / High) |
|---|---|---|
| `tree_oak` | 164 | zone trees (2 of 3), ×1.1 of the old size range |
| `tree_maple` | 152 | zone trees (1 of 3): autumn copper crown, golden top (season colour, plan §8) |
| `lamp` | 152 | along every open path, every other 38-unit slot |
| `bench` | 112 | the other slots, across the path, facing it |
| `bike_rack` | 220 | one per built hall / lab / library (two parked bikes, plan §8 "bikes") |
| `gate_open` / `gate_closed` | 312 / 300 | the shortcut gates: ivy hedge, stone pillars, iron leaves, gold rail |

**Draws:** `CampusWorld` MERGES these into meshes it already draws — trees into each zone mesh, lamps / benches /
racks / gates into the paths mesh (`MeshKit.Add(KitGeo …)`, readable import only for `kit/merge/`,
`SemesterArtImport`). Nothing per frame: the meshes are rebuilt only on structural changes (zone, build, gate, widen),
and `KitGeo.Load` caches each prop once. The gates no longer have their own GameObjects on ANY tier.

**Low tier:** trees and gates stay procedural (same geometry and tris as before), no lamps / benches / racks → **Low:
0 extra draws, 0 extra tris; draws −1 … −4** (the visible gates are merged into the paths mesh: 1 in chapter 1, 2 in
chapter 2, 4 in chapter 3). Low estimate at the full MVP campus: ground 1 + zone lawns 3 + paths 1 + bus stop 1 +
≤ 22 buildings / plots + crowd instancing ≤ ~17 ≈ **45 draws** (was ≈ 49), far under 150.
**Medium / High:** +0 draws, ≈ +22k tris at the full MVP campus (90 kit trees ≈ 14k instead of 2.7k procedural; 33
lamps, 18 benches, ≤ 10 racks ≈ 9.2k; gates +1k). The fallback stays: a missing / unreadable `kit/merge` file falls
back to the procedural tree / gate (dressing is skipped). Editor TODO: confirm in `perf_gate` (Low before/after line).

The UI renders (`render_ui.py`: coach, intro pager, curtain, placeholder icon) show buildings and characters only, so
they were not re-rendered.

**Store art** (`tools/art/render_store.py`, Blender EEVEE, the repo's own Meshy cast + kit only, no text; the walking
students / professor get their modelled T-pose arms swung down in the render copy):

- `store/feature-graphic.png` 1024 × 500 — a late-afternoon bell change on the main path: dean and professor in front,
  students streaming to class, the clock tower, halls, library, dorm, autumn maples, lamps, a bench, a bike rack.
- `store/icon-candidates/` (512 × 512, full bleed, no text; checked at 48 px):
  - `c1.png` — the dean, head and shoulders, clock tower and a maple behind, brand maroon sky.
  - `c2.png` — the clock tower building (blank clock face) between an autumn maple and an oak, maroon sky.
  - `c3.png` — the bell change from above: a brick hall, crossing paths, students hurrying in.
  The placeholder `unity/Assets/Art/Icon/icon_*.png` stays until the **owner picks** a candidate (then re-render the
  adaptive foreground from that scene).

Still to do at P2: season tint (colour grade only), interiors for the focused hall, leaf / snow particles (Medium / High),
a real art gate (Low draws / tris measured in the editor), store screenshots.
