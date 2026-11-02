# Volt City — P1 validation (sim core + data + balance)

> Patron, 2026-10-27. Code `unity/Assets/Scripts/Sim/` (C#, assembly `VoltCity.Sim`, `noEngineReferences`), data
> `unity/Assets/Resources/data/`, tests `unity/Assets/Tests/EditMode/SimTests.cs`, API `SIM-API.md`, final economy in
> `DESIGN.md` section 7. Check: `sh games/volt-city/unity/Tools/sim_dotnet_check.sh` (registered in `unity/Tools/check.sh`).

## 0. How this was validated

Mid-task the owner moved Volt City from Godot to **Unity** (Patron order 2026-10-27). The sim was first built as a GDScript
reference model (headless Godot, outside the repo, never committed) to explore and tune; the deliverable is the engine-free C# port.
Once the P0 agent put a .NET 8 SDK on the Mini (`/Volumes/BuildSSD/game-deps/dotnet-sdk`), the **C# itself** was compiled and run:

- `netstandard2.1`, C# 9 (Unity 6's language level), `-warnaserror`: **0 warnings, 0 errors**.
- EditMode suite under NUnit 3.14 / .NET 8: **26 tests, 0 failed** (stable over 5 consecutive runs).
- Balance: the C# `BalanceRunner` — **1,000 seeds × 9 policies = 9,000 careers + 40-seed plant matrix: BALANCE PASSED** (section 3).
  All numbers in this file and in DESIGN section 7 come from the C# run; the reference model agreed to within a few units.

Still open until the Unity editor is installed: the same suite inside Unity's EditMode runner (asmdef wiring, Mono / IL2CPP), and a
`-executeMethod` wrapper for `BalanceRunner.Cli`. Packages: the test assembly needs `com.unity.test-framework` in
`Packages/manifest.json` (P0's file).

## 1. Tests (`SimTests.cs`, 26)

| Area | Tests | Result |
|---|---|---|
| Data | 8 districts, 50 investments (first $420, total $3,031,443), 15 events, 5 chapters, 8 corridors + 3 ties, 6 plants × 3 levels, speeds `[1, 2, 3]` as ints, 24 IAP ids, Hospital Hill = highest priority; JSON integers round-trip exactly, bad JSON → null | pass |
| RNG | same seed = same stream (1,000 draws), `Frand` in [0, 1), `Mix` separates salts; no method named like an engine global | pass |
| Determinism | same seed + same commands = same shift hash in process; other seed differs; **across processes**: `sim_dotnet_check.sh hash 77` twice → `cbaffcf6f8dacb01:665` both times, and `Determinism_AcrossProcesses` compares with the previous process's file (keyed by a data fingerprint) | pass |
| Fixed step | tick sizes 0.016 / 0.25 / 1.7 s give the same shift | pass |
| Fairness (LL Pit Lane Boss #133) | a tap anywhere inside one 0.25 s window = one result; one window later = different; `EffectInS` = plant delay | pass |
| Speeds | 1/2/3 free, 4 only with `speed4`, dropped when the perk goes; 2× runs 80 steps in 10 s | pass |
| Tutorial | `TutorialSafe` shift under a forced storm + trips + squirrel: 0 sheds, 0 trips, only the scripted spark; `ScriptFault` refused outside the guide | pass |
| Shedding | whole city on Old Smoky L1: relays drop Stadium Row → Hilltop → Old Harbor, hospital stays lit; Riverside + Hospital with no operator: Riverside first, hospital on backup exactly 240 steps (60 s), never "shed" while on backup | pass |
| Lines | heatwave over a grown Riverside base line → overheat trip + dark steps; smart substation reroutes only when the partner line has room | pass |
| Crews | travel = road distance / truck speed (to the step), repair restores the district, `repaired` event | pass |
| Events | 600 planned shifts: ≤ 2 negative events, no storm after a 1-star shift, never two faults on one corridor; tutorial shifts calm; forecast = the shift that is then played | pass |
| Economy | prerequisites (`needs_district`, `locked_prev`, `needs_chapter`, `live`), rating-gated hookups open chapters, contract level rises, `dispatcher` perk unlocks the assistant early | pass |
| Offline | 8 h cap = 0.7 shift (700 of 1,000); `offline_plus` 16 h × 1.5 = 2,100; < 1 min pays nothing | pass |
| Rewards | single ≤ 0.2 day income, daily cash ≤ 0.5 day, once per report, 10 min cooldown, counts survive save/load, non-cash placements need their live context, nothing sold is a reward | pass |
| Store | `GrantPurchaseFunds` idempotent per transaction (also after a reload), only SHA-256 of the id kept, permanent products refused as cash | pass |
| Saves | **reopen-lock** at steps 1 / 37 / 400 / 701 / 959 of a storm shift: decode + continue = identical hash and cash, re-encode byte-identical; every phase, negative cash and a pending tap decode; **save fuzz 0 / 7,440 unreadable** (4 careers × 10 shifts, frame-timed saves every 11 frames incl. speeds 1-3, rewards at reports, offline returns); 300 mutated + 7 malformed saves refused without an exception | pass |
| Review gate | `review_moment` once each: `first_district_restored` (guide's spark repaired, 15 s) and `first_shift_completed` at play 240 s with `ready = true`; never again in the career | pass |
| ASCII | frame + strip render | pass |
| Balance | `Balance_QuickGates` (6 seeds × 9 policies + 3-seed matrix, ~4 s) — every gate | pass |

## 2. ASCII debug frame (`sim_dotnet_check.sh balance frame`)

Day 4 of a greedy career (seed 2026), chapter 2, forced storm + plant-trip shift. A thunderstorm fault on the Harbor line; truck 0
is driving there; the smart substation does **not** push Harbor onto the Riverside line because that line has no room (the cascade
fix of P1 — before it, the reroute overloaded Riverside, both lines tripped six times and the shift fell to 1★ / 86 % uptime).

```
VOLT CITY  shift 18  ch 2  14:19  step 333/960  weather rainy  cash $2092
NEEDLE  [.......===|===.......]  +0.0 %   demand 35.2 MW  supply 35.2 MW
  steam    L2   24.0/   80 MW  set 3/10
  gas      L2    0.0/   45 MW  set 0/10  off
  hydro    L1   16.0/   20 MW  set 8/10  reservoir 16 %
  wind     L1   21.3/   25 MW
  solar    L1    3.4/   25 MW
  battery  L1    0.0/   10 MW  auto  soc 99 %  out 0.0 MW
DISTRICTS HAR:o   RIV:#   HIL:#   MKT:   TEC:   STA:   HOS:   AIR:
LINES     har FAULT  riv 60%  hil 45%
  fault #1 line at l_harbor (thunderstorm) crew 0
  truck 0 driving at depot until step 360
  truck 1 idle at depot
  truck 2 idle at depot

hour  demand MW  supply MW  needle  lit districts
06:00      48.7       48.7   +0.0%  HAR RIV HIL
...
14:00      61.0       61.0   +0.0%  HAR RIV HIL
15:00      61.7       36.3   +0.0%  --- RIV HIL      <- Harbor dark until the crew finishes (~15:50)
16:00      69.0       69.0   +0.0%  HAR RIV HIL
17:00      75.3       75.3   +0.0%  HAR RIV HIL      <- heatwave evening carried by steam + hydro + wind
18:00      80.5       80.5   +0.0%  HAR RIV HIL
...
05:00      37.6       37.6   +0.0%  HAR RIV HIL

report: uptime 96.7 %, 2★, revenue $3,635, fuel $562, upkeep $419, profit $2,654, outage 94 min, 1 fault / 1 repair,
events heatwave + thunderstorm (2 negative = the cap), contract level ×1.497
```

Legend: `#` lit, `o` no path (line down), `-` auto-shed, `x` hand-shed, `B` hospital on backup, `~` part dark (transformer).

## 3. Balance (`BalanceRunner`, full: 1,000 seeds × 9 policies, matrix 40 seeds) — BALANCE PASSED

| Gate | Limit | Result |
|---|---|---|
| Normal day 7 | ≤ 70 % (35) every run; ≥ 45 % (22) | max 28, median **27 (54 %)**, min 26 |
| Normal: everything owned | day 25-30 in ≥ 95 % of runs | **100 %**, median day 26, p5 26, p95 27 |
| No stop | never 2 days in a row without a purchase | longest 1 |
| Idle cash at day end | ≤ ~1 day of the day's income (limit 1.25) | p95 1.03, max 1.23 |
| Premium day 7 / all | ≤ 78 % (39) / not before day 19 | max 31 (62 %), median 30 / min day 22 |
| Gas-only, battery-only | finish chapter 5 ≥ 20 % slower than mixed (shift 115) | never within 30 days (≥ 1.56×) |
| Paid dispatcher | within ±10 % of the hand-played bot | shift 115 vs 115 |
| Renewables-first / saver | finish chapter 5 (no wall) | shift 158 / 91 |
| Never-shed | ≥ 2★ in ≥ 80 % of chapter 1-2 shifts | **97.1 %** of 39,205 |
| Naive coach player | ≥ 2★ in ≥ 80 % of chapter-1 shifts | **92.1 %** of 9,024 |
| Event caps | ≤ 2 negative / shift; no storm after 1★; none in tutorial shifts | 2 / 0 / 0 |
| No dominant plant | every type best next buy somewhere; none > 50 % of decided cells | wind 62, solar 34, hydro 23, gas 7, battery 7, steam 4 (of 137; 3 cells where nothing pays back) |

Progression (median of 1,000): day 1 → 9, day 3 → 17, day 7 → 27, day 10 → 33, day 14 → 38, day 21 → 45, day 25 → 49, day 26 → 50.
Net per shift (seed 1): $1.0k → $2.4k → $8.7k → $9.4k → $17.8k → $34.2k → $45.9k. Contract level ×1.0 → ×6.99.

Plant matrix (cost / extra value per shift, unserved energy at 4× tariff; excerpt): day 1 heatwave — steam 3.9, gas 4.0, hydro
5.4 shifts; day 1 cold snap — **gas** 2.9; day 4 cold snap — **battery** 122; day 4 fog + calm — **hydro** 117; day 10 plant trips —
**gas** 30 vs wind 61; sunny / heat days from chapter 2 on — **wind / solar**. Late decision points have long paybacks (hundreds of
shifts): once the greedy city has built its capacity, plant upgrades mostly buy reliability (see open item 3).

### Tuning log (data only; no gate was relaxed in code)

1. F0 capacities (Old Smoky 60 MW, gas 30, …) left the grid always oversupplied: steam and gas never won a matrix cell. Capacities
   cut to 45/80/120, 25/45/70, 20/40/60, 25/50/80, 25/50/80, 10/25/45 MW; district growth cap 15 % → 25 %.
2. Income: F0's "income gain = cost / payback" became the contract level (section 7); hookups add physical demand on top, so the
   value scale and the payback ends were swept with the C# runner: (4→60, ×1.0) finished on day 12; final **(7 → 28 shifts, ×0.27)**.
   Lowering the last payback fixed the end-game idle cash (saving ~1.3 days for the last line upgrades) without finishing before day 25.
3. Naive player: under 30 % → 92 % ≥ 2★ in chapter 1 after two sim fixes — auto-restore picks the highest-priority district that fits
   (it used to wait forever for one that did not) and the coach does not repeat a tap while a change is still on its way
   ("takes effect in N s"); the guided first shift runs `TutorialSafe` as the game does.
4. Smart-substation reroute only with room on the partner line (cascade fix, section 2).
5. Idle cash is measured against the income the day actually earned (6 shifts + night crew), not 6.7 × the last shift (a single
   bad last shift made the ratio jump).
6. Plant matrix: decision points = the greedy career's state at the start of each of the first 14 days (chapter-start states had
   every cheap plant bought already); a cell where no option pays back has no winner.

## 4. Fleet rules covered

Seeded streams, no engine-global method names (LL Pit Wall P1) · integer state + integer µs time (LL Pit Wall P1) · event handlers
on the event's own step (planned faults / trips / marks fire at their step) · queued commands applied at the next fixed step
(LL Pit Lane Boss #133) · JSON numbers converted once, ints compared as ints (LL Driftwood P7) · tutorial cannot lose (LL Pit Lane Boss)
· save codec accepts every sim state + fuzz 0 + reopen-lock (CLAUDE §14) · rewarded caps 1/5 and 1/2 of `cash_s`, persisted
(CLAUDE §4) · idempotent consumables · commerce perks never saved · first-session review moment (CLAUDE §12, owner 10-27).

## 5. Open (for P3 / Patron)

1. Run the suite in the Unity editor once installed (+ `com.unity.test-framework` in `Packages/manifest.json`), and add an editor
   `-executeMethod` wrapper for `BalanceRunner.Cli`.
2. **Done in P3** (`unity/Tools/road_graph.py`, quick gate passed — P3-UI.md section 4). Was: `data/roads.json` is an abstract road graph; P3 maps its nodes onto the P2 layout (district substations, line midpoints, depot)
   and re-runs the quick gate (travel times move). Sim corridors map 1:1 to art corridors 0-7 (SIM-API section 3).
3. Late-game plant upgrades pay back slowly in money (their value is reliability); if P3 play tests show "nothing worth building"
   after day 10, raise late plant capacities or let reliability feed the contract level.
4. **Done in P3:** DESIGN section 9 step 6 now says $420 (the data price; the guide string `coach.6` says $420 too).
5. The sim saves only its own state; the shared save repository (`.bak`, `.unreadable`, `save_unreadable`) is the Unity layer's job.
