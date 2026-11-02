# Grill Convoy: Burger Truck Manager — plan #184 (burger keyword space)

> DOCS ONLY, English. Brief: `/Volumes/BuildSSD/tmp/slaves/S1/handoff-184/BRIEF.md`. Fleet rules: `games/CLAUDE.md`. Written 2026-10-31 by S1.
> Originality rule: nothing below is copied from the Codigames game (names, art, UI, text, unique mechanics such as research-queue/ad-skip tickets, restaurant-per-location structure). Generic genre words only.
> Source tags: **[PLAY-CG]** Codigames Play page `https://play.google.com/store/apps/details?id=com.codigames.idle.burger.empire.tycoon&hl=en_US` (fetched by S1 2026-10-31; package found via BlueStacks `https://www.bluestacks.com/apps/simulation/idle-burger-empire-tycoon-game-on-pc.html`); **[PLAY-x]** other Play pages fetched 2026-10-31 (URL inline); **[ASO]** `marketing-data/asoatlas/tycoon-2026-09-28/all_terms.csv` (our ASO Atlas export, US popularity; `estimate` = model estimate, `below_floor` = <10).
> Note: the task text also said "burger tycoon"; the Codigames Play title is **Idle Burger Empire Tycoon—Game**. Other titles with "burger tycoon" in the name are from different developers (see market table).

---

## 1. Reference summary (Codigames)

| Field | Value (source) |
|---|---|
| Play title | **Idle Burger Empire Tycoon—Game** (`com.codigames.idle.burger.empire.tycoon`) [PLAY-CG] |
| Developer | Codigames [PLAY-CG] |
| Installs | **1M+** [PLAY-CG] |
| Rating | **3.96** from **12,479** ratings [PLAY-CG] |
| Last update | May 4, 2026 [PLAY-CG] |
| Monetization | Contains ads + IAP **$0.99 – $124.99 per item** [PLAY-CG] |
| Tagline | "Build your own burger empire from scratch!" [PLAY-CG] |

Core loop (store text + reviews) [PLAY-CG]:
- Start in a small food stall cooking homemade burgers; upgrade kitchen, appliances, dining room; unlock new burger types and restaurants (reviewers: only ~4 locations, then "last one").
- Manage a team (cooks, bartenders, cleaners, janitors; hire/fire) and a **food delivery section** with orders.
- Progress through **timed research** (reviewers: 30 min to 6 h per research) and rewarded-ad "skip" tickets (a $6-ish ad-skip product is cited), weekend events with money goals.
- Customers queue, sit in the dining room; production ↔ customer balance breaks once maxed.

Takeaway: a restaurant idle with a wait-timer economy. There is no spatial design, no choice of *where* to sell, and no real kitchen flow.

Market context (fetched 2026-10-31):
| Game | Installs | Rating (count) | Source |
|---|---|---|---|
| Cooking Fever: Restaurant Game | 100M+ | 4.40 (5,024,089) | https://play.google.com/store/apps/details?id=com.nordcurrent.canteenhd |
| Food Truck Chef™ Cooking Games | 10M+ | 4.31 (285,022) | https://play.google.com/store/apps/details?id=com.nukeboxstudios.foodtruckchef |
| My Burger Shop 2: Food Game | 5M+ | 4.28 (96,735) | https://play.google.com/store/apps/details?id=br.com.tapps.myburgershop2 |
| Foodventure Inc: Idle Tycoon | 5M+ | 4.38 (25,203) | https://play.google.com/store/apps/details?id=com.idle.food.venture.fast |
| Burger Tycoon Idle (Katan Labs) | 5M+ | **2.89** (1,312) | https://play.google.com/store/apps/details?id=com.katanlabs.burgertycoonidle |
| My Burger Shop: Fast Food Game | 1M+ | 4.48 (13,662) | https://play.google.com/store/apps/details?id=br.com.tapps.myburgershop |
| American Burger Truck: Cooking | 500K+ | 4.26 (3,600) | https://play.google.com/store/apps/details?id=br.com.tapps.americanfoodtruck |
| My Food Truck Simulator | 500K+ | 4.25 (2,081) | https://play.google.com/store/apps/details?id=com.jeeneesoft.myfoodtruck |
| Burger Truck Chicago Food Game | 100K+ | 4.45 (1,492) | https://play.google.com/store/apps/details?id=br.com.tapps.chicago.burger.truck |
| Idle Burger Production Tycoon | 100K+ | 2.36 (133) | https://play.google.com/store/apps/details?id=com.al.idle.burger |
**Read:** the shelf is dominated by cooking/time-management games; *idle burger tycoons* are weak in quality (2.89, 2.36). There is **no strategy/dispatch food-truck game with real traction** in what we sampled (the biggest truck title is a cooking game). Caveat: first-page Play search only; not exhaustive.

## 2. What players love / hate

**Method.** All **20 reviews** in C3's captured raw data for the Codigames Play page: `reviews[0]` of `/Volumes/BuildSSD/dev/i18n-work/tasks/next-wave-2026-10-31/raw/evidence/com.codigames.idle.burger.empire.tycoon.embedded.json` (first page Google serves; `#1`–`#20` = order in that file; 3-review summary: `.../raw/com.codigames.idle.burger.empire.tycoon.md`). Stars: 1★ ×4, 2★ ×7, 3★ ×1, 4★ ×7, 5★ ×1 (mean 2.9, below the 3.96 store average). Dates: 10 reviews Nov–Dec 2022, 8 Jan–May 2023, 1 Nov 2024, 1 Mar 2026 — most complaints are from launch months, but #2 (Mar 2026) shows event bugs persist. Counts are n/20 in this sample, themes overlap; paraphrases only.

**Love**
| Theme | n/20 | Reviews | What they say |
|---|---:|---|---|
| Fun, easy to learn, a way to pass a few minutes | 9 | #1 #8 #9 #11 #12 #14 #17 #18 #19 | "Decent idle game", "easy but fun" |
| Several burger/restaurant types to unlock | 2 | #8 #14 | variety of venues is the draw |
| Ads are optional ("virtually no ads", watch for more money) | 2 | #9 #16 | players like opt-in rewarded ads |
| Plenty to do early | 2 | #11 #12 | first locations feel full |
| Rich wish-list of kitchen features (see below) | 1 | #14 | shows appetite for a deeper restaurant sim |

**Hate (ranked by n)**
| # | Theme | n/20 | Reviews | What they say |
|---|---|---:|---|---|
| 1 | **Too little content / ends fast** | 8 | #3 #4 #8 #9 #12 #14 #17 #18 | ~4 locations; "already on the last one"; no update for months; wants "new burger shop coming soon" |
| 2 | **Ads: broken, long, unskippable** | 6 | #3 #5 #8 #16 #18 #20 | ~1-minute click-through ads; ~50% freeze and the reward is lost; black screen; ad-skip tickets stuck at 0/5 or −2/5 |
| 3 | **Event / popup bugs and hard locks** | 6 | #1 #2 #5 #11 #13 #19 | event pack popup cannot be closed (game unplayable); tutorial hard-lock after researching Cheeseburger early (cannot even delete data); alien event: chefs vanish, text overlaps; "uncompletable" event tasks |
| 4 | **Pacing wall / paywall** | 5 | #4 #6 #7 #10 #15 | 30 min–6 h research waits; upgrades need millions by the 3rd site; maxed everything in 5–10 min then waited; "high paywalls" to unlock speed |
| 5 | **Ads required to progress** | 2 | #6 #7 | "you won't make enough unless you watch ads constantly" |
| 6 | **Kitchen/queue balance** | 3 | #6 #10 #13 | cooks both start the same ingredient and waste food, unused is not returned; beyond the 2nd shop the queue grows until customers stop entering; production/customer ratio breaks when maxed |
| 7 | **Price shock** | 3 | #7 #15 #17 | store prices "absurd"; ad-free feels like "greed over good gameplay" |
| 8 | **Events random, no warning** | 2 | #3 #17 | missed an event that "just happened"; only weekend events to do |
| 9 | **Performance** | 1 | #15 | crashes when idle, lag, phone heats up on a new flagship |
| 10 | **Support tab unusable** | 1 | #2 | cannot report the issue, nothing relevant listed |
| 11 | **Missing restaurant features** | 1 | #14 | drive-through, drink/dessert/side station, choose menu, mobile-order lane, self-serve kiosk, customer-type upgrades (family play centre, wifi), outdoor seating, car-hop |

**Design reading.** The audience is *happy with the cute, easy shell* (9/20) but hits (1) a content ceiling, (2) ad plumbing that loses rewards, (3) popups that lock the game, and (4) timers. #14's wish-list is almost a spec for our approach: stations, channels (drive-through/mobile-order/delivery), a configurable menu. Our rules answer the rest: no timers sold (research ≤ 20 min), no popup can block gameplay (every modal has an always-reachable close and a watchdog), rewarded credited only on "earned", events announced 48 h ahead and never gating progress, and a content engine (pitches × events × trucks) that does not end after four venues. #6 (cooks waste food) becomes a *visible kitchen rule* in our game: assigning who cooks what is the player's job.

## 3. OUR game

**Working title: Grill Convoy: Burger Truck Manager** (folder suggestion `games/grill-convoy`, package `com.coddykit.grillconvoy`).

Name options and Play-collision check (S1 searched Play 2026-10-31, first page only; re-check at P7):
| Option | Finding |
|---|---|
| **Grill Convoy** | Search "Grill Convoy" / "Grill Convoy burger truck": no game with this title; hits were restaurant ordering apps ("The Burger Truck"), freight apps ("Convoy"), BBQ chain apps. Best option; "Burger Truck Manager" subtitle adds search value. |
| Patty Fleet | "Patty Fleet" returned only fleet-management utilities; no game. Good fallback. |
| Bun Voyage: Burger Truck Tycoon | Search "Bun Voyage burger": only burger-restaurant ordering apps; no game of that name. Fun fallback (pun). |
| ~~Burger Rush / Patty Rush~~ | Rejected: "Burger Rush" exists on itch/Play indie; "Rush" also blurs fleet naming. |
| ~~Burger Truck / Food Truck …~~ | Rejected: generic, many existing titles ("Burger Truck Chicago Food Game", "Food Truck Chef"). |

**Pitch.** Cinder Bay is a harbour city with a festival every weekend. You have one beaten-up burger truck and a permit for the Office Plaza lunch pitch. Each morning you choose *where each truck goes*, what to load, and how the line inside is laid out; then the service starts and the crowd arrives: lunch rush at the plaza, a 90-second halftime stampede at the stadium, a drizzle on the boardwalk. Grow from one truck into a convoy with a commissary kitchen, delivery riders and a flagship diner. The game is about **flow**: the grill, the assembly bench and the window — and about being in the right place with the right stock.

## 4. Core loop — chaos managed as a process

Three nested loops:

**Loop 1 — Dispatch (the morning, ~60–90 s of thinking).**
- The city map shows pitches with an **event calendar** (lunch, match day, concert, beach hours, night market, festival, ferry arrivals) and a **weather forecast**.
- Choose which truck serves which pitch (permits, distance, pitch reputation, rival trucks), what menu board to run there, and the **stock loadout** (e.g. 140 patties, 160 buns, 3 sauces) drawn from the commissary. Under-load = sell-out; over-load = waste and fuel.
- Loading is a quick drag-and-drop on the truck "pack list"; assistants can auto-pack from rules.

**Loop 2 — Service (real-time, 3–4 min at 1×).**
- Customers queue at the window with patience bars. Order types: walk-up, **order-ahead pickup (time slot)**, **delivery rider** (batched pickup), festival-combo (large orders).
- Tickets run through: **Grill** (patty heat zones, flip timing, grease build-up) → **Assembly bench** (buns, cheese, toppings, sauce) → **Wrap/Fry station** → **Window**. Cook/runner assignments are set by the player; the **layout of the truck interior** determines walking distance.
- Player verbs: assign staff to stations, flag priority tickets, trigger batch-grill, open second window, swap sauce, call the runner, extinguish a flare-up, throw away overcooked patties (waste), tap "all hands" burst (cooldown).

**Loop 3 — Back-office (evening, ~30–60 s).**
- Commissary kitchen: prep batches (patties, sauces, buns, chopped toppings) with par levels, restock van runs, truck maintenance (propane, generator, tyres), pay permits, upgrade truck layout, hire/train.
- Day report: sales, **sell-out minutes**, waste, queue time, pitch reputation changes. Every metric points to a decision (load more, move stations, hire runner).

**Why this is not tapping:** the three levers (where, what, how the line runs) interact — a stadium night with half the stock and a badly laid-out truck is a disaster; a well-loaded, well-laid-out truck prints stars. Idle path: "Dispatcher" and "Head cook" rules (auto-pack, auto-assign) let a convoy run unattended; offline results reflect how well the rules were configured.

## 5. Systems

**5.1 Truck interior (spatial puzzle).** A small grid (starts 4×3, grows to 6×4). Stations are placed tiles: grill (1–3 plates), fryer, assembly bench, bun toaster, sauce rail, drink fridge, wrap bench, window (1–2), bin, prep fridge, extinguisher. Adjacency bonuses (grill next to assembly = +speed, fryer next to window = fresh fries). Layout presets can be saved/loaded per truck. This is the **main build puzzle** and is what separates us from restaurant-upgrade idles.
**5.2 Menu and burger builder.** Core menu cards (Classic, Cheese, Double, Spicy, Veggie, Breakfast, Kids-size, Premium). Each burger = patty type × bun × topping set × sauce; per-pitch **menu board** (max N cards; fewer items = faster line). Ingredient tiers (budget/standard/premium) change cost, prep time and review score. No gambling, no random draws.
**5.3 Staff.** Roles: griller, assembler, window cashier, runner, courier (rider), commissary prepper, mechanic. Traits (fast hands, calm under pressure, clumsy, night owl). Training 1–5 (cash+time, short). No card-pack managers; leadership = "Head cook" and "Dispatcher" rule upgrades.
**5.4 Customers.** Office worker (fast, lunch), match fan (huge simultaneous orders, impatient at halftime), beachgoer (family, kids), night-market crowd (late, big combos), tourist, delivery-app rider, regular (loyalty, favourite burger). Each leaves a quick rating that updates **pitch reputation**.
**5.5 Pitches and permits.** 8 pitches in MVP+; each has opening hours, crowd curve, rent/permit, weather sensitivity (beach: heat↑ rain↓), rival presence. Reputation per pitch (bronze→gold) increases footfall.
**5.6 Economy.** Cash (soft). Costs: ingredients, wages, permits/rent, fuel/propane, maintenance, waste. **Stars/Fame** (city reputation) unlock pitches, trucks, and the commissary tiers. No hard currency, no packs. Research is a short **cap of 10–20 min** per node (fleet anti-wall rule).
**5.7 Fleet.** Up to 4 trucks (chapter 5), each with its own layout, wear, livery. A **commissary** supplies all; a **courier van** moves stock; later **delivery riders** serve nearby pitches.
**5.8 Hazards done safely.** Grease flare-ups are a cartoon pulse with an extinguisher tap (no injuries, no destruction); generator trips; propane low; truck breakdown (towing costs time, not the save).
**5.9 No-reset rule.** Trucks and pitches persist; new trucks are added, not swapped; never "start the next location from zero".

## 6. Progression + content map (first ~10 hours)

| # | Chapter | Time | Introduces |
|---|---|---|---|
| 1 | **Truck #1 at Office Plaza** | 0:00–1:30 | Window, grill, assembly, tickets, first day report, layout editor basics |
| 2 | **Commissary + Boardwalk pitch** | 1:30–3:30 | Prep batches, stock loadout, weather forecast, second pitch dispatch, fryer |
| 3 | **Stadium match day + Truck #2** | 3:30–5:30 | Surge waves, pre-prepping, second window, truck layout presets, order-ahead pickup |
| 4 | **Night Market + Festival Green + riders** | 5:30–7:30 | Delivery riders, combos for large groups, night shifts, rival trucks, sauce-bar upgrade |
| 5 | **Convoy of 4 + Highway Rest Stop** | 7:30–9:00 | Fuel/maintenance, permits, Dispatcher rules, unattended convoy |
| 6 | **Convoy HQ flagship diner (brick-and-mortar)** | 9:00–10:00+ | Combined kitchen (static layout), franchise offers, season finale; endless "Circuit Season" (personal bests only) |

Post-10h: seasonal circuit with new events (Winter market, Beach festival), 3 scenario cities (IAP, optional), rotating "Chef's challenge" scenarios with fixed seeds.

## 7. Crises / events

Numbers are **tuning targets for P1** (design intent, not measured data). Every crisis lasts 30–120 s, is telegraphed (icon + 5 s warning), always has a free response (paid shortcuts exist but are never the only answer), and never fails the save: the cost is lost sales, waste and pitch reputation for that shift.

| # | Crisis | Trigger | Symptom on the diorama | Player's process response | Cost of ignoring |
|---|---|---|---|---|---|
| 1 | **Halftime stampede** | Stadium match day, minute 45 of the event clock | Crowd packs the window, patience bars shrink, queue wraps the fence | Pre-grill buffer before kickoff, open the second window, all-hands burst (60 s cooldown) | Queue > 30 → 40% of the surge walks away; pitch rep −1 step |
| 2 | **Lunch overflow** | Office Plaza 12:00–13:00 | Queue wraps the planter, order-ahead bubbles pile up | Open order-ahead slots, batch-grill same patties | Walk-outs; plaza rep stalls at bronze |
| 3 | **Rain on the boardwalk** | Forecast rain (beach pitches) | Walk-ins vanish, delivery rider orders rise | Redirect the truck or add a rider, shorten menu board | Idle crew paid for nothing; spoiled prep |
| 4 | **Heat wave** | Forecast heat | Cold drinks ×2, fridge strain icon | Add a drink fridge tile, order ice | Drinks out → combo orders fail, −1★ cluster |
| 5 | **Propane low / generator trip** | Fuel gauge < 15% or random on old truck | Grill plates go dark for 30 s | Swap to the backup bottle, refuel at the commissary, schedule maintenance | Grill offline 30 s; tickets age out |
| 6 | **Grease flare-up** | Plate not cleaned for N patties | Cartoon flame pulse over a plate, smoke card | Tap extinguisher / run the clean cycle | Patties burn; waste ~10%; plate offline 10 s |
| 7 | **Rival truck parks next door** | Pitch with rival presence, reputation ≥ silver | Rival awning appears, queue splits | Offer a combo, cut price on one card, or move pitch | Customers split ~30%; rep drift |
| 8 | **Permit inspection** | End of day on regulated pitches | Inspector icon at the window | Keep hygiene station placed and permit current | Fine + one-day pitch closure |
| 9 | **Wrong-order streak** | Assembly bench overloaded | Orders with red "wrong" tag at the window | Reassign assembler, add a pass-check tile | Rating drop; remake waste |
| 10 | **Truck breakdown** | Maintenance meter at 0 | Truck smokes, shift cancelled | Swap in another truck, tow cost | Shift lost (not the save) |
| 11 | **Viral burger** | A card sells fast for 3 days | One menu card ×3 demand | Re-stock that patty, prep more at the commissary | Sell-out minutes; missed rep boost |
| 12 | **Festival group orders** | Festival Green pitch | Large combo orders (10–20 items) | Use catering slots, batch-grill | Orders time out; bad ratings |

**Seasonal / live events (calendar shown 48 h ahead — answers "events just randomly happen, no warning").**
- **Match Day series** (weekly): stadium pitch opens, fixed crowd curve, personal-best sales target.
- **Beach Festival** (summer): beach pitch ×2 crowds, heat crises, limited sunset burger card.
- **Winter Market** (winter): night market stalls, cold-weather menu, string-light livery.
- **Night Harbour** (monthly): new night pitch for a week, rival trucks stronger.
- **Chef's Challenge** (monthly): fixed-seed scenario, same inputs for everyone, personal best only.
- **Rules:** events never gate progression or reset a truck, rewards are cosmetic or in-game earnable (never sold), no popup may block gameplay (modal close always reachable, auto-dismiss after 10 s for event packs).

## 8. Map / diorama + art direction

- **Camera:** two views, both 2.5D: (1) **city map** diorama with pitches as small stages; (2) **truck cutaway** (side-cut interior, window facing the crowd). Day/night and weather visible on both.
- **Look:** saturated street-food poster palette, chunky readable silhouettes, steam as cheap card sprites. Friendly caricatured characters; no brand logos or real chains.
- **Blender:** truck bodies (4 base models + modular awning/liveries), interior station kit, city pitches (plaza, stadium façade, boardwalk, night market stalls, festival stage, rest stop), commissary building, props (cones, bins, string lights).
- **Meshy:** 7 customer archetypes, 6 staff roles (palette swaps), crowd background characters, seagulls and pigeons (organics).
- **Low-profile budget plan** (prims ≤300k, draws ≤150, 30 FPS, PSS ≤300 MB, statics ≤250 MB):
  | Element | Medium/High | Low |
  |---|---|---|
  | Crowd in front of window | ≤40 animated | **≤16 animated** + pooled cards, 15 Hz |
  | Smoke/steam/sparks | particles | **sprite cards, no particles** |
  | Pitch backgrounds | multi-layer parallax | single baked backdrop |
  | Burger visuals | per-burger mesh stack | **atlas decal on a shared bun mesh** |
  | Weather | rain particles | tint + sprite overlay |
  | Lights (string lights, neon) | emissive + glow | emissive only, **glow off** |
  | Shadows | blob/simple | **off**, baked AO |
  | Materials | per-pixel | `PER_VERTEX`, no specular |
  | MultiMesh writes | on change | on change |
  New heavy visuals Medium/High only; Low before/after perf_gate line with each delivery. Truck cutaway kept to ≤60 draws; city map ≤80.

## 9. Onboarding steps (show-and-do; tutorial paused; no failure)

1. "Meet your truck." Tap **Open window**. First customer arrives and shows a ticket.
2. Tap the ticket: it flies to the **grill**. Tap the grill to place a patty (spot highlight).
3. Wait one beat, tap **flip** (large, forgiving), patty done.
4. Drag the patty onto the **assembly bench**, tap bun → cheese → sauce (3 taps; spotlight each).
5. Tap **window** to serve; coins pop; first rating star.
6. Day report (3 numbers) → tap **Next day**.
7. Hire a runner: tap **Hire** → drag to the assembly bench.
8. **Layout editor intro:** drag the bun toaster one tile closer to the grill; see "−12% walk time".
9. Commissary unlock: tap **Prep** → choose "Patties ×60" → confirm.
10. **Dispatch intro:** open the city map → choose Boardwalk → drag 60 patties onto the truck pack list (pack-list tutorial).
11. First **surge** is tiny: tap **All hands** burst.
12. Flare-up icon appears: tap **extinguish**.
13. Chapter 3 cards: second window, order-ahead pickup, stadium match day explainer.
14. Settings → "How to play" replays any step; "Skip" always available.
Events `onboarding_step|complete|skip`; headless tests (right action advances / wrong doesn't / skip → replay); 10-language overflow; smoke shows step 1 on first launch.

## 10. Monetization (IAP_STANDARD.md; no trial, no subscription, no loot boxes)

Product id pattern `com.coddykit.grillconvoy.<slug>`; 4 PPP tiers.

| Tier | slug | USD | Grants | ad_free? |
|---|---|---|---|---|
| Base | `noads` Remove Ads | 2.99 | interstitials off; rewarded rewards without video | **yes** |
| Premium | `premium` Pitmaster Edition | 9.99 | noads + gold grill/truck trim + chef whites + 1 extra save slot + starter pack | **yes** |
| Pass | `convoy_pass` Convoy Pass | 4.99 | permanent: +1 Dispatcher rule slot, +1 saved layout preset per truck, noads | **yes** |
| Convenience | `speed` Speed Pack | 2.99 | 3×/4× speed, auto-close day report | no |
| Convenience | `automation` Head Cook+ | 3.99 | extra auto-pack/auto-assign rules | no |
| Convenience | `slots` Extra Saves | 1.99 | +3 slots | no |
| Convenience | `offline_plus` Offline Plus | 2.99 | offline cap 4 h → 10 h, 50% → 75% | no |
| Content | `scenarios` Scenario Cities | 2.99 | 3 scenario cities (Beach Town, Mountain Fair, Night Harbour) | no |
| Cosmetic | `livery_*` truck livery sets (retro, neon, camo, pastel) | 1.49 each | looks only | no |
| Cosmetic | `uniforms` / `menuboards` / `window_awnings` | 1.99 each | staff outfits / menu-board styles / awnings | no |
| Cosmetic | `cosmetics_all` Collection | 4.99 | all cosmetics (~40% off) | no |
| Bundle | `bundle` Complete Grill Convoy | 19.99 | premium + convoy_pass + speed + automation + cosmetics_all | **yes** |
| Offer (once) | `starter` Quick Start | 1.99 | cash + runner, day 3–7 | no |
| Offer (event) | `crisis` Propane Rescue Kit | 2.99 | consumable: emergency fuel + generator swap + service insurance | no |
| Offer (event) | `comeback` Welcome-back Crate | 1.99 | consumable after 3+ days | no |
| Offer (event) | `milestone` New-Truck Pack | 3.99 | consumable at truck unlock | no |
| Consumable | `cash_s/m/l/xl` | 0.99/2.99/6.99/14.99 | 1/2.5/5/10 days of current income | no |

`ad_free` = noads OR premium OR convoy_pass OR bundle OR `legacy_ad_free`; add to `games/CLAUDE.md` §8 at P4 after owner sign-off (speed/automation/slots/offline/cosmetics/cash do NOT remove ads). Price guard: nothing above $19.99 (reference shows items up to $124.99); research waits are *never* sold; `speed` only raises sim speed, it does not skip a wait that exists for monetisation.

**Rewarded (AdMob, opt-in; fleet §4).** Cheapest consumable `cash_s` ⇒ single reward ≤ 0.2 day income, daily total ≤ 0.5 day; per-placement UTC-day cap + cooldown, persisted; `ad_free` players claim without video.
| Placement | Reward | Cap/day | Cooldown |
|---|---|---|---|
| Offline return | ×2 offline earnings (≤0.2-day rule) | 1 | – |
| Extra runner (3 min) | temporary helper | 3 | 15 min |
| Bonus patties | +20 patties to one truck load | 3 | 10 min |
| Quick prep | finish one commissary batch now | 2 | 20 min |
| Crisis last resort | halve one crisis penalty | 2 | 20 min |
| Daily bonus | small cash/ingredient voucher | 1 | – |
Not given by ads: cosmetics, premium, speed/automation, ad removal. Remote kill switch `rewardedEnabled`. Interstitial/banner = house ads only at natural breaks (after day report), never in tutorial/purchase. RevenueCat ad tracking from day one. In-app review after first successful service day, ~4 min into session 1.

## 11. ASO keyword set (Google Play)

English core. US popularity [ASO]: `burger tycoon` 36 (est, d44) · `burger empire` <10 · `idle burger` <10 · `food truck tycoon` <10 (all below_floor/low). Add: food truck game, food truck manager, burger truck, burger shop game, street food, burger restaurant, truck dispatch.
- Title (Play limit 30): **"Grill Convoy: Burger Trucks"** (27); "Burger Truck Manager" goes in the short description. (The 33-char "Grill Convoy: Burger Truck Manager" exceeds the limit.) Short: "Load the truck, beat the rush, grow a burger convoy across the city." Long text leads with *dispatch, kitchen layout, stadium rush, convoy*.
- Do not use "Idle Burger Empire" or other competitor names.
- Warning: `burger game` results are packed with cooking games (Cooking Fever 100M+, 5.0M ratings) → target "manager/tycoon/food truck" niche terms instead; expect modest search volume, rely on strategy-fan intent + house ads.
- Language hypotheses (**unverified**, validate in ASO Atlas): tr "hamburger oyunu / yemek arabası / burger kamyonu" · de "Burger Truck Spiel / Foodtruck Manager" · es "juego de hamburguesas / food truck" · fr "jeu de hamburger / food truck" · pt-BR "jogo de hambúrguer / food truck" · ru "игра бургер / фудтрак" · ja "ハンバーガー経営 / キッチンカー" · ko "햄버거 푸드트럭 경영" · hi "बर्गर गेम / फूड ट्रक".

## 12. Cannibalization with our games

| Our game | Overlap | How Grill Convoy differs |
|---|---|---|
| doner-night | Grilled-meat street food, crowd waves | Doner Night = **single fixed kiosk, one night in Istanbul, night-flow + city map of night spots**. Grill Convoy = **fleet dispatch across day pitches + spatial truck layout + commissary**; daytime events, burgers, brick-and-mortar finale. Don't reuse "night rush" angle; ad creatives: truck map and layout. |
| chaiwala-rush | Street stall | Different product and structure (single stall + train wave). |
| kaiten-sushi | Belt flow | Different mechanic. |
| pharmacy-rush | Counter + stock | No overlap besides queues. |
| airport-baron / train-baron | Dispatch/logistics | Those are infrastructure networks; here dispatch is 1–4 trucks to pitches (lighter, food-themed). Mention in house ads rotation. |
| Wave siblings | Food/retail | supermarket.md = indoor floor layout; coffee.md = ticket rail + roast; burger = truck layout + dispatch. |
House-ad note: related-first rotation will place doner-night/kaiten-sushi close to Grill Convoy; that is beneficial (food fans).

## 13. Risks

L = likelihood, I = impact (H/M/L).

| # | Risk | Category | L | I | Mitigation | Phase |
|---|---|---|---|---|---|---|
| 1 | **Cooking-game gravity**: Cooking Fever-style audience (100M+, §1) wants hands-on cooking | Market | M | M | Keep service tactile (flip, assemble, extinguish) but not a skill wall; target "manager/tycoon/food truck" niche; no UA before soft metrics | P7/P8 |
| 2 | **Three nested loops overload a new player** (dispatch + layout + service) | Retention | M | H | Dispatch limited to one pitch until chapter 2; layout editor with presets and undo; 14-step guided onboarding; internal gate: ≥ 75% finish step 8 | P3 |
| 3 | **Layout editor UX on a phone** (tile drag, ≥ 48 dp targets) | UX | M | H | Tap-to-select + tap-to-place alternative to drag; auto-suggest button; undo; test in `en` + `de` at 16:9 and 20:9 | P3/P6 |
| 4 | **Popup / modal hard-lock** (reference #1 #5 #11 #19 in §2) | Quality | M | H | Every modal has a reachable close; tutorial steps are idempotent (early-researched items skip the step, never block it); event packs auto-dismiss; modal-stack fuzz test; stuck-state watchdog | P1/P6 |
| 5 | **Balance of surge events** (stampede feels unfair) | Balance | M | M | "Practice shift" with no penalties; event preview with expected load; headless 30 min / 2 h / 7 d balance sim | P1/P6 |
| 6 | **Store policy: ads/IAP** (Play Ads policy, rewarded misuse) | Policy | L | H | Rewarded opt-in only, credited on "earned"; house interstitials only at natural breaks, never in tutorial/purchase; UMP; Data safety matches privacy page; no trial/subscription/loot boxes | P4/P7 |
| 7 | **Age rating / Families**: street food + cartoon could be read as child-directed | Policy | L | M | Not a Families title; IARC at P7; no gambling-like mechanics; no health claims; no real chains or alcohol; confirm 10+/Everyone | P7 |
| 8 | **Originality / IP**: resembles Codigames (restaurant idle with staff and upgrades) or real chains | IP | L | H | No per-site resets, research-queue + ad-skip tickets, restaurant-per-location structure or Codigames menu; no brand marks or chain names; legal quick-check P0; art review P2 | P0/P2 |
| 9 | **Perf on Low** (crowd + smoke/steam) | Perf | M | H | §8 budgets (crowd ≤ 16, sprite cards for smoke, truck cutaway ≤ 60 draws, city map ≤ 80); Low before/after perf_gate line each delivery; real-device PSS | P2/P6 |
| 10 | **Content volume** (8 pitches, 4 truck bodies, events) | Scope | M | M | Modular kit and palette swaps; MVP cut at chapters 1–3 (§14); burn-down vs §14 counts | P1–P3 |
| 11 | **Monetisation balance**: reference shows items up to $124.99 and ad-skip tickets that fail (§2 #2 #3) | Money | M | H | Nothing above $19.99; `noads` $2.99 standalone; research never sold, capped at 20 min; `speed` only raises sim speed; rewarded caps ≤ 0.2 day / 0.5 day | P4/P8 |
| 12 | **Retention after chapter 3 / "ends fast"** (8/20 reviews) | Retention | M | H | 6 chapters + endless Circuit Season, weekly Match Day, monthly Chef's Challenge; D7 gate in §14; live-ops calendar before P8 | P6/P8 |
| 13 | **Localisation**: long German/Russian labels on small HUD, CJK/Devanagari fonts, text overlap (reference #2) | i18n | M | M | One keyed string table; 10 languages before internal test; bundled fonts; overflow tests in `en` + `de` on every HUD panel | P3/P5 |
| 14 | **UA cost for food creatives** | Market | M | M | Organic + house ads first; UA only after soft metrics | P8 |

## 14. MVP scope

Target: **Play INTERNAL test build** (no iOS), chapters 1–3, all fleet-mandatory systems but a deliberately small content set.

**In (MVP)**
- Chapters 1–3: Office Plaza (Truck #1), Commissary + Boardwalk, Stadium match day + Truck #2. 2 trucks, 3 pitches, 1 commissary.
- Truck layout editor (4×3 → 5×4) with 8 station types, adjacency bonuses, saved presets, undo, tap-to-place alternative.
- Dispatch screen with pack list, weather/event forecast, order-ahead pickup channel, day report with sell-out minutes and waste.
- Tech tree ~20 nodes (≤ 20 min each, never sold), 3 ingredient tiers.
- 3 graphics tiers (auto per fleet §10), 10 languages, 14-step onboarding + replay, audio autoload (SFX + 2 loops, silent in tests), save codec + fuzz (ticket-in-flight, mid-dispatch), in-app review gate (first session, ~4 min).
- Full monetisation catalog (below), 6 rewarded placements, house ads, RevenueCat ad tracking, Firebase events.

**Out (post-MVP)**: chapters 4–6 (Night Market/Festival + riders, Convoy of 4 + Rest Stop, Flagship diner), delivery riders, trucks #3–4, scenario cities (`scenarios` SKU), seasonal event sets beyond Match Day, Chef's Challenge live-ops, leaderboards (never planned).

**Content counts**
| Item | MVP | Full game |
|---|---:|---:|
| Chapters / pitches | 3 / 3 | 6 / 8 (+3 scenario cities) |
| Trucks | 2 | 4 + flagship diner |
| Station types | 8 (grill, fryer, assembly bench, bun toaster, sauce rail, drink fridge, wrap bench, window) | 11 |
| Menu cards | 8 | 14 |
| Staff roles | 4 (griller, assembler, window cashier, runner) | 7 |
| Customer types | 5 (office worker, match fan, beachgoer, tourist, regular) | 7 |
| Crises | 6 (halftime stampede, rain, heat, propane, flare-up, rival truck) | 12 (§7) |
| Tech-tree nodes | ~20 | ~40 |
| IAP SKUs | **22** (noads, premium, convoy_pass, speed, automation, slots, offline_plus, livery ×2, uniforms, menuboards, window_awnings, cosmetics_all, bundle, starter, crisis, comeback, milestone, cash ×4) | 25 (+ scenarios, livery ×2) |
| Onboarding steps / languages | 14 / 10 | – |
| Rewarded placements | 6 | 6 |

**Success metrics for the internal test** (our own targets, directional with a small tester cohort; Play vitals thresholds from Google's public Android vitals documentation, verify at P6)
| Metric | Target | Why |
|---|---|---|
| Tutorial completion (step 8 reached / step 14 reached) | ≥ 75% / ≥ 60% | Layout editor intro (step 8) is the highest-risk step |
| Tutorial skip rate | ≤ 15% | Skip signals slow or confusing steps |
| D1 / D7 retention | ≥ 30% / ≥ 8% | Idle-adjacent tycoons; below this, fix before production |
| Median first-session length | ≥ 7 min | Needs first service day + day report + review prompt |
| Players who change the layout at least once by D1 | ≥ 60% | The differentiating mechanic is being used |
| Crash-free users / ANR | ≥ 99.5% / < 0.47% | Play bad-behavior thresholds (crash 1.09%, ANR 0.47%) are the floor |
| Low-profile perf (real device) | 30 FPS, p95 ≤ 50 ms, PSS ≤ 300 MB, draws ≤ 150 | Fleet §0/§10 gate |
| Modal hard-locks / unreadable saves | 0 / 0 | Reference's launch-month killer (§2 theme 3) |
| Rewarded opt-in rate (eligible sessions) | ≥ 8%; 0 uncredited rewards | Credit only on earned callback |
| Patron UI score / smoke | ≥ 8.5 / PASS | Fleet §17 and §1 |

**Estimated effort per phase** (unverified planning estimate in working days for one Claude slave + one Codex worker; revisit at P0 sign-off)
| Phase | Days | Notes |
|---|---:|---|
| P0 Strategy | 0.5–1 | sign-off, legal quick check |
| P1 Core sim | 8–10 | pitch/event/weather, dispatch + loadout, layout grid + walking-cost model, ticket flow, commissary, crises, save fuzz |
| P2 Diorama/art | 7–9 | 4 truck bodies, station kit, 3 pitches, Meshy characters |
| P3 UI/onboarding | 6–7 | city map, pack list, layout editor, truck HUD, menu board, day report, tech tree, 14-step tutorial |
| P4 Monetisation | 2–3 | catalog, billing, rewarded, parity doc |
| P5 i18n | 1–2 | Haiku sub-agents, fonts |
| P6 Perf/QA | 3–4 | perf_gate, layout-solver tests, balance bots, smoke |
| P7 Store assets | 2–3 | screenshots, listing × 10, trailer |
| P8 Internal → production | 1–2 | Patron/owner steps |
| **Total** | **~31–41** | ≈ 7–9 calendar weeks at one worker pair |

## 15. P0–P8 phase TODO

- [ ] **P0 Strategy** — sign-off; originality review vs Codigames; name collision recheck; `ad_free` table entry; ASO Atlas query for §11 terms.
- [ ] **P1 Core sim** — pitch/event/weather model, dispatch + loadout, truck-interior grid + adjacency + walking-cost model, ticket flow (grill heat/flip, assembly, window), order channels (walk-up/order-ahead/rider), commissary prep, economy/fame, crises engine, save/load + fuzz (ticket-in-flight, mid-dispatch), headless balance sim (30 min/2 h/7 d).
- [ ] **P2 Diorama/art** — Blender trucks/stations/pitches, Meshy characters + optimisation, atlas burger decals, Low variants, splash, `check-art`.
- [ ] **P3 UI/onboarding** — coddy_ui screens (city map, pack list, truck cutaway HUD, layout editor, menu board, day report, tech tree, shop, settings + UMP privacy block), TouchScroll gate, 14-step tutorial + tests, audio (SFX + 2 loops, ElevenLabs batch only after owner OK), review gate.
- [ ] **P4 Monetization** — catalog §10, Billing/RC, `ad_free`/legacy, rewarded caps + analytics, CoddyAds, RC ad tracking, parity doc vs Corrupt.
- [ ] **P5 i18n** — 10 languages via in-session Haiku sub-agents; fonts; overflow tests.
- [ ] **P6 Perf/QA** — Low perf_gate (prims/draws/FPS/memory), real-device PSS, balance bots, layout-solver tests, emulator smoke on the Mini.
- [ ] **P7 Store assets** — full-bleed screenshots, feature graphic, 10-language listing, Data safety/IARC/privacy, icon candidates for owner pick, trailer, Play name re-check.
- [ ] **P8 Internal → production** — Jenkins AAB (`GAME_SERVICES_MODE=production`, symbols), Play app creation by Patron, AdMob rewarded unit + UMP, RC credentials, ads backend registration, ASO Atlas tracking, internal → smoke → production on owner approval.

## Sources

- Codigames Play page (fetched 2026-10-31): https://play.google.com/store/apps/details?id=com.codigames.idle.burger.empire.tycoon&hl=en_US
- C3 raw capture (store facts, 20 embedded reviews): `/Volumes/BuildSSD/dev/i18n-work/tasks/next-wave-2026-10-31/raw/com.codigames.idle.burger.empire.tycoon.{json,md}` and `.../raw/evidence/com.codigames.idle.burger.empire.tycoon.embedded.json` (`reviews[0]`)
- Competitor Play pages listed in §1 (fetched 2026-10-31); BlueStacks listing: https://www.bluestacks.com/apps/simulation/idle-burger-empire-tycoon-game-on-pc.html
- ASO Atlas export (US popularity): `/Volumes/BuildSSD/dev/CoddyApps/marketing-data/asoatlas/tycoon-2026-09-28/all_terms.csv`
- Fleet rules: `games/CLAUDE.md` (§§1–4, 9–14, 17), `games/tools/IAP_STANDARD.md`, `games/tools/REWARDED_ADS_STRATEGY.md`
- Success-metric thresholds, effort and tuning numbers are our own planning assumptions (unverified), except the Play vitals thresholds, which come from Google's public Android vitals documentation (verify at P6).
