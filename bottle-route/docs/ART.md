# Bottle Route — art (P2)

Fleet rules: `LOOKDEV_STANDARD.md`, `NEW_GAME_PLAYBOOK.md` §11, `LESSONS_LEARNED.md` (Meshy lines).
**Meshy** = characters and organic hero props only; **Blender** = buildings, machines, trucks, depot modules, district
blocks (modular kit + palette swaps). No real brands, scheme names (DOA, TÜÇA, "Sıfır Atık"), retailer logos or text in
any texture (IP rule of `RECYCLING_GAME_PLAN.md`).

## Meshy set (2026-11-02, cloud session)

| Asset | Use | Pipeline | LOD tris | Texture | Credits |
|---|---|---|---|---|---|
| `operator` | guide coach (portrait in the coach bubble), depot hero at close zoom, store art | preview t2.5 + refine meshy-6 | 6000 / 2400 / 900 | 1024 (Low 512) | 15 |
| `technician` | technician card / jam fix hero, store art | preview + refine | 6000 / 2400 / 900 | 1024 / 512 | 15 |
| `citizen` | onboarding pager + day report art (the bottle bag is a separate Blender prop: Meshy ignored it) | preview + refine | 6000 / 2400 / 900 | 1024 / 512 | 15 |
| `bale_pet` | bale yard stacks (MultiMesh, LOD2 on Low) | refine on the aluminium bale's geometry (the PET preview came out as two pillows; one failed preview refunded) | 1500 / 700 / 300 | 1024 / 512 | 10 (+5 retry preview, unused) |
| `bale_alu` | bale yard stacks | preview + refine | 1500 / 700 / 300 | 1024 / 512 | 15 |

Total **75 credits** (session cap 200; balance after: 1270). Ledger: `~/.config/meshy/ledger.json` on the machine that
ran it (never in git). Textures checked by eye for text / logos: none (the technician's cap badge is an unreadable
generic emblem).

Files: `unity/Assets/Art/Meshy/<name>_lod{0,1,2}.fbx` + `<name>_base.png` (1024) + `<name>_base_low.png` (512).
Made with `tools/meshy/meshy.py` (fleet client: price table, cap check, ledger) → `tools/meshy/optimize.py` (weld, pivot
bottom-centre, +Y up, 1 unit = 1 m, decimate per LOD, 1024 + 512 PNG) → `tools/art/glb2fbx.py` (Unity has no glTF
importer and the project must not get one).

**Crowd lesson applied:** the citizens walking on the map are ~20–30 px — Meshy figures read worse than chunky canonical
figures at that size (Sky Tower 10-23, Kaiten 10-27). The map crowd therefore uses the Blender chunky figure (one mesh,
MultiMesh, vertex colours per archetype, 15 Hz on Low); the Meshy citizen is only used where it is large.

To redo: `export TMPDIR=<scratch>`; key in `~/.config/meshy/api_key` (needs `api.meshy.ai` + `assets.meshy.ai` in the
environment's allowed domains); Blender 4.x with numpy and an EGL/Mesa stack for the turntable renders.
