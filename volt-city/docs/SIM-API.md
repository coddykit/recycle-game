# Volt City — simulation API (P1, Unity C#)

> Code: `unity/Assets/Scripts/Sim/` (assembly `VoltCity.Sim`, namespace `VoltCity.Sim`, `noEngineReferences: true`: plain C# 9 /
> .NET Standard 2.1, no `UnityEngine`). Data: `unity/Assets/Resources/data/*.json`. Tests: `unity/Assets/Tests/EditMode/SimTests.cs`
> (Unity Test Framework, NUnit). **Status:** compiled for netstandard2.1 / C# 9 and tested under .NET 8 with
> `sh games/volt-city/unity/Tools/sim_dotnet_check.sh` (26 tests pass, balance gates pass, see `P1-VALIDATION.md`); the run inside
> the Unity editor is pending its installation.
> Owner decision 2026-10-27 (Patron): Volt City moves to Unity — this replaces the Godot `sim/` of the original PLAN.

## 1. Loading data

```csharp
// Unity layer (P3): read the TextAssets and hand the texts over (works on Android, no StreamingAssets I/O)
var gd = GameData.FromTexts(name => Resources.Load<TextAsset>("data/" + name)?.text);
if (!gd.Ok) Debug.LogError(string.Join("; ", gd.Errors));
// EditMode tests / tools
var gd2 = GameData.LoadDirectory("Assets/Resources/data");
```

`GameData` is typed and indexed: `Districts` (hookup order), `District[id]`, `PlantDefs[6]` (order `GameData.Plants` =
steam, gas, hydro, wind, solar, battery), `Corridors`, `Ties`, `Investments` (order 0..49, `Cost` = round(420 × 1.15^i),
`ValueMilli` = contract level it adds), `Events`, `Chapters`, `Weather`, `RoadDist[a][b]` (metres), `Placements` (rewarded),
`ConsumableDays`, every balance number as a field (`StepUs`, `ShedPct`, `GovernorPct`, …). JSON numbers are converted once here
(never compare raw JSON floats — LL Driftwood P7).

## 2. The career facade — `Simulation`

```csharp
var sim = new Simulation(gd, seed);              // new career: $600, Old Harbor + Riverside, Old Smoky L1, 1 crew truck
sim.SetCommercePerks(rights);                     // after every load: premium, offline_plus, speed4, dispatcher, forecast_plus
sim.Offline(nowUnix);                             // on resume: night-crew pay (8 h cap) -> OfflineSummary or null
sim.Touch(nowUnix);                               // on pause / save: remember when the player left
```

Phases (`sim.Phase`): **`idle`** (between shifts: build, hook up, forecast) → `StartShift()` → **`live`** (the 240 s shift) →
automatic end → **`report`** (shift report card) → `Collect()` → `idle`.

| Call | What it does |
|---|---|
| `Advance(realDt)` | every frame; runs whole 0.25 s sim steps at `Speed` (1/2/3, 4 with `speed4`); the remainder waits in integer µs. Does nothing while paused / not live. |
| `SetPaused(bool)` | the coach / a modal stops time (DESIGN section 9). |
| `Queue(Cmd)` | a tap. Validated now, **applied at the next fixed step** (LL Pit Lane Boss #133) → `QueueResult {Ok, ApplyStep, EffectInS}`; `EffectInS` is the plant's ramp delay for the "takes effect in 6 s" label. |
| `StartShift()` | plans weather + events for the shift (deterministic) and starts it. `StartShift(weather, force)` is for tests / probes only. |
| `Collect()` | report profit → cash; next shift number. |
| `Buy(id)` / `Economy.WhyNot(sim, id)` | an investment; reason codes: `owned`, `live`, `locked_prev`, `needs_district`, `needs_chapter`, `cash`, `unknown`. |
| `Hookup(id)` / `Economy.HookupWhyNot(sim, id)` | a district (in order); `rating`, `cash`, `locked_prev`, … Hooking up the chapter's district opens the chapter. |
| `Economy.BuyableNow(sim)` | everything buyable now, cheapest first. |
| `Forecast()` | the next shift's `ShiftPlan` (3 with `forecast_plus` / `premium`): weather kind + forecastable events. |
| `SetSpeed(x)` | 1/2/3 free, 4 needs `speed4` (integer compare). |
| `SetAuto(part, on)` | dispatcher assistant switches (`baseload`, `battery`, `crew`) once owned. |
| `DrainEvents()` | everything that happened since the last call (see section 4). |
| `Snapshot()` / `SaveCodec.Encode(sim)` | the whole save (canonical JSON). |
| `ShiftHash()` | determinism hash of the live shift (grid state + plan + command log, FNV-1a 64). The report keeps it. |

### Commands (`Cmd`)

| Factory | Effect |
|---|---|
| `Cmd.Ramp(plant, dir, n=1)` | steam / gas / hydro setpoint ±1 step (10 % of capacity) per tap; the change starts after the plant's delay (steam 6 s, gas 1 s, hydro 1 s), then output ramps (steam 2.5 %/s, gas 12 %/s, hydro 15 %/s). |
| `Cmd.Power(on)` | start / stop the gas peaker (3 s start). |
| `Cmd.Battery(mode)` | `charge` / `hold` / `discharge` / `auto` (auto follows the needle). |
| `Cmd.SendCrew(faultId, crew=-1)` | nearest idle (or returning) truck drives the road graph, then repairs. |
| `Cmd.Shed(district)` / `Cmd.Restore(district)` | manual shedding (never Hospital Hill) / bring a shed district back. |
| `Cmd.DemandResponse()` | smart meters: −8 % (L1) / −12 % (L2) demand for 30 s, 60 s cooldown, $30. |
| `Cmd.Import()` / `Cmd.Overtime(fault)` | rewarded only (via `GrantReward`), never sold. |
| `Cmd.ScriptFault(corridor)` | the guide's scripted spark (only while `TutorialSafe`). |

### Tutorial

`sim.TutorialSafe = true` (DESIGN section 9) while the guide runs: no auto-shedding, no plant trips, no overheat trips, no planned bad
events, rewarded offers hidden; `ScriptFault` works. The first shift of a career starts at 17:30 with Old Smoky at 5/10 so the
evening peak arrives ~5 s in (the 30-second hook); later shifts start at 06:00. The guide's onboarding state stays a setting
(`intro_done`, `coach_done`), not part of the sim.

## 3. Reading the live grid — `sim.Grid` (UI / diorama, read only)

| Member | Meaning |
|---|---|
| `Step`, `Steps` (960), `HourOfDay` | sim clock; 40 steps = 1 diorama hour; `Plan.StartMin` = shift start (minutes of day). |
| `NeedlePm` | balance needle, per-mille of demand (green band ±30 = ±3 %; auto-shed below −50 for 10 s; trip above +80 for 20 s). |
| `LastDemand`, `LastSupply` | kW this step. `DemandAt(step)`, `RenewablesAt(step)` = the forecast curve for the needle card. |
| `DIds`, `DHooked`, `DState[i]` (0 lit, 1 auto-shed, 2 hand-shed, 3 hospital on backup), `IsLit(i)`, `DPart[i]` (‰ dark behind a transformer fault), `Route(i)` (corridor feeding it, −1 none) | districts (the window-light ripple). |
| `PLevel`, `PCap`, `POut`, `PSteps`, `SetpointAfterPending(p)`, `POn`, `PTrip[p] > Step` (tripped), `WindCf[step]`, `SolarCf[step]` | plants (smoke, blades, panels). |
| `BatSoc / BatCapWh`, `BatMode`, `BatOut` (+ discharge / − charge kW), `ResWh / ResCapWh` | storage (battery strip, reservoir level). |
| `CIds`, `CCap`, `CFlow` (kW, signed), `CHeat` (ppm, trip at 1,000,000), `CFault[c] >= 0`, `COutUntil[c] > Step` | lines (`_VcCorridorState`: overheat when `CHeat` ≥ `LineWarnPct` × 10,000, down when faulted / tripped). |
| `Faults` / `OpenFaults()` (`Id`, `Kind` line / transformer, `Site` road node, `Crew`, `Cause`), `Crews` (`Node`, `State` idle / travel / repair / return, `Until` step) | sparks, trucks (P3 drives `coddy_anim`-style movers between road nodes; the sim owns the arrival step). |
| `Hourly` | per diorama hour: demand, supply, needle, lit mask (report chart). |
| `AsciiFrame.Frame(sim)` / `AsciiFrame.Strip(grid)` | text debug views. |

**Art ids (P2 `ART-IDS.md`).** The 8 sim corridors map 1:1 onto the art's upgradeable corridors by their district:
`c_harbor`→`harbor_line` (0), `c_riverside`→`river_line` (1), `c_market`→`market_line` (2), `c_hilltop`→`hill_line` (3),
`c_hospital`→`hospital_line` (4), `c_tech`→`tech_line` (5), `c_stadium`→`stadium_line` (6), `c_airport`→`airport_line` (7).
Smart substations: `sub_1` Harbor↔Riverside, `sub_2` Market↔Stadium, `sub_3` Hospital↔Hilltop (P3 places `substation_smart` there).
`data/roads.json` is GENERATED (P3) by `unity/Tools/road_graph.py` from the art layout: the 13 art roads in Unity metres, node =
`[x, z, y]` (the sim reads `[x, z]`), `d_<x>` = road point next to the district substation, `l_<x>` = road point next to the middle of
the district's art corridor, `depot` = the Riverside junction (-55, -30). Balance quick gate re-run: PASSED (P3-UI.md section 4).

## 4. Events (`SimEvent.Type`, `Step`, `Data`)

`shift_start` · `event_start` / `event_end` {id} · `fault` {fault, kind, site, cause, corridor, district} · `crew_sent` {crew, fault,
eta, from, to} · `crew_arrived` · `repaired` {fault, instant, site, district} · `shed` {district, auto} · `restore` {district, auto} ·
`backup_on` / `backup_end` (Hospital Hill) · `plant_trip` {plant, until, cause event / over_supply} · `line_trip` {corridor} · `dr` ·
`import` · `shift_end` · `shift_done` {shift, stars, profit, uptime_pm} · `chapter` {chapter} · `chapter_done` {5} · `bought`
{id, cost} · `hooked_up` {district, cost} · `offline` {cash, away_s, capped, away_days} · `reward` {placement, amount,
daily_index} · `purchase_funds` {product, amount} · **`review_moment`** {reason, play_s, tutorial, ready}.

**Review gate hook (games/CLAUDE.md section 12, owner 10-27).** `review_moment` fires once per career for
`first_district_restored` (the first repaired fault / hand-restored district — the "lights back on" ripple) and
`first_shift_completed` (shift 1 ends ~4 min into the first session at 1x). `ready` = play time ≥ `review.min_play_s` (240 s) and
not in the guide. The UI's ReviewGate (Corrupt model) asks the Play In-App Review only when `ready`, no modal / shop / ad is open,
≤ 3 attempts ≥ 30 days apart. The sim stores `S.Wow` so a reload never fires it twice.

## 5. Money, rewards, store

- `Economy.PriceMilli(sim)`: contract level (1000 = ×1). Revenue = served MWh × $2 × level; fuel and upkeep scale with it too.
- Shift report (`S.Report`): uptime ‰, served / demand MWh, revenue, fuel, upkeep, bonus (Hospital backup test), profit, stars (3: uptime
  ≥ 99 % and profit > 0; 2: ≥ 95 %), CO2 stars, outage minutes, sheds, faults, trips, band ‰, events, rating.
- City rating (`S.RatingX100`, 0..500) = mean of the last 5 shift ratings (uptime 75 % → 0★ … 99.5 % → 5★).
- `DayIncome()` = 6.7 × last shift net (floor $1,000) — the unit of consumables and reward caps.
- `RewardOffer(placement, now)` / `GrantReward(placement, now, fault)`: `offline_x2` (1/day), `shift_bonus` (+50 % profit, 3/day,
  10 min), `emergency_import` (60 s import in a deficit, 2/day, 15 min), `crew_overtime` (instant repair, 3/day, 5 min). Cash rewards
  are capped: single ≤ 0.2 day income (≤ 1/5 of `cash_s`), daily ≤ 0.5 day. Counts / cooldowns live in the save (restart does not
  reset). Call `GrantReward` only after the SDK "earned" callback, or directly for ad_free owners (same caps).
- `GrantPurchaseFunds(txnId, product)`: consumables (`cash_s` 1 day … `cash_xl` 10, `comeback` 1, `storm_kit` 3); idempotent per
  transaction (SHA-256 of the id kept in the save, ≤ 512).
- `SetCommercePerks`: `premium` / `forecast_plus` (3-shift forecast), `offline_plus` (16 h × 1.5), `speed4`, `dispatcher` (the three
  assistant investments purchasable from chapter 1 instead of 3/4/5). Never saved; re-applied from verified store rights.

## 6. Saves

`SaveCodec.Encode(sim)` → string; `SaveCodec.Decode(text, gd)` → `DecodeResult {Ok, Error, Sim}`; never throws, never mutates a
running game. Accepts every state the sim produces (negative cash, mid-ramp, a pending tap, a truck on the road, a fault being
repaired, overheat at capacity+1, the report screen, the purchase moment); a live shift is stored as plan + grid snapshot + command log
and continues bit-for-bit (reopen-lock test). The shared save repository (atomic tmp+rename, `.bak`, `.unreadable` copies,
`save_unreadable` event) is the Unity layer's job (P3), saving at every shift end and every 30 s (`time.autosave_s`) inside a shift.

## 7. Headless tools

- `BalanceRunner.Cli(args, print)`: `quick` (the check gate) · `full shard=K/N seeds=1000 out=F` · `merge F1 F2 …` · `curve [policy]` · `frame`; `data=DIR`.
  Today: `sh games/volt-city/unity/Tools/sim_dotnet_check.sh balance quick` (or `full …`, `merge …`, `hash 77`).
  Callable from a Unity `-executeMethod` editor wrapper or any .NET host. `BalanceRunner.Evaluate` returns the gates.
- `Policies` (greedy, saver, renewables_first, gas_only, battery_only, never_shed, naive_coach, premium, dispatcher) and
  `SimProbe` (fixed scenarios, cross-process hash).
- Determinism across processes: run the EditMode suite twice (two Unity processes); `Determinism_AcrossProcesses` compares the shift
  hash with the one the previous process left in the temp folder.
