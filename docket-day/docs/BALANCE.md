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

7 days, seed 42 (`BALANCE 7 days` in the test log):

```
day walk acc  W/L/S late util%  billed  fees  costs   net  stars standing
  1    0   1  0/0/0    0    25     329     0    250    79   6/9   2.00
  2    4   3  0/0/0    0    63     510    70    250   330   8/9   2.03
  3    3   3  1/0/0    0    76     342   189    250   281   8/9   2.15
  4    3   2  0/0/0    0    73       0    70    250  -180   8/9   2.18
  5    4   4  1/0/0    0    78     861   190    250   801   8/9   2.24
  6    3   2  2/0/0    0    86     507  1101    275  1333   9/9   2.51
  7    4   2  2/0/0    0    64     712   527    275   964   8/9   2.81
cash 1458 standing 2.81 chapter 1 staff 5 rooms 3 closed 6 (W 6 L 0 S 0) minCash 610
```

30 days, seed 9: Main Street (chapter 2) on day 14 (~40 min at 1×), Courthouse Row (chapter 3) on day 26 (~80 min);
83 matters closed (66 won, 4 lost, 13 settled), 13 people, 9 rooms, ~3 000 net per day at the end. Seeds 3 / 7 / 42 / 9
over 45 days: chapter 2 on days 13–14, chapter 3 on days 26–29.

## What the tests hold

- a week earns money, the second half earns more than the first two days, ≥ 4 matters closed, cash never below −1000;
- day 1 never digs a hole, no late filings; 25 days reach chapter 2 with wins > 2 × losses;
- the guide and the first 2 days never lower Standing; no crises during the guide or the first 3 days; no money cap.

## Known tuning questions (for P1 sign-off)

1. Income is lumpy (contingency wins land on hearing days: day 4 −180, day 6 +1 333). Intended (plan: "cash-flow dip"
   → Bridge Funds), but check it doesn't feel like a loss streak.
2. The bot never misses a filing and wins ~85 %: a careful player finds the game easy once staffed. Levers: odds base
   (35 %), M/L tier difficulty, walk-in rate vs the 1.6-day backlog rule, crisis rate.
3. Late game cash piles up (no sinks beyond rooms / hires; chapters 4–6 and Rebrand come after launch).
4. Pacing: chapter 2 at ~40 min / chapter 3 at ~80 min of 1× play is faster than the plan's 1.5 h / 3 h; the real player
   is slower than the bot, but raising the chapter gates (`office.json`) is the knob.
