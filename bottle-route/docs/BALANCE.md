# Bottle Route — balance (P1)

Numbers live in `unity/Assets/Resources/data/balance.json` (+ `map.json` districts, `research.json`, `events.json`).
The headless bot (`BalanceRunner`, run by `BalanceTests`) plays like a sensible new player: suggested routes for idle
trucks, sells bales on good prices or a full yard, banks the till, sends technicians, buys districts → research →
dispatch office → drivers → bigger bins / sensors on overflowing points → new machines, keeping a cash buffer.

A day = 600 game minutes at 2.5 game min per real second = **4 real minutes at 1×**.

## 2026-11-02 runs (after: crews clear jams on a visit, research ticks on real time, district costs 3.5k / 14k / 26k)

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

## What the tests hold

- day 1 earns money, ≤ 25 % turned away (`FirstDayFeelsGood`);
- a week: positive total, day 7 earns more than day 1, ≤ 15 % turned away on day 7 (`SevenDaysStay…`);
- 20 days reach chapter 2 (`TwentyDaysReachOldTownAndMore`);
- the guide and the first 3 days never lower the rating and have no crises.

## Known tuning questions (for P1 sign-off)

1. Efficiency stars sit at 1/3 (containers per km 8–13 vs the 25 / 40 thresholds): either lower the thresholds or make
   the suggested route smarter (it currently serves "any ≥ 30 %" when nothing is due — the bot over-drives).
2. The "empty at ~80 %" rule should be good but not always optimal: add a bot variant per policy and compare (plan §15 P1).
3. Bale income is lumpy (sales on days 2, 10, 16): consider smaller PET bales or tenders (out of MVP).
4. University / Stadium pacing needs a run with a greedier bot (2 h / 7 days targets).
