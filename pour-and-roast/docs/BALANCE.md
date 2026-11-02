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

30 days, seed 9: Corner Cafe opens on **day 16** (~70 min of play; plan §6: 1:15 h), Roastery Hall on **day 28** (~2 h;
plan: 3:00 h — the bot saves rather than decorating). Supplier beans (quality 52) cap reviews near 4.0 ★; the first own
roasts (quality 85–90) push the day-30 average to 4.8 ★ — the roastery is the road to 5 ★ (design §4 B). 0 stuck tickets.

```
 1 cart      revenue  848  net  612  review 3.55  rating 2.82
 7 cart      revenue 1418  net  992  review 3.76  rating 3.64
15 cart      revenue 1312  net  912  review 4.05  rating 4.03
20 corner    revenue 3415  net 2949  review 3.85  rating 3.75   (net incl. the cart's branch income)
25 corner    revenue 2957  net 2485  review 4.14  rating 4.00
30 roastery  revenue 4718  net 5575  review 4.79  rating 4.31   (beans quality 87, wholesale + 2 branches)
```

## What the tests hold

- 7 days: finite, every day profitable, > 50 served a day, reputation grows, average walk-out rate < 10 %, 0 tickets on
  the rail for 2 h (the stuck-ticket rule: walk-ins leave at their patience, pre-orders get late reviews, closing time
  closes the rail).
- Day 1: profitable, walk-outs < 12 %, average review ≥ 3.0 ★.
- 25 days reach the Corner Cafe.
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
