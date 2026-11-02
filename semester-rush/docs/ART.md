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
`scaffold`, `bus_stop` → `unity/Assets/Resources/kit/*.fbx` (3,104 tris in total, every prop ≤ 600). `CampusWorld`
loads them from `Resources/kit` (×1.6) and falls back to the procedural `MeshKit` buildings when one is missing. Gates,
paths, lawns, zone trees, ribbons and figures are procedural (one palette material).

`tools/art/render_ui.py` renders, from the repo's own models only: `Resources/ui/coach_dean.png`, `intro_1..3.png`,
`curtain.png`, `Art/Icon/icon_full.png` + `icon_adaptive_fg.png` — a **placeholder** icon; the owner picks the final icon
at P7. Palette (warm collegiate: brick, ivy, slate, maroon brand, 7 faculty colours): `unity/Tools/make_palette.py`.

Still to do at P2: season tint (colour grade only), interiors for the focused hall, leaf / snow particles (Medium / High),
a real art gate (Low draws / tris measured in the editor), store screenshots.
