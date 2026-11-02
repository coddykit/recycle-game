# Pour & Roast — balance (P1)

Numbers live in `unity/Assets/Resources/data/balance.json` (+ `venues.json`, `menu.json`, `research.json`,
`events.json`). The headless bot (`BalanceRunner`, run by `BalanceTests`) plays like a sensible new player: deadline-first
rule + batching, dials the grinder in when shots go bitter, unjams it, sizes tomorrow's milk order from today's demand,
preps the urn on rain days, hires a barista when people walk out, buys group heads / tables / the grinder / research / the
next venue while keeping a cash buffer; in the roastery it buys green lots, roasts the best open origin on its best
profile, taps Drop in the sweet spot, blends and signs contracts once the beans are good enough.

A service day = 660 game minutes (07:00–18:00) at 3 game min per real second = **3.7 real minutes at 1×** (+ the morning plan).

## 2026-11-02 runs

7 days, seed 42 (`SevenDaysStayFiniteProfitableAndNeverStuck`; the bot skips the guide — a real player gets the second
barista, a table, batching and the deadline rule during it):

```
day venue    wx     cust served walk%  items  revenue  costs    net  review rating   cash
  1 cart     sunny   169    167   0.6    262      921    272    649   3.60   2.83    929
  2 cart     rain    131    131   0.0    213      764    332    431   3.64   3.07   1300
  3 cart     sunny   190    190   0.0    292     1031    364    667   3.59   3.23   1427
  4 cart     fog     160    159   0.0    256      900    392    509   3.70   3.37   1625
  5 cart     rain    143    141   0.0    231      810    414    396   3.81   3.50   1692
  6 cart     heat    238    229   3.4    390     1365    436    929   3.71   3.57   2311
  7 cart     sunny   200    186   6.5    328     1089    427    662   3.62   3.58   2302
```

30 days, seed 9 (P1 numbers, before the 11-02 polish tempo pass below): Corner Cafe opens on **day 16** (~70 min of play;
plan §6: 1:15 h), Roastery Hall on **day 28** (~2 h; plan: 3:00 h — the bot saves rather than decorating). Supplier beans (quality 52) cap reviews near 4.0 ★; the first own
roasts (quality 85–90) push the day-30 average to 4.8 ★ — the roastery is the road to 5 ★ (design §4 B). 0 stuck tickets.

```
 1 cart      revenue  848  net  612  review 3.55  rating 2.82
 7 cart      revenue 1418  net  992  review 3.76  rating 3.64
15 cart      revenue 1312  net  912  review 4.05  rating 4.03
20 corner    revenue 3415  net 2949  review 3.85  rating 3.75   (net incl. the cart's branch income)
25 corner    revenue 2957  net 2485  review 4.14  rating 4.00
30 roastery  revenue 4718  net 5575  review 4.79  rating 4.31   (beans quality 87, wholesale + 2 branches)
```

## Tempo pass (cloud polish, 2026-11-02)

Clock: a day = 3.7 real min of service + ~1 min of morning plan ≈ **4.7 min at 1×**; the venue time below = (day − 1) × 4.7.
Target (plan §6): Corner Cafe at 1:15 h, Roastery Hall at 3:00 h, ±25 % (56–94 min / 135–225 min).

Changes (`venues.json`): Roastery Hall `cost` 12000 → **32000**, Corner Cafe `rent` 140 → **170**. Chapter 2 was 12 days
(~55 min) against the plan's 1:45 h: the corner earns ~2.5–3k a day, so the hall was bought after barely a week of saving.
Nothing in chapter 1 changed (days 1–15 are identical), so day 1 and the 7-day run are untouched.

| seed | Corner before | Corner after | Roastery before | Roastery after |
|---|---|---|---|---|
| 9 | day 16 · 70 min | day 16 · 70 min | day 28 · 126 min | **day 36 · 163 min** |
| 42 | day 18 · 79 min | day 18 · 79 min | day 28 · 126 min | **day 38 · 173 min** |
| 3 | day 18 · 79 min | day 18 · 79 min | day 29 · 131 min | **day 38 · 173 min** |
| 7 | day 23 · 103 min | day 23 · 103 min | day 33 · 149 min | **day 41 · 187 min** |
| 11 | day 17 · 75 min | day 17 · 75 min | day 29 · 131 min | **day 40 · 182 min** |
| 21 | day 14 · 61 min | day 14 · 61 min | day 27 · 121 min | **day 35 · 159 min** |
| **median** | **77 min (+3 %)** | **77 min (+3 %)** | **128 min (−29 %)** | **173 min (−4 %)** |

Seed 7 opens slowly on the cart (corner at 103 min, +37 %): that is the cart's weather draw, not chapter 2; the median and
5 of 6 seeds are inside ±25 %. Every seed: 0 stuck tickets, cash never below 280, rating at day 45 4.7–4.95 ★.

45 days, seed 9, after:

```
day venue    wx     cust served walk%  items  revenue  costs    net  review rating   cash
  1 cart     sunny   149    149   0.0    240      848    236    612   3.55   2.82    892
  7 cart     sunny   253    240   4.7    416     1418    427    992   3.76   3.64   2318
 15 cart     sunny   221    221   0.0    355     1312    400    912   4.05   4.03   4624
 16 corner   heat    317    236  24.9    489     1542    644   1394   2.84   3.67   2658
 20 corner   sunny   460    459   0.0    792     3415    991   2919   3.85   3.75   5199
 25 corner   sunny   419    419   0.0    678     2957    998   2455   4.14   4.00  10584
 30 corner   sunny   409    408   0.0    678     2949   1064   2381   4.15   4.12  22433
 35 corner   sunny   494    493   0.0    838     3683   1086   3094   4.30   4.25  35311
 36 roastery sunny   520    438  15.4    730     3410   1337   4020   3.77   4.10   6971
 40 roastery rain    470    469   0.0    799     4883   1270   5638   5.00   4.66  16285
 45 roastery heat    618    616   0.0   1041     6551   1876   6622   5.00   4.94  42275
```

The corner now plateaus near 4.1–4.3 ★ on supplier beans for a while before the hall — the "my reviews are stuck, I need
my own beans" moment the roastery chapter answers. Open for the owner: the move-in day of each venue still has a walk-out
spike (corner 25 % on a heat day, hall 15 %) while the new bar is staffed — unchanged by this pass.

## What the tests hold

- 7 days: finite, every day profitable, > 50 served a day, reputation grows, average walk-out rate < 10 %, 0 tickets on
  the rail for 2 h (the stuck-ticket rule: walk-ins leave at their patience, pre-orders get late reviews, closing time
  closes the rail).
- Day 1: profitable, walk-outs < 12 %, average review ≥ 3.0 ★.
- 25 days reach the Corner Cafe.
- Tempo (`ChapterTempoFollowsThePlan`, seed 9, 50 days): Corner Cafe at 75 min ±25 %, Roastery Hall at 180 min ±25 %.
- Forgiving start: the guide and days 1–3 never lower the reputation; no crises, no wear jams, no milk-van rolls before day 4.
- Research ≤ 20 min per node (data validation + test). Reward caps = data (kit limits test).

## Tuning knobs (where to look first)

| Feel | Knob |
|---|---|
| Too many walk-outs early | `customers[].patience`, `venues[].per_hour`, `work.shot_min` / `steam_min` |
| Money too fast / slow | `staff.roles[].wage`, `venues[].rent`, `menu.items[].price`, `quality.price_*` |
| Chapter pace | `venues[].cost` / `rating`, `economy.footfall_per_star` |
| Roastery payoff | `origins[].q`, `roast.bonus_*`, `contracts[].pay` |
| Crisis pressure | `suppliers[].late`, `grinder.jam_*`, `events[].weight` (frost), `reviews.spree` |
