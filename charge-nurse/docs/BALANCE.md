# Charge Nurse — balance (P1, headless)

All numbers live in `unity/Assets/Resources/data/*.json`; the bot is `Sim/BalanceRunner.cs` (a sensible player who does
not micro-manage: answers ambulances, hands over in the window, calls the surge team on a surge day, and between shifts
spends on what the bottleneck lens named most often that day). Tests: `BalanceTests` (7 days profitable, never broke,
average ≥ 4.5/9 stars, more beds and staff than at the start; day 1 earns with < 20 % bad outcomes on 3 seeds; Town
Hospital within 20 shifts; `ChaptersArriveOnThePlanClock`: Town Hospital on shift 11–19 and City General on shift 34–56,
seeds 9 and 42).

## Time

A shift = 07:00–19:00 game time at 3 game min / real s = **4 real minutes** at 1×. Handover window 18:30–19:00. The night
team then runs 12 h by itself at 50 % capacity (80 % with the Night Team upgrade), safe (nobody worsens or leaves at
night). Upgrades run on live shift minutes (120–420, i.e. 40 s–2.3 real min at 1×; plan: no wall-clock builder timers).

## Pipeline numbers

| | green | yellow | orange | red |
|---|---|---|---|---|
| share of walk-ins | 45 % | 32 % | 17 % | 6 % |
| case pay | 60 | 120 | 220 | 380 |
| treatment (min) | 12 | 22 | 34 | 48 |
| safe wait (min) | 240 | 120 | 45 | 15 |
| scan needed | 12 % | 45 % | 70 % | 80 % |
| admitted | 2 % | 12 % | 45 % | 85 % |
| ward stay (min) | 180 | 300 | 420 | 600 |

Walk-ins 2.0 / h × hour curve × reputation (0.7–1.3) × wing (clinic 1.0, town 1.45, city 2.0), night × 0.3. Ambulances
1.2 / 2.5 / 4 per day by chapter (+50 % pay). Tasks: register 2, triage 4 (82 % right, 95 % with training), discharge
paperwork 6, clean a bay 12 / a bed 20, porter trip 4 + 1 min / 40 m (+6 lift to ICU / surgical), surgery 90.
Red takes a doctor from a green / yellow patient. Waiting past the safe wait: worse once (+1 severity); a worsened red
past 3× + 45 min is transferred (no death); greens / yellows leave after 260 / 320 min.

Stars: care (bad outcomes ≤ 15 / 8 / 3 %), waits (door-to-doctor ≤ 90 / 45 / 20 min), flow (blocked bed-minutes ≤ 30 / 15
/ 6 %). Reputation = EMA (α 0.25) toward stars/9 − bad outcomes (≤ 5 per day); the guide and days 1–3 can only raise it.
Burnout +12 after a day > 85 % busy, −8 otherwise; ≥ 60 slows everyone 10 % and makes sick calls likelier.

Crises from day 4, 0.6 / 1.0 / 1.4 per day by chapter: flu week (×1.4 walk-ins, 2 days, announced — the surge team
helps), highway crash (4–12 tagged arrivals, sized to the chapter), power cut (scans + OR pause 45 min; generator
upgrade prevents), scanner down (120 min), sick calls (one team −1 today), stomach bug (2 ward beds closed 2 days),
supply delay (supplies ×1.5, announced), inspection (bonus 400 / fine 200 at the shift change).

## Tempo to the plan (polish pass 2026-11-02)

Plan §6: chapter 2 (Town Hospital) at ~1 h, chapter 3 (City General) at ~3 h. A shift is 4 real min at 1× (+ ~20–30 s
on the morning plan and the report), so ~1 h ≈ shift 13–15 and ~3 h ≈ shift 40–45. The P1 numbers opened both wings far
too early (cash piled up to ~380 k by day 60). Only the wing prices changed (`map.json`); day-1 economy, staff, rooms,
upgrades and crises are untouched.

| `map.json` wings | before | after |
|---|---|---|
| Town Hospital cost (reputation 60) | 10 000 | **38 000** |
| City General cost (reputation 72) | 40 000 | **180 000** |

Bot (`BalanceRunner`, 60 days, scratch harness over the same data):

| seed | Town Hospital before → after | City General before → after | cash at day 60 before → after |
|---|---|---|---|
| 9 | day 5 → **13** | day 16 → **44** | 383 737 → 128 611 |
| 42 | day 5 → **14** | day 17 → **44** | 381 939 → 111 093 |
| 3 | day 6 → **14** | day 17 → **43** | 363 783 → 139 325 |
| 7 | day 5 → **13** | day 16 → **46** | 370 477 → 94 627 |
| 11 | — → **14** | — → **47** | — → 93 899 |

In real time at 1×: Town Hospital ~52–56 min of shifts (~1 h with the plan / report screens; plan 1 h), City General
~2.9–3.1 h (plan 3 h) — inside ±25 %. Week 1 is now the clinic chapter: the bot fills the clinic (X-ray, doctors, bays,
obs beds, triage training, porter tablets, waiting hall) and saves for the Town Hospital; all 16 upgrades are bought by
the time City General opens.

### 7 days, seed 42 — after (`BalanceRunner.Run(gd, 42, 7)`)

```
day  arr treat bad  wait blk%   fees  ward  costs    net stars
  1   36    34   4    49    0   4290   210   1511   2989   5/9
  2   30    30   0    14    2   3780    70   1578   2272   9/9
  3   37    38   3    21    7   6682   219   1888   5013   6/9
  4   39    39   2    17    0   4822   146   1988   2980   8/9
  5   30    28   1    19    6   5206   296   1992   3510   8/9
  6   28    30   2    12    1   4803   225   1935   3093   8/9
  7   29    30   1    12    0   4458   225   1981   2702   8/9
cash 15109 reputation 77.1 chapter 1 beds 10 staff 11 upgrades 5 minCash 1076
```

Day 1 is unchanged (profitable on every seed: +1 303 … +2 989, bad outcomes ≤ 12 %), never broke (min cash 1 076),
average 7.4 / 9 stars. The old assertion "day 7 treats more than day 1" assumed the Town Hospital on day 5; it now checks
that the hospital grew (beds 6 → 10, staff 6 → 11).

### 7 days, seed 42 — before (P1)

```
day  arr treat bad  wait blk%   fees  ward  costs    net stars
  1   36    34   4    49    0   4290   210   1511   2989   5/9
  2   30    30   0    14    2   3780    70   1578   2272   9/9
  3   37    38   3    21    7   6682   219   1888   5013   6/9
  4   39    39   2    17    0   4822   146   1988   2980   8/9
  5   30    28   1    19    6   5206   296   1992   3510   8/9
  6   48    51   2    14    1   7244   225   2668   4801   8/9
  7   64    58   2    26    3   7580   150   3281   4449   7/9
cash 7764 reputation 73.5 chapter 2 beds 14 staff 13 upgrades 4 minCash 1076
```

Bot purchases (before): day 1 X-ray + doctor + bay, day 2 cleaner + triage training, day 3 obs bed + doctor, day 5 Town
Hospital, day 6 lab, day 7 surge team (flu week). Rough days exist (a crash or flu week into a full ward drops a day to
1–3 stars) and recover within 1–2 days — the bot does not tap Discharge / Clean or use rewards, a real player has those
levers.

Not tuned yet (needs real play): the money curve after City General (cash still piles up — chapters 4–6 are v1.1), the
night team share, inspection frequency, whether ~9 shifts of saving in a full clinic feel slow (a progress hint on the
Town Hospital card would help; the cash packs / starter scale with day income, unchanged).
