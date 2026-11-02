# Docket Day — art (P2)

Fleet rules: `LOOKDEV_STANDARD.md`, `NEW_GAME_PLAYBOOK.md` §11, `LESSONS_LEARNED.md` (Meshy lines). **Meshy** = large /
close-up characters and organic hero props; **Blender** = office furniture, the courthouse, street props; the office
tower floors and the street are procedural (`MeshKit`). Plan originality rules: no real firms, courts, people, crests,
inscriptions or the reference game's art; no text or logos in any texture.

## Meshy set (2026-11-02, cloud session) — 75 credits

| Asset | Use | Pipeline | LOD tris | Texture | Credits |
|---|---|---|---|---|---|
| `counsel` | guide coach portrait, hearing scene (your lawyer), intro / curtain / icon | preview t2.5 + refine meshy-6 | 6000 / 2400 | 1024 (Low 512) | 15 |
| `client` | hearing scene, intro 2, curtain | preview + refine | 6000 / 2400 | 1024 / 512 | 15 |
| `judge` | hearing scene, intro 3, curtain | preview + refine | 6000 / 2400 | 1024 / 512 | 15 |
| `associate` | intro 2, curtain | preview + refine | 6000 / 2400 | 1024 / 512 | 15 |
| `office_cat` | the mascot on the firm's front step in the diorama (LOD1, Low LOD2 + 512 texture), intro 1, icon | preview + refine | 4012 / 1200 / 500 | 1024 / 512 | 15 |

Ledger: `~/.config/meshy/ledger.json` on the machine that ran it (never in git). Textures checked by eye: no text or
logos (the judge has no crest; the associate's briefcase is plain). Budget for this game: ≤ 150 credits.

Files: `unity/Assets/Art/Meshy/<name>_lod{0,1}.fbx` + `<name>_base.png` (1024) + `_base_low.png` (512); the cat's game
LODs in `unity/Assets/Resources/kit/office_cat_lod{1,2}.fbx` + `office_cat_base(_low).png`. Pipeline: `tools/meshy/meshy.py`
→ `tools/meshy/optimize.py` (LOD0 only: Blender 4.0's glTF exporter writes empty decimated LODs, as in Bottle Route) →
`tools/art/glb2fbx.py` (LOD0) and `tools/art/glb2fbx_lod.py` (decimated LODs straight to FBX).

**Crowd lesson applied:** staff and clients in the diorama are ~25 px — chunky canonical figures (`MeshKit.Figure`,
GPU-instanced, one palette swatch per role, a manila folder for staff), ≤ 30 people and half the walkers at 15 Hz on Low.
The Meshy people appear only large (portraits, the hearing scene, pager art). The hearing scene uses fixed-pose portraits
(plan §8 Low plan: pose cross-fades, no skeletons).

## Blender kit + UI renders

`tools/art/kit.py` (Blender 4.0 headless) builds 11 props on the palette UVs → `unity/Assets/Resources/kit/*.fbx`
(2 864 tris in total; 22 props / 5 908 tris after the polish pass below): `desk` 308, `reception` 128, `bookshelf` 416, `coffee` 184, `cabinet` 288, `conference` 416,
`boxes` 144, `board` 116, `courthouse` 736 (columns, stepped plinth, a blank pediment), `lamp` 80, `bench` 48.
`RegionWorld` loads them from `Resources/kit` and falls back to the procedural `MeshKit` versions when one is missing.
Palette: `unity/Tools/make_palette.py` (navy firm brand, wood, paper, pale stone; cosmetic swatches for the office styles).

`tools/art/render_ui.py` renders, from the repo's own models only: `Resources/ui/coach_counsel.png` (guide portrait),
`hearing_judge.png` + `hearing_client.png` (hearing scene), `intro_1..3.png` (pager), `curtain.png` (boot),
`Art/Icon/icon_full.png` + `icon_adaptive_fg.png` — a **placeholder** icon (the counsel with the office cat); the owner
picks the final icon at P7 (`DocketIcons` still needs `icon_adaptive_bg / round / legacy`).

## Polish pass (2026-11-02, cloud) — kit street + tower, merged meshes, store art

**New Blender kit props** (`tools/art/kit.py`, palette UVs, bevelled boxes / faceted crowns, one material), tris each:

| Prop | Replaces | Tris | Tier |
|---|---|---|---|
| `townhouse_a` (3 floors, cream, gabled red roof, chimney) | procedural harbour-house boxes | 518 | all |
| `townhouse_b` (2 floors, brick, gabled grey roof) | 〃 | 362 | all |
| `townhouse_c` (4 floors, blue, flat roof + cornice + AC) | 〃 | 612 | all |
| `tree` (trunk + 3 faceted crowns) | 3-box tree | 80 | all |
| `floor_shell` (slab with concrete edge, back wall with 3 framed windows + mullions, front pilasters, trimmed lintel, baseboard) | 6 flat boxes per floor | 404 | all |
| `roof_top` (parapet, 2 AC units, water tank, stair hut) | one grey slab | 296 | all |
| `entrance` (Garage Front step, doormat, planter, shutter box) | step box | 164 | all |
| `office_decor` (2 potted plants, water cooler, wall clock — no numbers) | — | 256 / floor | Medium/High |
| `planter`, `hydrant` (street clutter) | — | 84, 88 | Medium/High |
| `skyline` (plain distant tower silhouettes, no landmarks) | — | 180 | Medium/High |

Kit total: 22 props, 5 908 tris (was 11 / 2 864). Contact sheet: `blender -b --python tools/art/kit.py -- unity <sheet.png>`.

**Merging (the Low draw saving).** `RegionWorld` now merges every static kit prop into three meshes: **street** (houses,
trees, lamps, benches + Medium/High clutter and skyline), **tower** (floor shells, roof, sign, entrance, reception,
all desks + Medium/High office decor) and **rooms** (every built room). Office styles recolour the merged floor shells by
swatch remap (`MeshKit.Append`: CREAM → the style's wall, WOOD trim → gold on Art Deco, CARPET → wood on the ground
floor). Kit FBX are imported **readable** (`DocketArtImport` v2, `Resources/kit/` only; the Meshy characters stay
non-readable); a model that is not readable yet falls back to its own object, a missing model to the procedural
`MeshKit` version at the same place. Build-time only, no per-frame work.

| Static draws (Low, no shadows) | Before | After |
|---|---|---|
| Chapter 1 (5 desks, 3 rooms) | street 1 + lamps 7 + benches 2 + tower 1 + reception 1 + desks 5 + rooms 3 + court 1 + cat 1 = **22** | street 1 + tower 1 + rooms 1 + court 1 + cat 1 = **5** |
| Chapter 3 (18 desks, 6 rooms) | **38** | **5** |

People (instanced `Crowd`) and the UI are unchanged. Low triangles: +~5 k in total (street ~0.5 k → ~4.5 k, tower shell
~0.2 k → ~1.9 k; desks unchanged) on a scene far below the 300 k budget; Medium/High add ~1.8 k more (decor, clutter,
skyline), still 0 draws. No new lights, particles or animations on any tier.

**FBX facing fixed.** The kit FBX carry Blender's Z-up data under a −90° X root rotation (read from the files: Model
`Lcl Rotation` −90, Z-up vertices), and Blender's front (−Y) lands on Unity +Z. The old `Prop()` overwrote the root
rotation (models would lie on their backs) and assumed front = −Z. Now every kit placement keeps the importer's root
rotation and turns `KitYaw` = 180° more, so kit and procedural fallbacks share one convention (front = −Z at yaw 0,
towards the camera); the reception now faces its queue (yaw 0) and desks keep the chair on the staff side. The Meshy cat
keeps its root rotation too. **Still verify in the editor** (first Unity open): courthouse columns face the street,
desk chairs face the back wall, the cat looks at the street.

**Store art** (`tools/art/render_store.py`, EEVEE + AgX, own models only, no text): `store/feature-graphic.png`
(1024×500, the cast and the office cat in front of the courthouse at dusk; the wordmark + tagline overlay comes from
`store/screenshots/en.json` at capture time) and three **icon candidates** (512×512, full-bleed, no text, checked at
48 px):

| Candidate | What | Reads at 48 px as |
|---|---|---|
| `store/icon-candidates/c1.png` | the office cat with its round glasses, close-up on navy | orange cat face + glasses (strongest) |
| `store/icon-candidates/c2.png` | the counsel (head + shoulders, smiling, grey bob + glasses) on warm amber | a lawyer portrait |
| `store/icon-candidates/c3.png` | the cat on the courthouse steps at dusk | courthouse columns + small cat |

The placeholder `unity/Assets/Art/Icon/icon_*.png` stays until the owner picks one (then re-render it at 1024 / 432 and
add `icon_adaptive_bg / round / legacy`). UI renders (`render_ui.py`) are unchanged: the kit props they use (reception,
desk, bookshelf, cabinet, courthouse) did not change.

Still to do: confirm the facing note above in the editor, window
reflections for High (baked cubemap, plan §8), paper particles on Medium/High, a real Low art gate (draws / tris
measured in the editor).
