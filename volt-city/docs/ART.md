# Volt City — art plan (F0, Patron 2026-10-27)

> Binding: CLAUDE §10 (tiers, **Low = 0 extra draws** rule of 10-24), playbook sections 10-12, [`../../docs/LOOKDEV_STANDARD.md`](../../docs/LOOKDEV_STANDARD.md)
> (the "Kaiten bar": the difference is visible at first glance), LL A (performance) and the Grid Boss P2 lessons. Newest Blender kit to
> copy: `games/pit-wall/blender/pwkit.py` + `build_art.py` + `validate_art.py` (vertex colour = palette × baked AO, alpha = material code,
> denylist scan of node/material names). Blender = `/Volumes/BuildSSD/blender/blender -b -P blender/<script>.py` under `shot-lock.sh`
> with `alarm 900` (never `/Applications/Blender.app` on the Mini). At most 3 heavy GPU jobs on the Mini at once.

## 1. Look

Calm night console over a toy-scale city. Warm amber windows and street lights against a deep navy-teal dusk; cyan for power flow;
red/amber only for trouble. The **environment is what reads** (LOOKDEV): the city blocks, the light pattern, the coast and river
are the hero; crews and cars are chunky readable tokens, not detailed people. Time of day = directional light + ambient + sky gradient
curves (0 draws). Night is never so dark that decor disappears (LL Doner: pale "dark scene" hid decor — keep ambient ≥ the P2 floor).

Signature effect: **district light ripple** — when a district goes dark/lights up, windows switch in a 0.6 s wave from the substation
outward. Implemented as a per-vertex "distance from substation" value baked into the district mesh (vertex colour alpha channel /
UV2.x) and **one shared shader** that compares it with a per-district uniform `wave_t` → no extra draw, no extra pass, the same shader
branch on every tier (Low gets the same wave, it costs nothing). If the Compatibility renderer refuses per-instance uniforms, fall
back to swapping between two shared materials (lit/dark) without the wave on Low — decided in P2 by measurement, written into ART-IDS.

## 2. Asset list

Everything is **Blender** (buildings, plants, grid, vehicles are modular hard-surface). **Meshy: 0 credits planned** — crews are
~20-30 px on screen in the portrait diorama, where Meshy figures read worse than chunky Blender figures (LL / LOOKDEV, Pit Wall used 0).
The only Meshy candidate is an optional lineman portrait for the fault card close-up (≥ 100 px) — only after a Patron-approved frame
shows a Blender figure is not good enough; ≤ 2 tasks (~60 credits), logged in `~/.config/meshy/ledger.json` through `meshy.py`, then
`games/tools/meshy/optimize.py`.

| # | Asset (GLB id) | Count / variants | Tri budget (one copy) | Notes |
|---|---|---|---|---|
| 1 | `terrain_city` coast + river + hills + roads | 1 | 9k | world-position surfaces in the shared static shader (asphalt, grass, sand, water) — zero geometry for texture detail |
| 2 | `district_<id>` merged block (buildings + street lights) | 8 | 4-7k each (≤ 46k all) | per district one mesh, windows as vertex-colour emissive quads, ripple distance baked |
| 3 | `bld_*` modular kit (warehouse, row house, villa, shop, office tower, data hall, stadium, hospital, terminal, crane) | ~24 pieces | 150-900 | assembled by script into #2; never placed one by one at runtime |
| 4 | `plant_steam` Old Smoky (3 levels) | 3 | 1.8k | chimney smoke = Medium+ particles; Low = static puff card in the same mesh |
| 5 | `plant_peaker` gas turbine hall (3 levels) | 3 | 1.2k | heat shimmer Medium+ only |
| 6 | `plant_hydro` dam + reservoir level (3) | 3 | 2.2k | reservoir height = vertex offset by uniform (0 draws) |
| 7 | `wind_tower` + `wind_blades` | up to 24 turbines | 180 + 120 | towers merged; blades **one MultiMesh** (1 draw) rotated by sim (written only when speed changes, 15 Hz on Low) |
| 8 | `solar_field` panel rows (3 sizes) | 3 | 1.5k | panels as one mesh; sun glint Medium+ |
| 9 | `battery_park` containers (3) | 3 | 900 | charge level = emissive strip via vertex code + uniform |
| 10 | `substation` (normal / smart) | 10 | 600 | smart = extra antenna + cyan ring |
| 11 | `pylon` + `line_span` | ~60 pylons | 140 | all pylons + spans of the network **merged into one mesh** (1 draw); a line's state (normal/overheat/down) via per-vertex corridor id + a small uniform array (≤ 16 corridors) |
| 12 | `truck_crew` (yellow utility truck, bucket up/down) | ≤ 5 | 600 | **MultiMesh** (1 draw); `use_colors = true` + white per instance (LL W68: custom data without colours = black on Android) |
| 13 | `car_*` traffic (3 shapes) | ≤ 40 (Low ≤ 20) | 200 | MultiMesh 1 draw, `coddy_anim` RoadGraph/VehicleMover, never overlap (`overlap_probe` worst_depth 0) |
| 14 | `fault_marker` spark + ring | ≤ 8 | 60 | MultiMesh 1 draw; Low = static icon card, Medium = pulse |
| 15 | `trees` | ~300 | 60 | MultiMesh 1 draw (Low: ≤ 50 % count, far trees hidden) |
| 16 | `sky_dome` gradient + stars | 1 | 400 | unshaded vertex colour; storm = colour curve, lightning = light energy flash (0 draws) |
| 17 | Splash + loading curtain art | 2 frames | — | rendered from the game (P2), own art, **no Godot logo** |
| 18 | Onboarding hero frames | 3 | — | real earned states captured by the game's bot (onboarding v2), ≤ 768 px, total ≤ 1 MB |
| 19 | Icon candidates | ≥ 4 + 48/96/192/512 sheet | — | **none installed**; Patron picks |
| 20 | Cosmetics (IAP.md): truck liveries ×2, pylon style, turbine blades, skyline window colour, control-room UI theme | 6 | ≤ the default's tris | every sold look is drawn on the default model, never worse than default, visible on Medium **and** canonical on Low (test) |

Textures: tileable ≤ 256 px procedural detail sampled by world position with mipmaps (`filter_linear_mipmap_anisotropic`, `.import`
`mipmaps/generate=true`); no per-cell hash textures (they shimmer; LL Grid Boss). Atlases ≤ 1024 px (512 px Low variants).
New GLB folders: run import → `python3 tools/glb_import_settings.py` → import again (white models otherwise; vertex-colour models need
the post-import script per folder, guarded in `check.sh` — LL A10c, Disaster W66 Low 24 → 85 draws).

## 3. Budgets per tier (perf_gate, Low profile is the release gate)

| | Low (weak/unknown) | Medium (strong) | High (flagship) |
|---|---|---|---|
| 3D draw calls (whole city visible) | **≤ 16** | ≤ 40 (shadows double the casters) | ≤ 60 |
| UI draw calls per screen | ≤ 45 (pill rows → ActionText) | ≤ 55 | ≤ 55 |
| Total draws | ≤ 61 (gate 150) | ≤ 95 | ≤ 115 |
| Visible primitives | **≤ 84k** | ≤ 160k | ≤ 220k (gate 300k) |
| Static memory | ≤ 180 MB (gate 250) | ≤ 220 MB | ≤ 240 MB |
| Shadows / SSAO / glow | none | simple directional shadow | full shadow + window glow pass (only where the MSAA leak guard allows) |
| 3D scale / AA | 0.85, **no MSAA** | 1.0 + MSAA 2x | 1.0 + MSAA 4x (`PerfProfiles.apply_core` → `guard_msaa`) |
| FPS | 30 cap | 60 | 60 |
| Traffic / trees / particles | ≤ 50 %, 15 Hz updates, no particles (smoke = static card) | full, rain + smoke particles | + sparks, heat shimmer |
| Materials | per-vertex, no specular (`low_shading.gd`), flat multi-material models merged (`low_merge.gd`) | per-pixel | per-pixel |

**Low draw count, line by line (target 16):** terrain 1 · districts 8 · plants (all built plants merged per level-set rebuild) 1 ·
wind blades MultiMesh 1 · pylons+lines 1 · trucks 1 · cars 1 · fault markers 1 · trees 1 = **16**. Sky dome is part of the clear
colour/terrain draw where possible (else 17 — written in the P2 hand-over).

**Low = 0 extra draws rule (owner 10-24, binding).** Every new visual after P2's baseline is Medium/High-only unless it adds **0 draws**
(at most +1 with a written reason), **≤ +5 % primitives**, no new shader branch, no extra render pass, no extra per-frame CPU. Every
hand-over carries a Low before/after perf_gate line, e.g. `Low prim 71,240→72,010 (+1.1 %) draw 16→16`. Crowds/traffic/particles on
Low: capped and at 15 Hz. MultiMesh instances are written only when they change (LL A5); never mirror MultiMesh instances; split any
map-sized MultiMesh into ≤ 128 m cells (LL A4, the city is ~600 m wide → trees and cars use 4-6 cells).

**Camera:** portrait, ~55° tilt, perspective FOV 35°, fits the **lit districts' boxes** (not the scene bounds), clamped pan/zoom; a
zoom-out never goes past the readability floor or the Low budget (LL Sky Tower). Follow-cam on a driving truck while its fault card is open.

## 4. P2 deliverables + evidence

- Deterministic build: two Blender builds = identical manifest hashes (`blender/manifest.json`).
- `docs/ART-IDS.md` (every GLB id, tris, material code, tier visibility, which cosmetic overrides it).
- Frames at phone size 1080×2340 for **Low and Medium**, dusk + night + storm, before/after compare sheets; fixed-size SubViewport
  captures (check PNG size; free the scene on every code path).
- perf_gate Low line inside the budget above; rendered-pixel colour check (no real utility logo colour pairs; crews' truck yellow is
  generic); `validate_art.py` denylist 0 on GLB node/material names and file names.
- Own splash (`boot_splash/show_image=false` + own curtain, or own image + matching bg colour) checked on the emulator, not only the editor.
