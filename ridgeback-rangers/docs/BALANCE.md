# Ridgeback Rangers — balance (P1)

All numbers live in `unity/Assets/Resources/data/{balance,species,map,events}.json`; the bot is
`Assets/Scripts/Sim/BalanceRunner.cs` (a sensible new player: feed board by the suggestion, the nearest free ranger to the most
urgent pin, a dig crew when a Handler can be spared, egg temperature checked, hatchlings rehomed, purchases with a buffer).
Tests: `BalanceTests` (`SevenDaysStayFiniteProfitableAndNeverStuck`, `FirstDayFeelsGood`, `TwentyDaysReachFernValleyAndMore`).

## The day

08:00–18:00 game time = 600 game min at 2.2 game min / real s ≈ **4.5 real minutes** at 1× (2× free, 3× Speed Pack).
Morning plan (paused) → live → day report. ~10 days ≈ 1 hour of play (with plan + report screens).

## 7 days, seed 42 (bot, guide skipped)

| day | visitors | rides | walked | pins (ok / failed) | tickets | other income | costs | net | stars | CP |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 284 | 284 | 0 % | 2 (carried over) | 994 | 337 | 750 | +581 | 7/9 | 26 |
| 2 | 297 | 297 | 0 % | 3 (5 / 0) | 1065 | 355 | 750 | +671 | 7/9 | 27 |
| 3 | 342 | 342 | 0 % | 3 (3 / 0) | 1248 | 409 | 750 | +907 | 7/9 | 29 |
| 4 | 357 | 357 | 0 % | 4 (4 / 0) | 1318 | 429 | 850 | +897 | 7/9 | 29 |
| 5 | 358 | 358 | 0 % | 3 (3 / 0) | 1332 | 527 | 850 | +1009 | 7/9 | 29 |
| 6 | 478 | 478 | 0 % | 7 (7 / 0) | 1789 | 786 | 850 | +1725 | 8/9 | 41 |
| 7 | 505 | 505 | 0 % | 5 (5 / 0) | 1915 | 838 | 980 | +1773 | 8/9 | 45 |

End: cash 4 162, rating 81, still Landing Cove, 5 animals (2 hatched), 7 rangers. Over 20 days (seed 9): Fern Valley opens on
day 11 (≈ 1 h, plan §6 "Ch 2 at 1–2.5 h"), the longer tram loop makes ~40 % of visitors walk for 4 days until the bot buys a
second car (the intended "a new zone changes the flow" moment), 23 animals / 5 species by day 20, Dune Mesa not yet (≈ day 25+).

## Levers

| Lever | Value | Why |
|---|---|---|
| Visitors | 22 / game h × hour curve × (0.6 + 0.8 rating) × appeal factor (0.5 + park appeal / 40, ≤ 2.5) | more and happier animals = more visitors |
| Ticket | 3.5 × (0.85 + 0.3 rating) | rating matters but never halves income |
| Tram | 16 seats / car, 14 km/h, 3 min dwell, patience 40 min | a longer loop (new zone) needs more cars |
| Joy target | 55 per rider | 3 visitor stars need ~2 zones or a varied herd |
| Variety | 0.8 / 1.0 / 1.15 / 1.25 for 1 / 2 / 3 / 4+ species in a zone | answers the "six identical dinos" complaint |
| Wages | Feeder 90, Vet 120, Handler 100, Fence Tech 100 / day | hiring is a real decision |
| Feed crate | 10 coins; station 12 (+8 with a cooler); heat spoils 10 %/h at Dune Mesa | the feed board trade-off |
| Pins | Ranger Call 3–5 / day + hunger, squabbles (Σ(1−harmony)² × 1.2 / h), sickness 1.2 % / animal / h × stress, fence wear 4 % / h ÷ tier | something to do every day, more with a bigger herd |
| Deadlines | hungry 90, squabble 75, sick 240 / 150 / 60 (green / yellow / red), fence 120, escape 60, flood 120 game min | a failed pin costs coins + calm, never animals or progress |
| Dig | 15 CP + one Handler for 100 min on site; 50 % shards per trip; 2 trips = 1 egg | CP ≈ 25–45 / day early: ~1 dig a day |
| Hatching | 6–14 real minutes (≤ 20, fleet rule), half speed until the temperature is checked; young for 2 days | |
| Forgiving start | no island events days 1–3, rating can only rise days 1–3 and in the guide, no pin fails in the guide | fleet §4 |
| Offline | 50 % of the average net, 0.5 day / hour, cap 4 h (Night Shift: 75 %, 10 h) | |

Open balance work (P6): the bot never misses a pin (perfect dispatcher), so real players will see more failed pins and lower
"Calm" stars; tune deadlines after the first internal playtest. The 3× speed and the pass perks are not in the bot.
