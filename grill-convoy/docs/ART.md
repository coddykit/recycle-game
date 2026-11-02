# Grill Convoy — art (P2)

Fleet rules: `LOOKDEV_STANDARD.md`, `NEW_GAME_PLAYBOOK.md`, `LESSONS_LEARNED.md` (Meshy lines). **Meshy** = characters and
organic hero props seen up close; **Blender** = trucks, commissary, street props (palette UVs, ONE material). No real
burger chain names, marks or colours, no text in any texture (originality rule of `BURGER_GAME_PLAN.md`).

## Meshy set (2026-11-02, cloud session) — 60 credits (budget ≤ 150)

| Asset | Use | Pipeline | LOD tris | Texture | Credits |
|---|---|---|---|---|---|
| `chef` | guide coach portrait, pager / curtain hero | preview t2.5 + refine meshy-6 | 6000 / 2400 / 900 | 1024 (Low 512) | 15 |
| `cashier` | pager / curtain staff hero | preview + refine | 6000 / 2400 / 900 | 1024 / 512 | 15 |
| `fan` | pager art (match-day customer) | preview + refine | 6000 / 2400 / 900 | 1024 / 512 | 15 |
| `burger` | icon, pager, curtain (food hero) | preview + refine | 3000 / 1200 / 500 | 1024 / 512 | 15 |

Total spent by this game: **60** (4 previews × 5 + 4 refines × 10; ledger notes "grill-convoy …"). Ledger:
`~/.config/meshy/ledger.json` on the machine that ran it (never in git). Every base-colour PNG checked by eye: no text,
no logos (the chef asked for a bandana and got a classic toque — kept, it reads "cook" instantly; the cashier's cap came
out brown).

Files: `unity/Assets/Art/Meshy/<name>_lod{0,1}.fbx` + `<name>_base.png` (1024) + `<name>_base_low.png` (512).
`tools/meshy/meshy.py` → `tools/meshy/optimize.py` → `tools/art/glb2fbx.py` (LOD1 of `fan` / `burger` through
`tools/art/glb2fbx_lod.py`: Blender 4.0's glTF exporter wrote empty decimated LODs again).

**Crowd lesson applied:** the customers in the lines are ~20–30 px on the map — chunky canonical figures
(`MeshKit.Figure`, GPU-instanced, one swatch per customer type, red shirt = walked away, a burger in hand = served;
Low ≤ 16 per line at 15 Hz). Meshy figures are only used where they are large (UI renders).

## Blender kit + UI renders

`tools/art/kit.py` (Blender 4.0 headless): `truck_starter`, `truck_box` (serving hatch + striped awning + counter on both
sides, blank menu board, roof sign with a bun badge, no text), `commissary`, `parasol`, `menu_board` →
`unity/Assets/Resources/kit/*.fbx` (1516 tris total; trucks 544 each). `RegionWorld` falls back to procedural `MeshKit`
meshes when a kit file is missing. Districts (office blocks, boardwalk deck + parasols, stadium bowl) are procedural boxes
merged into one mesh per district (cheap on Low); on Medium / High the district pieces below replace / dress them.

`tools/art/render_ui.py` renders from the repo's own models: `Resources/ui/coach_chef.png`, `intro_1..3.png`,
`curtain.png`, `Art/Icon/icon_full.png` + `icon_adaptive_fg.png` — a **placeholder** icon (the owner picks the final one
at P7; the build still needs `icon_adaptive_bg / round / legacy`). Palette: `unity/Tools/make_palette.py` (44 swatches,
street-food poster colours).

## Polish pass (2026-11-02, cloud) — district kit + store art

**District pieces** (`tools/art/kit.py`, bevelled, palette UVs, ONE material, `Resources/kit/d_*.fbx`, pivot bottom-centre,
front = Unity +Z). `RegionWorld.KitInto` copies their vertices once into the **existing district mesh** (the importer marks
only `d_*` readable) — no new GameObjects, no new draws, nothing per frame. When a file is missing / unreadable the
procedural block is drawn instead (fallback kept).

| Piece | Replaces / adds | Tris |
|---|---|---:|
| `d_office_a` glass tower (floor bands, rooftop plant, canopy) | plaza towers 1 + 3 | 288 |
| `d_office_b` mid-rise with setback + water tank | plaza towers 2 + 4, street blocks taller than 24 m | 500 |
| `d_house_a` townhouse (pitched roof, chimney, 2 floors of windows) | street blocks 10–24 m | 290 |
| `d_shop_a` shop (shopfront, striped awnings, AC unit) | street blocks ≤ 10 m | 268 |
| `d_kiosk` beach kiosk (striped roof, hatch, bun badge, surfboard) | the 5 boardwalk huts | 212 |
| `d_stand` stadium stand (5 seat tiers, back wall, roof) | the 14 bowl segments (now face the pitch) | 224 |
| `d_planter` planter + tree + bench | the 2 plaza planters | 288 |
| `d_lifeguard` tower | new, beach (×2) | 158 |
| `d_lights` string lights (12 warm bulbs, Lights swatch, no light / glow) | new, plaza (×1) + boardwalk (×2) | 256 |
| `d_floodlight` mast | new, stadium (×4) | 112 |
| `d_warehouse` saw-tooth shed | new, harbour (×2) | 182 |
| `d_crane` gantry crane | new, harbour shore + a plank pier | 252 |
| `d_boat` fishing boat | new, harbour water (×2) | 108 |
| `d_bits` bin + cones + bollard | new, plaza / boardwalk / commissary yard | 140 |

All pieces ≤ 600 tris (kit.py exits non-zero above that); kit total 4,794 tris (vehicles + props 1,516 + district 3,278).

**Tier rule.** Low keeps the procedural blocks it had (kit pieces and the harbour dressing are Medium / High only) →
**Low draws: unchanged (≈ 20 for the city: ground, commissary, 3 district meshes, 1–2 trucks, instanced crowd / looks; no
new draw, no new prims, no new shader branch).** One Low change, lighter: a road between two districts used to be dressed
by BOTH districts (overlapping duplicate blocks); now the later-chapter district dresses it once (plaza / boardwalk lose
~40 % of their street-block boxes, stadium unchanged). Medium / High: +1 draw (the harbour mesh; the plaza / boardwalk /
stadium pieces go into their existing meshes); static city ≈ 24k tris (plaza ~6k, boardwalk ~5k, stadium ~8k, harbour
~4k) vs the 300k budget. A tier switch rebuilds the district meshes once (`SetLow`). To verify in the editor (P6
perf_gate): Low before/after draws equal, Medium ≤ +1; the kit pieces face the right way (`d_stand` seats toward the
pitch — if the FBX axis swap flips them, rotate in `KitInto` once).

**Store art** (`tools/art/render_store.py`, `blender -b --python … -- <unity> <store>`; EEVEE, own models, no text):
`store/icon-candidates/c1.png` (the burger hero on a sunset radial), `c2.png` (the burger truck in 3/4, teal), `c3.png`
(the chef with a burger, cream–orange) — 512 × 512, full-bleed, checked at 48 px (c1 / c2 read best). **The owner picks
one**; until then the build keeps the placeholder in `unity/Assets/Art/Icon`. `store/feature-graphic.png` (1024 × 500):
lunch rush on the plaza, two trucks, crew at the hatches, a line of the game's chunky crowd figures, string lights, towers,
a burger hero in the left third for the tagline overlay. Meshy credits this pass: **0** (game total stays 60).
UI renders (`render_ui.py`) unchanged: they show the trucks / characters, which did not change.

Still to do at P2: truck cutaway close-up view (plan §8 camera 2), steam cards as real sprites, livery / awning cosmetic
meshes (today: tint), a measured Low draw / tris pass in the editor (targets: city ≤ 80 draws).
