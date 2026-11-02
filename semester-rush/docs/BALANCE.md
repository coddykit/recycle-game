# Semester Rush — balance (P1)

Numbers live in `unity/Assets/Resources/data/balance.json` (+ `map.json` zones / plots / paths, `research.json`,
`events.json`). The headless bot (`BalanceRunner`, run by `BalanceTests`) plays like a sensible new player: auto-fills
the timetable every Sunday (smart once the upgrade is in), opens gates, hires one professor per faculty (a second when a
faculty teaches > 8 classes), builds rooms when classes do not fit, a canteen / crew when students go hungry, a dorm or
marshals when > 8 % are late, the library, upgrades in list order, opens the next zone when prestige and cash allow,
grows the intake when the rooms have seats. Deterministic per seed.

**Time:** a day = 08:48–16:20 game time, five 80-minute slots, a 10-minute bell change before each. 12 game min per real
second in class, 2.5 during a bell change (the walk is watchable) → a day ≈ 53 s, **a week ≈ 4.5 real minutes at 1×**.
A semester = 6 weeks, the 6th is exam week (one exam per course).

**Core numbers:** tuition $8 per attending student per class × prestige multiplier (0.8 … 1.4); professor $400/week
(≤ 3 classes a day); staff $140–160/week; student services $2 per student per week; walking 90 m/min, path capacity
30 students/min (gate 20, shuttle 8, wider path ×2, marshal ×1.6 on one busy path); walking speeds spread ±20 %; a late
student earns half the class. Prestige per week = 45 × (grade − 0.55) + library / clubs / gym bonuses; semester close
adds 70 × (semester grade − 0.55). Zones: Quad at prestige 220 + $7,000, Science at 480 + $30,000.

**Forgiving start:** no crises in the guide or the first week; prestige can only rise in the guide and weeks 1–3; the
previous dean's timetable is ready (only the guide's class is missing); the main hall, canteen and admin are built.

## 2026-11-02 runs

7 weeks, seed 1 (`SevenWeeksStayFiniteProfitableAndNeverStuck`):

```
week stud  late%  hungry held/req grade  income  costs    net  prestige   cash
  1   110    0.0     50    8/8    0.78    3027   1250   1777     110    3727
  2   110    0.0      0    8/8    0.78    3049   1390   1659     121    3936
  3   110    0.0      0    8/8    0.78    3071   1390   1681     131    5617
  4   110    0.0      0    8/8    0.92    3093   1440   1653     154    4070
  5   110    0.0      0    8/8    0.94    3141   1440   1701     177    3971
  6E  110    0.0      0    4/4    0.94    2289   1590    699     224    4370
  7   139    0.0    123    8/8    0.84    4327   1648   2679     245    5549
cash 5549 prestige 245 chapter 1 buildings 4 profs 1 upgrades 4 minCash 1950
```

30 weeks, seed 1 (weeks 15–30; the Quad opens in week 15, the Science Campus in week 27):

```
 15   289   22.8    550   12/20   0.58    6339   2998   3341     421    4103
 16   289   13.5    550   20/20   0.87    9734   3798   5936     441    8439
 18E  289    0.0    119   10/10   0.80    5753   4208   1545     496    7553
 20   289   22.8    138   20/20   0.89   10275   5358   4917     540   13272
 24E  289    0.0      0   10/10   0.93    8880   4978   3902     652   11402
 27   372   16.4      0   20/20   0.90   16962   5424  11538     716   39404
 28   512   12.6    326   28/32   0.83   20859   6904  13955     735   20959
 30E  512    0.0     75   16/16   0.86   15484   7494   7990     804   20176
cash 20176 prestige 804 chapter 3 buildings 14 profs 7 upgrades 11 minCash 761
```

Reading: chapter 1 is calm and profitable (~$1.7k net a week; the 12:00 lunch crunch of week 1 is the guide's lesson).
A new zone is a real step: three new faculties enroll at once, classes do not fit until halls are built (12/20 held),
late rates jump to 13–23 % on the long walk to the quad and hunger returns — the player's next problems. Seeds 1–3 all
reach the Quad by week 20 (`TwentyWeeksReachTheQuadWithoutAWall`). ≈ 70 min to chapter 2, ≈ 2 h to chapter 3 at 1×.

Open: the late game piles cash (no chapter 4–6 sinks in the MVP); Credit Points (plan §5) are not in the MVP — upgrades
cost cash only.
