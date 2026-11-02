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
merged into one mesh per district (cheap on Low).

`tools/art/render_ui.py` renders from the repo's own models: `Resources/ui/coach_chef.png`, `intro_1..3.png`,
`curtain.png`, `Art/Icon/icon_full.png` + `icon_adaptive_fg.png` — a **placeholder** icon (the owner picks the final one
at P7; the build still needs `icon_adaptive_bg / round / legacy`). Palette: `unity/Tools/make_palette.py` (44 swatches,
street-food poster colours).

Still to do at P2: truck cutaway close-up view (plan §8 camera 2), steam cards as real sprites, livery / awning cosmetic
meshes (today: tint), string lights for evening, a measured Low draw / tris pass in the editor (targets: city ≤ 80 draws).
