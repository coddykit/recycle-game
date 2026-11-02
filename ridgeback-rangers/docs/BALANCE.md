# Ridgeback Rangers — balance (P1, tempo pass at P2 polish)

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

End: cash 4 162, rating 81, still Landing Cove, 5 animals (2 hatched), 7 rangers. After the P2-polish tempo pass (below) the
first week is unchanged within ±1 coin a day (the tram is not the bottleneck before Fern Valley): day 1 +581, week +7 568.

## Tempo to the plan (P2 polish, cloud, 2026-11-02)

Real time per park day ≈ 4.5 min live (600 game min ÷ 2.2) + ≈ 1–1.5 min morning plan and report ≈ **5.75 min**. A zone
bought on the morning of day N opens after N − 1 played days. Plan §6: Ch 2 Fern Valley at 1 h, Ch 3 Dune Mesa at 2.5 h
(±25 % → 45–75 min, 1.9–3.1 h). Bot: 40 days, seeds 9 / 42 / 3 / 7 / 11 / 23 (scratch harness around `BalanceRunner`).

| | Before | After |
|---|---|---|
| Fern Valley opens (morning of day) | 12 (all seeds) → **≈ 63 min** | 12–13 → **≈ 63–69 min** (plan 60, +5…15 %) |
| Walkers after Fern Valley opens (bot, 1 car) | 43–45 % for 3–4 days (6 days > 20 % walk per run) | **31–33 % for 2–3 days** (2–4 days > 20 %, the rest are school-trip surges) |
| Second tram car bought | day 14–16 | day 14–15 |
| Dune Mesa opens (morning of day) | 23–27 (mean 25.3) → **≈ 2.1–2.5 h** | 24–28 (mean 25.3) → **≈ 2.2–2.6 h** (plan 2.5 h, −12…+4 %) |
| Walkers after Dune Mesa opens | 25–49 % on some seeds until the 3rd car (day 16–29) | ≤ 21 % (3rd car day 16–17) |
| Day 1 net / first week net (seed 42) | +581 / +7 562 | +581 / +7 568 |
| Day 40 (seed 9) | cash 51 k, 50 animals, 8 species | cash 61 k, 53 animals, 8 species |

Changes (data only): `balance.json` `tram.seats` 16 → **20** and `prices.tram_car` 2 000 → **1 500** (the longer Fern
Valley loop still makes ~30 % walk until a second car — the "new zone changes the flow" moment and `zone.fern.d` hint — but
a real player can now afford that car the next morning instead of after 3–4 days of 40 %); `map.json` Dune Mesa `cost`
14 000 → **26 000** (the bigger tram earns more, which pulled Dune Mesa forward to day 21–23 ≈ 1.9 h; the price puts it back
on the plan's 2.5 h). Ch 2 and Ch 3 are inside ±25 % of plan §6 before and after; the pass shortened the tram crunch
without moving the chapters.

Content limit (not a tempo bug): after Dune Mesa (≈ 2.5 h) and its paddocks / water / shade / expansions (≈ day 30,
≈ 2.9 h) the MVP has nothing left to buy; cash piles up. Plan §6 Ch 3 runs to 4.5 h and Ch 4 (Marsh Delta) is post-MVP
— the first content update should land before players reach ≈ 3 h.

### 30 days, seed 9 (after)

| day | visitors | rides | walked | net | stars | note |
|---|---|---|---|---|---|---|
| 1 | 293 | 293 | 0 % | +623 | 7/9 | |
| 6 | 461 | 461 | 0 % | +1 608 | 8/9 | island events from day 4 |
| 11 | 696 | 696 | 0 % | +4 384 | 8/9 | |
| 13 | 677 | 461 | 32 % | +3 346 | 7/9 | Fern Valley opened this morning |
| 14 | 664 | 463 | 30 % | +3 263 | 7/9 | |
| 15 | 656 | 656 | 0 % | +3 976 | 8/9 | second car |
| 20 | 1 007 | 1 007 | 0 % | +6 691 | 8/9 | school trip |
| 24 | 682 | 682 | 0 % | +3 966 | 8/9 | Dune Mesa opened this morning |
| 30 | 621 | 621 | 0 % | +3 317 | 8/9 | 6 paddocks, 40 animals, rating 88 |

## Levers

| Lever | Value | Why |
|---|---|---|
| Visitors | 22 / game h × hour curve × (0.6 + 0.8 rating) × appeal factor (0.5 + park appeal / 40, ≤ 2.5) | more and happier animals = more visitors |
| Ticket | 3.5 × (0.85 + 0.3 rating) | rating matters but never halves income |
| Tram | 20 seats / car (was 16), 14 km/h, 3 min dwell, patience 40 min; a car 1 500 | a longer loop (new zone) needs more cars |
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
