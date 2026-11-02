# Grill Convoy — balance (P1)

Numbers live in `unity/Assets/Resources/data/{balance,map,research,events}.json`; the headless bot is
`Assets/Scripts/Sim/BalanceRunner.cs` (tests `BalanceTests`: 7 days finite + profitable + growing, day 1 earns, 20 days
reach the Boardwalk). The bot plays like a sensible new player: busiest free pitch, pack by forecast, keep the commissary
one day ahead, restock before selling out, put out flare-ups, All hands on long lines, hire, buy the slowest station
(auto-arranged), research, save up for the next pitch.

## Core loop numbers

| Item | Value |
|---|---|
| Service day | 10:00–20:00 game time = 600 min at 2.5 game min / s ≈ **4 real minutes** at 1× |
| Start | $1,500, Fame 30, truck #1 at the Office Plaza with griller + assembler, 120 patties / 140 buns; commissary 160 / 200 / 60 |
| Line | grill 1.0 / bench 1.2 / window 1.6 burgers per min per station × staffing (none 0.5, each extra +30 %); runner +0.25 to the slowest stage and halves walking; walk penalty 8 % per tile; toaster / sauce +15 / +10 % on the bench, fridge +10 % on the window; −5 % per menu card beyond 2 |
| Start line | 0.8 burgers / min (window-bound) → runner 1.0 → cashier + 2nd grill 1.2+ |
| Pitches | Plaza 40 / h base, lunch curve peak ×2.6 at 12:00, weekend ×0.6, permit $40 · Boardwalk 30 / h, afternoon curve, hot ×1.5, rain ×0.35, weekend ×1.5, permit $55 (Fame 35, $2,000) · Stadium 18 / h, match days every 3rd day ×2.2, halftime 17:45–18:00 ×5 with patience ×0.6, permit $70 (Fame 50, $6,000) |
| Customers | office 1 burger / 7 min patience · fan 1–3 / 5 · beachgoer 2–4 / 9 · tourist 1–2 / 8 · regular 1 / 12; queue cap 14 (+6 with a 2nd window) |
| Prices / costs | $4–10 per card, side $2.50; patty $1.00, bun $0.35, side $0.50 (prepped in batches), fixings $0.20 / 0.50 / 1.10 per burger by tier, wage $70 / crew / day, propane refill $30 / tank |
| Waste | leftovers return at night: patties / sides spoil 25 %, buns 10 % |
| Stars | service (lost ≤ 25 / 12 / 5 %), stock (sold-out time ≤ 20 / 8 / 2 % of open time and waste ≤ 35 / 20 / 10 %), speed (avg wait ≤ 9 / 6 / 3.5 min); Fame = EMA(0.3) of stars; never drops in the guide or days 1–3 |
| Crises | from day 4 (none in the guide): 0.5 / 0.8 / 1.1 random per day by chapter (propane, flare, rival) + weather (hot / rain 15 % each) + halftime on match days |
| Research | 24 nodes, 3–12 min real time (≤ 20, fleet rule), never sold |
| Offline | 50 % of avg net × 0.5 day / h, cap 4 h (Offline Plus 75 % / 10 h), × share of trucks on Head cook rules (min 25 %) |

## 7 days, seed 42 (bot)

| Day | Weather | Customers | Served | Lost % | Burgers | Avg wait | Sold-out min | Waste | Sales | Costs | Net | Stars |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | sunny | 254 | 208 | 18.1 | 233 | 3.3 | 0 | 5 | 1,272 | 630 | **642** | 7/9 |
| 2 | cloudy | 276 | 233 | 15.6 | 266 | 3.7 | 0 | 6 | 1,487 | 751 | **736** | 6/9 |
| 3 | sunny | 278 | 256 | 7.9 | 292 | 3.5 | 0 | 4 | 1,615 | 854 | **761** | 8/9 |
| 4 | sunny | 315 | 276 | 12.4 | 317 | 3.5 | 49 | 2 | 1,747 | 868 | **879** | 5/9 |
| 5 | sunny | 306 | 299 | 2.3 | 336 | 1.0 | 0 | 1 | 1,833 | 939 | **894** | 9/9 |
| 6 (Sat) | sunny | 192 | 192 | 0.0 | 229 | 0.0 | 0 | 3 | 1,254 | 722 | **532** | 9/9 |
| 7 (Sun) | rain | 182 | 182 | 0.0 | 214 | 0.0 | 0 | 4 | 1,197 | 653 | **544** | 9/9 |

End: cash $1,789 after buying a runner + cashier, a 2nd grill, a 2nd bench, the fryer research and the Boardwalk
(day 7); Fame 86. Weekends at the Plaza are quiet by design — the Boardwalk (weekend ×1.5) is the answer once open.

## 20 days, seed 9 (bot)

Boardwalk day 7, Stadium day 14, net climbs from ~$700 (day 1) to $1,300–1,800 on Boardwalk / match days; truck #2
($5,000) is the next goal after day 20. Pacing ≈ chapter 2 at ~35 min of play, chapter 3 at ~75 min (faster than the
plan's 1:30 / 3:30 h targets — tune `map.json` costs up if testers burn through it).

## Open tuning questions

- Day-1 guide: truck #1 starts with 120 patties and sells out mid-afternoon on purpose (teaches packing + restock);
  the forgiving start keeps Fame / reputation from dropping.
- 9/9 star days on quiet weekends are easy; stock / speed thresholds could scale with demand.
- The halftime stampede is brutal without a second window (by design); verify it feels fair in the internal test.
