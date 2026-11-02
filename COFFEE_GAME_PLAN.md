# Pour & Roast: Cafe Manager — plan #184 (coffee-shop keyword space)

> DOCS ONLY, English. Brief: `/Volumes/BuildSSD/tmp/slaves/S1/handoff-184/BRIEF.md`. Fleet rules: `games/CLAUDE.md`. Written 2026-10-31 by S1.
> Originality rule: nothing below is copied from the Codigames game (names, art, UI, text, unique mechanics such as the orchard/garden, event café, oven-task cards). Generic genre words only.
> Source tags: **[PLAY-CG]** Codigames Play page `https://play.google.com/store/apps/details?id=com.codigames.idle.coffee.shop.empire.tycoon&hl=en_US` (fetched by S1 2026-10-31; package found via the BlueStacks listing `https://www.bluestacks.com/apps/simulation/idle-coffee-shop-tycoon-on-pc.html`); **[PLAY-x]** other Play pages fetched 2026-10-31 (URL inline); **[ASO]** `marketing-data/asoatlas/tycoon-2026-09-28/all_terms.csv` (our ASO Atlas export, US popularity; `estimate` = model estimate, `asa` = Apple-Search-Ads-verified).
> Note: the task text also said "coffee corp"; S1 found no Codigames Play title with that name. The Codigames coffee title is **Idle Coffee Shop Tycoon** (an App Store listing exists too: `https://apps.apple.com/app/id6444160094`). "Coffee corp" = unverified.

---

## 1. Reference summary (Codigames)

| Field | Value (source) |
|---|---|
| Play title | **Idle Coffee Shop Tycoon** (`com.codigames.idle.coffee.shop.empire.tycoon`) [PLAY-CG] |
| Developer | Codigames [PLAY-CG] |
| Installs | **500K+** [PLAY-CG] |
| Rating | **3.99** from **6,366** ratings [PLAY-CG] |
| Last update | May 4, 2026 [PLAY-CG] |
| Monetization | Contains ads + IAP **$0.99 – $99.99 per item** [PLAY-CG] |
| Tagline | "Discover the ancient art of brewing coffee, and create a business empire" [PLAY-CG] |

Core loop (store text + reviews) [PLAY-CG]:
- Run a coffee-shop chain: buy ingredients, upgrade coffee machines, bakery ovens, smoothie makers; unlock coffee recipes (cappuccino, latte, mocha, frappe…) and snacks.
- **Orchard/garden:** plant, harvest and use local goods (seeds, beans) as ingredients; "small living world in miniature".
- Staff (baristas, waiters), visitors with ratings/VIPs ("online ratings"), decoration affects satisfaction; supplier deliveries; oven "tasks" that offer 4 recipe cards per round.
- Short list of venues (reviewers: two cafés, then "all venues unlocked") plus a recurring Christmas "event café".

Takeaway: a cute, low-content idle with a nice garden idea but thin systems (2 venues, repeated event, buggy staff AI, milk shortages). The roasting/sourcing side of coffee and the *ticket-flow* side of a busy bar are untouched.

Market context (fetched 2026-10-31):
| Game | Installs | Rating (count) | Source |
|---|---|---|---|
| Coffee Break – Cafe Simulation | 10M+ | 4.52 (160,760) | https://play.google.com/store/apps/details?id=io.supercent.coffeebreak |
| Good Coffee, Great Coffee | 10M+ | 4.28 (133,264) | https://play.google.com/store/apps/details?id=com.tapblaze.coffeebusiness |
| My Coffee Shop: Cafe Shop Game | 5M+ | 4.36 (78,512) | https://play.google.com/store/apps/details?id=br.com.tapps.mycoffeeshop |
| Own Coffee Shop: Idle Tap Game | 1M+ | 3.73 (30,813) | https://play.google.com/store/apps/details?id=com.owngames.owncoffeeshop |
| Coffee Craze – Barista Tycoon | 1M+ | 4.36 (8,281) | https://play.google.com/store/apps/details?id=com.fiveamp.idle.tycoon.coffeecraze |
| Coffee Shop Simulator 3D Cafe | 1M+ | 4.72 (7,210) | https://play.google.com/store/apps/details?id=com.dmg.coffeeshop.simulator |
| Coffee Roaster Empire Idle | 100+ | – | https://play.google.com/store/apps/details?id=com.ajansru.coffeeroasterempire |
**Read:** the cafe shelf is large (several 10M+/5M+), but the only game centred on *roasting* has 100+ installs: a real white space for the "roastery cafe" angle. Competing head-on in the generic cafe sim shelf is expensive; we enter through the process/roast hook.

## 2. What players love / hate

**Method.** All **20 reviews** in C3's captured raw data for the Codigames Play page: `reviews[0]` of `/Volumes/BuildSSD/dev/i18n-work/tasks/next-wave-2026-10-31/raw/evidence/com.codigames.idle.coffee.shop.empire.tycoon.embedded.json` (the first page Google serves; `#1`–`#20` below = order in that file; the 3-review summary is `.../raw/com.codigames.idle.coffee.shop.empire.tycoon.md`). Stars: 1★ ×3, 2★ ×4, 3★ ×10, 4★ ×2, 5★ ×1 (mean 2.7, below the 3.99 store average, so this page skews negative). Dates: 15 reviews Dec 2022–Mar 2023, 2 Sep 2023, 1 Sep 2025, 2 Jun–Aug 2026 — the pains are old *and still reported in 2026*. Counts are n/20 within this sample, themes overlap, it is a sample and not a survey. Paraphrases only, no long quotes.

**Love**
| Theme | n/20 | Reviews | What they say |
|---|---:|---|---|
| Fun / cute / enjoyable time-killer | 9 | #1 #2 #8 #10 #11 #13 #14 #15 #17 | "Not a bad game", cute concept, good way to pass time |
| Look: cute graphics, miniature feel | 4 | #1 #10 #11 #13 | Graphics "my type", cute |
| Ads not intrusive vs. same developer's other games | 1 | #8 | Finished all shops in ~11 h over a few weeks |
| Event cafe liked in principle | 1 | #1 | Wants more variety than one Christmas event |
| One product felt dominant | 1 | #8 | "Pastries are overpowered" — a balance warning, not praise |

**Hate (ranked by n)**
| # | Theme | n/20 | Reviews | What they say |
|---|---|---:|---|---|
| 1 | **Bugs, freezes, stuck state** (non-ad) | 9 | #1 #2 #3 #7 #11 #13 #14 #15 #18 | waiter freezes at dessert counter; customer forgotten for good; oven says "shelf full" when it is not; duplicate tasks; mission progress lost on closing; baristas stop taking orders → angry queues; hard freeze in the intro (#18, 2026) |
| 2 | **Ads and reward failures** | 5 | #5 #9 #10 #11 #13 | ~1-minute ads, reward not credited, crash after an ad, cannot exit ad |
| 3 | **Supply / throughput bottleneck** | 5 | #4 #6 #10 #14 #15 | never enough milk or ice; slow supplier deliveries; "one drink at a time"; shelves empty faster than they restock |
| 4 | **Content ends / drought** | 5 | #1 #6 #8 #17 #19 | only a Christmas event ever; "all venues unlocked, nothing to spend on"; cannot move to a third venue; no update for years (#8 is neutral: finished in 11 h) |
| 5 | **Localisation gaps** | 4 | #7 #9 #10 #13 | half the labels in Spanish, no way to switch |
| 6 | **Price / value of IAP** | 3 | #3 #5 #13 | ad removal only inside a $41 bundle, later $14 (#3); $6.99 builder "no effect" (#5); "$100 to remove ads" (#13) |
| 7 | **Staff AI the player cannot steer** | 3 | #2 #12 #14 | waiters serve by arrival and ignore hearted/tipping guests; carry upgrade stops at 6; baristas idle |
| 8 | **Dead-end guest satisfaction** | 2 | #6 #14 | visitors unhappy and nothing left to upgrade; lounge "still looks messy" |
| 9 | **Oven task-card randomness** | 2 | #15 #16 | cannot pick the recipe the task needs; only 3 crepes offered every time |
| 10 | **Lose the first shop when opening the second** | 2 | #19 #20 | "all of a sudden it was all gone"; stuck after 2nd shop |
| 11 | **Performance / loading** | 2 | #11 #13 | graphics glitch closes game; 15-minute first load; needs wifi |
| 12 | **Support silence** | 1 | #19 | in-game message never answered |

**Design reading.** Seven of the twelve hate themes (1, 3, 7, 8, 9, 10, 12) are about the shop *running badly with no way to intervene*. In our game those are the player's job with visible tools (ticket rail, station roles, milk supplier choice, maintenance window) and a watchdog that frees any stuck ticket. Themes 2, 5, 6 are fleet-rule items we already solve (opt-in rewarded only with credit on the "earned" callback; 10 languages in one string table; wide cheap IAP). Themes 4 and 10 become structural rules: no reset, branches persist, a monthly/seasonal calendar beyond one holiday. Mean 2.7★ on this page shows the cafe-idle audience is *under-served*, not saturated with quality.

## 3. OUR game

**Working title: Pour & Roast: Cafe Manager** (folder suggestion `games/pour-and-roast`, package `com.coddykit.pourandroast`).

Name options and Play-collision check (S1, Play search 2026-10-31, first page only; re-check at P7):
| Option | Finding |
|---|---|
| **Pour & Roast: Cafe Manager** | Search "Pour and Roast cafe" returned only unrelated cafe/roast-log apps (Roast Perfect, Roastit, Emari Roast Cafe…); no game with this title. Best option. |
| Bean Ticket: Cafe Manager | No collision found ("Bean Ticket cafe" → coffee-chain apps only). Explains the ticket mechanic less. |
| Crema Co.: Roastery Cafe | No *game* of this name, but many real coffee-business apps use "Crema Coffee Co." (`ca.craver.cremacoffeeco`, `com.eastcrema.app` …) → brand/trademark confusion; rejected unless P0 legal is relaxed. |
| ~~Crema Rush / Rush Hour Roasters~~ | "Rush" collides with fleet naming and web games ("Coffee Rush: Barista Game" on iOS, "Rush Hour Cafe" web). Rejected. |

**Pitch.** Brindle Quay is a rainy harbour town and your uncle's seaside coffee cart is the only thing he left you. Tickets stream in from the counter, from the town's pickup app and from delivery riders; the espresso machine drifts out of tune, the oat milk runs low, a squall empties the benches and a heat spike fills them. In the back room a small drum roaster turns green coffee into the beans that decide how good your cups are. Run the bar, run the roastery, and grow from a cart into a harbour-wide cafe and wholesale brand. A lively 2.5D diorama with real decisions: *which ticket first, who on which station, which beans, how dark to roast*.

## 4. Core loop — chaos managed as a process

The game has two tightly coupled layers (a "service day" ≈ 3–4 min at 1×; tutorial paused):

**A. The Bar (real-time, tactical).**
1. **Ticket rail:** every order becomes a ticket on a rail above the pass. Sources: walk-ins, **pre-orders with a pickup time** (promise windows), delivery-rider orders (batched pickup). A ticket has 1–4 drinks/pastries with modifiers (size, milk type, extra shot, iced).
2. **Stations:** Grinder → Espresso (2–6 group heads) → Steam wand → Cold bar → Pastry case → Pickup shelf. Baristas are assigned to stations or "all-rounder"; each station has a throughput and a failure mode (milk steamer runs dry, ice runs out).
3. **Routing decisions:** *batch* same-drink tickets (faster, but a pre-order may go late), *priority-flag* a ticket (VIP, deadline), *swap* a barista to the bottleneck, *pause* a station to refill milk.
4. **Dial-in:** espresso quality drifts with humidity/time/beans. A quick 3-second "dial-in" action (tap-and-hold rhythm on the grinder, skippable by an assistant upgrade) restores quality; ignoring it yields bitter shots → complaints → lower stars.
5. **Pickup shelf accuracy:** cups can be mixed up when shelf is overfull; floor staff fix it.

**B. The Roastery (planning, 1–2 min of attention per day).**
1. **Buy green coffee** from a rotating market: origin, lot size, price (changes with season/frost events), quality score, flavour tags.
2. **Roast batches** in the drum: pick a profile (light/medium/dark) and watch a timeline; a "first crack" sweet-spot window gives bonus quality (a simple one-tap timing, not a minigame wall; assistants can auto-roast at average quality).
3. **Rest** (beans need 1–2 in-game days) → **Blend recipes** (house espresso, filter, decaf, seasonal) → stock into the bar *and* sell **wholesale bags** to other cafes (standing orders with quality thresholds).
4. Quality of the beans sets drink tier, price ceiling and review stars. Poor planning = no good beans on a rainy Monday.

**Chaos → process mapping:** the *rush* is the ticket rail; the *crisis* is a machine/milk/weather problem; the *process* is the pipeline from sack to cup; the *score* is stars, waste and promise-kept rate. Idle: "Head barista" and "Roaster assistant" automate routine routing/roasting (set rules; offline earnings use them) — a better-configured shop earns more offline.

## 5. Systems

**5.1 Stations/equipment.** Grinder (burr tier), espresso machine (group heads, boiler size), steamers, cold brew tap, ice maker, blender (later), pastry case, pickup shelf, **roaster** (drum size 1→5 kg→15 kg), green storage, resting racks, packing bench (wholesale).
**5.2 Menu.** ~14 drinks + 8 pastries in MVP across 3 tiers; menu management screen toggles items (fewer items = faster but lower average ticket). Milk types: whole, oat, almond; each is a stock with supplier choices (cheap/regular/premium, speed differs). Ice and cups are stocks too.
**5.3 Staff.** Roles: barista, cashier/host, runner, pastry cook, roaster assistant, courier. Traits (quick, perfectionist, chatty, clumsy). Shifts + morale; training levels 1–5 (cash + time). No gacha cards; no AI that ignores rules — **priority rules are player-set** (FIFO, deadline-first, VIP-first, smallest-first).
**5.4 Customers.** Commuter, remote worker (stays, buys 2nd drink, needs power outlet), tourist, regular (names remembered, favourite drink), courier, influencer. Each posts a **review** with a star score; a rolling "Review Wall" drives footfall.
**5.5 Weather & calendar.** Forecast strip: rain → hot drinks, heat → iced, fog → fewer walk-ins but more pre-orders, festival/weekend events. The player can prep (pre-roast, stock ice, pre-brew).
**5.6 Economy.** Soft cash; **Stars** (reputation) unlock venues and wholesale tiers; **Beans reputation** per origin/roast style. Costs: wages, rent, utilities, green coffee, waste (expired milk/pastry). No hard currency, no packs. Upgrades: machines, seating, outlets, music, plants, bar layout tiles, ventilation.
**5.7 Wholesale.** Contracts from 3–5 other cafes: weekly volume + min quality → steady income, penalties for missed deliveries (soft, never fail state).
**5.8 No-reset rule.** Each venue is a persistent branch; switch between branches; branch managers automate when the player is elsewhere.

## 6. Progression + content map (first ~10 hours)

| # | Venue / chapter | Time | Introduces |
|---|---|---|---|
| 1 | **Quayside Cart** (kiosk under an awning) | 0:00–1:15 | Ticket rail, espresso, first barista, first review |
| 2 | **Corner Cafe** (seating, pastry case) | 1:15–3:00 | Milk stock & suppliers, pastry, steamers, pre-orders, dial-in, weather strip |
| 3 | **The Roastery Hall** (roaster + packing bench) | 3:00–5:00 | Green coffee market, roasting, resting, blends, wholesale contracts |
| 4 | **Bookshop Cafe** (quiet crowd, outlets) | 5:00–6:45 | Remote-worker flow, long stays, power/outlet management, cold-brew |
| 5 | **Market Hall Stall + courier fleet** | 6:45–8:30 | Delivery riders, batching, 2 locations at once, Head-barista rules |
| 6 | **Hilltop Flagship + wholesale brand** | 8:30–10:00+ | 5★ tuning, festival season, brand story ending; endless "Barista Cup" seasonal ladder (personal best only) |

Post-10h: monthly "Origin Trip" scenario (new green lots, limited blends), seasonal cup/theme sets, 3 extra scenario venues (IAP).

## 7. Crises / events

Numbers are **tuning targets for P1** (design intent, not measured data). Every crisis lasts 30–120 s, is telegraphed (icon + 5 s warning), has a free response and a paid shortcut that is *never* the only answer, and never fails the save: the cost is stars, waste and lost sales for the day.

| # | Crisis | Trigger | Symptom on the diorama | Player's process response | Cost of ignoring |
|---|---|---|---|---|---|
| 1 | **Rain squall** | Forecast strip shows rain (autumn chapters, ~2–3 days/week) | Rain on windows, benches empty, pre-order bubbles stack on the rail | Move a barista to pickup/packing, pre-brew hot drinks, widen promise windows | Late pre-orders: 3rd late one in a day costs −1★ review; pastry waste ~15% |
| 2 | **Heat spike** | Forecast heat | Iced orders dominate the rail, ice gauge drains 2× | Order ice, start a cold-brew batch, swap a barista to the cold bar | Ice-out cancels iced tickets (~40% of that wave's sales) |
| 3 | **Missed milk delivery** | Supplier roll: cheap 25% late, regular 10%, premium 3% | Milk fridge bar flashes, steam-wand icon greys out | Emergency run (cash), switch to oat stock, hide lattes from the menu board | All milk tickets auto-fail; walk-outs; 1★ wave risk |
| 4 | **Grinder jam** | Grinder wear > 80% | Grinder shakes, rail stalls at step 1 | Reassign barista to the second grinder, 3 s repair tap, schedule replacement | Queue +60 s, patience bars drop, dial-in lost |
| 5 | **Descale due** | Every 5 in-game days (machine health bar) | Steam weak, shots slow, health bar amber | Close the bar 30 s in a slow window to service | Machine fails mid-rush: station offline 60 s |
| 6 | **Frost at origin** | Market event, ~2/year | Green-market ticker shows an origin ×1.5 | Buy early, re-blend with another origin | Blend cost up; wholesale quality threshold missed |
| 7 | **Influencer visit** | Random, reputation ≥ 3★ | Crowd outside, camera flashes, pre-orders ×3 | Staff up, shorten the menu board, priority-flag VIP | Walk-outs and "too slow" reviews (upside wasted) |
| 8 | **Bad-review spree** | One drink below quality threshold on 3 tickets in a row | Review wall fills with 1★ cards about one drink | Find the cause (dial-in, stale milk), pull the drink, reply choice | Footfall −15% next day; stars drop |
| 9 | **Pickup-app glitch** | Rare, from chapter 2 | Duplicate tickets tagged DUP on the rail | Cancel the duplicates before they are made | Wasted drinks and stock |
| 10 | **Wholesale shortfall** | Contract due, bagged stock below volume | Packing-bench timer turns red | Rush-pack, negotiate a partial delivery | Fee + contract tier down one step |
| 11 | **Health check** | End of day from chapter 2 | Inspector icon at the door | Keep stations clean (tap-clean), no expired milk | −0.2★ and a fine |
| 12 | **Power flicker** | Storm days | Machines reboot one by one | Restart espresso first, hold pastry | Stations offline 20–30 s |

**Seasonal / live events (calendar shown 48 h ahead — answers the reference's complaints about unannounced and Christmas-only events).**
- **Harbour Festival** (summer, 3 in-game days): ferry-night crowds ×2, limited festival blend; earn a cup-sleeve cosmetic by serving N festival tickets on time.
- **Storm Week** (autumn): rain squalls ×3, late deliveries, a wholesale "storm contract".
- **Winter Market** (winter): hot-drink wave, cocoa/spice specials, string-light decor set.
- **Barista Cup** (monthly): fixed-seed scenario, same rail for every player, personal best only (no leaderboard needed).
- **Origin Trip** (monthly): new green lots for one month, limited single-origin blend.
- **Rules:** events never gate progression, never reset a branch, rewards are cosmetic or earnable in-game (never sold), and no holiday repeats back-to-back.

## 8. Map / diorama + art direction

- **Camera:** cutaway 2.5D bar diorama (counter, pass, rail, back room) with a small harbour-town map for branches. Rain/sun visible through windows (Medium/High).
- **Look:** warm cafe palette, steamy lighting hints (cheap emissive cards), tactile counter props, chalkboard menu. Cute, friendly characters; no garden/orchard mini-world (that is a Codigames signature we avoid).
- **Blender:** cart, cafe shell (3 variants), bookshop shell, market stall, roastery (drum roaster, racks, sacks), machines (3 tiers), furniture kit, awnings, signage, harbour props.
- **Meshy:** 6 customer archetypes + 6 staff roles (palette swap), plants, bags/sacks (organics), pets (cafe cat).
- **Low-profile budget plan** (prims ≤300k, draws ≤150, 30 FPS, PSS ≤300 MB, statics ≤250 MB):
  | Element | Medium/High | Low |
  |---|---|---|
  | Steam/rain particles | on | **off** (static sprite hints) |
  | Customers on screen | ≤30 | **≤14**, extras = pooled cards at 15 Hz |
  | Ticket rail | full anim | same UI, no extra 3D |
  | Cup/latte-art meshes | per-cup | **atlas decal on shared cup**, no per-cup mesh |
  | Window outdoors / harbour | animated | static backdrop |
  | Shadows/glow | simple/blob | **off** |
  | Materials | per-pixel | `PER_VERTEX`, no specular |
  | Roaster smoke | particles | none |
  | MultiMesh writes | on change | on change |
  New heavy visuals Medium/High only; every drop has a Low before/after perf_gate line.

## 9. Onboarding steps (show-and-do; tutorial is paused, no failure)

1. "Welcome to the Quayside Cart." Tap **Open**. First customer arrives.
2. A **ticket** appears on the rail (spotlight): tap it to select.
3. Drag the ticket to the **espresso machine** (barista starts). Watch the shot pull.
4. Tap the **pickup shelf**: "Hand the cup over." First coins and a first star review.
5. Second order includes **milk**: tap the milk fridge → see the stock bar (milk intro).
6. Hire a second barista (button pulses) and drag them to the **steam wand**.
7. Two same-drink tickets: tap **Batch** (batching intro).
8. A pre-order with a pickup clock appears: tap **Priority** flag.
9. The espresso shot shows "off-tune" icon: tap-and-hold the grinder for the **dial-in** (3 s, forgiving).
10. Unlock Corner Cafe: place a table from the build menu (layout intro).
11. Open the **forecast strip**: rain tomorrow — tap "Prep hot drinks".
12. Chapter 3 first-time cards: green coffee market → roast one batch (one-tap sweet spot) → rest → blend.
13. Settings → "How to play" replays any step; "Skip" is always available.
Firebase `onboarding_step|complete|skip`; headless tests (right action advances, wrong action doesn't, skip → replay from Settings); 10 languages, no overflow; smoke shows step 1 on first launch.

## 10. Monetization (IAP_STANDARD.md; no trial, no subscription, no loot boxes)

Product id pattern `com.coddykit.pourandroast.<slug>`; 4 PPP tiers.

| Tier | slug | USD | Grants | ad_free? |
|---|---|---|---|---|
| Base | `noads` Remove Ads | 2.99 | interstitials off; rewarded rewards without video | **yes** |
| Premium | `premium` Head Roaster Edition | 9.99 | noads + gold roaster/espresso trim + gilded aprons + 1 extra save slot + starter beans | **yes** |
| Pass | `roast_pass` Roaster's Pass | 4.99 | permanent: +1 Head-barista rule slot, +1 roast-assistant profile, noads | **yes** |
| Convenience | `speed` Speed Pack | 2.99 | 3×/4× speed, auto-close day report | no |
| Convenience | `automation` Head Barista+ | 3.99 | extra routing-rule slots, auto-dial-in | no |
| Convenience | `slots` Extra Saves | 1.99 | +3 slots | no |
| Convenience | `offline_plus` Offline Plus | 2.99 | offline cap 4 h → 10 h, 50% → 75% | no |
| Content | `scenarios` Origin Trips | 2.99 | 3 scenario venues (Mountain Hut, Night Espresso Bar, Rainy Port) | no |
| Cosmetic | `theme_*` (nordic, art-deco, tropical, midnight) | 1.49 each | cafe looks | no |
| Cosmetic | `cups` / `latte_art` / `aprons` | 1.99 each | cup sleeves/designs, latte-art patterns, uniforms | no |
| Cosmetic | `cosmetics_all` Collection | 4.99 | all cosmetics (~40% off) | no |
| Bundle | `bundle` Complete Pour & Roast | 19.99 | premium + roast_pass + speed + automation + cosmetics_all | **yes** |
| Offer (once) | `starter` Quick Start | 1.99 | cash + barista, day 3–7 | no |
| Offer (event) | `crisis` Milk Run Kit | 2.99 | consumable: emergency milk/ice + service insurance | no |
| Offer (event) | `comeback` Welcome-back Crate | 1.99 | consumable after 3+ days | no |
| Offer (event) | `milestone` New-Venue Pack | 3.99 | consumable at venue unlock | no |
| Consumable | `cash_s/m/l/xl` | 0.99/2.99/6.99/14.99 | 1/2.5/5/10 days of current income | no |

`ad_free` = noads OR premium OR roast_pass OR bundle OR `legacy_ad_free`; add to `games/CLAUDE.md` §8 at P4 after owner sign-off (speed/automation/slots/offline/cosmetics/cash do NOT remove ads). Price guard: max single item $19.99 (answers "$41 bundle" review); explicit "no ad-removal inside a bundle you have to pay $40 for" rule — `noads` is always its own cheap product.

**Rewarded (AdMob, opt-in; fleet §4).** Cheapest consumable `cash_s` ⇒ single reward ≤ 0.2 day income, daily total ≤ 0.5 day, per-placement UTC-day cap + cooldown, persisted; `ad_free` players get it without video.
| Placement | Reward | Cap/day | Cooldown |
|---|---|---|---|
| Offline return | ×2 offline earnings (≤0.2-day rule) | 1 | – |
| Extra barista (3 min) | temporary helper on any station | 3 | 15 min |
| Auto dial-in (5 min) | grinder stays tuned | 2 | 20 min |
| Emergency milk/ice run | refill one stock | 3 | 10 min |
| Crisis last resort | halve one crisis penalty | 2 | 20 min |
| Daily bonus | small cash/beans voucher | 1 | – |
Not given by ads: cosmetics, premium, speed/automation, ad removal. Remote kill switch `rewardedEnabled`. Interstitial/banner = house ads only at natural breaks (after day report), never in tutorial/purchase. RevenueCat ad tracking from day one. In-app review after first successful bar day, ~4 min into the first session.

## 11. ASO keyword set (Google Play)

English core. US popularity [ASO]: `idle cafe` 39 (est, d44) · `coffee tycoon` 36 (est, d38) · `coffee shop tycoon` <10 · (not ours: `cat cafe game` 43 est). Add: coffee shop game, cafe game, barista game, cafe manager, coffee roaster, coffee roastery, latte, espresso, coffee shop simulator, cafe simulator.
- Title: **"Pour & Roast: Cafe Manager"** (26). Short: "Run the bar, roast the beans: manage a busy harbour cafe." Long text leads with *tickets, roasting, wholesale, weather*.
- Never use "Idle Coffee Shop Tycoon" (competitor) in text.
- Position: "roastery" + "barista" are our differentiators; target the ~100-install "roaster" niche first, generic "coffee shop game" by ratings later.
- Language hypotheses (**unverified**, validate in ASO Atlas): tr "kahve dükkanı oyunu / kafe yönetimi / barista" · de "Café Spiel / Kaffee Manager / Barista" · es "juego de cafetería / barista" · fr "jeu de café / barista / torréfacteur" · pt-BR "jogo de cafeteria / barista" · ru "кофейня игра / бариста" · ja "カフェ経営 ゲーム / コーヒー焙煎" · ko "카페 경영 게임 / 바리스타" · hi "कैफे गेम / कॉफी शॉप गेम".

## 12. Cannibalization with our games

| Our game | Overlap | How Pour & Roast differs |
|---|---|---|
| chaiwala-rush (tea stall) | Hot drink stall, rush waves | Chaiwala = **single street tapri + local-train wave every 3 min + one product (cutting chai)**. Ours = multi-station bar with **ticket rail routing, custom orders, roasting pipeline, wholesale**, weather-driven demand, indoor cafe; no trains. Avoid "rush/tapri/stall" copy. |
| doner-night | Night food rush | Different time/place/verb (night kiosk vs day cafe + back-room roastery). |
| kaiten-sushi | Flow/conveyor, freshness | Belt mechanic vs ticket rail; our freshness = milk/pastry stocks, beans rest/age. |
| pharmacy-rush | Counter + queue + stock | No prescriptions; supply chain is roasting, not shelf layout. |
| lifeguard-hq / canal-control etc. | none | – |
| Wave siblings (supermarket.md, burger.md) | Food/retail | Supermarket = indoor floor layout + perishables; burger = truck line layout + fleet dispatch; coffee = ticket rail + roast pipeline. Distinct ad creative: "rail of tickets", "roast sweet spot". |
House-ad note: related-first rotation will put chaiwala-rush near Pour & Roast; that is fine (drink fans).

## 13. Risks

L = likelihood, I = impact (H/M/L). Owner = phase where it is retired.

| # | Risk | Category | L | I | Mitigation | Phase |
|---|---|---|---|---|---|---|
| 1 | **Crowded cafe shelf**: several 10M+ and 5M+ incumbents (Coffee Break 160k ratings, §1) | Market | H | H | Enter through the roastery + ticket-rail hook (the only roasting game found has 100+ installs); ASO niche terms first; house ads; no paid UA before D1/D7 are known | P7/P8 |
| 2 | **Staff/routing bugs reproduce the reference's #1 complaint** (9/20 reviews, §2) | Quality | M | H | Player-set priority rules (no hidden AI choices); deterministic sim with seeded replay; routing property tests; **stuck-ticket watchdog** frees any ticket idle > 90 s; save fuzz at ticket-in-flight states (fleet §14) | P1/P6 |
| 3 | **Roast layer overloads the tutorial / idle audience** | Retention | M | M | Roastery gated to chapter 3 (first hour is bar-only); one-tap sweet spot, fully automatable by the Roaster assistant; internal-test gate: ≥ 70% of D1 testers reach chapter 3 | P3/P8 |
| 4 | **Store-policy: ads/IAP** (Play Ads policy, unexpected interstitials, rewarded misuse) | Policy | L | H | Rewarded opt-in only, credit on the SDK "earned" callback; house interstitials only at natural breaks, never in tutorial/purchase; UMP; Data safety CSV matches privacy page; no trial, no subscription, no loot boxes | P4/P7 |
| 5 | **Age rating / Families**: cartoon cafe could be read as child-directed | Policy | L | M | Not a Families title: target audience 13+/all-ages decided at P7 with IARC; no gambling-like mechanics; no health/caffeine claims; ad-free-for-kids rule not needed unless owner chooses Families | P7 |
| 6 | **Originality / IP**: resemblance to Codigames (cafe + staff + upgrades) or to a real chain's look | IP | L | H | No orchard/garden, no event-café, no oven-task cards, no copied names/UI; no green-siren palette or real chain marks; legal quick-check P0, art review P2, ASO text free of competitor names | P0/P2/P7 |
| 7 | **Perf on Low** (steam/rain particles, customer count) | Perf | M | H | Budgets in §8 (customers ≤ 14, particles off, atlas cups); before/after perf_gate line on every delivery; real-device PSS check; new heavy visuals Medium/High only (fleet 10-24 rule) | P2/P6 |
| 8 | **Scope / content volume** (drinks, pastries, beans, venues) | Scope | M | M | Data tables + palette swaps; MVP cut at chapters 1–3 (§14); scenario venues and chapters 4–6 post-MVP; weekly burn-down against §14 counts | P1–P3 |
| 9 | **Monetisation balance**: reference shows $41 ad-removal bundle and "$100" prices (§2 #3 #13) | Money | M | H | `noads` always its own $2.99 product; no item > $19.99; `speed` only raises sim speed, never skips a monetised wait; rewarded caps ≤ 0.2 day / 0.5 day; watch refunds and 1★ mentions of price after launch | P4/P8 |
| 10 | **Localisation**: long German/Russian strings, CJK/Devanagari fonts, mixed-language screens (#7 #9 #10 #13 hate this) | i18n | M | M | One string table, every label keyed, 10 languages before internal test; bundled fonts; overflow test in `en` + `de`; no hard-coded text in scenes | P3/P5 |
| 11 | **Retention after chapter 3 / content drought** (reference themes 4 and 10) | Retention | M | H | Persistent branches (no reset), monthly Barista Cup + Origin Trip, four seasonal events (§7) instead of one holiday; D7 gate in §14; live-ops calendar drafted before P8 | P6/P8 |
| 12 | **Naming collision / trademark** ("Pour & Roast" is generic) | IP | L | M | Re-check Play search at P7, add the subtitle, keep fallback names (§3) | P7 |
| 13 | **UA cost for food/drink creatives** | Market | M | M | Organic + house ads first; UA decision only after soft metrics | P8 |

## 14. MVP scope

Target: **Play INTERNAL test build** (no iOS), chapters 1–3, everything the fleet rules require (§9 onboarding, §10 perf, §11 revenue parity, §12 live features) but with a deliberately small content set.

**In (MVP)**
- Chapters 1–3: Quayside Cart, Corner Cafe, Roastery Hall. Bar layer complete; roastery layer v1 (green market, roast, rest, one house blend, two wholesale contracts).
- Ticket rail, batching, priority rules (FIFO / deadline-first / VIP-first), dial-in (with the auto option), forecast strip, Review Wall, no-reset branches (3).
- Weather: rain, heat, fog + the 6 MVP crises below; calendar shows next 3 days.
- 3 graphics tiers (Low/Medium/High auto per fleet §10), 10 languages, 13-step onboarding + replay, audio autoload (SFX + 2 loops, silent in tests), save codec + fuzz, in-app review gate (first session, ~4 min).
- Full monetisation catalog (below), 6 rewarded placements, house ads, RevenueCat ad tracking, Firebase events.

**Out (post-MVP)**: chapters 4–6 (Bookshop Cafe, Market Hall + courier fleet, Hilltop Flagship), courier riders, remote-worker/outlet management, cold-brew tap, scenario venues (`scenarios` SKU), seasonal event sets beyond one (Harbour Festival), Barista Cup/Origin Trip live-ops, leaderboards (never planned).

**Content counts**
| Item | MVP | Full game |
|---|---:|---:|
| Venues / chapters | 3 | 6 (+3 scenario) |
| Station types | 8 (grinder, espresso, steam wand, pastry case, pickup shelf, roaster, rest racks, packing bench) | 11 |
| Menu | 10 drinks + 5 pastries | 14 + 8 |
| Green origins / blends | 4 / 3 | 8 / 6 |
| Staff roles | 4 (barista, cashier/host, runner, roaster assistant) | 6 |
| Customer types | 5 (commuter, tourist, regular, student, influencer) | 8 |
| Crises | 6 (rain, heat, milk, grinder, frost, review spree) | 12 (§7) |
| Tech-tree nodes | ~20, ≤ 20 min each | ~40 |
| Wholesale contracts | 2 | 5 |
| IAP SKUs | **22** (noads, premium, roast_pass, speed, automation, slots, offline_plus, theme ×2, cups, latte_art, aprons, cosmetics_all, bundle, starter, crisis, comeback, milestone, cash ×4) | 25 (+ scenarios, theme ×2) |
| Onboarding steps / languages | 13 / 10 | – |
| Rewarded placements | 6 | 6 |

**Success metrics for the internal test** (our own targets, directional with a small tester cohort; Play Android vitals thresholds quoted from Google's public vitals documentation, verify at P6)
| Metric | Target | Why |
|---|---|---|
| Tutorial completion (step 9 reached / step 13 reached) | ≥ 80% / ≥ 65% | Fleet §9 criterion: a beginner runs the core loop unaided in 5 min |
| Tutorial skip rate | ≤ 15% | Skip means steps are too slow |
| D1 / D7 retention | ≥ 30% / ≥ 8% | Idle-adjacent tycoons; below this, fix before production |
| Median first-session length | ≥ 7 min | Needs the first successful day + review prompt |
| Reach chapter 3 among D7-retained testers | ≥ 50% within 4 h play | Roastery hook is the differentiator |
| Crash-free users / ANR | ≥ 99.5% / < 0.47% | Play bad-behavior thresholds (crash 1.09%, ANR 0.47%) are the floor, not the goal |
| Low-profile perf (real device) | 30 FPS, p95 ≤ 50 ms, PSS ≤ 300 MB, draws ≤ 150 | Fleet §0/§10 gate |
| Stuck-ticket watchdog fires | ≤ 1 per 1,000 tickets; 0 unreadable saves | Answers the reference's #1 complaint |
| Rewarded opt-in rate (eligible sessions) | ≥ 8%; 0 uncredited rewards | Credit only on earned callback |
| Patron UI score / smoke | ≥ 8.5 / PASS | Fleet §17 and §1 |

**Estimated effort per phase** (unverified planning estimate in working days for one Claude slave + one Codex worker; revisit at P0 sign-off)
| Phase | Days | Notes |
|---|---:|---|
| P0 Strategy | 0.5–1 | sign-off, legal quick check |
| P1 Core sim | 7–9 | rail, stations, routing rules, roaster, weather, economy, save fuzz |
| P2 Diorama/art | 6–8 | Blender cafe kit + roastery; Meshy characters + optimisation |
| P3 UI/onboarding | 5–6 | ~9 coddy_ui screens, 13-step tutorial + tests, audio |
| P4 Monetisation | 2–3 | catalog, billing, rewarded, parity doc |
| P5 i18n | 1–2 | Haiku sub-agents, fonts |
| P6 Perf/QA | 3–4 | perf_gate, balance bots, smoke |
| P7 Store assets | 2–3 | screenshots, listing × 10, trailer |
| P8 Internal → production | 1–2 | Patron/owner steps |
| **Total** | **~28–38** | ≈ 6–8 calendar weeks at one worker pair |

## 15. P0–P8 phase TODO

- [ ] **P0 Strategy** — sign-off on this doc; originality review (no orchard, no event-café clone, no Codigames wording); legal quick check on name; `ad_free` table entry.
- [ ] **P1 Core sim** — ticket generator (walk-in/pre-order/courier), station model + routing rules, dial-in drift, milk/ice/cup stocks, roaster model (profile/sweet-spot/rest/blend), weather/calendar, wholesale contracts, reviews → stars, economy, save/load + fuzz, headless balance sim (30 min/2 h/7 d).
- [ ] **P2 Diorama/art** — Blender cafe kit + roastery + machines, Meshy characters, atlas cups/latte art, Low variants, splash, `check-art`.
- [ ] **P3 UI/onboarding** — coddy_ui screens (rail HUD, station panel, menu manager, roast screen, forecast, review wall, shop, settings + UMP privacy block), TouchScroll gate, 13-step tutorial + tests, audio (SFX + 2 loops; ElevenLabs only after owner OK), review gate.
- [ ] **P4 Monetization** — catalog §10, Billing/RC, `ad_free`/legacy, rewarded caps + analytics, CoddyAds, RC ad tracking, parity doc vs Corrupt.
- [ ] **P5 i18n** — 10 languages via in-session Haiku sub-agents; fonts; overflow tests.
- [ ] **P6 Perf/QA** — Low perf_gate (prims/draws/FPS/memory), real-device PSS, balance bots, routing-rule tests, emulator smoke on the Mini.
- [ ] **P7 Store assets** — full-bleed screenshots, feature graphic, 10-language listing, Data safety/IARC/privacy, icon candidates for owner pick, trailer, Play name re-check.
- [ ] **P8 Internal → production** — Jenkins AAB (`GAME_SERVICES_MODE=production`, symbols), Play app creation by Patron, AdMob rewarded unit + UMP, RC credentials, ads backend registration, ASO Atlas tracking, internal → smoke → production on owner approval.

## Sources

- Codigames Play page (fetched 2026-10-31): https://play.google.com/store/apps/details?id=com.codigames.idle.coffee.shop.empire.tycoon&hl=en_US
- C3 raw capture (store facts, 20 embedded reviews): `/Volumes/BuildSSD/dev/i18n-work/tasks/next-wave-2026-10-31/raw/com.codigames.idle.coffee.shop.empire.tycoon.{json,md}` and `.../raw/evidence/com.codigames.idle.coffee.shop.empire.tycoon.embedded.json` (`reviews[0]`)
- Competitor Play pages listed in §1 (fetched 2026-10-31)
- BlueStacks package listing: https://www.bluestacks.com/apps/simulation/idle-coffee-shop-tycoon-on-pc.html · App Store listing: https://apps.apple.com/app/id6444160094
- ASO Atlas export (US popularity): `/Volumes/BuildSSD/dev/CoddyApps/marketing-data/asoatlas/tycoon-2026-09-28/all_terms.csv`
- Fleet rules: `games/CLAUDE.md` (§§1–4, 9–14, 17), `games/tools/IAP_STANDARD.md`, `games/tools/REWARDED_ADS_STRATEGY.md`
- Success-metric thresholds, effort and tuning numbers are our own planning assumptions (unverified), except the Play vitals thresholds, which come from Google's public Android vitals documentation (verify at P6).
