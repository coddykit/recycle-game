# Charge Nurse — art

| What | Where | How |
|---|---|---|
| Palette (48 swatches, 8×8; polish pass added 46 asphalt, 47 hedge) + 4 theme palettes (modern / retro / playful / gold) | `unity/Assets/Resources/materials/palette*.png` | `python3 unity/Tools/make_palette.py`. A theme = palette swap (0 extra draws). Order = `ChargeNurse.World.Pal` |
| Blender kit (23 props, 4 368 tris total): bay (bed + curtain + monitor), ward bed, desk, waiting chairs, ambulance (plain red stripe, no emblem), X-ray, lab bench, CT, OR table + polish pass (below) | `unity/Assets/Resources/kit/*.fbx` (imported **readable**: static props are merged), sheet `docs/kit_sheet.png` | `blender -b --python tools/art/kit.py -- unity docs/kit_sheet.png` |
| Rooms (back-wall windows), locked-wing blocks (window bands), corridors (heat-map UVs), parking, Low trees, people (chunky instanced figures), fallbacks for every kit prop | code: `Scripts/World/MeshKit.cs`, `HospitalWorld.cs`, `Crowd.cs` | procedural, one palette material |
| Meshy hero characters (renders only, never the crowd — fleet lesson): nurse, doctor, patient, paramedic; LOD0 6k tris, 1024 + 512 textures | `unity/Assets/Art/Meshy/<name>_lod0.fbx`, `_base(_low).png` | `tools/meshy/meshy.py preview/refine` → `optimize.py --height … --lods 6000,2500,900` → `tools/art/glb2fbx.py` |
| Store art: Play feature graphic 1024×500, icon candidates c1–c3 512×512 (no text) | `store/feature-graphic.png`, `store/icon-candidates/c{1,2,3}.png` | `blender -b --python tools/art/render_store.py -- unity store` |
| UI art: coach portrait, intro pager 1-3, boot curtain, placeholder icon | `Resources/ui/{coach_nurse,intro_1,intro_2,intro_3,curtain}.png`, `Art/Icon/icon_full.png`, `icon_adaptive_fg.png` | `blender -b --python tools/art/render_ui.py -- unity` |

## Polish pass (2026-11-02) — building shells + grounds

The most placeholder parts of the diorama were the bare grounds and the grey locked-wing boxes, the 3 m box lift and box
trees. New Blender kit models (bevelled, palette UVs, one material; `tools/art/kit.py`):

| Prop | Tris | Where (HospitalWorld.Rebuild) | Tier |
|---|---|---|---|
| `entrance` porch: canopy on 2 posts, sliding glass doors, mat | 228 | reception's west wall (full-height side wall: hides nothing inside) | Medium / High (Low: 4-box procedural porch, 40 tris) |
| `amb_canopy`: bay roof with red edge stripe, bay doors, 4 bollards | 280 | back of the ambulance bay | Medium / High |
| `lift` core: shaft, two door pairs, floor indicators, call button | 196 | City General lift zone (replaces the 3 m box) | all tiers |
| `hvac` rooftop plant | 128 | 1–2 per closed wing block | Medium / High |
| `sign` wayfinding pylon: the four triage colours as plain bands (no text, no cross / emblem) | 136 | by the entrance | Medium / High |
| `tree_round`, `tree_cone` | 108, 56 | the two tree rows (alternating) | Medium / High (Low keeps the 30-tri box trees, half as many) |
| `hedge` | 44 | 14 along the south paving edge | Medium / High |
| `bench`, `planter` | 112, 88 | entrance forecourt | Medium / High |
| `car_a/b/c` generic hatchbacks (no badge, no plate text) | 224 each | 3 in the visitor parking | Medium / High |
| `stretcher` | 124 | under the ambulance canopy | Medium / High |

Procedural (MeshKit, every tier, quads only): glass windows on every open room's back wall (1 quad per ~3.5 m), two dark
window bands on each closed wing block (plan §8 Low plan: "exterior boxes with window textures"), a visitor parking lot
(asphalt + bay lines). Off Low: a roof parapet on closed blocks.

**Draws:** every static prop — the new ones *and* the desks / waiting chairs / lounge chairs / lift that used to be one
GameObject each — is merged into the one campus mesh (`MeshKit.Append`, kit FBX imported readable; procedural fallback when
a model is missing or not readable). Beds, scan machines and the ambulance stay separate (they change / move). No new
lights, particles, shaders or per-frame work; decor is built in `Rebuild()` only (structure changes).

**Estimate (world geometry, people included, `HospitalWorld` + `Crowd` caps):**

| State | Tier | Tris before → after | World draws before → after |
|---|---|---|---|
| full clinic (ch. 1, 10 beds, X-ray) | Low | 7 644 → 7 796 (+2.0 %) | 18 → 14 |
| all three wings (ch. 3, ~42 beds, 4 rooms) | Low | 18 650 → 18 960 (+1.7 %) | 55 → 49 |
| full clinic | Medium | 10 284 → 17 118 | 18 → 14 |
| all three wings | Medium | 21 290 → 26 400 | 55 → 49 |

Low stays inside the fleet rule (0 extra draws — 4–6 fewer — and prims ≤ +5 %). The real "Low before / after" perf_gate
line is still owed in the Unity editor (`docs/UNITY.md` §2): check the kit axis (fronts face −Z) and that the merged campus
mesh shows the props upright.

UI art was not re-rendered: the coach / intro / curtain renders use the unchanged furniture, ambulance and characters.
Meshy: no new credits this pass (ledger unchanged at 65 / 150).

## Icon candidates (owner picks; `unity/Assets/Art/Icon` keeps the placeholder until then)

| File | What | 48 px read |
|---|---|---|
| `store/icon-candidates/c1.png` | the charge nurse (Meshy), head + shoulders, light disc behind her head, brand teal | face + teal scrubs read |
| `store/icon-candidates/c2.png` | a ward bed from above at 3/4 on teal, a green triage disc on the blanket ("the bed is ready") | white bed + green dot read |
| `store/icon-candidates/c3.png` | the ambulance at 3/4 on teal (plain red stripe, blue light bar, no emblem) | strongest silhouette |

The feature graphic (`store/feature-graphic.png`) is a cut-away corner of the hospital: triage, three bays with staff and
a patient, the CT room, a corridor strip going yellow → orange → red with patients waiting, the ambulance under its canopy,
visitors' cars. No text (the deck renderer adds the wordmark + tagline per language).

## Meshy ledger (key 1, 2026-11-02) — 65 credits of the 150 budget

| Asset | Tasks | Credits |
|---|---|---|
| cn_nurse | preview (dress — rejected: an outdated uniform stereotype) 5 + preview (scrubs + trousers) 5 + refine 10 | 20 |
| cn_doctor | preview 5 + refine 10 | 15 |
| cn_patient | preview 5 + refine 10 | 15 |
| cn_paramedic | preview 5 + refine 10 | 15 |

Texture checks: no text or logos. The paramedic refine painted small red-ring sleeve patches (emblem-like): recoloured to
the uniform green in `optimize.py --recolor 345-12:46,100,74:0.5` — no cross / crescent / service emblem anywhere
(protected emblems + impersonation rule). Characters are stylised, modest, of different ages and skin tones; nothing
graphic.

## Open (P2 / P7)

- Look at the diorama in the editor: kit axis (fronts face −Z), figure scale at campus zoom, marker placement.
- The intro images are A-posed placeholders (Meshy rest pose). A rig + pose pass (`pose_figure.py`, 5 cr each) would read
  better — optional.
- Icon: the owner picks c1 / c2 / c3 (above), then `render_ui.py` / NurseIcons take it over.
- The feature graphic shows Meshy A-pose figures; a pose pass would read better (optional, 5 cr each).
