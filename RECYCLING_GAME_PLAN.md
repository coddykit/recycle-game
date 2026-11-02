# Bottle Route: Recycling Tycoon — plan #12 of the next wave (recycling / deposit-return keyword space)

> DOCS ONLY, English. Same 15-section brief as the other next-wave plans (`README.md` in this folder). Fleet rules:
> `games/CLAUDE.md`. Written 2026-11-01 by the Patron on the owner's order (11-01: "a game about collecting the bottles
> people return to deposit machines; the machines fill up and must be emptied, but driving far burns fuel and emptying too
> often costs money; basically like Borderline Empire; an environmental-transformation tycoon about collecting recyclables").
> **No implementation until the owner says start.**
>
> **Originality / impersonation rule (hard).** The real-world inspiration is Türkiye's deposit-return scheme (DOA, run by
> the Turkish Environment Agency, TÜÇA). The game uses **none** of: the DOA name or logo, TÜÇA / ministry names, logos or
> colours, the "Sıfır Atık" mark, real retailer brands seen in the news (BİM, Carrefour …), real politicians' names or
> quotes. The scheme in the game is a fictional, generic "deposit mark" in an invented coastal region. We only reuse
> generic facts (deposit machines, a flat refund per container, field operators, counting centres, balers).
>
> Source tags: **[DOA]** `https://doa.gov.tr/` (fetched 2026-11-01) · **[NEWS-n]** news URLs listed in §1 · **[COMP]**
> complaint pages (sikayetvar.com, URL inline) · **[ASO]** our ASO Atlas export
> `marketing-data/asoatlas/chaos-2026-10-20/table.txt` (block #42 "Çöp grevi şehri") ·
> **[PLAY-x]** third-party store trackers (URL inline; installs from trackers are **estimates**).

---

## 1. Reference summary

Unlike the other 11 plans there is **no single Codigames reference**. There are two references: the real system (the theme)
and the Play genre shelf (the market).

### 1a. The real system: Türkiye's deposit return (DOA), as reported

| Fact | Value (source) |
|---|---|
| What | Deposit return for deposit-marked **PET, glass and aluminium** beverage packaging, **0.10–3 L** [DOA], [NEWS-1] |
| Launch | Nationwide **1 July 2026**, 81 provinces / 973 districts; earlier pilot paid 25 kuruş [NEWS-1], [NEWS-2] |
| Refund to citizen | **1 TL per container**, paid into the app wallet; **max 200 per day** at machines [DOA], [NEWS-3] |
| Machines at launch | **1,148 reverse vending machines**, 6 domestic manufacturers (earlier plan: 5,000) [NEWS-2] |
| Uptake | 7.6 M containers in 5 days; 1.39 M users [NEWS-4] · 100.2 M by 11 Aug · **205.7 M by 5 Sep, 3.21 M registered users** [NEWS-5] |
| Field operators | 17 "DSYS" field operators authorised in the 2nd round [DOA]; shops/HoReCa contract an operator, who **collects regularly and takes packaging to counting and verification centres** [NEWS-6] |
| Mobile DOA (from ~29 Jul) | Free handheld terminal so corner shops / kiosks become **manual return points**; citizen 1 TL, **shop gets 50 kuruş per container**; shop hands the bags to the field operator [NEWS-7] |
| Counting & verification centres (from ~21 Jul) | Bulk returns (people with large hauls): **50 kuruş per container**, no 200 cap [NEWS-8] |
| A day at a depot (Sakarya) | Field crews leave **09:00**, bring everything back the same evening; ~**7,000 containers/day**; HoReCa bags go through a count/verify line; **PET and aluminium sorted, pressed and baled; glass stored in bays (~4 t/day)**; bales sold to recyclers by tender; PET intake 200 → 600 kg/day after launch [NEWS-9], [NEWS-10] |
| Abuse | People entered relatives' ID/phone numbers to beat the 200 cap (1,500–2,000 containers per person); from 17 Jul ID/phone entry was suspended for ~2 months, app QR only [NEWS-3] |

**The real pain points — this is the game.** Complaint pages repeat the same operational failures [COMP]:
- **Machine full and nobody empties it** (Aydın, Gaziantep, Kütahya, Perşembe; "no one came to empty a full machine").
- **Broken or jammed machine**, sometimes for weeks (Ordu Ünye ~20 days; Antalya "inactive since 15 July"; Çerkezköy "faulty since day one").
- **App says "available", machine is full or broken on site** → a Tekirdağ user made 6–7 wasted trips of ~15 km each.
- **Too few machines** in busy districts (Küçükçekmece) and **long trips** to the nearest one.
- Queue disputes at machines (Bolu, people using others' ID numbers) [NEWS-11].

So the operator's dilemma the owner described is real and documented: **empty too rarely → full machines, angry citizens,
wasted trips; empty too often → half-empty trucks, fuel and wages burned.** Add volume vs weight (loose PET is bulky,
glass is heavy), and a depot whose count line and baler can bottleneck.

Sources:
- [NEWS-1] karekod.org, "Depozito iade ücretleri": `https://www.karekod.org/blog/depozito-iade-ucretleri-sise-basina-kac-tl-verilecek/`
- [NEWS-2] Anadolu Agency / Hürriyet Daily News on launch and machine count: `https://www.aa.com.tr/en/turkiye/turkiyes-deposit-return-system-for-beverage-containers-expected-to-boost-economy-cut-waste/3975532`, `https://www.hurriyetdailynews.com/amp/turkiye-to-pay-1-lira-per-recycled-container-222959`
- [NEWS-3] Diken, ID restriction: `https://www.diken.com.tr/?p=1746261`
- [NEWS-4] Daily Sabah, 7.6 M in 5 days: `https://www.dailysabah.com/turkiye/turkiyes-deposit-return-system-collects-76m-containers-in-5-days/news/amp`
- [NEWS-5] NTV, 205 M collected: `https://www.ntv.com.tr/ntvpara/doada-205-milyon-ambalaj-toplandi-1740495`
- [NEWS-6] DHA, business registration (DBYS): `https://www.dha.com.tr/gundem/isletmeler-icin-depozito-bilgi-yonetim-sistemine-kayit-sureci-basladi-2868535`
- [NEWS-7] Bigpara, Mobile DOA: `https://bigpara.hurriyet.com.tr/haberler/ekonomi-haberleri/mobil-doalar-devreye-alindi_ID102217524/`
- [NEWS-8] Bengütürk, counting & verification centres: `https://www.benguturk.com/gundem/doa-sisteminde-toplu-iadelere-iliskin-sayma-ve-dogrulama-merkezleri-kuruldu-274972h`
- [NEWS-9] Gazete Birlik, "the journey of the containers": `https://www.gazetebirlik.com/ekonomi/bakan-kurum-paylasti-iste-makinelere-atilan-icecek-ambalajlarinin-yolculugu/1050058`
- [NEWS-10] Alomaliye, Sakarya depot ×3: `https://www.alomaliye.com/2026/07/27/doa-sisteminde-geri-donusum-yogunlugu-uc-kat-artti/`
- [NEWS-11] GZT, Bolu queue dispute: `https://www.gzt.com/gundem/depozito-iade-makinesinde-kavga-boluda-10-kisinin-tc-kimlik-numarasini-kullaniyor-iddiasi-gundem-oldu-4241196`
- [COMP] `https://www.sikayetvar.com/doa-doagovtr/depozito-iade-makinelerinde-surekli-bozukluk-ve-yaniltici-uygulama` (+ related pages on the same site)

### 1b. The Play genre shelf (recycling / garbage tycoons)

| Title (developer) | Signal | Core loop | Source |
|---|---|---|---|
| Trash Tycoon Idle business (Funcell) | **12.8 M** installs (top of `waste management game`, `garbage tycoon`, `recycling tycoon`) | Idle trash processing | [ASO], `https://appgoblin.info/apps/com.funcell.trashinc` |
| Trash Tycoon: idle simulator (ALEXPLAY) | 4.49★, ~88.8 K ratings (tracker) | Idle: compress trash into cubes | `https://appgoblin.info/apps/net.alexplay.trash_tycoon` |
| Garbage Truck City Tycoon (EMKA) | ~59 K installs, 3.84★ (tracker estimate) | Drive an eco truck with a vacuum | `https://appgoblin.info/apps/com.emka.garbagetruckcitytycoon` |
| Garbage Recycling: Truck Rush (Tap Dog) | ~1.3 K installs (tracker) | Drive + small factory | `https://appgoblin.info/apps/com.tds.garbage.recycling.truck.rush/keywords` |
| Recyclings Run (Headstart) | ~429 K installs | Runner, collect bottle caps | `https://appgoblin.info/apps/com.CactusStudios.Recyclings` |

**What the shelf lacks:** every top title is an idle "tap to process trash" factory or a "drive the truck yourself" toy.
**Nobody sells the operator's problem**: a network of filling machines, when to send which truck, fuel vs service level,
and a depot that can choke. That is exactly our fleet style (Borderline Empire, Train Baron, Airport Baron).

## 2. What players love / hate

Genre review themes (store pages of the titles above; **summary-level, not a full review scrape — deepen at P0**):
- **Love:** satisfying "full → empty" feedback (compressing, baling), a clean eco fantasy, a city that visibly gets cleaner,
  upgrades that make trucks and machines faster.
- **Hate (idle shelf, recurring across tycoons):** forced ads, progress walls, nothing to decide, resets.
- **Theme signal from real life (Türkiye):** the scheme turned bottle returning into a habit for millions (3.21 M registered
  users in two months [NEWS-5]) and full/broken machines are a daily public topic [COMP]. A Turkish player instantly
  understands "the machine is full again"; a global player knows bottle deposits (Germany, Nordics, Australia, US states).

Design answers: real decisions every minute (routing, timing, placement), visible cleanliness, offline earnings that
reward a well-set network, no resets, no walls (research cap ≤ 20 min, like the other plans).

## 3. OUR game

**Working title: Bottle Route: Recycling Tycoon** (30 chars, exactly Play's limit; folder suggestion `games/bottle-route`,
package `com.coddykit.bottleroute`).

Name options and collision check (web search 2026-11-01; Play search itself still to do at P7):
| Option | Finding |
|---|---|
| **Bottle Route** | No game with this title found (searched "Bottle Route", "Return Run", "Refund Route"); closest hit "Recyclings Run" (different). Best option. |
| Refund Run | Not seen in results; "Return and Earn" is a real Australian deposit app, so avoid "Return & …" names. |
| Loop Haulers | Not seen; less clear about bottles. |
| ~~Recycle Tycoon~~ | Rejected: itch.io jam game and iOS "Recycle Tycoon 3D" exist. |
| ~~Recycle Empire~~ | Rejected: iOS title exists, and "Empire" is our Borderline brand anyway. |

**Pitch.** A coastal region has just switched on its bottle deposit scheme and the whole town is bringing back bottles at
once. You win the field-operator contract with one van and three deposit machines outside corner shops. Machines fill,
jam and overflow; cafés ring because their bags are piling up; the depot's counting line backs up at 18:00; the price of
PET bales drops the week you've stored the most. In a living 2.5D diorama you place machines, draw truck routes, decide
when to empty and what to leave for tomorrow, and turn the region's bottles into bales, then into cleaner streets. The
satisfaction is a network that **flows**: no full machines, no wasted kilometres, no queue at the baler.

## 4. Core loop — chaos managed as a process

A **work day** = ~4 real minutes at 1× (paused during the tutorial):

1. **Morning plan (09:00 dispatch).** The map shows every return point with a fill bar per material (PET / alu / glass).
   Without sensors the bars are *estimates* from history (fuzzy); with sensors they are live. The player builds the day's
   routes: tap a truck, tap stops in order (or drag a loop), see the predicted km, fuel, time and load.
2. **Field (day).** Citizens walk to machines with bags (visible MultiMesh crowd). Each deposit raises the fill bar.
   A full machine turns red, then **turns citizens away**; each turned-away citizen = a "wasted trip" that costs
   reputation. Trucks drive their routes; the load bar fills by **volume** (loose PET) and **weight** (glass), whichever
   comes first. Re-route mid-day by dragging a red machine onto a truck.
3. **Depot (evening).** Trucks unload into the intake. HoReCa bags and bulk hauls go through the **count & verify line**
   (throughput per hour). The **sorter** splits material; PET and aluminium go to the **baler**; glass goes to bays. A full
   intake makes trucks queue at the gate (wages tick).
4. **Settle and sell.** The day report: containers collected, **handling fee** earned (per container, from the scheme),
   km driven, fuel, wages, wasted citizen trips, machine downtime, depot backlog. Bales can be sold today at the spot price
   or stored (bay space is limited) for a better tender.

**The chaos (what goes wrong):** a machine fills early (match day, heat wave, festival); a machine jams (bottle stuck,
label unreadable, scanner fault); the app shows "available" but the sensor is offline → wasted trips; a café's back room
overflows; a bulk returner blocks a machine for 40 minutes with 300 bottles; a truck breaks down mid-route; fuel price
spike; the bale price crashes; the baler is down; glass breaks in a truck (lost load); an audit finds a count mismatch.

**The levers (process, not tapping):**
- **Route design:** stops, order, depot return points; **ring routes vs star routes**; a far village costs fuel; decide
  whether to serve it daily, every 3 days, or put a bigger machine there.
- **Service policy per point:** "empty at ≥ 80 %" / "fixed days" / "only when called". Low threshold = safe but costly,
  high threshold = cheap but risky. Policies drive offline earnings.
- **Machine placement and type:** where to put new machines (foot traffic heat map), size (bin volume), **compactor**
  upgrade (×2.5 PET capacity, more jams if unmaintained), multi-material vs single-material.
- **Manual return points:** sign up corner shops with a handheld scanner (cheap, no machine), but each shop's bags need
  a pickup and the shop is paid a fee per container.
- **Bulk channel:** open a **counting centre** so big hauls stop clogging machines (lower handling fee, no daily cap).
- **Daily cap rule:** a policy setting per region (e.g. 100 / 200 / 400 per person per day): low cap = shorter queues but
  angrier power-users; high cap = machines fill faster.
- **Fleet:** cargo bike (old-town alleys, tiny load), van, compactor truck (volume), glass flatbed (weight), later an
  electric truck (cheap per km, range-limited) and a ferry slot for the island.
- **Depot design:** intake bays, count lines, sorter, baler, glass bays, storage; each is a bottleneck to balance.
- **Crew:** drivers (shift hours, overtime), technicians (fix jams; travel to machines), depot workers.
- **Priority taps (hands on):** "Send the nearest truck", "Dispatch technician", "Open the 2nd count line", "Hold bales".
- **Idle/automation (secondary):** a **Dispatcher** office upgrade auto-builds routes from the service policies; offline
  earnings use the player's policies, so a well-tuned network earns more while away.

**Day result:** stars for service (no full-machine minutes, ≤ X wasted trips), efficiency (containers per km) and
throughput (depot backlog 0). Stars = **Green Rating**, which wins new districts and contracts.

## 5. Systems

**5.1 Return points.** Deposit machine (S/M/L bin, single or multi-material, compactor, sensor, card reader for the
"wallet" fantasy without a real-money reference), manual shop point (handheld), HoReCa pickup (café/hotel/restaurant
bags; contract volume), bulk counting centre, event bins (stadium, festival; temporary).

**5.2 Materials.** PET (light, bulky, best value when baled), aluminium (light, compact, high value, theft-prone),
glass (heavy, low value, breaks), later cartons/HDPE as content packs if needed. Each has volume/weight per unit, bale
size and a price series.

**5.3 Citizens (archetypes).** Commuter (2–5 bottles, low patience), family (20–40, weekend), student (cans, evenings),
café owner (calls for pickup), eco-enthusiast (comes even far, writes reviews), **bulk returner** (hundreds; blocks a
machine unless a counting centre exists), tourist (summer, coastal). Patience and walk distance drive "wasted trip"
penalties. No mockery of real informal waste pickers; bulk returners are ordinary citizens with big hauls.

**5.4 Vehicles.** Per type: volume, weight cap, speed, fuel per km, crew, maintenance interval, allowed roads (alleys,
ferry). Breakdown chance rises with skipped maintenance. Fuel price is a regional series.

**5.5 Depot.** Intake (truck slots), count & verify lines (containers/hour), sorter, baler (bales/hour, jam chance),
glass bays (tonnes), bale yard (storage cap), outgoing freight to the recycler. Overflow = trucks wait.

**5.6 Economy.** Cash (soft). Income: handling fee per collected container (higher for machine points, lower for bulk),
bale sales (spot/tender), HoReCa contracts, district bonuses. Costs: fuel, wages, maintenance, machine lease/purchase,
shop fees, depot power. **Green Rating** = reputation (earned only). No hard currency, gems or loot boxes.

**5.7 Upgrades / research.** Three branches: **Network** (sensors, bigger bins, compactors, more points), **Fleet**
(trucks, route optimiser, electric, maintenance plan), **Depot** (count lines, baler speed, storage, glass crusher).
~30 nodes in MVP, research ≤ 20 min per node.

**5.8 Never-reset rule.** Every district stays; new regions are added to a portfolio map (like Aisle Boss). Offline uses
the service policies per district.

## 6. Progression + content map (first ~10 hours)

| Ch | District | New thing | Est. time |
|---|---|---|---|
| 1 | Harbour Quarter | 3 machines, 1 van, depot with one count line. Learn fill bars, routes, unload | 0–30 min |
| 2 | Old Town | Narrow alleys (cargo bike), cafés and HoReCa pickups, manual shop points | 30–90 min |
| 3 | University Hill | Evening can spikes, sensors, service policies | 1.5–2.5 h |
| 4 | Stadium & Fairground | Event days (match, concert), temporary bins, crowds | 2.5–4 h |
| 5 | Industrial Estate | Regional depot, baler line, bale market + tenders, bulk counting centre | 4–6 h |
| 6 | Riviera Resort | Summer tourist season (×3 volume), hotels, heat waves | 6–8 h |
| 7 | Hill Villages + Island | Long-distance routes (the fuel dilemma), ferry slots, big remote machines | 8–10 h |
After that: a second region (inland city, winter, different material mix), seasonal events, contract challenges.

## 7. Crises / events

| Event | Effect | Lever that answers it |
|---|---|---|
| Heat wave | +60 % cans/PET for 2 days | Raise service frequency, temporary bins |
| Match / concert day | Stadium points fill in 2 h | Pre-position a compactor truck |
| Machine jam | Point offline until a technician arrives | Technician dispatch, maintenance plan |
| Sensor offline ("app says available") | Wasted trips pile up silently | Sensor redundancy upgrade, manual check route |
| Bulk returner | Blocks a machine, fills it | Counting centre, daily cap policy |
| Truck breakdown | Route stops mid-way | Spare truck, maintenance |
| Fuel price spike | Cost per km ×1.5 for a week | Shorter rings, electric fleet, bigger remote bins |
| Bale price crash | Spot price −40 % | Store bales (yard space), wait for a tender |
| Baler down | Depot backlog grows | 2nd baler, overtime |
| Glass breakage | Lost load, cleanup time | Glass flatbed, careful-driving trait |
| Audit | Count mismatch fine if count lines are overloaded | Don't overload the count line |
| Storm / road closure | Detours, ferry cancelled | Alternate routes, island buffer machine |
Crises are short (1–2 days), never game-over; "last resort" rewarded placements (§10) exist for two of them.

## 8. Map / diorama + art direction

- **Diorama:** one district at a time in tilt-shift 2.5D (like Train Baron); roads, shops with machines out front, a
  depot at the edge. Bright clean palette; the district visibly gets cleaner as Green Rating rises (fewer litter decals,
  greener parks), a cheap but strong "wow".
- **Blender:** buildings, machines (3 sizes), trucks (5), depot modules (intake, count line, sorter, baler, bale stacks,
  glass bays), event props. Modular kit + palette swaps.
- **Meshy:** citizens with bags (6 archetypes), drivers/technicians (characters only, optimisation pipeline).
- **Signature visuals:** machine fill bars (in-world gauge + map icon), truck bed fill, bales stacking up in the yard,
  baler press animation (Medium/High only).
- **Low profile (fleet §10, owner 10-24 rule):** citizens ≤ 50 % and updated at 15 Hz, MultiMesh crowds and bale stacks,
  no baler press animation (static frames), litter decals as one MultiMesh, no shadows, per-vertex lighting, 30 FPS cap,
  route lines as a single immediate mesh; target draws ≤ 120, prims ≤ 200k. Route planning is computed when the plan
  changes, never per frame.

## 9. Onboarding steps (show-and-do, no jeopardy, fleet §9)

1. "This machine collects bottles. The bar shows how full it is." → tap the machine.
2. "It's almost full. Let's empty it." → tap the van.
3. "Tap the machine to add it to the van's route." → tap the machine.
4. "Add the second machine too." → tap it (wrong tap repeats the hint, no penalty).
5. "Send the van." → tap Go. Time is paused until the player acts.
6. "Watch the van's load bar." → wait for the first stop (sped up).
7. "Now bring it back to the depot." → tap the depot.
8. "The count line counts the bottles. You earn a fee for each." → tap the coins.
9. "Plastic goes to the baler." → tap the baler.
10. "Sell your first bale." → tap Sell.
11. "Driving far costs fuel. Look at this number." → tap the fuel badge.
12. "Set a rule: empty this machine when it's 80 % full." → set the policy.
13. "Buy a bigger bin for the busy corner." → first upgrade.
14. "Hire a second driver." → first crew hire (automation step).
Later first-time tips: sensors, cafés, counting centre, bale market, events, ferry. Skip + Settings → "Replay the guide".
Firebase `onboarding_step` / `onboarding_complete` / `onboarding_skip`. Headless test per step (right action advances,
wrong action doesn't, skip → replay). **Volt City lesson (11-01, "bug di guide eror" 1★):** test the guide in every
language and on a Low device, and make every step recoverable if the player leaves the screen mid-step.

## 10. Monetization (games/tools/IAP_STANDARD.md; no trial, no subscription, no loot boxes)

Product id pattern `com.coddykit.bottleroute.<slug>`; 4 PPP tiers; restore for permanent products.

| Tier | slug | USD | Grants | ad_free? |
|---|---|---|---|---|
| Base | `noads` Remove Ads | 2.99 | interstitials + banner off; rewarded without video (same caps) | **yes** |
| Premium | `premium` Chief Operator Edition | 9.99 | noads + gold truck livery + extra save slot + starter cash | **yes** |
| Pass | `route_pass` Network Pass | 4.99 | permanent: +1 Dispatcher rule slot, +10 % handling fee, noads | **yes** |
| Convenience | `speed` Speed Pack | 2.99 | 3×/4× speed, auto-skip day report | no |
| Convenience | `dispatcher_plus` Dispatcher+ | 3.99 | 3 extra auto-route rule slots | no |
| Convenience | `offline_plus` Offline Plus | 2.99 | offline cap 4 h → 10 h, rate 50 % → 75 % | no |
| Convenience | `slots` Extra Saves | 1.99 | +3 save slots | no |
| Content | `scenarios` Scenario Regions | 2.99 | Snowy Inland City, Festival Island, Mega-Port | no |
| Cosmetic | `liveries_*`, `machine_skins_*`, `depot_themes_*` | 1.49–1.99 | looks only | no |
| Cosmetic | `cosmetics_all` Collection | 4.99 | all cosmetics | no |
| Bundle | `bundle` Complete Bottle Route | 19.99 | premium + route_pass + speed + dispatcher_plus + cosmetics_all | **yes** |
| Offer (once) | `starter` Starter Fleet | 1.99 | cash + 1 van, shown day 3–7 | no |
| Offer (event) | `event_kit` Match-Day Kit | 2.99 | consumable: temporary bins + extra truck for 1 day | no |
| Offer (event) | `comeback` Welcome-back Crate | 1.99 | consumable after 3+ days away | no |
| Consumable | `cash_s/m/l/xl` | 0.99 / 2.99 / 6.99 / 14.99 | 1 / 2.5 / 5 / 10 days of current income | no |

`ad_free` = noads OR premium OR route_pass OR bundle OR `legacy_ad_free`; to be added to `games/CLAUDE.md` §8 at P4.

**Rewarded (AdMob, opt-in only, fleet §4).** Cheapest consumable `cash_s` = 1 day income ⇒ single reward ≤ 0.2 day,
daily total ≤ 0.5 day; per-placement daily cap + cooldown (UTC day, persisted); `ad_free` owners get it without video.
| Placement | Reward | Cap/day | Cooldown |
|---|---|---|---|
| Offline return | ×2 offline earnings (bounded) | 1 | – |
| Emergency empty | one full machine emptied instantly | 3 | 10 min |
| Fuel voucher | one route at half fuel cost | 2 | 15 min |
| Baler rescue (crisis last resort) | baler repaired now | 1 | – |
| Daily bonus | small cash / research voucher | 1 | – |
Not given by ads: cosmetics, premium, speed/automation, ad removal. Kill switch `rewardedEnabled`. Interstitial and
banner = **house ads only** (CoddyAds; after the day report or chapter change, never in tutorial or purchase screens);
banner per the 10-26 standard; RevenueCat ad tracking (§5b). Review prompt: first session ~4 min, after the first
successful day report (`MIN_PLAY_S ≈ 240`).

## 11. ASO keyword set (Google Play)

English core from [ASO] (chaos table, US; columns = popularity band, difficulty, games in top 10, # ≥1M, median installs):
- `waste management game` — Low, **d28** (the easiest term in the whole table), 8/10 games, median 546k, top Trash Tycoon 12.8 M, no new ≥100k entrant.
- `recycling tycoon` — Low, d44, 10/10, median 126k.
- `garbage tycoon` — Low, d47, 10/10, median 500k.
- `eco tycoon` — Medium, d48, median 73k; `junkyard tycoon` popularity 47 (estimate) in the 09-28 export.
- Add: recycling game, bottle recycling, garbage truck game, waste collection, eco game, route planner, logistics tycoon.
- Title **"Bottle Route: Recycling Tycoon"** (30). Short description: "Empty the machines, plan truck routes, bale the bottles — run the recycling network."
- Don't use real scheme names (DOA, "Return and Earn", "Pfand" as a brand) or competitor titles in the listing.
- Language notes (**unverified**, check in ASO Atlas at P7): tr "geri dönüşüm oyunu / çöp kamyonu oyunu / depozito"
  (the DOA rollout makes TR topical, but **no DOA trademark**) · de "Recycling Spiel / Müllabfuhr / Pfand" (strong deposit
  culture) · es "juego de reciclaje / camión de basura" · fr "jeu de recyclage / camion poubelle" · pt-BR "jogo de
  reciclagem / caminhão de lixo" · ru "игра переработка мусора" · ja "リサイクル 経営 ゲーム" · ko "재활용 경영 게임" · 10th
  language hi "रीसाइक्लिंग गेम" (or zh; decide at P5).

## 12. Cannibalization with our games

| Our game | Overlap | How Bottle Route differs |
|---|---|---|
| smuggler (Borderline Empire) | Vehicle logistics, routes, contracts, risk (the owner's model) | Borderline moves goods *outward* with smuggling risk; Bottle Route is **reverse logistics**: many filling nodes, *when* to empty, volume vs weight, a depot pipeline, a material market. Clean civic theme. |
| pest-patrol (Critter Crew) | Dispatch crews to map calls | Pest calls are reactive incidents; here the core is **predictive service timing** and route efficiency. |
| volt-city (Grid Keeper) | Civic infrastructure, network | Electricity balance vs physical collection routes. |
| train-baron / airport-baron / canal-control | Network flow | Fixed lines/slots vs free road routes with fill-level inventory at every stop. |
| disaster-command | Map dispatch, surges | Disaster is emergency triage; this is daily operations. |
| Next-wave burger.md | Truck fleet dispatch to spots | Burger trucks *sell* at spots; ours *collect* from spots and feed a depot. Keep house-ad creatives distinct. |

## 13. Risks

1. **Impersonation / government branding.** Using DOA/TÜÇA names, logos or green-white colours would look official.
   Mitigation: fictional scheme and region, own icon set, legal check at P0; no real ministry quotes in the store text.
2. **Families policy drift.** Eco themes attract kids. Mitigation: target audience 13+, not designed for children, no
   cartoon-baby art; IARC questionnaire honest; if Play pushes us to Families, rules in `games/CLAUDE.md` §2 apply.
3. **Too "spreadsheet".** Route planning can feel dry. Mitigation: big visible feedback (fill bars, crowd turned away,
   cleaner streets), 90 s first payoff, auto-route Dispatcher as an option.
4. **Route UI on small screens.** Mitigation: tap-to-add stops, suggested route button, ≥ 48 dp targets, TouchScroll.
5. **Perf:** many machines + citizens + trucks. Mitigation: §8 budgets, MultiMesh, 15 Hz crowds on Low, perf_gate every delivery.
6. **Balance:** a punishing fill rate makes it stressful. Mitigation: forgiving first 3 days, short crises, no failure state.
7. **Theme fatigue in TR news** (complaints, price-rise claims). Mitigation: neutral, positive tone; the game is about
   doing the job well, not criticising a real scheme.
8. **Small genre leader size.** The top recycling game is 12.8 M, smaller than hotel/prison; we win on low difficulty
   (d28) and the empty "operator" niche, not volume.

## 14. MVP scope

- Chapters 1–4 (Harbour Quarter → Stadium), one region; portfolio map stub.
- 3 return point types (machine S/M, manual shop, HoReCa), 3 materials, 3 vehicles (cargo bike, van, compactor truck),
  depot with intake + count line + sorter + baler + glass bays, spot bale market (tenders after MVP).
- 6 citizen archetypes, 8 crises (heat wave, match day, jam, sensor offline, bulk returner, breakdown, fuel spike, baler down).
- Service policies + Dispatcher (3 rule slots), research 20 nodes.
- 3 graphics tiers, 10 languages, 14-step onboarding, full IAP catalog (except scenario regions), rewarded placements,
  house ads, Firebase events, in-app review gate.
- Out of MVP: chapters 5–7, island/ferry, electric fleet, tenders, second region, cosmetics beyond liveries.
- **MVP success metrics (soft launch, internal → production):** D1 ≥ 35 %, tutorial completion ≥ 70 %, first-session
  length ≥ 8 min, crash-free ≥ 99.5 %, Low p95 ≤ 50 ms.

## 15. P0–P8 phase TODO

- [ ] **P0 Strategy** — owner sign-off on this doc; impersonation/legal check (no DOA/TÜÇA/ministry marks); name check on
  Play; full review scrape of the §1b titles (deepen §2); ASO Atlas queries for §11; `ad_free` table entry approved.
- [ ] **P1 Core sim (Godot 4.7.2, pure `sim/`)** — road graph, return points with fill per material, citizen demand
  model, vehicles (volume + weight caps, fuel), route planner + service policies, depot pipeline, bale market, crises
  engine, economy, Green Rating, save/load + `save_fuzz`, headless balance sim (30 min / 2 h / 7 days); fill-rate vs
  route-cost curve tuned so "empty at ~80 %" is good but not always optimal.
- [ ] **P2 Diorama/art** — Blender kit (machines, trucks, depot modules, district blocks), Meshy citizens + crew,
  MultiMesh crowds/bales/litter, Low variants, own splash, art gate.
- [ ] **P3 UI/onboarding** — coddy_ui kit (map HUD, route editor, depot screen, day report, research, shop, settings +
  UMP privacy block), TouchScroll gate, 14-step guide + tests (incl. leave-mid-step recovery), in-app review gate, audio
  autoload (SFX: machine crunch, baler press, truck; 2 music loops; ElevenLabs batch only after owner approval).
- [ ] **P4 Monetization** — catalog (§10), Play Billing/RevenueCat, `ad_free`/legacy logic, rewarded placements + caps,
  house ads (+ `house_related.py` tags: logistics / civic / eco), RC ad tracking, `docs/MONETIZATION_PARITY.md` vs Corrupt.
- [ ] **P5 i18n** — 10 languages via in-session Haiku sub-agents; fonts bundled; overflow tests (de, ru long strings).
- [ ] **P6 Perf/QA** — Low perf_gate (prims ≤ 300k, draws ≤ 150, static ≤ 250 MB, 30 FPS, p95 ≤ 50 ms), physics gate
  (no physics → Dummy engine, 30 Hz), 7-day balance bots, emulator smoke on the Mini (sequential, silent).
- [ ] **P7 Store assets** — full-bleed gameplay screenshots (no phone frame), feature graphic, 10-language listing,
  Data safety/IARC/privacy section, icon candidates (owner picks), trailer via `tools/store-video/games/trailer.py`.
- [ ] **P8 Internal → production** — Jenkins `android-linux` AAB, symbols zip, Play app creation (Patron), AdMob rewarded
  unit + UMP form, RC credentials, `ads_app` registration, ASO Atlas tracking, internal → smoke → production (§15 steps).
