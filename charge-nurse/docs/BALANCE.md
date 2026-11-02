# Charge Nurse — balance (P1, headless)

All numbers live in `unity/Assets/Resources/data/*.json`; the bot is `Sim/BalanceRunner.cs` (a sensible player who does
not micro-manage: answers ambulances, hands over in the window, calls the surge team on a surge day, and between shifts
spends on what the bottleneck lens named most often that day). Tests: `BalanceTests` (7 days profitable, never broke,
average ≥ 4.5/9 stars; day 1 earns with < 20 % bad outcomes on 3 seeds; Town Hospital within 20 shifts).

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

## 7 days, seed 42 (`BalanceRunner.Run(gd, 42, 7)`)

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

Bot purchases: day 1 X-ray + doctor + bay, day 2 cleaner + triage training, day 3 obs bed + doctor, day 5 **Town
Hospital**, day 6 lab, day 7 surge team (flu week). 20 days (seed 9): City General on day ~16–17, 74 treated / shift,
net ~9 k, reputation ~70. Rough days exist (a crash or flu week into a full ward drops a day to 1–3 stars) and recover
within 1–2 days — the bot does not tap Discharge / Clean or use rewards, a real player has those levers.

Not tuned yet (needs real play): the money curve after City General (cash piles up — chapters 4–6 are v1.1), the
night team share, inspection frequency.
