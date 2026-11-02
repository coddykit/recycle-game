# coddy_perf — shared performance module (games/CLAUDE.md §10, W60-B1)

Weak phones must play smoothly and strong phones should still look good. Every game uses the same device class, the
same Low / Medium / High rules and the same dynamic downgrade. The reference implementation is Chaiwala Rush (P7,
2026-10-21), and this module was extracted from it.

| File | What it does |
|---|---|
| `addons/coddy_perf/perf_device.gd` | **Device class.** `probe()`, `classify()`, `default_tier()` and `raise_to_default()`. Owner decision 2026-10-23 (replaces "a handheld never defaults to High"): a *flagship* starts on High, a *strong* phone on Medium, a weak or unknown one on Low, desktops on High. Android *flagship* = OS RAM ≥ 7000 MB (an "8 GB" phone reports ~7.2–7.6 GB), ≥ 8 cores and a GPU on the flagship list (Adreno 8xx / 730+, Mali Immortalis, Mali-G720 / G725 / G9xx, Xclipse 9xx); iOS flagship = RAM ≥ 7000 MB (kept conservative: RAM is the only reliable iOS figure). Android *strong* = RAM ≥ 5.2 GiB, ≥ 8 cores and a GPU outside the weak families; iOS strong = RAM ≥ 3.4 GiB. **The renderer is not a criterion.** The fleet exports Android with `gl_compatibility`, so a "Vulkan running?" test put every phone on Low (W60 trap). `flagship_upgrade()` is the one-time Medium → High for existing players (never over a Settings pick, never lowers). PerfGuard stays the safety net. |
| `addons/coddy_perf/perf_profiles.gd` | **Profiles.** `REFERENCE` holds the three tiers. `validate(table)` checks the §10 rules on a game's own `[low, medium, high]` table: Low has a 30 FPS cap, 3D scale 0.80–0.85, MSAA ≤ 2x, no shadows, SSAO / glow off, crowd and particles ≤ 50 % of the full (High) count, and LOD at least as coarse as Medium; Medium and High cap at 60, set `msaa`, Medium ≥ 2x and High ≥ Medium (W67, 2026-10-23: REFERENCE = Low 0.85 without MSAA, Medium 1.0 + 2x, High 1.0 + 4x; `guard_msaa()` (called by `apply_core`) keeps MSAA off on Android / Web GLES3 whenever an internal 3D buffer is in use — render scale ≠ 1, glow / adjustments / SSAO — because Godot 4.7.2 leaks a framebuffer per frame there (Airport 69d4de8021, Corrupt f9bf16ac6e; upstream 81a533bb9e); a game that turns such an effect on later calls `PerfProfiles.guard_msaa(viewport)` again). `apply_core()` sets the frame cap, render scale, MSAA and mesh LOD. `live_cap()` paces a handheld: 20 FPS under a window only after it has been left alone 1.5 s (`COVERED_IDLE_S`; Play review 10-23, lists scrolled at 20 FPS) — poll `idle_s = idle_step(idle_s, delta, get_viewport())` in `_process` (`ui_active()`: finger down or a TouchScroll drag / fling, which never reach `_input`). |
| `addons/coddy_perf/profile_compare.gd` | **W67 profile comparison.** Builds a perf-gate scene, applies each `scale:msaa` config to every 3D viewport, times 240 frames (vsync off; relative only — an M4 is not a phone GPU) and saves a capture per config for close-up edge comparisons. The Compatibility renderer has MSAA but no screen-space AA (FXAA renders identical pixels), so MSAA is the AA on every tier. Evidence: `game-builds/w67/profiles/<game>/compare_closeup.jpg`. |
| `addons/coddy_perf/perf_guard.gd` | **Dynamic downgrade.** 10 s in a row under 25 FPS lowers the tier by one step. The first 5 s after start-up or a tier change are ignored, and the guard never raises the tier by itself. `perf_line()` prints the debug PERF line that the emulator report reads. |
| `addons/coddy_perf/perf_checks.gd` | **Shared headless checks** for a game's tests: `device_failures(mapping)`, `profile_failures(table)` and `guard_failures(hook)`. |
| `qa_lowend_report.sh` | **Emulator low-end report** for any game: 2 GB RAM, 1600×720, software GPU, the Low profile injected and about 3 minutes of input. It records FPS min / median and PSS memory. |
| `sync.sh` | Copies the module into games (`--check` reports any copy that differs). |

## Adding it to a game
1. Run `bash games/tools/perf/sync.sh games/<game>`. This creates `games/<game>/addons/coddy_perf/`. Never edit that copy; edit here and sync again.
2. Device class: make the game's own `DeviceTier.classify` / `default_quality` delegate to `PerfDevice`. Keep the game's API, whether it uses names or ints.
3. Profiles: give the game a `[low, medium, high]` table with the REFERENCE keys it has. The 30 FPS cap on Low is required. Apply that table with `PerfProfiles.apply_core` or the game's own code.
4. Guard: call `PerfGuard.sample(Engine.get_frames_per_second(), delta, tier)` once per frame during real play, never in captures, `--perf` runs or headless. On a result ≥ 0:
   - set the tier and save it;
   - show the one-time notice ("Graphics lowered for smoother play — you can change it in Settings"). Reuse the game's existing, already translated quality notice where there is one.
   - When the player changes quality in Settings, call `guard.reset()`.
5. Players the old rule put on Low: this applies once (a quality policy version bump). A handheld still below its device default is raised to it, up to Medium and never lowered, and told once. Any other choice stands.
   Flagship (owner 10-23): one more policy bump runs `PerfDevice.flagship_upgrade(current, default, handheld, players_choice)` — a flagship still on the old automatic Medium and not picked in Settings goes to High once, quietly. Record Settings picks (`quality_user` or the game's key) so a player's own Medium is never changed.
6. Tests: call the three `PerfChecks` functions on the game's own mapping, table and hook. Keep the game's existing quality tests green.
7. Device evidence: `ROUNDS=18 SETTINGS_FILE=files/<settings>.json SETTINGS_JSON='{…Low…}' INPUT='swipe …' bash games/tools/perf/qa_lowend_report.sh <qa.aab> <package> <out>`. This needs a debug or QA AAB, because the PERF line is debug-only. It runs one at a time under the global smoke lock.

Module tests: `godot --headless --path games/tools/perf -s tests/run.gd`. They include proof that the checks catch the old "every phone Low" rule.

## ⚠️ Shader warm-up trap (lego 1.4.2, S3 2026-10-21)
`perf_warmup.run()` on a **throw-away warm world** (its own camera + materials / meshes / particles made only for the
warm-up, freed right after, then `change_scene`) crashed the GL thread on the Android emulator on cold starts whose
warm-up took 9–19 s: SIGSEGV, fault addr 0x6e00000051, ~1.5 s after the warm-up line, 4 of 6 runs. Removing the sky
Environment, dropping the particles, and keeping every warmed material + mesh alive (nodes freed 2 frames later) did
not fix it; skipping the warm-up did. Games that warm on their **real** world, which stays (skiresort, zombi, corrupt), passed every smoke
(chaiwala / disaster warm a throw-away boot diorama too — they passed, see below). Until the shared module is fixed and proven: warm on the scene that keeps
running, never on a temporary world freed before a scene change; prove any new warm-up with ≥ 3 cold emulator smokes
whose warm-up is slow (≥ 9 s). lego ships with the warm-up off (`WARM_SHADERS := false`, scripts/core/main.gd).
## Shader warm-up (`perf_warmup.gd`) — the fixed design (2026-10-22, after lego 1.4.2)
Two failure modes, both guarded in the module (headless proof: `tests/run.gd`, 68 checks):
- **Overload:** forcing every warm-up shader in ONE frame (lego 16 + 6 particles, disaster-command 308 materials,
  zombi 42 + 11 particles) blocks the GL thread for seconds (emulator SIGSEGV, PowerVR ANRs in `IMGeglSwapBuffers`).
  `run()` now draws **one item per generated shader** (materials that only differ in colour share one quad), **≤ 6 items
  per frame** (starts at 4, halves after a > 250 ms frame), each batch for one frame, and stops after **3 s**
  (`budget_ms`); what is left compiles lazily in play, most-used shaders first. `opts.limit` caps the item count
  (e.g. a Low-profile cut). A `warm_set()` holder (a direct child named `ShaderWarm`) or a holder passed in `retired`
  is sliced the same way. `{"hold": true}` also slices a world added in the same frame (its camera's cull mask is held
  on layer 20, like corrupt-tycoon's `ShaderWarmup`).
- **Early free:** warm-up-only resources freed the frame after their first draw. Every drawn material, the stage's
  quad / MultiMesh samples and every resource of the warm-up-only nodes are kept for the app lifetime; the nodes are
  hidden → 2 × `frame_post_draw` → freed → 3 frames. `retire([tree])` does the same for a tree built outside `run()`.
- Still a rule: warm on the scene that keeps running. chaiwala-rush and disaster-command warm a **throw-away boot
  diorama** before `change_scene` (the lego shape; they passed their smokes but use `hold` + `retire` now) — prove any
  such warm-up with ≥ 3 cold emulator smokes whose warm-up is slow (`user://perf_warmup_slow_ms`, debug builds only:
  ms slept after every warm-up frame; `adb shell run-as <pkg> sh -c 'mkdir -p files && echo 6000 > files/perf_warmup_slow_ms'`).

## Fleet status
| Game | Device class | Low 30 FPS | Dynamic downgrade | Commit |
|---|---|---|---|---|
| chaiwala-rush | ✅ (reference, own copy) | ✅ | ✅ | de197a446d |
| disaster-command | ✅ 706127df7c | — | — | B1 next wave |
| corrupt-tycoon | W60-B1 | W60-B1 | W60-B1 | see git log |
| trading-sim | W60-B1 | ✅ (fps auto) | W60-B1 | see git log |
| remaining 10 games | — | — | — | B1 next wave |

## Low-profile shading (`low_shading.gd`, W63 #69, S3 2026-10-22)

Not part of `addons/coddy_perf` (`sync.sh --check` would fail every game without it): the source is
`games/tools/perf/low_shading.gd`, copied unchanged to `<game>/addons/coddy_low_shading/low_shading.gd`.
`install(get_tree())` once + `set_cheap(tier == low)` wherever the game applies its graphics tier. Low: every lit
StandardMaterial3D / ORMMaterial3D per vertex without specular, every lit spatial ShaderMaterial on a cached
`vertex_lighting, specular_disabled` variant (parameters stay on the material); unshaded surfaces and shaders with a
custom `light()` untouched; Medium / High restore the originals. New nodes are picked up through `SceneTree.node_added`
(deferred one frame). Optional knobs: `keep` (Callable(BaseMaterial3D) -> bool, true = leave as authored, e.g. a
game's glow rule) and `shaders = false` (leave ShaderMaterials alone) — the factory games' ArtLibrary wrappers use both. The saving is fragment work on weak phone GPUs (doner / critter emulator 8–18 → 30 FPS); an M4
Mac shows no difference. Games: see `games/SMOOTHNESS_AUDIT.md` § W63 Low profile.

## Performance gate (`perf_gate.gd` + `perf_gate.sh`, owner rule 2026-10-22 — games/CLAUDE.md §0, Patron #83)

Every game, before every release: `sh games/tools/perf/perf_gate.sh games/<game>` (also the last step of the game's
check). Windowed Godot (Compatibility renderer, a 20:9 window in the game's orientation, under shot-lock), Low profile,
the game's `tests/perf_gate_hook.gd` scenes (`scenes()`, `setup(tree, scene)` with Low chosen BEFORE the world is built,
optional `teardown`). Limits per scene: primitives ≤ 300k (**real_prims**, see § Real primitives), draw calls ≤ 150, static memory ≤ 250 MB → otherwise FAIL
(exit 1). Median process time and frame time are reported only. Without a display (Linux CI) it prints
`PERF_GATE SKIP`. Results: `games/SMOOTHNESS_AUDIT.md` § Performance gate.

## Real primitives (`real_prims`, #181, S3 2026-10-31)

**The bug.** Godot 4.7.2's GLES3 renderer (the Compatibility renderer every fleet game ships on Android) counts a mesh
surface that has LODs **once, at the LOD it picked** — its LOD branch never multiplies by the MultiMesh's instance count
(the non-LOD branch does). Every imported `.glb` gets LODs by default, so a MultiMesh of 700 imported trees reads as one
tree. Skiresort W66 (S1): Medium read +8k for the HD forest, the real cost was +311k; Low reads 28,048, really 92,660.

**What perf_gate prints now** (old keys first and unchanged, new keys appended — a grep for `prims=`, `PASS`, `FAIL`
or `PERF_GATE: n scenes, 0 failures` keeps working):

`PERF_GATE <scene> prims=<reported> draws=… ui_draws=… process_ms=… (p95 …) frame_ms=… static_mb=… real_prims=<real> lod0_prims=<LOD off> lod_mm=<n> PASS|FAIL …`
`PERF_GATE_LOD_MM <scene> <node path> instances=<n> counted=<reported share> real=<share × n>` — the 5 biggest LOD'd MultiMeshes.

- `prims` — the reported counter (what the gate used to budget). `measure()` still returns only this, so the capture
  tools that call it are unchanged.
- `real_prims` — **the budgeted number.** For every visible MultiMeshInstance3D whose mesh has LODs (≥ 2 drawn
  instances, mesh LOD on in its viewport) the gate hides that node's own render instance on a paused frame
  (`RenderingServer.instance_set_visible`) and reads its share of the counter, per pass type (visible / shadow).
  Godot multiplies the mesh's LOD-less surfaces by the instances but adds each LOD'd surface once, at the LOD it drew,
  so share = `NL × N × k + L` (NL = triangles of the LOD-less surfaces, N = drawn instances, k = how often that pass
  drew the node — viewports, shadow splits — and L = the LOD'd surfaces as drawn once). k comes from the same share with
  mesh LOD off (then every surface is multiplied: `T0 × N × k`). Real share = `NL × N × k + L × N`; `real_prims` =
  `prims` + Σ (real share − share). Godot picks one LOD per surface for the whole MultiMesh (from its AABB), so L is
  exactly the LOD really drawn.
- `lod0_prims` — informative upper bound: one sample with `mesh_lod_threshold = 0` on every 3D viewport. With LOD off the
  renderer multiplies by instances again (right for a MultiMesh that draws LOD0), but every mesh that really draws a
  coarser LOD is counted at LOD0 — not what a phone draws, so it is not the budget.
- Why not walk the scene and sum triangles? The LOD a surface draws depends on the renderer's private distance /
  screen-size rule (AABB support point, LOD multiplier, threshold per viewport size); re-implementing it would drift
  from Godot. Hiding one node and reading the renderer's own counter uses the renderer's choice.
- Not corrected: GPUParticles3D / CPUParticles3D (instanced too, but their draw meshes are normally primitive meshes without LODs).

**Proof** (windowed, known answer computed by hand in the file header — a 64-instance MultiMesh of a 200-tri grid whose
LOD is 50 tris drawn at LOD0, the same drawn at its LOD, a 16-of-64 visible one, a two-surface mesh (one LOD'd, one
plain), a single LOD'd mesh and a LOD-less MultiMesh: reported 6,400, real 23,850, LOD off 39,000):
`sh games/tools/perf/tests/real_prims_test.sh` → `REAL_PRIMS_TEST: 13 checks, 0 failures`. Headless half (LOD
detection, drawn instances, budget key) in `tests/run.gd`. Skiresort Low: reported 28,048, real 92,660 (= S1's
`SKI_PERF_LOD0` LOD0 total — the forest is drawn at LOD0 from the gate's camera).
Shadow passes use the same formula (k per pass type) but are not in the synthetic proof: Low has no shadows.

**Budget.** The 300k primitive limit compares `real_prims`. A game whose real number is over the limit and cannot be
fixed in the same change may set, in its `tests/perf_gate_hook.gd`, `static func budget_on_reported() -> bool: return true`
with a TODO pointing at the fleet table — the line then shows `REAL_OVER=<n>>300000(budget_on_reported:TODO)` and the
gate budgets the old `prims`. Every game's gate greps the same thing (exit code + `PERF_GATE: n scenes, 0 failures`;
none greps a number): fleet table and which hooks use `budget_on_reported` —
`/Volumes/BuildSSD/dev/i18n-work/tasks/PERF_REAL_PRIMS_2026-10-31.md`.

## Game-specific copies (`addons/coddy_perf/KEEP`, Patron #85)

`sync.sh` copies every module file into every game it is given — it used to overwrite lego's own `perf_warmup.gd`
(warm-up off since 1.4.2) and the older `perf_profiles.gd` of moneytycoon / trading-sim. A game lists the files it
keeps as it has them (its own variant, or none) in `<game>/addons/coddy_perf/KEEP`, one name per line, `#` comments;
both the copy and `--check` skip them. Today: lego (perf_warmup.gd), moneytycoon and trading-sim (perf_profiles.gd,
perf_probe.gd, perf_warmup.gd). Sync all games: `bash games/tools/perf/sync.sh $(find games -maxdepth 4 -type d -path
'*addons/coddy_perf' | grep -v games/tools/ | sed 's#/addons/coddy_perf##')`.

