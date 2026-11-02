# Volt City — P2 art hand-over (Patron agent, 2026-10-27)

**Engine change (owner decision, relayed by the Patron mid-phase):** Volt City targets **Unity 6 LTS, URP, Android,
portrait**. The Blender pipeline stays (deterministic build, `validate_art.py`, denylist, budgets); output is now FBX
(+ GLB twin + sidecar JSON) under `unity/Assets/Art/Models/<group>/`, the shared static shader is URP HLSL/ShaderLab
under `unity/Assets/Art/Shaders/`, the Godot MultiMeshes become GPU-instanced batches. **Unity is not installed on the
Mini yet**: the shaders and C# are written against URP 17 / Unity 6 and are compiled for the first time in P3; the
frames below are Blender Eevee renders of the same models with the shader rules rebuilt as node materials.

## What is in the repo

| Path | What |
|---|---|
| `blender/vckit.py` | palette, material codes, bmesh builder (Model), baked AO, two data UVs, FBX / GLB-safe export |
| `blender/layout.py` | the fictional coastal city plan: 8 districts, hubs, 6 plant sites, river / coast / reservoir, terrain height, 16 corridors, 13 roads |
| `blender/kit_buildings.py` | modular building kit (22 pieces) |
| `blender/build_art.py` + `build.sh` | builds the 36 models -> FBX + GLB + sidecar, `layout.json`, `manifest.json` (shot-lock, alarm 900) |
| `blender/validate_art.py` | stdlib gate: hashes, one mesh / primitive / material, COLOR_0 + 2 UVs, budgets, denylist on file / node / mesh / material / FBX strings / palette keys + planted-name proof |
| `blender/perf_budget.py` | static per-tier draw / primitive line (Unity perf gate replaces it in P3) |
| `blender/render_frames.py` + `frames.sh` + `sheets.py` | Eevee frames, compare sheets, rendered-pixel colour check |
| `blender/splash_art.py`, `make_textures.py` | own splash + curtain art; the tileable world-surface texture |
| `unity/Assets/Art/Models/**` | `world/` terrain + sky + 8 districts + grid · `plants/` 18 · `grid/` 2 substations · `props/` 5; `layout.json`, `manifest.json` |
| `unity/Assets/Art/Shaders/` | `VcWorld` (Medium/High) + `VcWorldLow` over `VcWorldCore.hlsl`; `VcProp` + `VcPropLow` over `VcPropCore.hlsl`; `VcBlades`; `VcCommon.hlsl` (globals) |
| `unity/Assets/Art/Editor/VcArtImport.cs` | import settings for every model folder + axis check against the sidecar bounds + texture settings |
| `unity/Assets/Art/Runtime/` | `VcCameraFit`, `VcLightCurves`, `VcInstancedProps`, `VcCityArt` (P3 starting point) |
| `unity/Assets/Art/Textures/surface.png`, `Splash/{splash,curtain}.png` | 256 px data texture; 720 x 1560 splash / curtain |
| `docs/ART-IDS.md` | every model id, tris, material, tier visibility, cosmetic overrides, codes, corridors |

## Exit gates

| Gate | Result |
|---|---|
| Two Blender builds = identical manifest hashes | **PASS** — manifest sha256 `c517c661…f57b25` on two fresh builds (= the committed files; FBX header time + FBX UUID hashing pinned in `build_art._deterministic_fbx`) |
| Denylist 0 | **PASS** — `P2_ART_PASS: 36 models (fbx+glb+sidecar), 28,629 triangles, 3.77 MB, names clean (46 entries), proof 7/7` |
| Perf Low ≤ 17 draws, ≤ 84k prims | **PASS (static)** — `PERF_BUDGET low start draws=15 prims=40,482` · `low late draws=16 prims=44,703` (medium start 29 / 100,422 · medium late 31 / 108,864, limit 40 / 160k) |
| No per-frame CPU on Low | by construction: rotors spin in the vertex shader, marker pulse / tree sway / smoke are Medium-only vertex code, ripple / corridor / plant / light values are written only on change; the only per-frame work is the 5 `RenderMeshInstanced` submits of cached arrays (`VcInstancedProps.Draw`) — measure in P3 with the Unity profiler |
| Frames 1080 x 2340 Low + Medium, dusk / night / storm, sheets | **PASS** — `/Volumes/BuildSSD/game-builds/volt-city/p2/` |
| Rendered-pixel colour check | **PASS** — `PIXEL_CHECK: 12 frames, 0 failures` (`pixel_check.json`): not flat, lit warm windows, cyan network, luminance band, no real-utility logo colour pair |

Low draw groups (one draw each, whole city visible): terrain + sky 1 · districts 8 · plants (built set combined) 1 ·
grid (lines + 10 substations combined) 1 · wind rotors 1 · trucks 1 · cars 1 · fault markers 1 · trees 1 = **16**
(15 at the start: no wind farm yet). Sky dome merged into the terrain draw (17 avoided).

## Frames (`/Volumes/BuildSSD/game-builds/volt-city/p2/`)

- `low/{dusk,night,storm,ripple}.png`, `medium/…`, `before/…` (1080 x 2340; Low rendered at 0.85 and upscaled like
  the game's 3D scale), `medium/city.png` (whole-city overview).
- `compare-<moment>.png` = before (plain import look: vertex colour only) | after Medium | after Low.
- `tiers.png` = Low row / Medium row.
- Moments: **dusk** = start view (Old Harbor, Riverside, Market Square), Riverside mid-ripple; **night** = Riverside,
  Market, Tech Park, Airport Flats not hooked up (dark), Tech line overheating (amber); **storm** = wet, River line down
  (red), Riverside dark, Stadium line overheating, crews (bucket truck) at two fault markers; **ripple** = Riverside
  close-up with the wave half way out from the substation.
- Low vs Medium in the Blender frames: Low = no shadows, no specular, 0.85 internal scale, half the trees, 20 cars;
  per-vertex lighting itself is not reproducible in Eevee (P3 shoots the real Unity Low tier).

## Unity notes for P3

1. **Import** — `VcArtImport.cs` handles every FBX under `Assets/Art/Models`: Mesh Compression Off, Read/Write On,
   no lightmap UVs (UV1 is data), no materials, no tangents, bake axis conversion, then compares the imported bounds
   with the sidecar and fixes a rotated / mirrored convention (logs it) or fails loudly. Player Settings > Vertex
   Compression: untick TexCoord0 + TexCoord1. Do **not** install a glTF package (the `.glb` twins would import twice).
2. **Colour** — project in Linear space; vertex colours are sRGB-encoded and decoded in the shader (`VcColourIn`).
3. **Materials** — one material per tier and family: `World_Terrain` / `World_Grid` / `World_Plants` + 8
   `District_<id>` instances (VoltCity/World or WorldLow), `Prop_<kind>` (VoltCity/Prop or PropLow, GPU instancing
   ON), `Blades`. City-wide values are `Shader.SetGlobal*` (night, wet, sky, corridor states, plant outputs, battery,
   reservoir, cosmetics) — written by `VcLightCurves.Apply` / `VcCityArt.Set*` only when they change.
4. **Instancing plan (was MultiMesh)** — `Graphics.RenderMeshInstanced` per kind with a MaterialPropertyBlock holding
   `_InstCustom` / `_InstColor` arrays (instancing buffer in `VcPropCore.hlsl`), rewritten only when an instance
   changes; `_InstColor` always white; custom values small and bounded (no absolute time). ≤ 511 instances per batch
   (GLES3-safe). Trucks: `_InstCustom.x` = boom raise; cars: x = shape (hatch / van / city car), y = paint 0-7; markers:
   x = pulse phase; rotors: x = rad/s, y = phase. Trees: tint from the instance position hash.
5. **Shadows** — Medium: main-light shadows (one cascade at 2.5-3 km) on districts, plants, grid and props; terrain
   receives only. Low: VcWorldLow has no ShadowCaster pass, so a shadow setting can never add draws there.
6. **Camera** — `VcCameraFit.Apply(cam, litDistrictBoxes, dioramaRect)`: 55 deg tilt, 35 deg vertical FOV, near 20 /
   far 9000 (depth-safe for the 0.2-0.35 m pavement / street lifts at 2 km), distance clamped 260-2600 m.
7. **Light curves** — `VcLightCurves` day / dusk / night / storm (sun, ambient ≥ 0.62 floor, exponential fog, sky
   gradient, window glow); lightning = `Flash()` on storm events. Values identical to `render_frames.CURVES`.
8. **Splash** — Player Settings > Splash: Unity splash off (optional on every licence since Unity 6), no engine logo,
   background `#0A1721`, then the own `Splash/splash.png` frame; the loading curtain uses `Splash/curtain.png` under
   the indicator. Check on a real device / emulator, not only the editor.

## Open (P2 → P3)

- First Unity compile of the shaders + C# (written without a Unity install) and the real perf gate on the Low tier
  (draws / triangles / frame time on the 2 GB emulator profile) replacing `perf_budget.py`.
- Unity-rendered Low + Medium frames (the Eevee frames prove the art and the shader rules, not URP itself);
  Patron visual approval against the LOOKDEV bar.
- Window AA: the URP shader box-filters windows with `fwidth`; check for shimmer on device at the full-city zoom.
- Rain / smoke particles (Medium only), heat shimmer + sparks (High) — not in P2.
- P1 line ids → corridor indices table (ART-IDS) once P1's `data/lines.json` lands; road graph for trucks / cars
  (`layout.json roads[].centre`, lanes ±1.8 m) in P3.
- Onboarding hero frames (P3, need the real loop) and icon candidates (P6).
- Meshy: 0 credits used.
