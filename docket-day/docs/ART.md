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
(2 864 tris in total): `desk` 308, `reception` 128, `bookshelf` 416, `coffee` 184, `cabinet` 288, `conference` 416,
`boxes` 144, `board` 116, `courthouse` 736 (columns, stepped plinth, a blank pediment), `lamp` 80, `bench` 48.
`RegionWorld` loads them from `Resources/kit` and falls back to the procedural `MeshKit` versions when one is missing.
Palette: `unity/Tools/make_palette.py` (navy firm brand, wood, paper, pale stone; cosmetic swatches for the office styles).

`tools/art/render_ui.py` renders, from the repo's own models only: `Resources/ui/coach_counsel.png` (guide portrait),
`hearing_judge.png` + `hearing_client.png` (hearing scene), `intro_1..3.png` (pager), `curtain.png` (boot),
`Art/Icon/icon_full.png` + `icon_adaptive_fg.png` — a **placeholder** icon (the counsel with the office cat); the owner
picks the final icon at P7 (`DocketIcons` still needs `icon_adaptive_bg / round / legacy`).

Still to do at P2: verify FBX axes / desk facing in the editor, tower floor kit pieces (today procedural), window
reflections for High (baked cubemap, plan §8), paper particles on Medium/High, a real Low art gate (draws / tris
measured in the editor).
