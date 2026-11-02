# W66 — 25 October desert redo

Spec/clarify: order C3 #7 authorizes six adobe silhouettes, identification/removal of the orange slab, and readable locked terrain. No economy, version, store, icon or paid-generation changes. Desktop evidence is art/performance evidence only; Android smoke belongs to the release owner.

Plan/tasks: retain placement footprint/RNG and shared palette; export six single-surface Blender houses with cheap variants <=204 triangles and detailed Medium/High variants. Reuse region MultiMesh and LowBatch. Locked terrain gets an alternate mesh with dusty colour, existing dune shading and a baked boundary strip, swapped on unlock. Three compact plant silhouettes replace repeated round crowns. Identify slab in matching screenshot. Run structural/tier/unlock tests, check.sh, before/after perf_gate and screenshots.

Analysis: draw budget is most sensitive in locked regions (unmerged). Boundary belongs to the existing tile surface, no extra node/material/draw. House low mesh replacement must happen before rebaking. Preserve original metadata for placement so decoration does not change route or occupied plots. No shader changes or frame-loop additions. Shared palette unchanged. pre-implement-check.sh and docs/WORKFLOW.md referenced by base are absent from current origin/main; explicit order and this reviewable task plan cover implementation scope.
