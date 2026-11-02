# Volt City (store name: Grid Keeper: Energy Tycoon, P6) — game design (F0, Patron 2026-10-27)

> Working folder `games/volt-city/`, package **`com.coddykit.voltcity`** (proposed; P6 title knockout decides the store title, the package
> stays). Portrait. Godot 4.7.2, Compatibility renderer, Android export `gl_compatibility`. Google Play only (no iOS work).
> Rules that are not repeated here: [`../../CLAUDE.md`](../../CLAUDE.md) (wins over this file), [`../../LESSONS_LEARNED.md`](../../LESSONS_LEARNED.md),
> [`../../NEW_GAME_PLAYBOOK.md`](../../NEW_GAME_PLAYBOOK.md). Sibling docs: [UI](UI.md) · [ART](ART.md) · [IAP](IAP.md) · [STORE](STORE.md) · [PLAN](PLAN.md).
> All numbers below are the **F0 sketch**; the P1 headless balance runner is the real gate and may move them (write the final values
> back here with the commit id).

## 1. Pitch

You run the control room of Volt City's power company. The whole city is a living 2.5D night-and-day diorama: thousands of windows,
street lights, a stadium, a harbour, a hospital on the hill. Demand rises and falls all day — the morning kettle rush, the midday
solar glut, the evening peak — while storms knock trees onto lines, a heatwave turns every air conditioner on and a plant trips at
the worst moment. You keep supply and demand balanced, send repair crews, decide which district goes dark when there is not enough,
and grow a small two-district grid into a whole city of wind, sun, water and batteries.

The core feeling: **the lights-out ripple and the lights-on ripple.** Every good decision is visible as a district lighting back up.

## 2. Why this niche (demand, ASO Atlas Play US, 2026-10-27; research 2026-10-20)

| Term | Demand band | Difficulty | Note |
|---|---|---|---|
| `grid manager` | Medium | **24** | lowest difficulty of the set, exact fit for the loop |
| `power grid game` | Medium | 38 | top-10 holds 1 game ≥ 1M (Energy Manager 1.1M), median installs **25k** |
| `energy tycoon` | Medium | 42 | idle energy clickers (Idle Energy 361k, Power Inc 333k) |
| `power tycoon` | Medium | 47 | idle clickers, no real-time grid game |
| `solar tycoon` | Medium | 41 | secondary keyword (solar field build) |
| `storm game` | Medium | 41 | chaos keyword, shared with Disaster Command — do not cannibalise its title |
| `reactor game` | Medium | 37 | Reactor 9.5M is an idle tycoon; we have **no nuclear** in v1 (content tone), keyword only in long text if ever |
| `power grid manager`, `power outage`, `city power`, `electricity tycoon`, `energy simulator`, `blackout game` | Low | 32-45 | long-tail for the full description |

Shelf: every big neighbour is an **idle clicker** (tap generator, buy upgrade) or a puzzle that uses the word "blackout". Nobody on
Play sells the real-time "keep the lights on" fantasy in a living city diorama (on PC: Power to the People, Texas PowerGrid Challenge —
small). Paid UA is plausible: energy is not a restricted ad category, and "city goes dark in a wave, one tap lights it back" is a
15-second creative by itself (playbook section 5: food and medicine auctions are not; this one is).

## 3. Core loop

**Minute to minute (a live shift).** One shift = one in-game day = **240 s at 1×** (2× and 3× free; 4× is the `speed` product).
- The **Balance needle** (supply minus demand, shown as a gauge with a green band of ±3 % and fictional "grid hertz") is the
  always-visible goal of the moment.
- Player actions, all single taps (no hold buttons): ramp a plant one step up/down (10 % of its capacity per tap, takes effect after
  the plant's ramp time), start/stop a peaker, set a battery to Charge / Hold / Discharge / Auto, send a crew to a fault, shed or
  restore a district by hand (priority order), call Demand Response (smart meters, cooldown).
- Commands are **queued and applied at the next fixed sim step** (0.25 s game time; LL Pit Lane Boss #133: the result never depends on
  the exact millisecond of the tap). The UI shows "takes effect in 6 s" for ramps.

**Shift end (natural break).** Report card: uptime %, customers served, revenue, fuel, crew, profit, outage minutes, CO2 stars,
1-3 shift stars. `Collect` → cash. House interstitial may show here (never in the first session, pacing rules), rewarded "shift bonus" offer here.

**Between shifts (meta).** Build tab: plants, lines, substations, crew depot, programmes; City tab: hook up a new district when the
city rating allows; Forecast card: the next shift's weather and scheduled events, so the player prepares (battery charged before the
heatwave, peaker built before the match night).

**Sim model (P1 implements; `sim/` pure RefCounted, fixed step, seeded RNG).**
- District demand `d(t) = base × profile(hour) × weather × events`; profile = morning bump 07-09, midday dip, evening peak 18-21, night low.
- Supply = Σ plant outputs; each plant has capacity, min stable output, ramp time, fuel cost/MWh, availability (wind/solar = weather curve).
- Balance `B = S − D`. `B < −5 %` for 10 game-seconds → automatic under-frequency shedding of the **lowest-priority** lit district
  (Hospital Hill is always last and has a backup generator for 60 s). `B > +8 %` → wasted fuel, and after 20 s a random thermal unit trips.
- Lines: a mostly radial network with two loops; corridor flow > capacity fills an overheat bar; full bar → line trips → downstream
  district dark unless a smart substation (upgrade) reroutes.
- Faults: a fault is a marker on a line/transformer; a crew truck drives along the road graph (`coddy_anim` VehicleMover, never overlap)
  and repairs in `repair_s`; districts behind it stay dark until repaired.
- Revenue = served MWh × tariff; reputation (city rating 0-5 stars) = rolling uptime of the last 5 shifts.
- Determinism: same seed + same command list = same shift result, in process and across two processes (P1 gate).

## 4. The 30-second hook (first launch, after the 3-page pager)

1. 0 s — The city at dusk, camera slowly settling over Old Harbor and Riverside; windows switching on in a wave.
2. 5 s — Evening peak arrives: the needle swings to orange, a soft alarm. Coach: "The city needs more power. Tap **+** on Old Smoky."
3. 10 s — Player taps; the plant's chimney puffs, the needle returns to green, coins float up from the lit districts.
4. 18 s — Thunder flash; a spark on the Riverside line; half of Riverside goes dark **in a ripple**. Coach: "A line is down. Tap the spark."
5. 23 s — Player taps → "Send crew" → a small yellow truck leaves the depot, drives the road, sparks fly.
6. 30 s — Riverside lights back **in a ripple**, a "Back on!" toast, +cash. The goal line now says "Upgrade the Riverside line — $420".

During the tutorial nothing can be lost (time stops while the coach waits, no automatic shedding, no random events; LL Pit Lane Boss:
the tutorial shift has a sim flag `tutorial_safe` that disables bad events, the replay too).

## 5. Chaos events (v1: 15)

Every event: forecast where it makes sense (so planning beats reflex), a counterplay, a cap per shift, and nothing that kills or hurts
anyone (content policy section 11). P1 data file `data/events.json`; probabilities per shift after the tutorial chapter.

| Event | When / chance | Effect | Counterplay |
|---|---|---|---|
| Thunderstorm | forecast 1 shift ahead, 18 %/shift (Ch.2+) | 1-4 line faults during the storm window | crews, tree-trimming programme, smart substations |
| Heatwave | forecast, multi-shift (1-3), 8 % | demand +20-30 % 12:00-20:00 | battery pre-charge, peakers, Demand Response |
| Cold snap | forecast, 6 % | morning peak +25 % 06:00-09:00 | baseload ramp before dawn |
| Big Match Night | scheduled (calendar), Stadium Row | +40 % in Stadium Row 19:30-21:30 | peaker, line upgrade into Stadium Row |
| Half-time kettle surge | during Big Match Night | +15 % city-wide for 20 s at half time | battery Discharge at the whistle |
| Festival of Lights | calendar, Market Square | +25 % demand **and** +50 % tariff for the evening | profitable if you are ready |
| Plant trip | 5 %/shift per thermal unit, never in tutorial | a unit drops to 0 for 30-90 s | reserve margin, battery Auto |
| Squirrel on a transformer | 10 %/shift | one local fault, small district part dark | 1 crew job ("the squirrel is fine") |
| Party balloons in the line | 6 % | short fault, flashing line | 1 crew job |
| Windless fog | 10 % | wind output −70 % for 3 h | gas/hydro |
| Cloud front | 15 % on sunny days | solar −60 % for 1-2 h | battery, hydro |
| Solar eclipse | rare scheduled (once per chapter 4+) | solar −80 % for 2 game-hours, a sky event | plan from the calendar |
| Fuel price spike | 7 % | gas fuel cost ×1.5 for the shift | run renewables/hydro, Demand Response |
| Hospital backup test | calendar | Hospital Hill must stay lit during a window; bonus if uptime 100 % | priority order |
| New district rush | when a district is hooked up | that district's demand overshoots +30 % for its first evening | line upgrade before the hookup |

Caps: at most 2 negative events per shift, never two faults on the same corridor at once, storms never in a shift right after a
1-star shift (anti-tilt), no event while a modal/guide step is open. Event handlers use the event's own time (LL Grid Boss P1).

## 6. Progression

**Districts (8)** — each a visual island of the diorama, unlocked by city rating + hookup cost (soft gates, never a wall: rating
recovers by playing, there is always another investment to buy).

| # | District | Rating to hook up | Hookup | Base peak demand | Flavour |
|---|---|---|---|---|---|
| 1 | Old Harbor | start | — | 18 MW | warehouses, cranes, harbour lights |
| 2 | Riverside | start | — | 22 MW | homes, river promenade |
| 3 | Hilltop | 2.0★ | $2,000 | 20 MW | villas, wind ridge site |
| 4 | Market Square | 2.5★ | $6,500 | 28 MW | festival square, shops |
| 5 | Tech Park | 3.0★ | $18,000 | 40 MW | data halls (flat, high night load) |
| 6 | Stadium Row | 3.5★ | $45,000 | 30 MW (+40 % on match nights) | stadium, fan zone |
| 7 | Hospital Hill | 3.5★ | $90,000 | 26 MW, priority | hospital, backup generator |
| 8 | Airport Flats | 4.0★ | $180,000 | 48 MW | airfield lights, solar flats |

**Chapters (5)** give the story frame and the event calendar: 1 Two districts (tutorial + first week), 2 Storm season, 3 Growing
city (Tech Park, data-hall night load), 4 Big nights (stadium, festival, eclipse), 5 Whole city (airport, all events). After
chapter 5: endless mode with a weekly seeded "Grid Challenge" shift (opt-in leaderboard only with consent, CLAUDE §12 / Apple 5.1.2 style rule).

**Plants (6 types × 3 levels)** — Old Smoky steam plant (start, slow ramp, cheap fuel, CO2), Gas peaker (fast, expensive fuel), Hydro
dam (reservoir = stored energy, fast), Wind farm (weather), Solar field (day curve, clouds), Battery park (charge/discharge).
**Grid (16)** — 8 corridors × 2 line upgrades; smart substations (3) enable reroute. **Operations (13)** — crew trucks 2-5 (4),
crew speed (2), tree trimming (2), smart meters / Demand Response (2), dispatcher assistant (3: auto-baseload, auto-battery, auto-crew;
the paid `dispatcher` product unlocks all three at once early, the free path earns them at chapters 3/4/5).

Total **50 investments** + 6 district hookups. No premium currency.

## 7. Economy numbers + balance (P1 final, measured — Patron 2026-10-27)

> Final values from the real sim (Unity C# `VoltCity.Sim`, run under .NET 8 by `unity/Tools/sim_dotnet_check.sh`), 1,000 seeded
> careers x 9 bot policies (`BalanceRunner` full + merge): **BALANCE PASSED**, evidence `P1-VALIDATION.md`. Data:
> `unity/Assets/Resources/data/*.json` (`balance.json` holds every number below). The F0 sketch numbers it replaced are in git
> history. Units: cash in `$` (fictional, no real-world price claims).

**Time.** One shift = 240 sim s = 24 diorama hours, fixed 0.25 s step (960 steps; 10 s = 1 h). Shift 1 (the guided one) starts at
17:30 so the evening peak arrives ~5 s in; later shifts start at 06:00.

**Start.** $600, Old Harbor (18 MW) + Riverside (22 MW), Old Smoky L1 **45 MW** (the free start unit; was 60 MW in F0 — with 60 MW
capacity was never short and plant choice never mattered), 1 crew truck, city rating 2.5★. Guided first shift ≈ **$930 profit**;
end of day 1 ≈ **$1.0k per shift**.

**Grid rules.** Tariff $2 per served MWh. Needle green band ±3 %; B < −5 % for 10 s → automatic shedding, lowest priority first
(Stadium Row, Hilltop, Old Harbor, Riverside, Market Square, Tech Park, Airport Flats; **Hospital Hill last**, first on its own
backup generator for 60 s); B > +8 % for 20 s → a running thermal unit trips for 30-90 s. Governor (primary response) of running
steam / gas / hydro units = 7 % of their capacity. Shed districts reclose by themselves after 5 s of spinning reserve. Districts
grow +0.8 % per shift connected, up to +25 %.

| Plant | L1 / L2 / L3 (MW) | min | delay → ramp | fuel $/MWh | storage |
|---|---|---|---|---|---|
| Old Smoky (steam) | 45 / 80 / 120 | 25 % | 6 s → 2.5 %/s | 0.55 | — |
| Gas peaker | 25 / 45 / 70 | 20 % | 3 s start, 1 s → 12 %/s | 1.35 | — |
| Hydro dam | 20 / 40 / 60 | 0 | 1 s → 15 %/s | 0 | reservoir 130 / 260 / 420 MWh, inflow 6 / 12 / 18 MW × weather |
| Wind farm (Hilltop) | 25 / 50 / 80 | — | weather | 0 | — |
| Solar field | 25 / 50 / 80 | — | day curve, clouds | 0 | — |
| Battery park | 10 / 25 / 45 | — | instant | 0 | 30 / 70 / 130 MWh, 95 % each way |

Lines (8 corridors, base / I / II, MW): Harbor 21/27/36, Riverside 24/32/42, Hilltop 24/34/46, Market 32/41/54, Tech 45/58/76,
Stadium 33/45/58, Hospital 30/38/50, Airport 54/70/92; overheat 0.4 / s per unit of overload, trip → reclose after 15 s. Smart
substations reroute a dark district through the tie partner **only when the partner has room** (no cascade): Harbor↔Riverside,
Market↔Stadium, Hospital↔Hilltop. Crews: 40 m/s on the road graph (+25 % / +50 % with the speed upgrades), repair 8 s (line) / 6 s
(transformer) / 4 s (balloons). Demand Response −8 % / −12 % for 30 s (60 s cooldown, $30).

**Investments (50).** Old Smoky L2-L3, gas / hydro / wind / solar / battery L1-L3 (17 plants); 16 line upgrades + 3 smart
substations (19 grid); crew trucks 2-5, crew speed ×2, tree trimming ×2, smart meters / DR ×2, weather station, dispatcher assistant
×3 (14 operations; the weather station was added because Old Smoky L1 is the free start unit). Order and costs unchanged:
`c_i = 420 × 1.15^i` (first $420 = Riverside line I, the guide's step 6 — guide text fixed to $420 in P3), total
**$3,031,443**, plus district hookups $341,500 (table in section 6, unchanged).

**Income model.** Every investment raises the **contract level** (better grid → better customer contracts) by
`value_i = (c_i / payback_i) / stage_base_i × 0.27`, payback growing linearly from **7 shifts** (first) to **28 shifts** (last)
(`stage_base` = the measured base net per shift for the districts expected at that point of the curve). Revenue, fuel and upkeep
are all multiplied by the contract level (×1.0 at the start, **×6.99** with all 50), so the physical choices (fuel mix, outages)
matter at every stage. District hookups add their customers physically (+18 to +48 MW each).

- Offline ("night crew"): unchanged — 8 h cap, worth 0.7 of the last shift's net at the cap (`offline_plus`: 16 h, ×1.5).
- Normal player model: **6 shifts a day + one overnight away (23.5 h, capped at 8 h)**, buys the cheapest affordable thing after
  every shift (greedy = the mixed reference bot).

Result (1,000 seeds, greedy; net per shift from seed 1):

| Day | Investments owned (median of 50; p5-p95) | Net income / shift | Idle cash at day end (× the day's income) |
|---|---|---|---|
| 1 | 9 (8-9) | $1.0k | 0.12 |
| 3 | 17 | $2.4k | 0.29 |
| 7 | **27 (54 %)** (26-27) | $8.7k | 0.05 |
| 10 | 33 | $9.4k | 0.38 |
| 14 | 38 | $17.8k | 0.74 |
| 21 | 45 | $34.2k | 0.77 |
| 25 | 49 | $45.9k | 0.54 |
| **26** | **50 (100 %)** — median day 26, p95 27 | $46.9k | — |

Premium model (premium + offline_plus + forecast_plus perks): day 7 = **30 (60 %)**, max 31; everything on **day 22** (min 22).
Districts (greedy): Hilltop day 2, Market Square day 4, Tech Park day 7, Stadium Row day 10, Hospital Hill day 14, Airport Flats day
19; chapter 5 finished (3 good shifts + 4.0★) at shift 115.

Gates (all real, all **pass**; `BalanceRunner.Evaluate`): normal day 7 ≤ 70 % in every run (max 28) and ≥ 45 % (min 26); everything
owned on day 25-30 in 100 % of runs; never 2 days in a row without a purchase (longest 1); idle cash ≤ ~1 day of income (p95 1.03,
max 1.23, limit 1.25 — the last two line upgrades cost ~1 day each); premium ≤ 78 % on day 7 (max 31 = 62 %) and never all before
day 19 (min 22); plus:
- **no dominant strategy:** gas-only and battery-only bots never finish chapter 5 within 30 days (≥ 1.56× slower than the mixed bot's
  shift 115); in the plant matrix (14 decision points × 10 weather / event mixes, 40 seeds, unserved energy valued at 4× tariff) every
  plant type is the best next buy somewhere — wind 62, solar 34, hydro 23, gas 7, battery 7, steam 4 of 137 decided cells (gas wins
  plant-trip shifts, battery wins cold snaps, steam the early heat / trip days);
- **shedding is a tool, not a trap:** the never-shed bot gets ≥ 2★ in **97.1 %** of chapter 1-2 shifts;
- **naive player:** taps only the coach highlight → ≥ 2★ in **92.1 %** of chapter-1 shifts (gate 80 %);
- **pay-to-win check:** the paid `dispatcher` (assistant from chapter 1) finishes chapter 5 at shift 115 = the hand-played bot;
- renewables-first (shift 158) and saver (91) finish chapter 5 (no wall); ≤ 2 negative events per shift; no storm right after a
  1-star shift; no bad event in the 3 tutorial shifts.

Consumable sizes stay tied to this curve (IAP.md): 1 / 2.5 / 5 / 10 days of the **current** income (`DayIncome` = 6.7 × last shift
net, floor $1,000). Rewarded caps (CLAUDE §4): single reward ≤ 0.2 day of income (≤ 1/5 of `cash_s`), daily sum ≤ 0.5 day — enforced
in `Economy.RewardOffer`, counts and cooldowns in the save.

## 8. Session length

- Shift 240 s at 1×; a typical session = **2-4 shifts + building ≈ 10-15 min**; the game is fine in a 4-minute bus-stop session
  (one shift) and saves at every shift end and every 30 s inside a shift (atomic, `src/save_repository.gd`).
- Day-1 target: 3 sessions, ~25 min total; tutorial chapter (first 3 shifts) ≈ 12 min.
- Offline summary on return (capped, rewarded ×2 optional). No energy, no timers that block play, no forced waiting.

## 9. Onboarding (CLAUDE §9, onboarding v2)

**Pager (3 pages, `UiPager`, real earned hero frames):** (1) "Keep the lights on" — the evening city; (2) "Fix what storms break" —
a truck at a spark; (3) "Grow a whole city" — the 8-district skyline. Then the coach, one action per step; time is stopped while a step
waits; wrong tap = the step flashes again, no penalty.

| Step | Highlight | Player must… | Advances when |
|---|---|---|---|
| 1 | `+` on Old Smoky (Grid tab) | ramp up once | the ramp command is queued |
| 2 | Balance needle | tap the needle (opens its one-line explainer) | explainer opened |
| 3 | spark marker on the Riverside line | tap it | fault card open |
| 4 | `Send crew` pill | send the truck | crew dispatched (truck drives, time runs only for the truck) |
| 5 | Shift report `Collect` | collect | cash added |
| 6 | Build tab → "Riverside line I" ($420) | buy it | owned (tutorial cash guarantees it is affordable) |
| 7 | Gas peaker card (Build) | build the peaker | owned |
| 8 | `Start` on the peaker during the 2nd evening peak | start it | peaker running |
| 9 | City tab → Hilltop goal line | open Hilltop ("hook up at 2.0★") | card opened — guide done |

**P3 implementation notes (Unity, `unity/Assets/Scripts/Game/Coach.cs`, docs/P3-UI.md):** step 1 shows as the shift opens (the first seconds of the hook play while time waits); between the numbered steps a **balance assist** spotlights the control that brings the needle back ("still short: tap + again", sized by the projected supply so it never asks for a tap that is already on its way; never "turn down" while a district is dark) — the P1 naive-coach model that the balance gate measured; after the guide it continues in chapter 1 as a soft ring (no dim, no pause). The guide's own spark is scripted once the needle is back near green and is mended ~2 s after the truck arrives ("time runs only for the truck"). Newbie audit (bot taps only the highlight, 5 seeds): guide done in 248-359 s, shift 1 = 2★ (uptime 95.9-97.0 %).

Context hints the first time each appears (one card, ≤ 2 lines): battery modes, forecast card, manual shedding (first deficit after
the guide), Demand Response, heatwave, line overheat, smart substation, dispatcher assistant, offline summary, shop (only after
session 2). Skip exists; Settings → "How to play / Replay guide" brings it back; onboarding state is a setting (`intro_done`,
`coach_done`), **not** "save exists" (LL Driftwood P7). Firebase `onboarding_step` / `onboarding_complete` / `onboarding_skip`.
Tests: right action advances, wrong does not, skip → replay, relaunch mid-guide never locks, newbie audit (taps only the highlight)
finishes in < 10 min with ≥ 2★ on shift 1.

## 10. Languages (10)

en, tr, de, es, fr, pt-BR, ru, ja, ko **+ hi**. Why hi: India is the biggest Android audience for a free management game, power cuts are
an everyday theme there (relatable, ASO in hi: "बिजली"), and it fits the fleet's Devanagari font pipeline already used by 8 games.
Game text, store listing and IAP names use the same set. Translation = in-session Haiku sub-agents with the glossary
(`i18n/game.csv`: Balance needle, peaker, shed, Demand Response, crew, district names stay **untranslated proper names** except where
a locale needs transliteration — decided once in P5). Bundled Noto subsets for Latin / Cyrillic / CJK / Devanagari.

## 11. Content policy + IP (binding; the P0 `IP_RULES.md` and `tests/ip_denylist.txt` are built from this)

- Fictional city, fictional company ("Volt City Grid Co."), fictional plant names. **No real utilities or grid operators** (e.g.
  National Grid, ConEd, PG&E, Duke Energy, EDF, E.ON, Enel, TEPCO, Iberdrola, ERCOT, Statnett), **no real plants or dams** (Hoover,
  Three Gorges, Itaipu, Drax, Battersea), **no disaster names** (Chernobyl, Fukushima, Three Mile Island), no real stadiums/teams.
- **"Power Grid" is the title of a well-known board game** (Funkenschlag in German): the words appear only as the lowercase generic
  phrase "power grid" inside a sentence or after our own brand ("Volt City: Power Grid Manager"), never alone as a name, never with
  "board game", never its art style (no auction/city-network board look). Also avoid SimCity, Cities: Skylines, Power to the People.
- No nuclear plant in v1 (tone + no meltdown imagery). Nobody is hurt: outages cost money and rating, never lives; the hospital has
  a backup generator and the text never says patients are in danger. Squirrels and balloons are always "fine". No politics, no real
  energy-price claims, no climate lecture (CO2 stars are a soft score, not a moral).
- Colours/icons must not resemble a real utility's logo (a lightning bolt in a circle is generic; no specific brand colour pairs).
- Denylist test as playbook section 5 (NFKD, leetspeak, separators, `~substring` entries, asset/GLB names, every language, proof
  fixture); generated names checked against the real-people list (crew names are first names only).
