# Docket Day — balance (P1)

Numbers live in `unity/Assets/Resources/data/` (`balance.json` time / economy / odds / Standing / fatigue / intake /
rewards, `matters.json` areas × tiers × judges × parties, `office.json` roles / chapters / rooms, `events.json` crises).
A day = 09:00–18:00 = 540 game minutes at 3 game min per real second = **3 real minutes at 1×** (plan §4).

The headless bot (`BalanceRunner`, run by `BalanceTests`) plays like a sensible new player: takes the clients it has
hours for (≤ 1.6 days of backlog), refers conflicts, staffs the rail with the same auto-assign the Office Manager uses,
covers collisions (free lawyer → visiting counsel → continuance), settles when the settlement beats the expected verdict,
does the prep table on 3 of 5 matters, bills every evening, builds rooms, hires when busy, moves up a chapter when it can.
It starts after the guide (day 1 = only the scripted ticket; in real play the guide covers days 1–3).

## Model in one paragraph

Each matter = filing (soft deadline) + evidence (reveals the hidden twist) + prep + a court date (10:00 or 14:00).
People burn their daily hours on their assigned tasks, earliest deadline first (partner 6 h, associate 7 h, paralegal /
intern 8 h; skills per task kind; fatigue only from hearing overtime). Hourly work is billed by the hour, retainer work is
drawn from the client's trust money, contingency pays a third of the claim on a win. Odds = 35 % + prep (≤ 25) + prep
table (≤ 15; a paralegal sorts exhibits at 60 % if you skip it) + lawyer trial skill + judge habit + 2 %/Standing star −
tier − twist − missing filing / evidence, clamped 5–95 %. Settlement = 60 % of the win fee × judge × conference room.

## 2026-11-02 runs

7 days, seed 42 (`BALANCE 7 days` in the test log; after the tempo pass — before it, nearly the same money (±2) and Standing 2.81 on day 7):

```
day walk acc  W/L/S late util%  billed  fees  costs   net  stars standing
  1    0   1  0/0/0    0    25     329     0    250    79   6/9   2.00
  2    4   3  0/0/0    0    63     510    70    250   330   8/9   2.02
  3    3   3  1/0/0    0    76     342   189    250   281   8/9   2.10
  4    3   2  0/0/0    0    73       0    70    250  -180   8/9   2.12
  5    4   4  1/0/0    0    78     859   190    250   799   8/9   2.14
  6    3   2  2/0/0    0    86     504  1100    275  1329   9/9   2.32
  7    4   2  2/0/0    0    64     707   523    275   955   8/9   2.52
cash 1443 standing 2.52 chapter 1 staff 5 rooms 3 closed 6 (W 6 L 0 S 0) minCash 610
```

(Before the tempo pass below.) 30 days, seed 9: Main Street (chapter 2) on day 14 (~40 min at 1×), Courthouse Row (chapter 3) on day 26 (~80 min);
83 matters closed (66 won, 4 lost, 13 settled), 13 people, 9 rooms, ~3 000 net per day at the end. Seeds 3 / 7 / 42 / 9
over 45 days: chapter 2 on days 13–14, chapter 3 on days 26–29.

## Tempo pass (2026-11-02, cloud polish) — chapters at the plan's pace

Plan §6: chapter 2 (Main Street Suite) at ~1.5 h, chapter 3 (Courthouse Row) at ~3 h of play; a day = 3 min at 1×, so
the targets are ~day 30 and ~day 60 (±25 %: days 23–38 and 45–75). The bot unlocked them on days ~13 and ~28 because the
Standing gate (the binding one — cash and closed matters were always met first) fell after two weeks. Changes:

| Knob | File | Before | After |
|---|---|---|---|
| Main Street gate: Standing / closed matters / cost | `office.json` chapters | 4.0 / 15 / 6 000 | **6.8 / 80 / 15 000** |
| Courthouse Row gate: Standing / closed / cost | `office.json` chapters | 12.0 / 70 / 25 000 | **15.0 / 190 / 60 000** |
| Standing per win / settlement / referral | `balance.json` standing | 0.12 / 0.06 / 0.03 | **0.08 / 0.04 / 0.02** |

Slower Standing also paces the per-area tier unlocks (Medium at ★1.5, Large at ★3.0), so bigger matters arrive over the
first hour instead of the first 20 minutes. Losses / late filings / walk-outs keep their Standing costs (mistakes weigh
relatively more). Nothing on day 1–7 money changed: day 1 nets +79 (seed 42 and every seed), no late filings, the week
earns +3.6 k … +6.0 k, cash never below +480. Odds were left alone: lowering the base (0.35 → 0.32) made the bot settle
more and barely moved its win rate (88 → 84–86 %), so the "careful player wins most hearings" question stays open for
playtests.

Bot over 100 days, 8 seeds (3 7 9 42 11 23 5 77), 1×:

| | Chapter 2 day (min) | Chapter 3 day (min) | Win % (W / L) | Day-1 net | Week-1 net | Min cash | Closed by day 100 |
|---|---|---|---|---|---|---|---|
| Before | 12–14, mean 13.3 (**~40 min**) | 26–32, mean 28.5 (**~86 min**) | 87.9 % | +79 | 3.6 k – 6.0 k | 483 | 301–314 |
| After | 27–31, mean 28.5 (**~86 min**, plan 90) | 61–70, mean 65.4 (**~196 min**, plan 180) | 86.0 % | +79 | 3.6 k – 6.0 k | 482 | 303–324 |
| Plan §6 | ~30 (1.5 h) | ~60 (3 h) | — | profitable | — | — | — |

Both within ±10 % of the plan (the brief allows ±25 %). The real player is slower than the bot (the guide covers days 1–3,
and nobody plays every day flawlessly), but the free 2× speed and "skip ahead" roughly cancel that. Late-game cash still
piles up (question 3 below): the higher chapter costs are the first new sink.

Harness used for the table (not in the repo; any copy of `BalanceRunner` over N days / seeds reproduces it): a net8.0
console that compiles `Assets/Scripts/Sim` and prints the `"day N chapter C"` lines of `BalanceReport.Log`.

## What the tests hold

- a week earns money, the second half earns more than the first two days, ≥ 4 matters closed, cash never below −1000;
- day 1 never digs a hole, no late filings; Main Street is NOT reached in 20 days but is within 40 (seed 9: day 28),
  with wins > 2 × losses;
- the guide and the first 2 days never lower Standing; no crises during the guide or the first 3 days; no money cap.

## Known tuning questions (for P1 sign-off)

1. Income is lumpy (contingency wins land on hearing days: day 4 −180, day 6 +1 333). Intended (plan: "cash-flow dip"
   → Bridge Funds), but check it doesn't feel like a loss streak.
2. The bot never misses a filing and wins ~85 %: a careful player finds the game easy once staffed. Levers: odds base
   (35 %), M/L tier difficulty, walk-in rate vs the 1.6-day backlog rule, crisis rate.
3. Late game cash piles up (no sinks beyond rooms / hires; chapters 4–6 and Rebrand come after launch).
4. ~~Pacing~~ — done 2026-11-02 (tempo pass above): chapter 2 at ~86 min, chapter 3 at ~196 min of 1× bot play.
