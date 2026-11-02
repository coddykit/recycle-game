# Engine-free P7 robustness gate

`Tools/check.sh` includes `robustness_check.sh`: 5,000 byte corruptions and a short 10-second soak. For acceptance run
`SOAK_SECONDS=1800 sh unity/Tools/robustness_check.sh` from the game directory (`DOTNET=` can select the .NET 8 SDK).
No Unity, scene/UI changes or player data are involved. Temporary project/save directories are unique and removed.

Two independent continuous careers must have identical canonical state at each shift report and after each day.
One career saves/reloads through SaveRepository on disk at step 320 and after every shift. Two separate processes
also compare a fixed-seed scripted shift hash. After 64 warm-up days, collected retained heap is sampled throughout
and limited to baseline +8 MiB; RSS is reported separately. This detects bounded-memory regressions, not every leak.

Seed 2305000 creates exactly 2,500 random truncations and 2,500 single-bit flips. Invalid input must recover the good
backup and preserve the unreadable bytes. Valid mutations must round-trip canonically and continue simulation.

The first long run found a real determinism failure on day 1,689: restoring Grid recomputed routes across a line-trip
boundary, changing the dispatcher decision, battery energy and final income. New saves persist the completed step's
DRoute cache; old saves still reconstruct routes. Invalid route lengths/indices/foreign corridors are rejected.
Two focused NUnit tests cover the line-trip boundary and compatibility/validation. The old process-hash cache was
also shared by different code revisions; its filename now includes the compiled module identity.

Acceptance, 2026-10-30 (Mini, .NET only), after the fix:

- 1,800.0 seconds; 88,988 days; 177,976 disk reloads; 88,988 matching days; 60 heap samples.
- Retained heap: baseline 1,331,584 B; final 1,232,144 B; maximum 1,335,440 B (+3,856 B peak, no sustained growth).
- RSS baseline/maximum both 58,015,744 B.
- 5,000 corruptions: 1,095 valid mutations; 3,905 recoveries; zero exceptions.
- Cross-process hash `d83482928f65c42a:461`; NUnit 29/29; complete `Tools/check.sh` PASS without Unity.
- Logs: `/Volumes/BuildSSD/tmp/C3-p7/volt-fixed-1800.log`, `volt-sim-final.log`, `volt-check.log`.

The hash changed because the canonical save now includes d_route; gameplay determinism is compared within the same
compiled revision. Older saves cannot recover a route cache they never recorded, and retain the old load semantics.
