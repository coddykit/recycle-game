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

## Polish pass (cloud, 2026-11-02): venue shells + store art

**What changed.** The most placeholder part of the diorama was the shell: flat boxes for the walls, the windows, the chalk
board and the quay. `tools/art/kit.py` now also builds one **venue shell per venue** on the palette UVs (bevelled, one
material) — `kit/shell_cart`, `shell_corner`, `shell_roastery` + `_low` twins:

| Shell | Medium/High tris | Low tris (procedural before) | What is in it |
|---|---|---|---|
| `shell_cart` | 1428 | 74 (46) | quay + paving, stone coping blocks, quay wall to the water, harbour, 2 bollards with caps; Medium+: lamp post, crates + a sack, milk churn, 2 flower boxes, 2 moored boats (static) |
| `shell_corner` | 1608 | 248 (74) | floor + floorboard seams, back/left walls, panelled wainscot + dado rail, 3 framed windows (jambs, head, sill; Medium+: mullion, transom), framed chalk board (no text), runner rug + door mat; Medium+: crown moulding, 3 copper pendant lamps, jar shelf |
| `shell_roastery` | 2022 | 268 (84) | the corner set on the wider hall + the half wall to the back room; Medium+: cap rail + posts, brick band in the back room, 5 pendants, extraction duct from the roaster chimney to the wall, bag shelf over the sacks |

Pieces (Medium/High): window 204 tris, jar shelf 220, chalk board 160, wainscot (back wall) 120, pendant 80, crown 42 —
every piece ≤ 600. Low twins: no bevels, no hidden faces (against-the-wall / bottom faces deleted), no small dressing.

**Draws.** Each shell is ONE mesh that replaces the procedural shell 1:1 (`CafeWorld.KitShell`, spawned at the origin;
the procedural `ProceduralShell` stays as the fallback) → **Low: 0 extra draws** (also 0 on Medium/High). Low prims:
corner +174, roastery +184, cart +28 tris on a Low frame of ~5–6.5k tris (≈ +3 %, under the +5 % rule); no per-frame
work (all at build), no new animation / particles / lights. Theme looks (Nordic, Art Deco) recolour the shell at build by
moving the wall / floor / floor-seam / trim UVs to the theme swatches (`CafeWorld.Recolour`; the shells import readable,
`RoastArtImport`), the themed copy is destroyed on the next rebuild (the procedural shell mesh is now destroyed too).
**Editor check needed:** the shells are authored like the other kit props (front = Blender −Y). `KitShell` turns a shell
180° when its bounds say it arrived the other way round (more street in front of the cafes, the harbour behind the cart);
the first editor run should confirm the walls stand at the back + left with the props facing the camera.

**UI art re-rendered** (`render_ui.py`): `intro_1` now stands the cart on the quay with the harbour and boats behind,
`intro_3` puts the roaster in the back room (brick band, duct, pendants, bag shelf). Coach portrait, `intro_2`, curtain and
the placeholder icon were not changed.

**Store art** (`tools/art/render_store.py`, own models only, no text): `store/feature-graphic.png` (1024×500, the cart on
the quay, Mara, a customer, the cat; the left third stays calm for the tagline) and three icon candidates (512×512, full
bleed, checked at 48 px) for the owner to pick from — `unity/Assets/Art/Icon` keeps the placeholder until then:

| Candidate | Idea |
|---|---|
| `store/icon-candidates/c1.png` | takeaway cup (burnt-orange sleeve) + croissant + beans on teal — the placeholder's idea, bolder |
| `store/icon-candidates/c2.png` | the drum roaster with its orange drum and cooling tray on deep navy — the roastery hook |
| `store/icon-candidates/c3.png` | a latte with heart art seen from above, on walnut — reads best at 48 px |

Meshy: **0 credits** this pass (total stays 90).

Still to do at P2: rain / steam / roaster-smoke effects (Medium/High only), harbour backdrop through the windows, latte-art
decals for the cups cosmetic, a real art gate (Low draws / tris measured in the editor).
