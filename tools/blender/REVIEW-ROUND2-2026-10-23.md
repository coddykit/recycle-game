# Blender round 2 — smuggler

Barn door braces and silo hoops; warehouse loading hardware, clerestory windows and corrected roof footprint; cottage porch, shutters and chimney cap. First-round apartment/office/shop excluded.

| Model | Before / after / LOD1 triangles |
|---|---|
| barn | 132 / 492 / 354 |
| warehouse | 248 / 536 / 385 |
| house_cottage | 100 / 316 / 227 |

Material counts and anchor transforms are preserved. All three models are under 1,500 triangles; explicit LOD1 assets are for review/export, runtime automatic import LOD remains unchanged.

Evidence: /Volumes/BuildSSD/game-builds/blender-live2/smuggler; comparison.jpg uses identical before/after Low and Medium cameras. Full scene evidence and geometry.json are included.

Validation: game suite RESULT PASS; quality 60/0; Low batching 13/0. No full UI acceptance is claimed in this model review.

```
PERF_GATE start prims=32846 draws=86 ui_draws=44 process_ms=6.63 (p95 6.63) frame_ms=2.43 static_mb=96 PASS
PERF_GATE midgame_near prims=32292 draws=37 ui_draws=73 process_ms=8.21 (p95 8.21) frame_ms=2.79 static_mb=99 PASS
PERF_GATE lategame_far prims=72995 draws=81 ui_draws=65 process_ms=7.73 (p95 7.73) frame_ms=3.23 static_mb=101 PASS
PERF_GATE: 3 scenes, 0 failures (limits: primitives 300000, draws 150, static 250 MB)
```

Shared-machine timing is diagnostic, not a mobile FPS claim. Review branch `blender2/smuggler` requires Patron approval before main. No store/release/version operations.

## Patron approval and integration #49 — 2026-10-23

Approved review `e0df7ad165` rebased onto main; official UI kit `facda685a3` synced. Reviewed GLBs unchanged. Fresh full tools/check.sh PASS (263 s): UI kit, touch suites, gameplay, quality/lifecycle, IAP/ads/consent, low batching, physics/performance and EN/DE/RU layout scan. Low3/0, draws 86/49/81; measured static memory <=101 MB. Final logs: `/Volumes/BuildSSD/game-builds/blender-live2/smuggler/integration49/`. No version/store/build/icon/paid-generation changes.
