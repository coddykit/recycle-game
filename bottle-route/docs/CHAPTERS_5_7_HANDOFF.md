# Chapters 5–7 — handoff (2026-11-02)

## Done (all data-driven, nothing gated off)
- **Ch5 Industrial Estate** (`map.json` `industrial`, 130k / GR 66): 5 sites, new archetype `bulk` (weight 0 elsewhere);
  **bale tenders** (`balance.json` `tenders`: ≤2 open, 1.25–1.45× price, offer lapses after 2 days, no fine when missed,
  auto-delivered at day end; Depot sheet Accept / Decline / Deliver); **bulk counting centre** (`bulk_centre`, +1200/h, 15k).
- **Ch6 Riviera Resort** (250k / GR 68): 3 machines + 2 **hotels** (600 L, +60 bonus); **summer season** (`season`: every
  20 days, 5 days, ×3 arrivals in riviera+island, heat waves ×3 likelier; `season_start/_end` events, HUD chip, toasts).
- **Ch7 Hill Villages** (320k / GR 70): 4 `machine_l` (1000 L) + tavern, 10–14 km out. **Island** (200k / GR 72,
  `requires: villages`): ferry edge (`["r_pier","k_pier","ferry"]`), departures every 40 min, ticket 30, 18 km/h, no fuel
  on water; `Estimate` walks the clock (FerryWait, Ferries); recall at the pier never crosses.
- One-time tips: `tip.industrial/riviera/villages/island/tender/season/ferry` (Coach.TipFor); 59 new keys × 10 languages.
- World: new districts from map JSON; Low = all districts in 1 mesh + instanced points (≤4 draws); island ground, piers,
  ferry lane merged into ground; Medium/High ferry boat (+1 draw). No per-frame allocation.
- Save: new fields optional (`tenders`, `tender_id`, `season`, depot `bulk_centre`, truck `ferry`); old save test green.
- Balance: chapters 1–4 unchanged; medians 13 seeds: Industrial day 59, Riviera 89, Villages 119, Island 138 (plan 60/90/120).

## Files touched
- Sim: `GameData.cs`, `RoadGraph.cs` (ferry km matrices), `State.cs`, `SaveCodec.cs`, `Simulation.cs`, `Crises.cs`,
  `Depot.cs`, `Economy.cs`, `Fleet.cs`, `BalanceRunner.cs`, **new** `Chapters.cs`.
- Game/UI/World: `Coach.cs`, `GameActions.cs` (`tender:<accept|decline|deliver>:<id>`), `Sheets.cs`, `GameScreen.cs`,
  `BottleMain.cs`, `RegionWorld.cs`.
- Data: `map.json`, `balance.json`; strings `Tools/i18n/*.json` → `Resources/i18n/strings.csv`.
- Tests: **new** `Tests/EditMode/ChapterTests.cs` (16), `SimTests.cs` (+`LateChaptersFollowThePlanContentMap`, district
  count 8), `GameTests.cs` (+tips test, dynamic keys).
- Docs: `BALANCE.md` (Chapters 5–7 section), `ART.md`, `README.md`, plan §14 note.

## Test status
`sh bottle-route/unity/Tools/check.sh` → `BOTTLE_ROUTE_CHECK: PASS`; 145 NUnit (sim 62, game 28, kit 55), late tempo
test ≈3 s.

## Next steps (Unity editor)
1. Open the scene; check Low instanced point props (kit root rotation/scale, submesh 0) match the Medium/High GameObjects.
2. Check ferry boat height/heading under a sailing truck; island + ferry lane look; camera fit with the island unlocked.
3. perf_gate Low before/after with all districts (estimate ≈25 draws, +≈4.7k tris).
4. Balance follow-ups: 8-truck cap flattens late income; electric fleet; village "fixed every 3 days" policy in the bot.
