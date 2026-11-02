# Pour & Roast — art (P2)

Fleet rules: `LOOKDEV_STANDARD.md`, `NEW_GAME_PLAYBOOK.md`, `LESSONS_LEARNED.md` (Meshy lines). **Meshy** = characters and
organic hero props; **Blender** = machines, furniture, the cart, shells. Originality (plan §13 risk 6): no orchard / garden,
no event café, no real chain marks, no green-and-white chain palette, no text or logos in any texture.

## Meshy set (2026-11-02, cloud session)

| Asset | Use | Pipeline | LODs (tris) | Texture | Credits |
|---|---|---|---|---|---|
| `barista` (Mara, head barista) | guide coach portrait, pager, curtain | preview meshy-t2.5 + refine meshy-6 | 6000 (LOD0 FBX in `Art/Meshy`) | 1024 | 15 |
| `customer` (commuter) | pager, curtain | preview + refine | 6000 | 1024 | 15 |
| `cat` (cafe cat) | corner / roastery diorama (Medium+), intro 3, curtain | preview + refine | 3000 / 1200 / 500 | 1024 / 512 | 15 |
| `sack` (burlap coffee sack) | roastery back room, intro 3, curtain | preview + refine | 2984 / 900 / 400 | 1024 / 512 | 15 |
| `plant` (monstera in terracotta) | decor upgrade in the cafes | preview + refine | 2400 / 900 / 400 | 1024 / 512 | 15 |
| `croissant` | pager, placeholder icon | preview + refine | 2000 | 1024 | 15 |

Total **90 credits** (budget ≤ 150 after the owner's 11-02 raise; ledger `~/.config/meshy/ledger.json`, never in git).
Every base-colour texture checked by eye: no text, no logos, no stamps (the sack prompt asked for plain fabric).

Files: in-world props `unity/Assets/Resources/kit/<name>_lod1.fbx` / `_lod2.fbx` + `_base.png` (1024) / `_base_low.png`
(512, Low tier); render-only heroes `unity/Assets/Art/Meshy/<name>_lod0.fbx` + `_base.png`. Pipeline:
`tools/meshy/meshy.py` → `tools/meshy/optimize.py` (pivot bottom-centre, 1 unit = 1 m) → `tools/art/glb2fbx.py` (LOD0) /
`glb2fbx_lod.py` (LOD1/2 from LOD0 — Blender 4.0's glTF exporter writes empty files for decimated LODs; the sack's LOD0
was re-run undecimated for the same reason).

**Crowd lesson applied:** customers and staff in the diorama are ~25–40 px — chunky canonical figures
(`MeshKit.Figure`, GPU-instanced, a shirt swatch per customer type, aprons on staff; Low ≤ 14 customers at 15 Hz). The
Meshy characters are only used where they are large (portrait, pager, curtain, store art).

## Blender kit + UI renders

`tools/art/kit.py` (Blender 4.0 headless) builds 18 props on the palette UVs: `cart`, `counter`, `back_bar`,
`espresso_1..6` (one per group-head count), `grinder`, `fridge`, `pastry_case`, `pickup_shelf`, `ice_maker`, `roaster`,
`table`, `rest_rack`, `packing_bench` → `unity/Assets/Resources/kit/*.fbx` (4036 tris in total; every prop ≤ 460).
`CafeWorld` loads them from `Resources/kit` and falls back to the procedural `MeshKit` meshes when one is missing.
Palette: `unity/Tools/make_palette.py` (warm walnut / cream / burnt-orange brand + teal accent; 48 swatches).

`tools/art/render_ui.py` renders, from the repo's own models only: `Resources/ui/coach_barista.png` (guide portrait),
`intro_1..3.png` (pager: the cart on the quay · the bar · the roastery), `curtain.png` (boot), `Art/Icon/icon_full.png` +
`icon_adaptive_fg.png` — a **placeholder** icon (takeaway cup + croissant); the owner picks the final icon at P7.

Still to do at P2: rain / steam / roaster-smoke effects (Medium/High only), harbour backdrop through the windows, latte-art
decals for the cups cosmetic, a real art gate (Low draws / tris measured in the editor).
