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
student earns half the class. Prestige per week = 30 × (grade − 0.55) + library 2 / clubs 2 / gym 1 / dorm comfort 1;
semester close adds 46 × (semester grade − 0.55). Zones: Quad at prestige 220 + $7,000, Science at 680 + $60,000
(polish pass 2026-11-02, see "Tempo" below).

**Forgiving start:** no crises in the guide or the first week; prestige can only rise in the guide and weeks 1–3; the
previous dean's timetable is ready (only the guide's class is missing); the main hall, canteen and admin are built.

## 2026-11-02 runs (P1, before the polish pass)

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

## Tempo to the plan (polish pass 2026-11-02)

Plan section 6 content map: chapter 2 (Liberal Arts Quad) from ~1 h, chapter 3 (Science Campus) from ~3 h. A week ≈ 4.46
real minutes at 1× (live time only; Sunday planning and the reports come on top). The Quad was on time; the Science
Campus came at ~2 h because both gates were cash gates the bot passed early (prestige 480 was reached by week 18, the
$30,000 by week 27). Now the Science Campus is a **prestige** gate (the plan's "chapter gate = prestige achieved by play")
and prestige grows ~1/3 slower, so 50 weeks end at ~770–790 prestige instead of the 1000 cap (headroom for chapters 4–6).

| Data | Before | After |
|---|---|---|
| `balance.json` `prestige.weekly_gain` / `semester_gain` | 45 / 70 | 30 / 46 |
| `balance.json` `prestige.clubs` / `gym` (dorm comfort 1 unchanged) | 3 / 2 | 2 / 1 |
| `balance.json` `buildings.library.prestige` | 3 | 2 |
| `map.json` science zone prestige / cost | 480 / $30,000 | 680 / $60,000 |

Bot (`BalanceRunner`, 1×), week the zone is bought (it opens the next week) → real time:

| Seed | Quad before | Quad after | Science before | Science after |
|---|---|---|---|---|
| 1 | 14 → 62 min | 14 → 62 min | 27 → 2.0 h | 38 → 2.8 h |
| 2 | 13 → 58 min | 13 → 58 min | 24 → 1.8 h | 36 → 2.7 h |
| 3 | 13 → 58 min | 15 → 67 min | 26 → 1.9 h | 36 → 2.7 h |
| plan | ~1 h (45–75 min at ±25 %) | | ~3 h (2.25–3.75 h) | |

Guarded by `BalanceTests.ChaptersOpenOnThePlansClock` (seeds 1–3, 50 weeks: Quad after weeks 10–17, Science after
weeks 30–50, prestige below the cap). Day 1 / the first week are unchanged (no crisis, prestige cannot fall in weeks 1–3,
first week +$1.8k); `SevenWeeksStayFiniteProfitableAndNeverStuck` and `TwentyWeeksReachTheQuadWithoutAWall` stay green.

7 weeks, seed 1, after (week 1–3 identical in money; prestige rises a little slower):

```
week stud  late%  hungry held/req grade  income  costs    net  prestige   cash
  1   110    0.0     50    8/8    0.78    3027   1250   1777     107    3727
  2   110    0.0      0    8/8    0.78    3042   1390   1652     114    3928
  3   110    0.0      0    8/8    0.78    3056   1390   1666     121    5595
  4   110    0.0      0    8/8    0.92    3071   1440   1631     136    4026
  5   110    0.0      0    8/8    0.94    3103   1440   1663     152    3889
  6E  110    0.0      0    4/4    0.94    2289   1590    699     183    4288
  7   139    0.0    123    8/8    0.84    4215   1648   2567     197    5355
```

45 weeks, seed 1, after (weeks 15–45; the Quad opens in week 15, the Science Campus in week 39):

```
 15   289   22.8    550   12/20   0.58    5963   2858   3105     315    3680
 16   289   13.5    550   20/20   0.87    9146   3658   5488     328    7568
 18E  289    0.0    119   10/10   0.80    5753   3928   1825     365    6725
 24E  289    0.0      0   10/10   0.93    8880   4838   4042     471   10822
 30E  372    0.0     68   10/10   0.90   12324   5284   7040     578   60305
 36E  479    8.8     51   10/10   0.75   13629   5498   8131     667  128620
 38   618   10.2   1097   20/20   0.63   22079   6216  15863     690  155637
 39   758    1.4   1051   28/32   0.64   27421   7956  19465     702   95402
 42E  758   15.9    145   16/16   0.59   21304   8616  12688     736  115559
 45   758   15.2    363   31/32   0.67   27260   8896  18364     766  163050
cash 163050 prestige 766 chapter 3 buildings 17 profs 7 upgrades 15 minCash 575
```

Before (30 weeks, seed 1, weeks 15–30; the Quad opened in week 15, the Science Campus in week 28):

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

Reading (before and after): chapter 1 is calm and profitable (~$1.7k net a week; the 12:00 lunch crunch of week 1 is the guide's lesson).
A new zone is a real step: three new faculties enroll at once, classes do not fit until halls are built (12/20 held),
late rates jump to 13–23 % on the long walk to the quad and hunger returns — the player's next problems. Seeds 1–3 all
reach the Quad by week 20 (`TwentyWeeksReachTheQuadWithoutAWall`). After the polish pass: ≈ 1 h to chapter 2,
≈ 2.7–2.8 h to chapter 3 at 1× (≈ 3 h with planning time).

Open: the late game piles cash (no chapter 4–6 sinks in the MVP; the $60,000 Science Campus is one sink, the Quad era
still ends with ~$150k in the bank — the bot grows the intake, a real player has more to buy); Credit Points (plan §5) are not in the MVP — upgrades
cost cash only.
