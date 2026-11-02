# Ridgeback Rangers: Dino Sanctuary (working title) — game plan #184 (S1, 2026-10-31)

> Folder name when built: `games/dinosaur-park/`. DOCS ONLY. Fleet rules: `games/CLAUDE.md`. Format model: `games/NEW_GAMES_2026-10-20.md`, `games/pest-patrol/STRATEGY.md`.
> Source tags used below (all fetched 2026-10-31 by S1 directly from the public Play pages; Codex C3 raw folder had no dinosaur data):
> [P-DINO] https://play.google.com/store/apps/details?id=com.codigames.idle.dinosaur.park.tycoon&hl=en_US
> [P-THEME] https://play.google.com/store/apps/details?id=com.codigames.idle.theme.park.tycoon&hl=en_US
> [P-DT] https://play.google.com/store/apps/details?id=com.umg.dino.park.land.tycoon.android&hl=en_US
> [P-JD] https://play.google.com/store/apps/details?id=com.sparklingsocietytycoon.jurassicdinosaurprehistoricpark&hl=en_US
> [P-SHB] https://play.google.com/store/apps/details?id=com.shbstudiogames.dinoparktycoon&hl=en-US
> Anything not traceable to one of these is marked "unverified". Play shows install bands, never exact counts.

## 1. Reference summary (Codigames dinosaur park game)

- **Title on Play:** "Dinosaur Park—Jurassic Tycoon" (package `com.codigames.idle.dinosaur.park.tycoon`); the developer's own description text calls it "Idle Dinosaur Park Tycoon". Developer: Codigames. [P-DINO]
- **Installs:** 1M+ (Play band). **Rating:** 4.41 average from 25,929 ratings (Play page data; a cached search snippet showed 4.3 / 22.8K, so figures move). **Last updated:** May 5, 2026. Contains ads + IAP. Content rating Everyone. [P-DINO]
- **Core loop (from the store description, [P-DINO]):**
  - Send an expedition team into a forest to "friendly capture" species (Allosaurus, Quetzalcoatlus, Triceratops, Stegosaurus, Brachiosaurus, Iguanodon, T-Rex are named) and bring them to the park.
  - Unlock enclosures/habitats; each species needs its own vegetation and water zones; keep dinos fed and happy.
  - Open visitor services (ticket booths etc.); put scientists on research; reinvest earnings into the team and the park. Positioned as an idle/management game ("If you like management and idle games…"); specific automation/offline details are unverified.
- **What the reviews add (sample on the page, [P-DINO]):** builders (one at a time, builds up to hours), feeding drones, a food warehouse, 4–5 biomes ("desert exhibit", "marshland"), ticket booths, rank caps per exhibit.
- Close competitors in the same keyword space (verified install bands): **Dino Tycoon – 3D Building Game 5M+ (4.63, 30,122 ratings, upd. Nov 28 2025)** [P-DT]; **Jurassic Dinosaur: Dino Game 5M+ (4.23, 15,930 ratings, upd. Jun 18 2026)** [P-JD]; Dino Park Tycoon: Dinosaur (shbstudiogames) 1,000+ [P-SHB]. So the shelf is real (two 5M+ titles) but none is a *management-of-chaos* game; they are all build + idle.

## 2. What players love / hate

Sample = the reviews embedded in the Play page HTML [P-DINO] (a handful, not statistically valid; a few could belong to sibling listings on the same page — treated as themes only).

**Love**
- The dinosaurs themselves and the habitats ("dinosaurs are cute", "everything about the game is great including the dinosaurs and habitats").
- Casual, no-friction play; "solid simple fun"; devs seen answering reviews and adding habitats (3 → 4–5 biomes in a few days).
- Seeing 5-star dinos in a maxed habitat as a collection goal.

**Hate**
1. **Too little content / game ends in ~1–2 weeks** ("I beat it in about 1 week while still going to work"; "once you build the 5 biomes and level them up, you're basically done").
2. **Single builder / long build timers** (up to ~4 h) and pay-to-add-builder pressure; players ask for ad-to-skip or cheap extra builders.
3. **Upgrades that backfire** — two feeding drones follow each other, the first feeds everything, the last habitat starves; "don't raise carrying capacity past 12". (A logistics bug that players *want* to be a real system.)
4. **Soft-locks / caps** — "max rank" reached while two dino slots stay empty; food warehouse stuck; last ticket booth unobtainable.
5. **Monotony** — the most lucrative species are all one type, so you end with an exhibit of 6 identical dinos.
6. **Ads/IAP friction** — "paid to win for no ads… pricey for how short the game is"; random-item/premium-currency tool shortage; occasional crash on free digging tools.

**Take-away for us:** the audience wants (a) more to *do* than wait, (b) logistics that make sense, (c) variety per species, (d) a long tail. A process-management game addresses all four.

## 3. OUR game

- **Working title: Ridgeback Rangers: Dino Sanctuary.** Options: (2) *Hatchling Ridge: Dino Rangers*, (3) *Dino Keeper HQ*.
- **Name collision check (S1, web search 2026-10-31):** no Google Play listing found for "Ridgeback Rangers", "Hatchling Ridge", "Dino Keeper HQ", "Raptor Ridge Rangers", "Saurian Keepers", "Cinder Isle", "Dino Ranger HQ", "Paleo Ranger". Related but different names seen: *Raptor RPG – Dino Sim*, *Dinosaur Isle: Survival*, *Saurian* (Steam/Kickstarter survival), a browser game "Hatchling" on GitHub. Re-check the exact string on Play search at P7 before the package name is fixed (`com.coddykit.ridgebackrangers` proposed). Avoid the word "Jurassic" anywhere (franchise trademark; Codigames uses it in a title, we do not) and avoid "Saurian".
- **Pitch:** You are the new Chief Warden of Ridgeback Isle, a volcanic island reserve where scientists hatch rescued prehistoric animals. It is a living 2.5D diorama: herds roam, a safari tram loops past the enclosures, rangers drive between barns, eggs hatch, storms roll in. You never "tap to earn". You run the **day** — decide who is fed first, which keeper team goes to which problem, which animals may share a paddock, when the tram runs and when it must wait — and keep the island calm. Gentle, funny, no animal ever hurts a person; a breach means a tram halt and an evacuation, not a tragedy.
- **Originality statement:** original island, characters, UI, names and mechanics. Not copied from Codigames: no "expedition → friendly capture" loop, no builder timers, no scientist-research tree, no managers-per-building idle ladder. No franchise names (no "spared no expense", no gates/jeeps imagery that mimics films).

## 4. Core loop — chaos managed as a process

A **Park Day** is ~6–8 real minutes (speed 1×/2×/3× later). The player plans, then runs, then reads the result.

1. **Morning plan (30–60 s).** Feed board (which barns send crates to which feeding stations), ranger rosters (4 roles), tram timetable (how many cars, how many stops), special exhibit of the day.
2. **Run (4–5 min).** Visitors stream in; the tram loops; animals live on a mood clock. Problems appear as **pins on the diorama**: a squabble between two herbivores, a hungry herd at the far feeder, a limping hadrosaur, a loose fence post, a rain squall flooding a path. The player taps a pin, then a ranger team; teams drive/walk there on real paths and take real time. Teams are scarce → **prioritise**.
3. **Flow pressure.** Tram capacity and path crowding are the visitor-side bottleneck; feed trucks and vet bay queue are the animal-side bottleneck. They compete for the same ranger pool and the same road lanes (one-lane service road: trucks and ambulances block each other — real routing choices, not an idle number).
4. **Evening watch (60–90 s).** Nocturnal species wake, lamp lines turn on (power budget), the Night Safari tram runs. Optional high-risk/high-reward.
5. **Day report.** Sanctuary Rating (stars), Care Score, donations (conservation), revenue. Unspent pins show what was ignored (so the player learns the cause).

**Compatibility (the unique puzzle):** every species has *Diet* (fern / fruit / fish crates / protein crates), *Biome* (forest / dune / marsh / ash / sky), *Temperament* (Calm / Skittish / Bossy / Nocturnal) and *Herd size*. Mixing is allowed but each pair has a **Harmony** value; low harmony = squabbles spawn pins. Learning pairings is the long-tail depth (30 species → hundreds of pairings) and answers "six identical dinos in one exhibit" (a mono-herd is *worse* for visitors than a varied paddock; visitors score variety).

**Automation as reward, not a wall:** Chief Ranger (auto-dispatch for pins of one type), Dock Foreman (auto feed schedule), Tram Conductor (auto timetable). Each is unlocked by play (Care Score), can be switched off, and is *sub-optimal* versus a careful player, so the player always has a reason to look.

## 5. Systems

- **Roles (rangers).** Feeder, Vet, Handler (calms animals, escorts), Fence Tech. 2 of each at start; cap 8 per role. Each has 2–3 trait slots (Quick, Gentle, Night Owl) from training; training takes game-hours but runs in parallel (no single-builder bottleneck, directly answering review #2).
- **Zones (6 biomes, §6):** Fern Valley, Dune Mesa, Marsh Delta, Ash Highlands, Skyline Aviary, Night Hollow. Each zone = an enclosure ring + a feeding station + a viewpoint + (later) a water point.
- **Enclosure rules:** capacity (m²), water %, shade %, vegetation %, fence tier (wood → steel → double-moat). Overcrowding raises Stress. Fence tier gates which species (Bossy carnivores need tier 3).
- **Feed logistics (real lanes):** Barns (Fodder Barn, Fish Dock, Protein Shed) → crate trucks → feeding stations → animals. Crates spoil in heat; truck capacity, road lane, and distance matter. Fixes the "feeding drone does all the work" complaint: stations pull from the nearest truck, and the UI shows the route.
- **Vet bay:** triage queue with 3 severities; green/yellow/red cases; red must be seen within 90 game-seconds or the animal leaves the exhibit for the day (no deaths).
- **Hatchery / Dig Sites (deterministic, no gacha):** each Dig Site on the Chart lists the exact species it can yield. Dig crews (Handler + Fence Tech) bring *fossil shards*; 100% shards = a Rescued Egg of that species; Incubator slots (3 → 8) need temperature kept in a band (simple slider mini-task, 10 s, skippable by Hatch Tech upgrade). Nursery → Young → Adult (adult gives full appeal).
- **Visitors:** groups with interests (Big Ones, Babies, Water, Birds, Quiet). They ride the **Safari Tram** (a loop with 3–8 stops; each stop is a viewpoint; tram cars have seats) or walk. Satisfaction = variety + activity (animals *doing* something at feed time: feeding shows are timed events that draw crowds) + cleanliness + queue time.
- **Economy:** Coins (tickets, snacks, gift shop, donations); **Conservation Points** (earned by Care Score; unlock species/zones/research); **Amber** (rare; milestone rewards + IAP packs, never required). Prestige is a **Season Reset** later (see §6), keeping the species log.
- **Upgrades:** tram cars, road lanes (second lane), power plant, generators for night, shade sails, water points, vet bay beds, feed station capacity, fence tiers, gift shop, cafes.
- **Anti-soft-lock rules (from review #4):** every cap has a visible next step; unlock gates are shown with progress bars; no "max rank" with empty slots.

## 6. Progression + content map (first ~10 hours)

| Ch | Zone / site | Hours | New things | Chaos introduced |
|---|---|---|---|---|
| 1 | Landing Cove | 0–1 | Gate, 2 paddocks (3 calm herbivores), 1-car tram, Feeder + Vet | Hungry herd pin; first squabble |
| 2 | Fern Valley | 1–2.5 | Hatchery, 2nd tram stop, Handler role, Dig Site 1 | Crate spoilage in heat; path crowding |
| 3 | Dune Mesa | 2.5–4.5 | Water points, shade sails, Skittish species, road lane 2 | Heat wave; water shortage |
| 4 | Marsh Delta | 4.5–6.5 | Wetland paddocks, mud storms, Fish Dock, vet bay 2 | Flooded paths; truck jams |
| 5 | Ash Highlands | 6.5–8.5 | Bossy carnivore sector (tier-3 fence), tremor events, generators | Tremor breaks fence tiles; power flicker |
| 6 | Skyline Aviary + Night Hollow | 8.5–10+ | Flying species, Night Safari, Season Reset | Gust events; nocturnal wake-ups |

Species targets: MVP **14** (4 Fern, 3 Dune, 3 Marsh, 2 Ash, 2 Sky), full release 28–32. Each species has 4 states (idle, eat, roam, play), one unique feed-time "show" animation.

Retention levers (not idle walls): daily **Ranger Call** (3 short pins), weekly **Island Event** (storm week, hatch festival), species log completion, **Season Reset** (new island layout seed + permanent Conservation bonus).

## 7. Crises / events

- **Squabble** (low harmony pair) · **Hungry herd** (feed lane delayed) · **Sick animal** (vet triage) · **Loose fence tile** · **Escape alarm** (animal wanders onto a path: tram halts, Handler escorts it back; reputation dips, nobody is hurt).
- **Weather:** rain squall (paths flood, outdoor stops close), heat wave (water + crate spoilage), dust storm (visibility, tram speed), fog (night).
- **Island:** tremor (fence tile loss in Ash), power flicker (lamps, vet lights), supply boat late (barn stock).
- **Visitor:** school-trip surge (young groups, want Babies), VIP film-crew day (hidden scoring), photo contest (needs animals active at the viewpoint).
- **"Last resort" (rewarded, §10):** Storm Shelter (repairs all fence tiles once) — capped.
- Rule: every event is non-violent and recoverable; no animal or person dies; a failed pin costs reputation and coins, not progress.

## 8. Map / diorama + art direction

- **Look:** warm toy-diorama island, readable at phone size, saturated greens and terracotta, soft shadows. Isometric-ish tilted camera with pinch zoom and focus-sector swipe. Chunky silhouettes, big eyes on young animals (cute, per review #1).
- **Buildings (Blender, hand-built low-poly kit):** gate, barns, vet bay, hatchery, tram station, viewpoints, fences (3 tiers), shade sails, towers, lamps. Modular, shared materials, atlas textures.
- **Animals (Meshy, then cleaned/retopo in Blender):** 14 MVP species, 3 LODs, shared skeleton families (biped, quadruped, long-neck, flyer) so animation sets are reused; texture variants (color morph) for variety without new meshes. **People:** Meshy base mesh + 6 outfit variants, instanced crowds.
- **Meshy budget:** within the monthly credits; each model through the optimisation line (prim budget, owner image approval).
- **Low-profile plan (fleet budget: prims ≤ 300k, draws ≤ 150, 30 FPS):**
  - Shadows off; per-vertex lighting; MSAA off; 3D scale 0.85 (§10 of games/CLAUDE.md).
  - Only the **focused sector** animates fully; other sectors drop to 10 Hz poses / frozen idle (pins still show).
  - Visible animals ≤ 24 on Low (≤ 48 Mid/High), crowds via MultiMesh ≤ 60 visitors (≥ 150 on High), billboard impostors beyond 25 m.
  - Foliage density ×0.5, no grass shader, rain as a single particle emitter ≤ 150 on Low; no heat-haze; lamp glow as sprite not light.
  - Tram = one merged mesh per car; fences merged per tile row; MultiMesh writes only when something changed.
  - New heavy effects (volcano ash, god rays, water reflections) **Mid/High only** (rule 10-24 "no lag on weak devices"; every delivery logs a Low before/after perf_gate line).
- **Native debug symbols:** release script calls `games/jenkins/release_symbols.sh` (W63-12).

## 9. Onboarding (step-by-step, show-and-do; §9 of games/CLAUDE.md)

Spot-lit tap targets; each step advances only on the real action; wrong tap re-shows the step; no time pressure (sim paused or paced); Skip + Settings → "How to play / Replay guide". Firebase `onboarding_step`, `onboarding_complete`, `onboarding_skip`.

1. Welcome to Ridgeback Isle (one card) → tap the Chief's badge.
2. Pan the island (swipe) → pinch to zoom.
3. Tap the hungry triceratops pin.
4. Pick a Feeder ranger and send her (shows route).
5. Watch the crate arrive (auto-camera) → tap "Fed!" toast.
6. Open the Gate (tap the gate); first visitors arrive.
7. Tap the tram car to *send it out*; watch visitors board.
8. Meet a squabble pin → drag the Handler to it.
9. Open the Compatibility card (tap two animals) → read the harmony bar.
10. Build your first fence post (tap a build slot).
11. See the Day report → tap "Next day".
12. Hatchery teaser: tap an egg → "check temperature" slider (first time, forgiving).
Context hints on first use: vet bay, water point, tremor, Night Safari, Dig Site chart.
Safe: no animal can leave the paddock and no ranger can be "lost" during steps 1–11.

## 10. Monetization (per `games/tools/IAP_STANDARD.md`; NO trial, no loot boxes, no subscription, Play only)

Product ids `com.coddykit.ridgebackrangers.<slug>` (final package decided at P7). Prices = T1 USD reference; 4 PPP tiers.

| Layer | Slug | USD | Gives | ad_free? |
|---|---|---|---|---|
| Base | `noads` Remove Ads | 2.99 | interstitials off; rewarded delivered without video (same caps) | **yes** |
| Premium | `premium` Chief Warden Edition | 9.99 | noads + golden tram/gate/badge + 2 extra save slots + starter Amber | **yes** |
| Pass (permanent) | `ranger_pass` Ranger Pass | 4.99 | 5th ranger slot per role, Chief Ranger auto-dispatch unlocked early, +1 incubator slot | **yes** (listed as "pass" in §8; owner confirmation required) |
| Comfort | `speed` Speed Pack | 1.99 | 3× day speed, auto-skip day report | no |
| Comfort | `automation` Park Crew | 3.99 | Dock Foreman + Tram Conductor auto, toggleable | no |
| Comfort | `slots` Extra Saves | 1.99 | +3 saves | no |
| Idle | `offline_plus` Night Shift | 2.99 | offline ratio/cap higher | no |
| Cosmetic | `skin_tram_*` (3) · `theme_fence_*` (3) · `skin_ranger_*` (3) | 0.99–1.99 | looks only | no |
| Cosmetic | `cosmetics_all` Collection | 4.99 | all cosmetics (~40% off) | no |
| Content | `pack_species_*` (2) Rare Herd Packs | 2.99 | 2 extra species each (balance-neutral, alt. pairings) | no |
| Bundle | `bundle` Complete Sanctuary Collection | 17.99 | premium + ranger_pass + speed + automation + all cosmetics | **yes** |
| Offer (once) | `starter` Welcome Crate | 1.99 | coins + 1 incubator slot; day 3–7 | no (does not include Remove Ads) |
| Offer (event) | `storm_kit` / `comeback` / `milestone` | 2.99–4.99 | event-triggered consumable | no |
| Consumable | `cash_s/m/l/xl` | 0.99/2.99/6.99/14.99 | 1/2.5/5/10 days of income (scaled) | no |
| Consumable | `amber_s/m/l` | 1.99/4.99/9.99 | Amber (never required for progress) | no |

Rules: offer pop-up never in tutorial/first session, max 1 per session, same offer returns ≥ 3 days later. Restore button in Shop and Settings. Consumables are not restored (say so in store text). "No ads" actually removes all interstitials *and* video-required rewards (answer to the Codigames-type complaint that paying still shows ad offers).

**ad_free products (to add to games/CLAUDE.md §8 after owner approval):** `noads`, `premium`, `ranger_pass`, `bundle`. Everything else (speed, automation, slots, cosmetics, packs, consumables) does not remove ads.

**Rewarded (AdMob only, opt-in, ready+cap allow only; earned callback only):** cheapest consumable = `cash_s` (1 day income) → one reward ≤ 0.2 day income, all rewards per UTC day ≤ 0.5 day income. Placements and daily caps (persisted, cooldown 10 min):
1. Dawn Report: "Double overnight donations" — value 0.08, ×2/day.
2. Incident: "Borrow a ranger for 2 game-minutes" — 0.04, ×3/day.
3. Hatchery: "Warm the incubator" (−10 min) — 0.05, ×2/day.
4. Crisis last resort: "Storm Shelter" (fence repair once) — 0.10, ×1/day.
5. Info: "Scout's tip" (reveals one Dig Site yield) — no coin value, ×2/day.
Total value ≤ 0.48 day. Never rewarded: cosmetics, premium Amber, permanent speed/slots, ad-free. Ad_free owners get them videoless (`rewarded_skipped_paid_user`). Events: `rewarded_offer_shown`, `rewarded_offer_accepted`, `rewarded_completed`, `rewarded_skipped_paid_user`. Remote kill: `rewardedEnabled`.

**Interstitial/banner = house only (CoddyAds):** banner standard after onboarding, none in tutorial/purchase/boot; interstitial at natural pauses (day-report close), post-reward deferral. No AdMob interstitial/banner. Age: target audience 13+ (not Families), see risk R4. RevenueCat ad-tracking hook in from day one (§5b). Remember Play listing "contains ads".

## 11. ASO keyword set (Play, EN core + 10-language notes)

- **Core (EN):** dino park tycoon, dinosaur park, dino zoo, dinosaur sanctuary, dino ranger, hatch dinosaurs, dino management game, safari park tycoon, prehistoric park, zoo manager (secondary).
- Title (≤30): `Ridgeback Rangers: Dino Park` (28) · Short: "Run a cozy dino sanctuary: feed herds, hatch eggs, calm the island." Avoid "Jurassic".
- Data: Codigames' title uses "Jurassic Tycoon" and "Dino Park" — we compete on "dino park/dino sanctuary/ranger" and a differentiated screenshot story (pins, tram, rangers). Demand figures: **unverified** — pull ASO Atlas bands (`dino park`, `dinosaur park`, `dino zoo`, `dino tycoon`) in P7 before final.
- Per language: tr "dinozor parkı / dinozor oyunu" · de "Dino Park Spiel / Dinosaurier Zoo" · es "parque de dinosaurios" · fr "parc de dinosaures" · pt-BR "parque dos dinossauros" · ru "парк динозавров" · ja "恐竜パーク" · ko "공룡 공원 게임" · hi "डायनासोर पार्क". Translate the pitch, not literally the title.

## 12. Cannibalization with OUR games

| Game | Overlap | Why this one differs |
|---|---|---|
| pest-patrol (Critter Crew) | Creature crews, dispatch on a map, kind tone | Critter Crew = city calls, catch-and-release, no visitors; here = a visitor park with enclosure ecology, feeding lanes, tram flow. Keep the Handler/dispatch UI distinct (pins on a living diorama, no call queue) |
| lifeguard-hq | Dispatch + crowd | Beach rescues vs animal husbandry; different art and role set |
| driftwood | Island, nature | Driftwood = survival, single player; here = managed visitor sanctuary |
| skiresort (Snow Empire) | Visitor park tycoon | Ski lifts/slopes vs enclosures; no animals; no overlap in the core |
| disaster-command | Evacuation, weather events | Events are flavour here; evacuation only as an "escape alarm" subset |
| theme-park (Midway Control, sibling #184) | Visitor flow tech | Shares the `visitor_flow` kit (≈40% code); audiences differ (animals/kids-adjacent vs thrill rides); house ads cross-promote |
Mild house-ad synergy: Critter Crew players are the likeliest dino audience (animal-care), so rotate them in banners.

## 13. Risks

- **R1 Content depth** — the #1 competitor complaint. Mitigation: pairing/harmony system + 28–32 species + weekly events; but art volume is the cost. MVP 14 species.
- **R2 Animal art volume/perf** — mitigated by shared skeletons, colour morphs, sector animation freeze.
- **R3 Complexity vs casual audience** — onboarding of 12 steps + progressive reveal; automation as comfort.
- **R4 Families policy** — dinosaurs attract children. If Play judges the app as appealing to children, Families rules (no AD_ID, no AdMob except certified, no external links) apply. Decision needed from owner: declare 13+ and avoid child-directed marketing (default), or build a kids variant later (no new work now).
- **R5 Idle-genre expectation** — idle players expect no attention required. Mitigation: Chief Ranger / Dock Foreman automation, offline earnings, Night Shift.
- **R6 IP** — no film imagery; no "Jurassic"; species names are scientific/public.
- **R7 Policy/IAP** — "random items" criticism avoided by deterministic dig sites and no loot boxes.
- **R8 Install volume unverified** — market sizing uses only Play bands (5M+ / 1M+), not downloads.

## 14. MVP scope

- Chapters 1–3 (Landing Cove, Fern Valley, Dune Mesa), 8 species, 2 biomes, 2 paddocks per zone, 1 tram line, 4 ranger roles, feed lanes (Fodder Barn only), vet bay v1, Hatchery v1, Dig Site 1–2, day report, 6 incident types, 5 rewarded placements, shop with `noads`, `premium`, `cash_s/m`, `starter`, 10 languages, onboarding 12 steps, perf budget on Low.
- Out of MVP: Marsh/Ash/Sky zones, Night Safari, Season Reset, pass/automation/pack products (post-launch weekly).

## 15. P0–P8 TODO (fleet phases)

- **P0 Strategy** — this doc; owner/Patron picks the name; Play name re-check; ASO Atlas bands pulled; `games/dinosaur-park/STRATEGY.md` + `PLAN.md` created from this; paket name fixed; add to `house_related.py` labels (zoo / animals / tycoon).
- **P1 Core sim (headless first)** — day clock, pin/incident engine, ranger roles + pathing, feed lanes, harmony matrix, hatch/dig sites, visitor satisfaction; save codec (§14 fuzz test, `.unreadable` handling); `tests/run.gd`, `physics_gate` (30 Hz dummy if no physics); audio manifest stub.
- **P2 Diorama/art** — Blender kit (gate, barns, vet, hatchery, tram, fences ×3); Meshy 8 species → retopo → LODs; shared skeletons; Chapter 1–3 map; Low-profile switches (focused-sector animation, crowd MultiMesh).
- **P3 UI/onboarding** — W64 coddy_ui kit (pins, compatibility card, roster sheet, compact shop ≥ 3 products on screen, settings with UMP privacy block); TouchScroll gate; 12 onboarding steps with headless tests; 10 languages scaffolding.
- **P4 Monetization** — shop catalog `data/shop.json`; RevenueCat; ad_free entitlement (`noads` or big pack) + `legacy_ad_free` n/a for new game; rewarded 5 placements with caps and Firebase events; CoddyAds house banner/interstitial; RC ad tracking; parity table `docs/MONETIZATION_PARITY.md` vs Corrupt; in-app review (first session, ~4 min, success moment: first full Park Day completed).
- **P5 i18n** — en, tr, de, es, fr, pt-BR, ru, ja, ko, hi; session Haiku sub-agents; fonts (Devanagari/CJK/Cyrillic) bundled; text-fit test with `de`.
- **P6 Perf/QA** — Low profile perf_gate (prims, draws, PSS on device, 30 FPS, p95 ≤ 50 ms), Mini emulator smoke (never parallel), save fuzz, audio test, Android vitals baseline.
- **P7 Store assets** — full-bleed screenshots (no phone frame), icon (owner approval), Play text ×10, data safety CSV + privacy section, trailer (`trailer.py`), ASO terms final.
- **P8 Internal → production** — Play INTERNAL; AdMob/UMP, RC credentials, privacy, ASO Atlas registration, ads_app/ads_slot backend rows, AdMob-Play link; production only with Patron + owner approval and taze smoke PASS.
