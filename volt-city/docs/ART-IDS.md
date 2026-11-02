# Volt City — art ids (P2, 2026-10-27)

Source of truth: `unity/Assets/Art/Models/manifest.json` (written by `blender/build_art.py`; triangles, budget,
Unity bounds, sha256 of every FBX / GLB / sidecar). Every model = **one mesh, one material**, vertex colour = palette x
baked AO (sRGB-encoded), **alpha = material code**, UV0 / UV1 = data channels. Each model has `<id>.fbx` (the Unity
import), `<id>.glb` (reference / validation twin) and `<id>.json` (sidecar: material, palette key -> code + colour, UV
meaning, expected Unity bounds). Space: Unity, X east, Y up, Z north, metres; the camera sits south looking north.

## Models

| id | folder | tris | budget | material / shader | Low | Medium / High | cosmetic overrides |
|---|---|---|---|---|---|---|---|
| `terrain_city` | world | 9,990 | 10,500 | vc_world, `_MeshKind` 0 | yes (one draw with the sky) | yes | — |
| `sky_dome` | world | 144 | 400 | vc_world 0 (code 4, unlit gradient + stars) | merged into the terrain draw | same | — |
| `district_old_harbor` | world | 2,078 | 6,500 | vc_world 1, own `_Ripple` | yes | yes + shadows | `skyline_neon` (windows) |
| `district_riverside` | world | 1,604 | 6,500 | vc_world 1 | yes | yes | `skyline_neon` |
| `district_hilltop` | world | 1,264 | 6,500 | vc_world 1 | yes | yes | `skyline_neon` |
| `district_market_square` | world | 1,856 | 6,500 | vc_world 1 | yes | yes | `skyline_neon` |
| `district_tech_park` | world | 1,062 | 6,500 | vc_world 1 | yes | yes | `skyline_neon` |
| `district_stadium_row` | world | 1,038 | 6,500 | vc_world 1 | yes | yes | `skyline_neon` |
| `district_hospital_hill` | world | 1,108 | 6,500 | vc_world 1 | yes | yes | `skyline_neon` |
| `district_airport_flats` | world | 1,274 | 6,500 | vc_world 1 | yes | yes | `skyline_neon` |
| `grid_lines` | world | 1,564 | 7,000 | vc_world 2 (UV0.x = corridor index) | yes (one draw with the substations) | yes + shadows | `pylons_deco` (code 17 bands) |
| `substation` / `substation_smart` | grid | 214 / 318 | 300 / 420 | vc_world 2 (UV0.x = -1: never hidden) | merged into the grid draw (10 copies) | same | — |
| `plant_steam_l1..3` | plants | 198 / 308 / 430 | 1,800 | vc_world 3 (UV0.y = type 0 + .5) | built set merged = one draw | + animated smoke | — |
| `plant_peaker_l1..3` | plants | 150 / 238 / 326 | 1,200 | vc_world 3 (type 1) | merged | same | — |
| `plant_hydro_l1..3` | plants | 135 / 163 / 191 | 2,200 | vc_world 3 (type 2; code 9 reservoir level) | merged | same | — |
| `plant_wind_l1..3` | plants | 304 / 608 / 912 | 4,400 | vc_world 3 (type 3): 8 / 16 / 24 towers | merged | same | — |
| `plant_solar_l1..3` | plants | 44 / 68 / 116 | 1,500 | vc_world 3 (type 4) | merged | same | — |
| `plant_battery_l1..3` | plants | 68 / 116 / 164 | 900 | vc_world 3 (type 5; code 13 charge strip) | merged | same | — |
| `wind_blades` | props | 12 | 60 | vc_blade (VoltCity/Blades), instanced | one batch, spun in the vertex shader | same | `blades_candy` (`_BladeStyle` 1) |
| `truck_crew` | props | 294 | 600 | vc_prop, `_PropKind` 0, instanced | one batch (≤ 5) | + shadows | `livery_sunset`, `livery_arctic`, `theme_gold` (`_LiveryA/B`) |
| `car` | props | 156 | 200 | vc_prop 1 (3 shapes + 8 paints from `_InstCustom`) | one batch, ≤ 20 | ≤ 40 | — |
| `fault_marker` | props | 72 | 72 | vc_prop 2 | one batch (≤ 8), static | pulse | — |
| `tree` | props | 42 | 70 | vc_prop 3 | one batch, every 2nd layout tree (278) | all 556 + sway | — |

Cosmetics are uniforms / globals on the default models (IAP.md: every sold look visible on Medium **and** on Low,
never worse than default): `livery_*` / `theme_gold` = `_LiveryA/_LiveryB` on the truck material, `blades_candy` =
`_BladeStyle`, `skyline_neon` = global `_VcSkylineStyle`, `pylons_deco` = global `_VcPylonDeco`; `room_retro` /
`theme_gold` UI themes are not art-pipeline items.

Modular building kit (Blender-only builders in `blender/kit_buildings.py`, assembled into the district meshes, never
placed at runtime; reference-size triangles from the manifest `kit` section): block 20 · gable_house 16 · row_houses
94 · apartment 52 · shop_row 88 · tower 50 · warehouse 42 · harbour_crane 84 · containers 84 · data_hall 60 · stadium
178 · hospital 134 · terminal 12 · control_tower 86 · hangar 34 · market_hall 56 · villa 62 · lamp 20 · parking ·
villa_pool · cargo_ship · airliner.

## Material codes (vertex alpha, index = round(a x 20))

World (`VcWorldCore.hlsl`): 20 matte · 18 gloss · 17 pylon steel · 16 road asphalt (UV0 = lateral m, m along: lane
marks) · 15 district street (UV0.x ripple, UV0.y baked lamp pool) · 14 ground (height picks water / sand / grass /
rock, slope -> rock) · 13 battery strip (UV0.x along vs `_VcBatteryCharge`) · 12 roof · 11 smoke puff (hidden when the
plant's output is 0; animated on Medium) · 10 water · 9 reservoir (lowered by `_VcReservoirLevel`) · 8 window wall
(windows painted from world position: UV0.x ripple, UV0.y building seed, UV1 = (style, base height)) · 7 district
lamp (ripple) · 6 always lit (on plants gated by `_VcPlantOn[type]`) · 5 conductor (`_VcCorridorState[UV0.x]`: normal
cyan flow glow / overheat amber (pulse on Medium) / down red / not built = collapsed) · 4 sky · 3 cyan flow accent ·
1 foliage.
Props (`VcPropCore.hlsl`): 20 matte · 18 gloss · 17 livery primary · 16 livery secondary · 19 car paint · 6 lights
(head / tail / beacon / marker, brighter at night) · 1 foliage.

Window styles (UV1.x; `kit_buildings.STYLE`): 1 home · 2 office ribbon · 3 curtain glass · 4 warehouse clerestory ·
5 shop (tall glazed ground floor) · 6 data hall LED strips (cyan-white, 96 % lit) · 7 clinic · 8 villa · 9 terminal ·
10 stadium concourse.

## Light ripple (the signature effect) — decision

Per-vertex "distance from the district substation" (UV0.x, 0..1 over the district's ripple radius, `layout.json`
`ripple_radius`) + one shared shader comparing it with the district's `_Ripple = (front, target)`; a window flips when
`u + 0.12 x hash(window) < front`, so the switch sweeps out from the substation in 0.6 s with a per-window shimmer. The
**same code runs on every tier** (Low included: it costs nothing). Unity mechanism chosen: **one material instance
per district** (same shader + variant, SRP Batcher compatible, still one draw each). The Godot-era "two-material
fallback" is not needed; if P3 measures a problem with 8 material instances, the fallback is a MaterialPropertyBlock per
district renderer (one draw each, those 8 renderers leave the SRP Batcher) — record the measurement here.

## Corridors (grid_lines UV0.x; `_VcCorridorState[16]`)

| idx | id | from → to | | idx | id | from → to |
|---|---|---|---|---|---|---|
| 0 | harbor_line | west_hub → old_harbor | | 8 | steam_tie | Old Smoky → west_hub |
| 1 | river_line | old_harbor → riverside | | 9 | peaker_tie | peaker → east_hub |
| 2 | market_line | riverside → market_square | | 10 | hydro_tie | hydro → hospital_hill |
| 3 | hill_line | market_square → hilltop | | 11 | wind_tie | wind ridge → hilltop |
| 4 | hospital_line | hilltop → hospital_hill | | 12 | solar_tie | solar → airport_flats |
| 5 | tech_line | market_square → tech_park | | 13 | battery_tie | battery → east_hub |
| 6 | stadium_line | riverside → stadium_row | | 14 | east_loop | east_hub → tech_park |
| 7 | airport_line | stadium_row → airport_flats | | 15 | west_loop | west_hub → stadium_row |

0-7 are the 8 upgradeable city corridors (DESIGN section 6); 8-13 are plant ties (built with their plant); 14-15 close
the two loops. P1's line ids map onto these indices in P3 (one table, no geometry change). State x: 0 normal, 1
overheat, 2 down, 3 not built (vertices collapse); y: flow 0..1.

## Plant types (UV0.y on plant meshes; `_VcPlantOn[6]`)

0 steam (Old Smoky) · 1 peaker · 2 hydro · 3 wind · 4 solar · 5 battery — `layout.json plant_types`.
