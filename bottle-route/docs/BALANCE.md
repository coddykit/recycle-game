# Bottle Route — balance (P1)

Numbers live in `unity/Assets/Resources/data/balance.json` (+ `map.json` districts, `research.json`, `events.json`).
The headless bot (`BalanceRunner`, run by `BalanceTests`) plays like a sensible new player: suggested routes for idle
trucks, sells bales on good prices or a full yard, banks the till, sends technicians, buys districts → research →
dispatch office → drivers → bigger bins / sensors on overflowing points → new machines, keeping a cash buffer.

A day = 600 game minutes at 2.5 game min per real second = **4 real minutes at 1×**.

## 2026-11-02 runs, before the polish pass (after: crews clear jams on a visit, research ticks on real time, district costs 3.5k / 14k / 26k)

7 days, seed 42:

```
day  arr  turned%  coll   km   perKm  fees  sales  costs   net   stars  rating
  1    99     1.0  1059   101   10.5  1059      0    438    621   6/9
  2    83     0.0   660    88    7.5   670    446    420    696   6/9
  3   111     0.0   877    77   11.3   899      0    405    494   6/9
  4    90     1.1   772    89    8.7   797      0    512    286   6/9
  5    93     0.0  1036    97   10.7  1075      0    522    553   6/9
  6   129     0.0  1326   106   12.5  1381      0    550    831   7/9
  7   169     3.6  2158    99   21.8  2275      0    539   1735   6/9
```

20 days, seed 9: Old Town opens on day 15 (~60 min of play; plan §6: 30–90 min), 8 research projects, Green Rating 65.
University is not reached in 20 days by this cautious bot (plan: 1.5–2.5 h) — a player who buys machines earlier gets
there sooner.

## Polish pass 2026-11-02: tempo to the plan's content map (§6)

Target at 1× (a day = 4 real minutes): Old Town at ~30 min (day ≈ 7.5), University Hill at ~1.5 h (day ≈ 22.5),
Stadium & Fairground at ~2.5 h (day ≈ 37.5); ±25 %.

**Changes**

| What | Before | After | Why |
|---|---|---|---|
| `map.json` Old Town cost | 3 500 | **1 500** | the bot's research spending kept cash under 3.5k + buffer for two weeks |
| `map.json` University Hill cost | 14 000 | **10 000** | reach ~1.5 h |
| `map.json` Stadium cost / Green Rating | 26 000 / 60 | **30 000 / 64** | was rating-bound and early once the spiral below was fixed |
| `balance.json` efficiency stars (containers per km) | 12 / 25 / 40 | **10 / 18 / 28** | tuning question 1: the efficiency star sat at 0–1; a good ring now earns 2 |
| Sim: a "sensor offline" crisis | stayed until a truck visited — but the frozen ≤ 40 % number kept the machine off every route, so it never got visited (seed 5: one machine turned away 100 % for 3+ weeks, rating 64 → 36) | **resyncs overnight** (`NextDay`, `sensor_fixed` event; plan §7: crises are short) | test `AWrongSensorResyncsOvernight` |
| Bot | technicians only for jams | also for an offline sensor (what the crisis tip tells the player) | |

Old Town and University Hill keep their Green Rating gates (45 / 55).

**District day (bot, 60 days, 13 seeds 1–12 + 42; median [sorted], "-" = not reached)**

| District | Plan | Before | After |
|---|---|---|---|
| Old Town | ~30 min (day 7.5) | day **14** ≈ 56 min [10,12,13,13,13,14,14,16,20,20,23,41,-] | day **8** ≈ 32 min [4,6,6,6,6,7,8,8,8,9,10,10,10] |
| University Hill | ~1.5 h (day 22.5) | day **41** ≈ 2.7 h [25,26,28,28,32,40,41,47,48,58,-,-,-] | day **22** ≈ 88 min [19,21,21,21,22,22,22,23,25,25,26,26,28] |
| Stadium & Fairground | ~2.5 h (day 37.5) | day **58** ≈ 3.9 h [36,39,42,46,49,53,58,-,-,-,-,-,-] | day **36** ≈ 2.4 h [31,32,33,34,35,35,36,37,38,38,39,39,42] |

The new test `TempoFollowsThePlanContentMap` (seeds 1–5, 50 days) holds the medians inside ±25 % (now 32 / 88 / 144 min).
Chapters 5–7 (Industrial Estate, Riviera, Hill Villages) are not built yet: after the stadium the bot banks 60–110k by
day 50 — the next content (or cash sinks) belongs there.

**7 days, seed 42 — after** (day 1 unchanged: +621, 1 % turned away; Old Town bought on day 7):

```
day  arr  turned%  coll   km   perKm  fees  sales  costs   net   stars  rating
  1    99     1.0  1059   101   10.5  1059      0    438    621   7/9
  2    83     0.0   660    88    7.5   677    446    420    703   6/9
  3   111     0.0   877    77   11.3   906      0    405    500   7/9
  4    90     1.1   772    89    8.7   809      0    512    297   6/9
  5    93     0.0  1036    97   10.7  1086      0    522    564   7/9
  6   129     0.0  1326   106   12.5  1404      0    550    854   7/9
  7   169     3.6  2158    99   21.8  2301      0    539   1762   7/9
```
(Before: the table above; the same days earn within ~3 % of it — the extra efficiency star raises the rating and so the fee multiplier a little.)

## Chapters 5–7 (2026-11-02): Industrial Estate, Riviera Resort, Hill Villages + Island

Plan §6 at 1× (a day = 4 real minutes): Industrial Estate from ~4 h (day ≈ 60), Riviera Resort from ~6 h (day ≈ 90), Hill
Villages + Island from ~8 h (day ≈ 120; the island is the second district of chapter 7 and needs the villages first).

**What is new (all numbers in the data files)**

| Chapter | Content | Data |
|---|---|---|
| 5 Industrial Estate | 5 sites (3 machines, works canteen, catering kitchen); a sixth archetype **bulk** (20–60 containers a visit, weight 0 in older districts so chapters 1–4 replay exactly); **bale tenders**: an offer to deliver N bales of one material by day D at a fixed 1.25–1.45 × the usual bale price; accept / decline in the Depot sheet, at most 2 open, an offer lapses after 2 days, an accepted one that runs out costs nothing but the premium; delivered from the yard by a button or automatically at day end; **bulk counting centre** (depot upgrade, +1 200 counted per hour, 15 000, unlocked with the district) | `map.json` `industrial`, `balance.json` `tenders`, `bulk_centre`, `archetypes[5]` |
| 6 Riviera Resort | 5 sites (3 machines, 2 **hotels** = big HoReCa: 600 L, +60 per pickup); **summer season**: days 10–14, 30–34, 50–54 … (every 20 days, 5 days) the season districts' points get **×3 arrivals** (in the no-sensor estimate too: the season is on the calendar), heat waves are 3× likelier, start / end are announced in the morning and shown as a chip | `riviera`, `points.hotel`, `season` |
| 7 Hill Villages | 4 **large remote machines** (`machine_l`, 1 000 L, lease 34/day) + a tavern, ≈ 10–14 km from the depot by road (the fuel dilemma: perKm falls from ≈ 17 to ≈ 11) | `villages`, `points.machine_l` |
| 7 Island | 2 large machines + a hotel behind the **ferry** (edge flag `ferry`, pier to pier): a truck waits at the pier for the next departure (every 40 game minutes from 09:00), pays a 30 ticket, sails at 18 km/h with no fuel; the route estimate walks the clock and includes the wait and the tickets | `island` (`requires: villages`), `ferry` |

Unlock costs / Green Rating: Industrial **130 000** / 66, Riviera **250 000** / 68, Hill Villages **320 000** / 70, Island
**200 000** / 72. The bot also learned: accept every tender, keep promised material out of spot sales, deliver when the yard
has it, buy the bulk centre when the count line backs up.

**District day (bot, 150 days, 13 seeds 1–12 + 42; median [sorted]; "before" = the map had no chapters 5–7)**

| District | Plan | Before | After |
|---|---|---|---|
| Industrial Estate | ~4 h (day 60) | – | day **59** ≈ 3.9 h [55,55,56,57,57,58,59,60,60,62,62,63,63] |
| Riviera Resort | ~6 h (day 90) | – | day **89** ≈ 5.9 h [85,87,87,87,87,88,89,90,91,91,92,93,95] |
| Hill Villages | ~8 h (day 120) | – | day **119** ≈ 7.9 h [115,117,118,118,118,119,119,119,120,122,124,124,128] |
| Island | in chapter 7 (8–10 h) | – | day **138** ≈ 9.2 h [132,134,135,136,137,137,138,138,138,142,142,145,150] |
| Old Town / University / Stadium | 30 min / 1.5 h / 2.5 h | day 8 / 22 / 36 | **unchanged** (day 8 / 22 / 36; the 7-day seed-42 table above is identical) |

**The bank finds a sink** (median cash of the same 13 seeds): before, the bot kept every coin after the stadium — **142k on
day 60, 331k on day 90, 532k on day 120, 737k on day 150**; after, the four districts (900k) + their machines, hotels and
the bulk centre take it — **50k / 46k / 40k / 154k** (day 50 is 75k both times: chapters 1–4 untouched).

**A typical day per chapter** (seed 42, median of 10 days; sales = mean, they are lumpy):

```
days      arrivals turned  collected   km  perKm   fees  sales  costs    net  stars
 41-50       628    2.1 %     6928    392   17.3   7472   1321   1522   6770   7/9   (ch4)
 61-70      1076    2.1 %     9852    598   17.4  10519   1863   2281   9212   6.5/9 (ch5)
 91-100      895    0.5 %     8970    750   11.8   9620   1725   2719   8890   7/9   (ch6)
121-130      907    1.1 %     8772    871   11.4   9440   1688   2748   9692   6.5/9 (ch7)
141-150      872    0.8 %     8793    848   10.3   9362   8047   3160  15280   6.5/9 (ch7 + island)
```

The test `LateChaptersFollowThePlanContentMap` (seeds 1–5, 145 days, ≈ 3 s) holds the medians inside ±25 % (now
**228 / 352 / 476 min**, island 544 min); `TempoFollowsThePlanContentMap` still reads 32 / 88 / 144 min.

**Tuning notes for later:** the bot's fleet hits the 8-truck cap around chapter 5, so its collections stay flat (~9k a day)
while km grow — a player with compactor trucks and a tuned Dispatcher earns more, and the late chapters reward exactly
that; the 8-truck cap, the electric fleet (plan §4) and village "fixed every 3 days" policies are the next levers. Tenders
pay ≈ 5–15 % of income in the bot (more on the days they land).

## What the tests hold

- day 1 earns money, ≤ 25 % turned away (`FirstDayFeelsGood`);
- a week: positive total, day 7 earns more than day 1, ≤ 15 % turned away on day 7 (`SevenDaysStay…`);
- 20 days reach chapter 2 (`TwentyDaysReachOldTownAndMore`);
- the plan's district times within ±25 % (`TempoFollowsThePlanContentMap`, medians of 5 seeds);
- an offline sensor resyncs by the next morning (`AWrongSensorResyncsOvernight`);
- the guide and the first 3 days never lower the rating and have no crises.
- chapters 5–7 within ±25 % of the plan (`LateChaptersFollowThePlanContentMap`, 5 seeds × 145 days);
- tenders: only with the Industrial Estate, ≤ 2 open, accept → deliver pays the tender price, auto-delivery at day end,
  offers lapse and accepted ones run out without a fine; the season window ×3 only in the season districts; the ferry
  wait in the estimate and on the road (no fuel / km on the water, tickets), a recall at the pier never crosses; an older
  save without the new fields loads (`ChapterTests`).

## Known tuning questions (for P1 sign-off)

1. ~~Efficiency stars sit at 1/3~~ — thresholds lowered to 10 / 18 / 28 (2026-11-02); the suggested route still serves
   "any ≥ 30 %" when nothing is due (the bot over-drives).
2. The "empty at ~80 %" rule should be good but not always optimal: add a bot variant per policy and compare (plan §15 P1).
3. Bale income is lumpy (sales on days 2, 10, 16): consider smaller PET bales or tenders (tenders are in since chapters 5–7, 2026-11-02).
4. ~~University / Stadium pacing~~ — tuned to the plan's §6 map (2026-11-02, above).
