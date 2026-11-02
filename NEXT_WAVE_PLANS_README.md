# Next wave: 11 original tycoon plans in Codigames keyword spaces (#184)

Date: 2026-10-31 · Author: S1 (Patron/owner order #184) · Status: **phase 1 draft, owner-approved scope; deepening deferred** (docs only) (owner 10-31: "that many plans is enough for now; the new games will be built later"). No code, no store work.

> **Deepening deferred.** A second pass was planned for the thin sections (§2 using all 20 embedded reviews, §7 ≥ 8 crises, §11 ASO demand numbers, §13 ≥ 8 risks, §14 MVP counts/metrics); see "Thin sections" below. Only coffee.md and burger.md (§13 risks, §14 MVP) and part of supermarket.md got it before it was stopped. Do this pass in each game's P0 before coding.

## Purpose and scope

- **Goal.** Original 2.5D management games that compete for the same Google Play search spaces as 11 Codigames idle
  tycoons: prison, police, law firm, hotel, hospital, university, supermarket, coffee, burger, dinosaur park and theme park.
- **Fleet style.** The player runs a living 2.5D diorama and **manages chaos as a process**: routing, prioritising,
  staffing and finding the bottleneck. This is not idle tapping. Models: Corrupt State, Airport Baron, Train Baron and
  Borderline Empire.
- **Originality rule (hard).** We take no names, art, UI layouts, text or unique mechanics from Codigames, and we use no
  "Idle … Tycoon" or "… Empire Tycoon" naming. Each plan has an originality note. The fact-check removed one mechanic
  slip (see "Fact-check log").
- **Fleet rules apply unchanged** (`games/CLAUDE.md`):
  - Google Play only.
  - Wide IAP catalog, no trial.
  - AdMob is used for opt-in rewarded ads only. Interstitials and banners are house ads only.
  - Step-by-step onboarding and 10 languages.
  - Low-profile performance budget: ≤300k primitives, ≤150 draw calls, 30 FPS.
- **Each plan** is one file with the 15 sections from the brief (`/Volumes/BuildSSD/tmp/slaves/S1/handoff-184/BRIEF.md`).

## Portfolio

Reference figures come from C3's raw Play capture (en-US, 2026-10-31 07:14 UTC). "Ratings" is Play's
`aggregateRating.ratingCount`, not the number of written reviews.

| # | Reference (Play title · package) | Installs | Rating (count) | Last update | Our working title | Key differentiator | Build order | Overlap with our games (how we differ) | Plan |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Hotel Empire Tycoon－Idle Game · `com.codigames.hotel.empire.tycoon.idle.game` | 10M+ | 4.39 (510,931) | May 5, 2026 | **Checkout Crunch: Resort Manager** | The 11:00 turnover day: pack guests into a room-night calendar and route housekeeping carts | **1** | Low. sky-tower-ops shares the "hotel" keyword but is about elevators; we are horizontal turnover. pharmacy-rush has a queue, but reception is only one phase here | [hotel.md](hotel.md) |
| 2 | Idle Supermarket Tycoon－Shop · `com.codigames.market.idle.tycoon` | 10M+ | 4.14 (599,845) | Oct 14, 2026 | **Aisle Boss: Grocery Manager** | Shelf plan, perishables and markdowns, truck deliveries; stores you open are never reset | **2** | Medium. pharmacy-rush is one counter checking prescriptions; this is a walk-through store with a supply chain | [supermarket.md](supermarket.md) |
| 3 | Prison Empire Tycoon－Idle Game · `com.codigames.idle.prison.empire.manager.tycoon` | 10M+ | 4.24 (663,726) | Oct 28, 2026 | **Cellblock Clock: Prison Sim** | A 24-hour schedule per wing: time-slot inmate groups, defuse tension, score safety and successful release (not profit) | **3** | Low. corrupt-tycoon is political favours; smuggler plays the smuggler; bureaucracy-office is desk queues | [prison.md](prison.md) |
| 4 | Idle Theme Park Tycoon · `com.codigames.idle.theme.park.tycoon` | 10M+ | 4.06 (389,154) | Oct 30, 2026 | **Midway Control: Park Ops** | Run the park day: guest wish lists, queue steering, ride crews, crowd surges when shows end, weather closures | 4 | Low to medium. airport-baron and sky-tower-ops also have queues, but those are vehicles or elevators; here guests have wishes and itineraries | [theme-park.md](theme-park.md) |
| 5 | Idle Police Tycoon - Cops Game · `com.codigames.idle.police.department.tycoon.cop` | 10M+ | 4.25 (210,468) | Nov 20, 2025 | **Beatline: Police Patrol** | Draw patrol routes on the map; police presence prevents incidents before their fuses escalate; three shift rosters | 5 | Medium. pest-patrol and disaster-command also dispatch on a map, but they react to calls; here prevention is the core verb | [police.md](police.md) |
| 6 | University Empire Tycoon －Idle · `com.codigames.university.empire.idle.tycoon` | 1M+ | 3.74 (43,737) | Nov 20, 2025 | **Semester Rush: Campus Dean** | Weekly timetable puzzle plus routing the crowd across the quad when the bell rings | 6 | Low. bureaucracy-office also does scheduling and admin, but here admin is a minor bar | [university.md](university.md) |
| 7 | Hospital Empire Tycoon - Idle · `com.codigames.idle.hospital.empire.tycoon` | 1M+ | 4.36 (34,927) | May 4, 2026 | **Charge Nurse: Hospital Manager** | A capacity map of the whole hospital: triage, beds, porters, the discharge bottleneck, ambulance diversion | 7 | Medium. pharmacy-rush shares medical keywords and flu; disaster-command shares surge events; here it is steady-state bed flow | [hospital.md](hospital.md) |
| 8 | Law Empire Tycoon - Idle Game · `com.codigames.idle.law.empire.tycoon` | 1M+ | 4.40 (51,739) | Nov 20, 2025 | **Docket Day: Law Firm Rush** | A calendar of colliding hearing dates: fit staff hours into it, choose settle or litigate, keep client money separate | 8 | Low to medium. bureaucracy-office is counters and stamps; silent-evidence is deduction | [law-firm.md](law-firm.md) |
| 9 | Dinosaur Park—Jurassic Tycoon · `com.codigames.idle.dinosaur.park.tycoon` | 1M+ | 4.41 (25,929) | May 5, 2026 | **Ridgeback Rangers: Dino Sanctuary** | Dispatch rangers on a living island: feeding order, which species can share a paddock, tram flow, gentle escape drills | 9 | Medium. pest-patrol has creature crews and calls; driftwood is an island survival game. Sibling of theme-park (both use the `visitor_flow` kit) | [dinosaur-park.md](dinosaur-park.md) |
| 10 | Idle Burger Empire Tycoon—Game · `com.codigames.idle.burger.empire.tycoon` | 1M+ | 3.96 (12,479) | May 4, 2026 | **Grill Convoy: Burger Truck Manager** | Send trucks to selling spots, load stock, lay out the line inside the truck | 10 | Medium to high. doner-night is also grilled street food with crowd waves; we differ by daytime truck fleet dispatch | [burger.md](burger.md) |
| 11 | Idle Coffee Shop Tycoon · `com.codigames.idle.coffee.shop.empire.tycoon` | 500K+ | 3.99 (6,366) | May 4, 2026 | **Pour & Roast: Cafe Manager** | Route order tickets across drink stations, plus a back-room coffee roasting pipeline and wholesale | 11 | High. chaiwala-rush is also a hot-drink stall with rush waves; we differ by ticket routing and roasting | [coffee.md](coffee.md) |
| 12 | *(no Codigames reference; real-world theme: Türkiye's DOA deposit return, genre leader Trash Tycoon Idle business 12.8M)* | — | — | — | **Bottle Route: Recycling Tycoon** | Reverse logistics: deposit machines fill up; when to empty, which truck, fuel vs service level, depot count line + baler, bale market (owner 11-01, Borderline Empire model) | owner decides | Medium. smuggler shares vehicle logistics, pest-patrol map dispatch; here it's predictive emptying + a depot pipeline | [recycling.md](recycling.md) |

**Overlap inside this wave.** The plans fall into four groups:

| Group | Plans | Each game's core verb |
|---|---|---|
| Justice | prison, police, law | Time schedule; spatial coverage; calendar capacity |
| Food and retail | supermarket, coffee, burger | Floor layout and stock; ticket routing and roasting; truck dispatch |
| Visitor parks | theme park, dino | Rides and crowds; animal care |
| Service buildings | hotel, hospital, university | Rooms; beds; timetable |

Within each group the plans already separate the core verbs as shown. They must also keep their meta-progression and
ad creative distinct.

## Suggested order and reasoning

Signals, with how each one was weighed:

- **Market size:** the Play installs band plus the rating count, which is a better size proxy inside one band.
- **Opening:** a low reference rating, or a stale reference (last updated Nov 2025 or May 2026), means weaker live-ops competition.
- **Fit with the fleet:** our proven flow and queue systems, plus reusable Blender and Meshy kits.
- **Cannibalization:** overlap with our live games.
- **Cost:** art and performance cost on the Low profile (my estimate from each plan's §8).

| Order | Game | Market | Opening | Fit / reuse | Cannibalization | Art/perf cost | Main risk |
|---|---|---|---|---|---|---|---|
| 1 | Hotel | 10M+, 511K ratings | Reference not updated since May 2026; reviews hate forced ads and passive waiting | Very high: same turnover and flow style as airport-baron | Low | Medium (resort map + carts) | 10M+ incumbent at 4.4 |
| 2 | Supermarket | 10M+, 600K; the 3D "simulator" shelf reaches 100M+ | Card-pack manager gate and progress resets in reviews; 4.14 | High: retail props from pharmacy-rush; ASO data exists (`supermarket simulator` 56) | Medium (pharmacy-rush) | Medium (cutaway + shoppers) | Simulator audience wants first-person 3D |
| 3 | Prison | 10M+, the largest by ratings (664K); ASO Atlas scraper 38.3M | `prison tycoon` Low d46 and `prison game` Medium d50; thin pure-management shelf below the top 4 | High: schedule and queue puzzles like bureaucracy-office and train-baron | Low | Medium (inmate groups drawn as MultiMesh) | Tone and policy (reference is Everyone 10+); active incumbent |
| 4 | Theme park | 10M+, 389K | 4.06; no game on the shelf is about park operations | Medium; `visitor_flow` kit shared with dino | Low to medium | **High** (rides + crowds) | Perf on Low; RollerCoaster Tycoon Touch is 10M+ |
| 5 | Police | 10M+, 210K; ASO Atlas 13.1M | Reference stale since Nov 2025; crashes and "no new map" in reviews | Medium: dispatch from pest-patrol and disaster-command | Medium | Medium to high (pedestrians + vehicles) | Policing sensitivity; Police Chief (2026) is new |
| 6 | University | 1M+, 44K | **Weakest reference: 3.74**, stale since Nov 2025 | Medium | Low | Medium to high (crowd at the bell change) | Smaller ceiling |
| 7 | Hospital | 1M+, 35K | 4.36; the reference has random-item IAP | Medium | Medium (pharmacy-rush) | Medium | Medical tone (target Everyone 10+) |
| 8 | Law firm | 1M+, 52K; ASO Atlas 3.49M | Stale since Nov 2025; reviews say it is too short | Medium | Low to medium | **Low** (office tower) | Niche demand (`lawyer game` Low d41) |
| 9 | Dinosaur park | 1M+, 26K; two other 5M+ dino titles | 4.41, so a strong reference | Medium (`visitor_flow`) | Medium | **High** (animated creatures) | **Families policy** (see owner decisions) |
| 10 | Burger | 1M+, 12K | 3.96; weak idle burger shelf (2.89, 2.36) | Medium | Medium to high (doner-night) | Medium | `burger game` search is crowded by cooking games |
| 11 | Coffee | 500K+, 6K | 3.99; roasting niche is empty | Medium | **High** (chaiwala-rush) | Low | Smallest reference |

**Recommendation: start Hotel, Supermarket and Prison.**
- They are the three largest proven markets (each 10M+ with 500K+ ratings).
- Each has a clear, demonstrable decision layer: turnover calendar, shelf and expiry, and the 24-hour schedule.
- Cannibalization is low to medium and art cost is medium.
- **Law firm** is the best low-cost filler for a slave with little quota left: cheapest art and a stale reference.
- **University** is the cheapest bet on a weak reference.
- Run Theme park and Dino on one shared `visitor_flow` kit, in sequence, not in parallel.
- Burger and Coffee come last because their overlap with doner-night and chaiwala-rush is the highest in the wave.
- Optional, following the 10-20 model: before coding, run a 15-second Blender mock-up ad creative test for the top 3.
  This costs money, so the owner must approve it.

## Shared systems to reuse (all exist under `games/tools/` or in fleet games)

- **UI:** the `coddy_ui` kit (`tools/ui`) for onboarding pager, coach mark, compact shop, modal/sheet, loading screen and
  the Privacy block. Also TouchScroll and the `sync.sh --check` gate.
- **Ads:**
  - `tools/ads`: `CoddyAds.gd` + `CoddyInterstitial.gd` (house only) and the banner kit (`reserve_slot`, `side_gaps`).
  - Add each new game's tags to `house_related.py`, register it in `ads_app`, then run `sync.sh`.
- **Revenue:** shop template `tools/shop` (coddy_iap), following `IAP_STANDARD.md`, with the `ad_free` + `legacy_ad_free`
  logic. Also `RcAdTracking.gd` (RevenueCat ad tracking, `games/CLAUDE.md` §5b) and `docs/MONETIZATION_PARITY.md`
  against Corrupt.
- **Performance:** `tools/perf` `coddy_perf` (`perf_device.gd` device class, `perf_profiles.gd`, PerfGuard,
  `guard_msaa()`), perf_gate, and the physics gate (Dummy engine at 30 Hz when the game has no physics).
- **People animation:** `tools/anim` `coddy_anim` (walking, lane-keeping vehicles). Most plans need MultiMesh crowds.
- **Kit candidates (not yet in `tools/`):** the plans propose a shared `visitor_flow` kit for theme park and dino. A
  crowd-routing core could also serve the university quad, prison yard, hospital corridors and hotel paths.
- **Saves:** shared `save_repository.gd` + `tools/save_fuzz`.
- **Audio:** `tools/audio` + the `Audio` autoload. All voice work is on hold until the ElevenLabs grant arrives.
- **Onboarding QA:** the newbie bot (`newbie_audit.gd`, train-baron model, `NEW_GAME_PLAYBOOK.md` P3) and `tools/playtest` play10.
- **In-app review:** `ReviewGate.gd`/`ReviewNative.gd` (Corrupt): first session, at a success moment around 3–5 minutes.
- **Art pipeline:** `tools/blender` (buildings), `tools/meshy` (characters and animals; keep the ledger),
  `tools/fonts`, `tools/i18n`.
- **Store:** `tools/store` (full-bleed screenshots, no phone frame), the trailer pipeline in `tools/store-video/games`,
  `tools/reviews/fetch_reviews.mjs` (use it in P0 to get a larger review sample), and `tools/vitals`.
- **Build:** the Jenkins `android-linux` AAB build and `jenkins/release_symbols.sh`.

## Owner decisions needed

1. **Which 3 to start**, and slave assignment (recommended: hotel, supermarket, prison).
2. **`ad_free` product lists for `games/CLAUDE.md` §8.** Every plan proposes the same shape. Cheap packs, speed,
   automation, cosmetics, starter and cash packs do **not** remove ads.

   | Game | Proposed ad_free products |
   |---|---|
   | hotel | `noads`, `premium` (Grand Host Edition), `host_pass`, `bundle` |
   | supermarket | `noads`, `premium` (Store Manager Edition), `aisle_pass`, `bundle` |
   | prison | `noads`, `premium` (Director's Edition), `warden_pass`, `bundle` |
   | theme park | `noads`, `premium` (Grand Marshal Edition), `gate_pass`, `bundle` |
   | police | `noads`, `premium` (Chief's Edition), `patrol_pass`, `bundle` |
   | university | `noads`, `premium` (Chancellor Edition), `dean_pass`, `bundle` |
   | hospital | `noads`, `premium` (Chief of Medicine Edition), `chief_pass`, `bundle` |
   | law firm | `noads`, `premium` (Managing Partner Edition), `partner_pass`, `bundle` |
   | dino | `noads`, `premium` (Chief Warden Edition), `ranger_pass`, `bundle` |
   | burger | `noads`, `premium` (Pitmaster Edition), `convoy_pass`, `bundle` |
   | coffee | `noads`, `premium` (Head Roaster Edition), `roast_pass`, `bundle` |

3. **Families risk for Dino** (`dinosaur-park.md` R4). Dinosaurs appeal to children. If Play treats the app as
   child-directed, Families rules apply: no house interstitial or banner, AdMob rewarded only with child tags, no AD_ID
   (games/CLAUDE.md §2/§4). That would cut revenue.
   - Decide up front whether to declare 13+ and keep store art adult-friendly, or to accept the Families model.
   - Theme park also targets 13+ and should get the same check.
4. **Age rating and tone.**
   - Prison: the reference is Everyone 10+ (Mild Violence, Comic Mischief). The plan says Teen.
   - Police: the reference is Everyone (Mild Language). The plan says Teen.
   - Hospital: Everyone 10+.
   - Confirm each in IARC in P0.
5. **Names.**
   - No plan did a real Play-UI search for every option. Hotel, hospital, university, prison, police, law and dino used
     web search only. Supermarket, coffee and burger checked only the first page of Play search.
   - Re-check the exact strings on Play in P0 and again at P7 before the package names are fixed.
   - "Rush" fatigue: *Docket Day: Law Firm Rush* and *Semester Rush* would add to the four existing "…Rush" titles.
     The plans list alternatives.
   - *Grill Convoy: Burger Truck Manager* is 33 characters, over Play's 30-character title limit. The plan's fallback
     is *Grill Convoy: Burger Trucks* (27).
6. **Theme park's 10th language:** zh-Hans or hi.
7. **Optional ad creative test spend** (see "Suggested order and reasoning").

## Data caveats

- **Review samples are small.**
  - C3 kept 3 visible reviews per game. The embedded page data has **20 per game**
    (`raw/evidence/<pkg>.embedded.json` → `reviews`).
  - Prison, police and law used the 20. Hotel, hospital and university used only 3. Supermarket, coffee, burger, dino
    and theme park used page samples plus third-party snippets.
  - None of these samples is representative: no sorting and no pagination. Run `fetch_reviews.mjs` (100+ newest) in P0.
- **Installs are Play bands only.** Exact numbers exist only from the ASO Atlas scraper (2026-10-20): prison 38.3M,
  police 13.1M, law 3.49M. Theme park's "~27M" is an unverified aggregator figure.
- **ASO demand is mostly unverified.**
  - Prison, police and law have ASO Atlas bands (`chaos-2026-10-20`).
  - Supermarket, coffee and burger have popularity scores (`tycoon-2026-09-28`, many `estimate`).
  - Hotel, hospital, university, theme park and dino have **no numbers yet**. Pull ASO Atlas bands in P0/P7.
- **Unknowns.** C3 marks these as `unknown`: energy or stamina gating, current per-item prices, ad frequency, and some
  content descriptors.
  - Review claims about prices, ad pressure and bugs are dated anecdotes, not current behaviour.
  - The IAP ranges ($0.99 up to $99.99–$139.99 per item) are store ranges, not a catalog.
- **Competitor figures inside plans** (RollerCoaster Tycoon Touch, Coffee Break, My Supermarket Simulator 3D, Cooking
  Fever, dino titles) were fetched by the plan agents on 2026-10-31. They were not re-verified by C3 or in this pass.
- **Ratings move.** Theme park's earlier figure (389,155) and its prison citation (4.23 / 663,727) differed by one from
  C3's capture. Both now cite C3.

## Fact-check log (2026-10-31, section 1 against C3 raw)

All 11 packages, titles, install bands, ratings and update dates were checked against `raw/<pkg>.json`. Corrections:

| Plan | Correction |
|---|---|
| **law-firm.md** | The release and update dates were marked "unverified which is which"; they are now set to Aug 31, 2021 and Nov 20, 2025, plus the IAP range and Everyone rating. **Originality fix:** the plan kept a money "vault cap" (a Codigames mechanic); it now says no vault or stored-cash cap at all (§2 table, §5) |
| **police.md** | Same date fix: Sep 16, 2020 and Nov 20, 2025, plus content rating Everyone (Mild Language) and IAP $0.99–$119.99 |
| **prison.md** | IAP range was "unclear/unverified"; now $0.99–$124.99. Content rating now includes Everyone 10+. The [S1b] source path pointed to a non-existent `ds_10.json` and now points to the real `.embedded.json` |
| **theme-park.md** | Rating count 389,155 → 389,154. Prison citation 4.23 / 663,727 → 4.24 / 663,726 |
| **hotel.md**, **university.md** | Exact Play titles with the full-width dash ("Hotel Empire Tycoon－Idle Game", "University Empire Tycoon －Idle") |
| **hospital.md** | Release year → exact Play date, Mar 9, 2022 |
| supermarket, coffee, burger, dinosaur-park | All section 1 facts match; no change |

No plan uses a Codigames title or wording as our name or text. Codigames titles appear only in §1, §2 and the "avoid"
lists in §11.

## Sources

- **C3 raw Play capture (primary):** `/Volumes/BuildSSD/dev/i18n-work/tasks/next-wave-2026-10-31/raw/`
  - Index: `README.md`. Validation: `validation.json` (PASS).
  - Per game: `<pkg>.json` + `<pkg>.md` fact sheets, and `evidence/` (HTML, embedded JSON, visible text, screenshots).
  - Play pages: `https://play.google.com/store/apps/details?id=<pkg>&hl=en&gl=US`.
- **ASO Atlas:** `/Volumes/BuildSSD/dev/CoddyApps/marketing-data/asoatlas/chaos-2026-10-20/` (`gp_US.json`,
  `table.txt`) and `marketing-data/asoatlas/tycoon-2026-09-28/all_terms.csv`.
- **Per-plan sources** are cited inline and in each plan's source list:
  - Competitor Play pages.
  - Pocket Tactics and PocketGamer guides.
  - Current Affairs (2025-08-11) on the ethics of prison tycoon games.
  - The devuego press kit.
  - Name-collision web searches.
- **Fleet rules and format:** `/Volumes/BuildSSD/dev/CoddyApps/games/CLAUDE.md`, `games/NEW_GAMES_2026-10-20.md`,
  `games/tools/IAP_STANDARD.md`, `games/NEW_GAME_PLAYBOOK.md`.

## Thin sections (for the deferred deepening pass, P0 of each game)

| Plan | Lines | Thin sections |
|---|---|---|
| hospital | 208 | §7 crises, §13 risks, §5 systems, §8 art; §11 no demand numbers; §2 used 3 of 20 reviews |
| university | 203 | §13, §5; §11 no numbers; §2 used 3 of 20 reviews |
| hotel | 203 | §13; §11 no numbers; §2 used 3 of 20 reviews |
| police | 215 | §7, §13, §11, §9 onboarding |
| law-firm | 213 | §7, §11, §12 overlap, §9, §8 |
| prison | 221 | §7, §9, §11 |
| theme-park | 214 | §7; §11 no numbers |
| dinosaur-park | 215 | §7; §11 no numbers; Families decision (owner) |
| coffee / burger | 348 / 354 | deepened §13, §14 (done) |
| supermarket | 274 | §14 partly deepened |

§14 MVP scope is thin in the eight plans not deepened.

