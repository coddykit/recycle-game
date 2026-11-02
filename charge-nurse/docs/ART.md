# Charge Nurse — art

| What | Where | How |
|---|---|---|
| Palette (46 swatches, 8×8) + 4 theme palettes (modern / retro / playful / gold) | `unity/Assets/Resources/materials/palette*.png` | `python3 unity/Tools/make_palette.py`. A theme = palette swap (0 extra draws). Order = `ChargeNurse.World.Pal` |
| Blender kit (9 props, 2 196 tris total): bay (bed + curtain + monitor), ward bed, desk, waiting chairs, ambulance (plain red stripe, no emblem), X-ray, lab bench, CT, OR table | `unity/Assets/Resources/kit/*.fbx`, sheet `docs/kit_sheet.png` | `blender -b --python tools/art/kit.py -- unity docs/kit_sheet.png` |
| Rooms, corridors (heat-map UVs), trees, people (chunky instanced figures), fallbacks for every kit prop | code: `Scripts/World/MeshKit.cs`, `HospitalWorld.cs`, `Crowd.cs` | procedural, one palette material |
| Meshy hero characters (renders only, never the crowd — fleet lesson): nurse, doctor, patient, paramedic; LOD0 6k tris, 1024 + 512 textures | `unity/Assets/Art/Meshy/<name>_lod0.fbx`, `_base(_low).png` | `tools/meshy/meshy.py preview/refine` → `optimize.py --height … --lods 6000,2500,900` → `tools/art/glb2fbx.py` |
| UI art: coach portrait, intro pager 1-3, boot curtain, placeholder icon | `Resources/ui/{coach_nurse,intro_1,intro_2,intro_3,curtain}.png`, `Art/Icon/icon_full.png`, `icon_adaptive_fg.png` | `blender -b --python tools/art/render_ui.py -- unity` |

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
- Final icon candidates at P7 (owner picks).
