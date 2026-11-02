# W67 look-dev standard (pilot: Kaiten Sushi Rush, S3 2026-10-23 · sister pilot: `lookdev/sky-tower-ops.md`)

Goal: "first-generation parity" (corrupt / cinnamon / train level) for the factory games **without** extra cost on weak
phones. Renderer on every Android phone = `gl_compatibility` (no SSAO / SDFGI / volumetrics / sky ambient with a colour
background). Status: **Kaiten round 2 APPROVED (Patron 10-23) and the owner: "Suşi restoranı çok güzel olmuş" — this is the visual bar
for every factory game.** Shipped in Kaiten 1.1.3 (5991e505b4 look, f5bc0c0fe9 chefs/outfits, 9ba46a65ae store art).

Evidence (Kaiten): `/Volumes/BuildSSD/game-builds/w67/kaiten/` — `before/`, `after/` (round 2), `after-r1/` (round 1,
rejected), each `medium/` + `low/` at 1440×3120 (store-capture moments: lunch rush, full belt, pinch close-up, dinner,
late); `compare-<shot>.png` = before Medium | after Medium | before Low | after Low (`r1-compare-*` = round 1). Tool:
`games/kaiten-sushi/tools/lookdev/hero.{sh,gd}` (copy per game; tuning env `LOOKDEV_Q`, `LOOKDEV_SHOTS`, `LOOKDEV_ENV` =
Environment overrides as JSON, `LOOKDEV_VIEW` = view knobs, e.g. `{"pitch": 52, "lamp_energy": 2}`).

## Lesson of round 1 (Patron: "the difference is hard to see")

Round 1 spent the budget on **character / prop detail** (2× segments, real plates: Medium 68k → 229k triangles) and the
frame barely changed — 60 % of the screen was flat wood floor and a grey kitchen. **The environment is what reads.**
Order of work per game: (1) surfaces of the big areas, (2) restaurant dressing, (3) light pools + vignette + grade,
(4) camera, (5) only then model detail — and every step must be visible on **Low** too.

## The two-tier rule (owner 10-23 12:10)

| | Low | Medium / High |
|---|---|---|
| Triangle / draw budget | **owner rule 10-24 (binding, replaces the old +10 %):** extra draws **0** (at most +1, justified in the hand-over), primitives **≤ +5 %**, no new shader branch / render pass, no extra per-frame CPU (traffic / crowds / particles capped and 15 Hz on Low); anything heavier is Medium/High only (tier-gated). perf_gate Low start + late, before/after line in every hand-over | ≤ ~120k in the busy scene (Patron) |
| Surfaces (floor grain, tiles, rugs), dressing, pools, vignette, grade, vertex AO | **yes** — this is where Low improves | yes |
| Models | base GLBs, untouched | HD set (`assets/models/hd/`) where it exists; Meshy chef |
| Shading | per vertex, no specular (`vertex_lighting` shader variant) | per pixel, per-vertex gloss, rim |
| Real Omni / Spot lights, glow, shadows, particles | no | Medium ≤ 2 lamps + glow; High 4 lamps + shadows |

Kaiten round 2 (perf_gate): **Low start 7,266 → 7,782 prims (+7.1 %), 21 → 23 draws (+9.5 %); late 58,741 → 59,309
(+1.0 %), 87 → 89 draws**; the game's §10 play-frame test 59,923 / 60,000. **Medium late 68,181 → 119,709**, 88 → 90
draws. `tools/check.sh` green.

## Recipe (in order of visible gain per cost)

1. **Surface detail from world position, in the shared static shader** (`src/art/kaiten_surface.gdshaderinc`, included
   by `kaiten_flat.gdshader` and its Low variant; only the shop's static placements get `static_mode = 1` via
   `ArtMesh.static_material()` in `_place`): wood grain per 0.3 m plank with a random offset per board, seams and
   board-to-board tone; the floor darkens towards the walls (the light lives at the counter); the kitchen runner becomes
   two-tone tiles with grout + speckle; counter tops get a soft grain. **Zero geometry, zero draws, every tier** — the
   single biggest change on Low. Textures: `tools/lookdev/make_textures.py` (Pillow, deterministic, tileable;
   wood_grain / kilim / tile_noise / mat, all ≤ 155 KB, mipmapped).
   ⚠ Godot shaders may write varyings **only in `vertex()`** and a helper parameter may not share a varying's name — a
   failing ShaderMaterial silently renders as a flat default grey/beige (r2a's first try): `hero.sh` now stops on
   `SHADER ERROR`.
2. **Rugs + dressing as ONE mesh / ONE draw** (`src/game/shop_dressing.gd`): kilim runners on the stool line of every
   loop (stadium band, pattern along the path) + `blender/build_dressing.py` models (bamboo at the frame corners, shrubs,
   andon floor lanterns, a kitchen back shelf with pots / rice tub / boards) merged with their baked AO. The vertex
   colour's alpha is a material code for the static shader: 1 matte, 0 gloss, 0.5 = kilim texture by UV, 0.75 = glowing
   shoji paper (warm emission + a lattice from world position). Not ART-IDs: no gameplay; tagged `lookdev_fx`.
   Budget them hard — Kaiten's Low start had ~600 spare triangles and no spare draw (dressing ≈ 330 tris + runners).
   ⚠ Merging the existing static shop into one mesh to free draws was rejected: the P3/P4 tests count placements per
   art id (`static_root` children) — dress around them instead.
3. **Light pools** (additive radial-gradient quads, one MultiMesh, every tier): small defined pools under pendant-lamp
   positions every 1.5 m along both counter runs + the ends, the chef island, the andon lanterns, the wall lamps;
   strength per phase (lunch 0.18 → late 0.34). Larger / stronger pools wash the cream counter and belt out.
4. **Vignette** on a `CanvasLayer` at layer −1 (`src/art/vignette.gdshader`, `blend_mul`, screen space, warm tint,
   0.22 → 0.48 per phase). ⚠ Not a 3D quad on the near plane: with glow on it darkened the whole frame.
5. **Camera**: pitch 58° → **52°** — the shop seen from a little more inside, people show their fronts, plates still read
   round (`bar_view.pitch`, tunable with `LOOKDEV_VIEW`).
6. **Grade**: **Filmic** + colour ambient, `adjustment_*` contrast 1.06 / saturation 1.10. ACES / AgX burned the light
   wood; sky ambient does nothing in Compatibility with a colour background. Re-test per game (Sky Tower chose AgX).
7. **Steel belt**: the slat shader gets a brushed-steel band per slat (bright middle, dark edges, more specular) — reads
   as metal even per vertex.
8. **Medium only**: HD set (`blender/build_hd.py`: 2× round segments; plates with foot ring / rolled rim; dishes 1.5×;
   stacks always base — up to 240 plates), the **Meshy chef** (#116: textured + rigged, Idle loop, LOD1 1.2k tris, 512
   texture, `assets/models/meshy/chef.glb`; default look only — the sold outfits keep their models), glow, 2 warm lamps.
   ⚠ The Meshy export faces −Z (Kaiten's figures +Z): rotate the rig 180°; it is stockier than the stylised cast (×0.92).
9. **MSAA + post-processing on Android GLES3 leaks a framebuffer per frame in Godot 4.7.2** (Patron 10-23: glow /
   adjustments / scale < 1 with MSAA) — S1's shared guard turns MSAA off there. Build the look from colour, light and
   texture (mipmapped), use bold forms instead of thin geometry, never set MSAA in game code (Kaiten only uses the
   shared `Graphics.apply`).

## Characters (Meshy) — variety, pose, sold cosmetics (Kaiten 1.1.3)

- **One textured, rigged Meshy model per role** (≈ 31 credits incl. rig + 2 clips; `games/tools/meshy`), LOD1 ~1.2k tris +
  512 texture, Medium / High only; Low keeps the canonical vertex-coloured figure.
- **Variety without new models**: one small shader on the Meshy texture (`src/art/meshy_chef.gdshader`) recolours by
  texel class, keeping the shading (luminance ratio): navy (blue-dominant, dark) = apron / trousers, unsaturated bright
  below a world-height line = jacket, above it = cap. Default role: 4 apron tones cycled per instance, ±5 % height,
  idle phase per instance. ⚠ A skinned `VERTEX` is not in metres (the rig's skeleton is in cm) — use world height
  `(MODEL_MATRIX * vec4(VERTEX, 1)).y`.
- **Working pose**: strip the arm tracks from EVERY clip of the model (the AnimationMixer resets any bone that any clip
  of the player animates), then set the arms once with a shortest-arc aim in skeleton space (upper arm down-forward,
  forearm forward). Spine / shoulder sway still moves them; no per-frame cost.
- **Sold cosmetics must never look worse than the default** (Patron 10-23, critical): every sold outfit becomes a
  recolour (or Meshy re-texture) of the same model on Medium / High; a test asserts each look is drawn on the new model
  on Medium and the canonical one on Low (`tests/perf.gd`), and frames prove it (`compare-chef-outfits.png`).
- The Meshy export faced −Z (rotate 180°) and was stockier than the stylised cast (×0.92).
- **Not for tiny crowds:** a Meshy rider at ~25 px (Sky Tower) read worse than the chunky canonical figure — Meshy is
  for characters the camera shows large; for small crowds keep bold, blocky silhouettes and strong colours.
- **Framing for "show more"** (Sky Tower round 2): never past the game's readability floor (a constant on the 720
  canvas — NativeView makes view sizes physical) and never on the scenes where it would blow Low's budget.

## Release checklist after a look-dev (Kaiten 1.1.3)

1. `tools/check.sh` green **including** the kit sync checks (`games/tools/ui`, `games/tools/perf` synced — kit pager
   one-tap 2f27c3cf32 + 7d81b39ec9, coddy_perf flagship → High + shared MSAA guard 32e8496f85).
2. perf_gate Low within the owner's weak-device rule (start AND late: draws +0 (≤ +1 justified), prims ≤ +5 %; before/after line in the hand-over) and the game's own §10 test. After release, compare Play Android vitals (slow / frozen frames) with the previous version; a worse game gets the change switched off on Low.
3. Store screenshots re-shot (`tools/store/capture.sh` → `render.mjs` → strict `validate.py`) — they go STALE with any
   capture-source change; a kit / policy change that does not alter frames may stay STALE (report it).
4. versionName + Play "what's new" in 10 languages (in-session Haiku sub-agent), Jenkins `Games/<game>-mobile`
   build-only (Linux agent: any `check.sh` call into `games/tools` must be `[ ! -f … ] ||` guarded — 9fe4d1ceb3),
   AAB from the job artifacts, `games/jenkins/smoke_android.sh`, then a pager walk-through on the emulator
   (`tools/qa_emu.sh`: one tap per page into the game) → final frame to Patron, who uploads.

## Process per game (≈ 1 session)

1. Copy `tools/lookdev/hero.{sh,gd}`; shoot `before/` (Medium + Low) from a clean `git archive origin/main` copy.
2. perf_gate Low baseline (start + late) → write the ceilings down: draws +0 (≤ +1 justified), prims +5 %. Anything heavier goes to Medium/High only.
3. Surfaces in the static shader → rugs / dressing (one mesh) → pools + vignette + grade → camera.
4. Tune with `LOOKDEV_ENV` / `LOOKDEV_VIEW` on 1–2 shots; after every Blender export: Godot import →
   `python3 tools/glb_import_settings.py` → import again (or the GLBs lack the vertex-colour import script).
5. Model detail only for Medium and only inside its budget.
6. `after/`, `compare-*.png`, perf_gate Low + Medium, `tools/check.sh` → report → Patron approval → onboarding hero
   frames from the approved look; store screenshots re-shot in the release round (they go STALE).

## Not done / next (Kaiten)

- Plate variety (shapes / lids per tier) and customers from Meshy (Patron: re-evaluate the character set with the
  Meshy chef first). The Meshy chef idles with arms down — a work pose / clip would read better at the stations.
- Night phases: the city strip behind the window is still base; lanterns glow only by emission (+ bloom on Medium).
