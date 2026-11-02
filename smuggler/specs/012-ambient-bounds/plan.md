# Plan

- Lazily load a game-local VehicleMover extension only on Medium/High. Keep Low and the shared kit unchanged.
- Cache each model's conservative half-extents on its proxy, including off-centre bounds. Project both bodies into the following direction and lateral band.
- Allow opposing real-width bodies to pass. Reserve bends/U-turns as junctions, group adjacent short connectors, and expand each reservation by that car's corner radius.
- Release a blocked queue head so a vehicle with a clear exit can pass. Never retain a future junction reservation while waiting at another junction.
- Gate actual ambient footprints in traffic_test and fleet_overlap_test. Test quality switches and use a fixed-seed/fixed-step Low perf fixture for A/B.
