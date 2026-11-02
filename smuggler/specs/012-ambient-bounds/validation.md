# Validation — 2026-10-30, Patron #83

Baseline: `9478b0684b` (origin/main when this worktree was created).
Evidence: `/Volumes/BuildSSD/tmp/C1-smuggler83/`.

## Acceptance

- `tools/check.sh`: exit 0, including TRAFFIC 10/0, FLEET_OVERLAP 12/0, 72 layout screens/0 findings, Low perf 4/0 and 10 font locales/0 failures (`check-final.log`).
- High, all 26 ambient cars, 180 s at 15 Hz: real model overlap 0, longest stand 24.733 s, maximum step 0.248 m. Quality-switch checks restore the original Low mover and then the sized Medium mover.
- Default full-world test (High 90 s + Low 45 s): ambient and ambient/fleet overlap both 0, all 12 checks pass. Baseline High ambient overlap was 0.31 m in 188/5,400 steps.
- High 240 s / 14,400 steps: ambient and ambient/fleet overlap 0, 17 deliveries, longest stand 21.9 s, fading 83.6/6,208.9 car-seconds (1.35%).
- No HQ/fleet parking, models, translations, version, export or store changes.

## Low A/B

The normal perf gate is live and unseeded; its visible traffic varies between runs. Compare with seed 83 and Godot `--fixed-fps 60`. The committed `tests/traffic_perf_hook.gd` wraps the existing four-scene hook; run perf_gate with `--hook=res://tests/traffic_perf_hook.gd`, using the same fixture in both worktrees.

| Scene | Before/after primitives | Before/after 3D draws | Before/after UI draws |
|---|---:|---:|---:|
| start | 20,538 / 20,538 | 41 / 41 | 48 / 48 |
| midgame_near | 33,407 / 33,407 | 36 / 36 | 68 / 68 |
| lategame_far | 85,410 / 85,410 | 107 / 107 | 67 / 67 |
| tab_shop | 88,518 / 88,518 | 122 / 122 | 89 / 89 |

`perf-proof-before.log` / `perf-proof-after.log`: 4/0 each. Rounded static MB: 100/104/106/110 before, 100/104/107/110 after. A preceding direct-hook copy with the same seed/step (without the wrapper coroutine) also matched all geometry **and** static MB exactly: 100/104/107/110 (`perf-ab-before.log`, `perf-ab-final.log`). Wall-clock timings on the shared host are informational, not equality assertions. The extra mover is loaded lazily and never loaded at a Low start.

The isolated Low 120 s simulation logs are byte-for-byte identical before/after (`low-alone-before.log`, `low-alone-after.log`). This checks movement as well as render cost.

## Explicit remaining limits (Low unchanged by order)

- That isolated Low 120 s test exposes an existing ~0.01 m ambient contact in 1/7,200 frames, on both baseline and candidate. The new strict ambient assertion reports it; the standard Low 45 s gate remains zero.
- Extended mixed High 240 s → Low 120 s: High passes; Low has ~0.01 m contact in 8 frames and fading 24.5/1,189.4 car-seconds (2.06%, above the 2% limit). Resetting the test's substep/spawn timers does not remove it. This mixed extended run is **not** reported as green; standalone Low A/B above proves the Low simulation is unchanged.
- Fleet/fleet overlaps (up to 1.12 m) and crew/crew overlaps remain measured, as before; HQ/fleet parking is deferred by the owner.

Re-run long profiles independently with `fleet_overlap_test.tscn -- --seconds=240 --profile=high` and `--seconds=120 --profile=low`. Default invocation still exercises both profiles.
