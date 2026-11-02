# Grill Convoy — balance (P1)

Numbers live in `unity/Assets/Resources/data/{balance,map,research,events}.json`; the headless bot is
`Assets/Scripts/Sim/BalanceRunner.cs` (tests `BalanceTests`: 7 days finite + profitable + growing, day 1 earns, chapter
times within ±25 % of the plan, quiet weekends stop at 2 service stars). The bot plays like a sensible new player: the free
pitch that sells the most (forecast crowd × group size × card price, capped by its line), pack by forecast, keep the commissary
one day ahead, restock before selling out, put out flare-ups, All hands on long lines, hire, buy the slowest station
(auto-arranged), research, save up for the next pitch.

## Core loop numbers

| Item | Value |
|---|---|
| Service day | 10:00–20:00 game time = 600 min at 2.5 game min / s ≈ **4 real minutes** at 1× |
| Start | $1,500, Fame 30, truck #1 at the Office Plaza with griller + assembler, 120 patties / 140 buns; commissary 160 / 200 / 60 |
| Line | grill 1.0 / bench 1.2 / window 1.6 burgers per min per station × staffing (none 0.5, each extra +30 %); runner +0.25 to the slowest stage and halves walking; walk penalty 8 % per tile; toaster / sauce +15 / +10 % on the bench, fridge +10 % on the window; −5 % per menu card beyond 2 |
| Start line | 0.8 burgers / min (window-bound) → runner 1.0 → cashier + 2nd grill 1.2+ |
| Pitches | Plaza 40 / h base, lunch curve peak ×2.6 at 12:00, weekend ×0.6, permit $40 · Boardwalk 30 / h, afternoon curve, hot ×1.5, rain ×0.35, weekend ×1.5, permit $55 (Fame 40, $11,000) · Stadium 18 / h, match days every 3rd day ×5 from 16:00 to 18:30, halftime 17:45–18:00 ×7 with patience ×0.9, permit $70 (Fame 55, $27,000) · Box Truck (truck #2, chapter 3) $9,000 |
| Customers | office 1 burger / 7 min patience · fan 1–3 / 5 · beachgoer 2–4 / 9 · tourist 1–2 / 8 · regular 1 / 12; queue cap 14 (+6 with a 2nd window) |
| Prices / costs | $4–10 per card, side $2.50; patty $1.00, bun $0.35, side $0.50 (prepped in batches), fixings $0.20 / 0.50 / 1.10 per burger by tier, wage $70 / crew / day, propane refill $30 / tank |
| Waste | leftovers return at night: patties / sides spoil 25 %, buns 10 % |
| Stars | service (lost ≤ 25 / 12 / 5 %; the 3rd star also needs the line busy ≥ 30 % of the open time — `stars.busy`, not in the guide / days 1–3), stock (sold-out time ≤ 20 / 8 / 2 % of open time and waste ≤ 35 / 20 / 10 %), speed (avg wait ≤ 9 / 6 / 3.5 min); Fame = EMA(0.3) of stars; never drops in the guide or days 1–3 |
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
| 6 (Sat) | sunny | 192 | 192 | 0.0 | 229 | 0.0 | 0 | 3 | 1,254 | 722 | **532** | 8/9 (was 9/9) |
| 7 (Sun) | rain | 182 | 182 | 0.0 | 214 | 0.0 | 0 | 4 | 1,197 | 653 | **544** | 8/9 (was 9/9) |

End: cash $1,789 after buying a runner + cashier, a 2nd grill, a 2nd bench, the fryer research and the Boardwalk
(day 7, before the polish pass; now the Boardwalk is saved up for until ~day 18); Fame 81. Weekends at the Plaza are quiet by
design — the Boardwalk (weekend ×1.5) is the answer once open — and since the polish pass a quiet day shows 8/9, not 9/9.

## Tempo to the plan (polish pass 2026-11-02)

Clock: one game day ≈ **5 real minutes at 1×** (600 game min at 2.5 / s = 4 min of service + ~1 min morning and report);
"unlock day N" = N − 1 days played. Bot, 8 seeds (42, 9, 3, 7, 11, 21, 77, 100), 70 days.

| Milestone (plan §6) | Plan | Before (seed range → mean) | After (seed range → mean) |
|---|---|---|---|
| Chapter 2 — Boardwalk | 1:30 h | day 7 (all seeds) → **~30–35 min** | days 17–19 → **~86 min** (80–90) |
| Chapter 3 — Stadium | 3:30 h | days 12–15 → **~56–75 min** | days 38–47 → **~211 min** (185–230) |
| Truck #2 (chapter 3 content) | 3:30–5:30 h | days 18–30 | days 44–55 (~3:35–4:30 h) |
| Day 1 (seed 42 / mean) | profitable, forgiving | +$642, 18 % lost | unchanged (+$642, 18 % lost; the bot changes nothing before day 1) |
| 9/9 weekend days | rare | 2–6 of 16 per seed (60 days); 7-day table days 5–7 all 9/9 | 0–4 of 20 per seed (70 days) |
| 9/9 weekdays | — | 3–17 of 44 | 4–8 of 50 |
| Halftime walk-aways, 1 window | ~40 % if ignored (plan §7 #1) | not reachable: the Stadium never out-sold the Plaza, a sensible player never went | 33–35 % (big surges, 42–46 fans) |
| Halftime walk-aways, 2 windows | lower | — | 0–52 %, median ~10 % |

What changed:
- `map.json`: Boardwalk $2,000 / Fame 35 → **$11,000 / Fame 40**; Stadium $6,000 / Fame 50 → **$27,000 / Fame 55**.
- `balance.json`: Box Truck $5,000 → **$9,000**; match day ×2.2 17:00–18:30 → **×5 from 16:00** (fans arrive before
  kick-off, so a match day is worth the trip: the Stadium now out-sells the Plaza on match days and weekends), halftime
  ×5 with patience ×0.6 → **×7 with patience ×0.9** (still a stampede, but a one-window truck loses about a third of the
  surge instead of a wall — the plan's "~40 % if ignored"; the 2nd window, All hands and packing for the forecast win it).
- `balance.json` `stars.busy = 0.3` + `Simulation.BusyShare()`: the 3rd service star needs the line working ≥ 30 % of
  the open time — quiet weekends at the offices and rain at the beach show 8/9 with the existing "A smooth day. Try a
  busier pitch" hint (plan §7 #3: "idle crew paid for nothing"). Off during the guide and days 1–3. No new strings.
- `BalanceRunner`: the bot values a pitch by forecast crowd × group size × the card each customer type likes on the board,
  capped by the truck's line (before: raw headcount, so it never chose the Stadium).

The second chapter is a jump on purpose: the Boardwalk's beach groups (2–4 burgers) overwhelm a plaza-sized truck for the
first days (seed 9 days 19–21: 3/9, ~30 % lost, but +$1,500 / day) — the report hints point at stations and packing.
Research slows during chapter 2 because the bot saves for the Stadium; a player who researches more lands a little later,
still inside the ±25 % band. Test: `PacingMatchesThePlanChapterTimes` (seeds 9 and 42: Boardwalk days 15–23, Stadium
days 33–53).

## Open tuning questions

- Day-1 guide: truck #1 starts with 120 patties and sells out mid-afternoon on purpose (teaches packing + restock);
  the forgiving start keeps Fame / reputation from dropping.
- Done in the polish pass: quiet-day 9/9 (busy rule) and the halftime stampede (×7, patience ×0.9). Verify both feel
  fair in the internal test; the bot never pre-grills (no such verb yet), real players may do better at halftime.
