# HOSPITAL — "Charge Nurse: Hospital Manager" (working title) — game plan #184 (S1, 2026-10-31)

> DOCS ONLY. Fleet rules: `games/CLAUDE.md`. Format model: `games/NEW_GAMES_2026-10-20.md`. Monetization: `games/tools/IAP_STANDARD.md`.
> Originality rule: nothing below copies Codigames names, art, UI, text or mechanics. Codigames facts are market evidence only.
> Play page fetched 2026-10-31 (local copy in session scratchpad; same data as the URLs cited).

## 1. Reference summary (Codigames hospital game)

- **Title (actual):** "Hospital Empire Tycoon - Idle" by Codigames SL (package `com.codigames.idle.hospital.empire.tycoon`; the brief's "Idle Hospital Tycoon" is not the real title). Released on Play **Mar 9, 2022** (C3 raw `/Volumes/BuildSSD/dev/i18n-work/tasks/next-wave-2026-10-31/raw/com.codigames.idle.hospital.empire.tycoon.json` → `release_date`; press kit: https://www.devuego.es/en/presskit/hospital-empire-tycoon-idle).
- **Play installs: 1M+**; rating **4.36 from 34,927 ratings** (page: "4.4 / 34.9K reviews"); Everyone; "Contains ads"; in-app purchases **$0.99 - $139.99 per item**, flagged "In-Game Purchases (Includes Random Items)"; updated **May 4, 2026**. Source: https://play.google.com/store/apps/details?id=com.codigames.idle.hospital.empire.tycoon&hl=en
- **Core loop (Play description, same URL):**
  - Start in a modest clinic; reinvest income into departments (radiology, trauma, infirmary, ER, waiting room, operating rooms).
  - A "booster" upgrade system: the player chooses where boosts go (more patient types, shorter waits, more profitable surgery).
  - Emergencies: ambulances bring more patients for bonus rewards.
  - Staff by department: doctors, specialists, nurses, assistants, ambulance drivers, stretcher handlers, receptionists, cleaners, janitors, security guards.
- **Takeaway:** hospital is a validated but smaller space than hotel (1M+ vs 10M+ for the same publisher's hotel title). Our upside is not volume of an idle clone but a clearer, more *meaningful* take on the real hospital problem: flow and capacity.

## 2. What players love / hate

Reviews embedded in the Play page (https://play.google.com/store/apps/details?id=com.codigames.idle.hospital.empire.tycoon&hl=en), 2026-10-31; only 3 extractable → counts **unverified**.

| Theme | Evidence |
|---|---|
| Love: good graphics, zooming into characters, "set idea is wonderful" | Play review: "The graphics are good. I enjoyed zooming to get a closer look at the characters" |
| Hate: waiting for upgrades, builders/timers, paywall in the "meat" of the game | Play review: "MASSIVE pay wall once you get into the meat… always waiting to upgrade" (bought a 2nd builder, still waiting hours) |
| Hate: staff constantly demanding raises, no breathing room | Play review: "over abundance of staff demanding raises… no breathing room between the requests" |
| Hate: freeze/skip after ads | Play review: "freeze or skip… especially after an ad" |
| Hate: real-time waits; wants in-game time | Same review: "Needs in-game time instead of using real time" |
| Hate: broken logistics (distributor gives 2/4 materials and never returns) | Same review (unplayable state = a bug class we avoid by deterministic supply) |
| **Love (relevant to us):** the publisher's *university* title praised for "in-game engagement with schedules and competitions" | Same review — evidence that players reward games that give something to *do* during waiting |

Design consequences: in-game time (not wall-clock) for everything except offline; no builder timers; interruptions (like raise requests) are rate-limited; supply logistics are deterministic and visible.

## 3. OUR game

**Working title:** *Charge Nurse: Hospital Manager*.
**Name options:** (1) Charge Nurse: Hospital Manager, (2) Bedline: Hospital Manager, (3) Shift Handover: Hospital Manager.
**Collision check (WebSearch 2026-10-31):** no Google Play listing found titled "Charge Nurse", "Bed Flow", "Ward Rush" or "Night Ward". Near/conflicting concept: **"The Ward: ER Hospital Drama" (iOS App Store, triage + patient flow over 15-hour shifts, https://apps.apple.com/us/app/-/id6756368229)** — close in mechanic, so we avoid "Ward" in the title and differentiate on the *whole-hospital capacity map*. Other Play/store hospital titles seen: Happy Clinic (Nordcurrent), Pocket Hospital, Operate Now: Hospital, Dream Hospital, Hospital Rush: Doctor ASMR, Hospital Empire Tycoon (Codigames). Final Play-search + trademark glance in P0.

**Pitch.** You are the charge nurse of a small-town hospital — and by chapter 6 the CEO of a teaching campus. It is 6:50 a.m., shift change in ten minutes, the ER is full, three admitted patients are waiting for beds that are waiting for cleaners that are waiting for porters. You never treat a patient with a mini-game; you **keep the hospital moving**: tag and prioritize arrivals, open and close beds, send stretcher porters, balance the day and night teams, decide when to divert ambulances. The satisfying moment is a corridor that goes from jammed to flowing because you found the real bottleneck (usually discharge, not the ER).

## 4. Core loop — chaos managed as a process

The hospital is a **pipeline with four stages and the player owns the capacity board**: **Arrive → Triage → Diagnose → Treat/Admit → Discharge/Clean**.

1. **Arrivals (continuous + bursts):** walk-ins, referrals, ambulances. Each patient gets a color tag by severity (green/yellow/orange/red — no gore, no blood; stylized cards and bedside icons only).
2. **Triage (player + nurse team):** the player sets **rules** (e.g. "yellow waits max 20 min, red always jumps queue") and can override per patient. Wrong tag = patient worsens, extra cost, or a bed wasted.
3. **Diagnose:** X-ray / lab / scan rooms with queues and breakdown risk; the player chooses which department gets the next technician.
4. **Treat / admit:** treatment rooms, operating theater schedule, ward beds, ICU. The **capacity board** shows beds by ward, who is boarding in the ER, and the next discharges.
5. **Discharge + clean:** discharge paperwork → porter → cleaner → bed ready. This stage is the usual hidden bottleneck; the player discovers it through a **bottleneck lens** (tap the red corridor; the game explains the cause in one sentence).

**Decisions that matter (not tapping):**
- *Capacity:* open a closed ward (costs staff + money) vs keep patients boarding in the ER.
- *Diversion:* accept an ambulance now (bonus, risk overload) or divert (reputation cost with the region).
- *Staff teams:* set **day / night / float** teams; shift changes at 07:00 and 19:00 create a **handover window** (team fatigue resets only if a quick handover is done; a rushed one drops 1-2 patient notes → delays).
- *Equipment:* one scanner, two queues: who goes first?
- *Surge plans:* pre-position a surge team when the forecast banner says "flu week".
Idle is limited to the **offline "night shift"**: the default plan runs at reduced efficiency (capped).

## 5. Systems

**Staff (hired as teams, no individual micromanagement):** nurses, doctors (general + 3 specialist tracks), technicians (radiology, lab), porters, cleaners, receptionists, pharmacy tech (as a supplier department only — no counter mini-game; avoids pharmacy-rush overlap), security. Attributes: skill 1-3, fatigue, shift. A **burnout meter** rises with sustained overload; high burnout raises sick calls (a crisis), not raise demands.
**Zones (horizontal campus, corridor graph):** ED/triage, waiting hall, radiology, lab, treatment bays, OR suite, wards (general, surgical, pediatric from chapter 5), ICU, discharge lounge, laundry/supply, pharmacy store, ambulance bay, helipad (chapter 4+).
**Flow:** patients, stretchers, wheelchairs, porters, supply carts, ambulances. **Corridor throughput** is a real constraint (one-way lanes, doors, lifts at chapter 3+).
**Economy:** per-case payment by severity + insurer mix; costs = wages, supplies, equipment maintenance; **reputation** (outcomes + waits) drives referral volume and region contracts; grants unlock research/teaching. No wall: slow income only slows expansion.
**Upgrades:** *Flow* (corridors, doors, discharge lounge, porter tablets), *Care* (equipment tier, specialist tracks), *Comfort* (waiting hall, ward rooms), *Resilience* (generator, surge stock). All upgrades use **in-game time** (not wall-clock builder timers) and may close a department for a day.
**Prestige:** "Regional expansion" — take over a smaller clinic network; carries brand perks.
**Safety / tone:** no graphic injury, no death animation; failed patients "transfer elsewhere" with a reputation hit; content rating target Everyone 10+ (medical themes, no real brands/drugs).

## 6. Progression + content map (first ~10 hours)

| Chapter | Site | Hours | New bottleneck / teach | Unlocks |
|---|---|---|---|---|
| 1 | Village Clinic (8 beds, one corridor) | 0-1 | tags, one porter, discharge → clean loop | X-ray, second nurse team |
| 2 | Town Hospital (ED + general ward) | 1-3 | ER boarding, handover window, diagnostics queues | lab, night team, discharge lounge |
| 3 | City General (OR suite, ICU, lifts) | 3-5 | OR schedule, ICU scarcity, lift congestion | specialist tracks, diversion rules |
| 4 | Trauma Center (helipad, surge wing) | 5-7 | burst arrivals, generator, mass-casualty drills | helicopter arrivals, surge plans |
| 5 | Family Wing (maternity + pediatric) | 7-9 | gentle high-demand ward, visiting hours, noise | maternity, pediatric ward |
| 6 | Teaching Campus (research, residents) | 9-10+ | grants, residents' learning curve, rankings | research tree, franchise region |

After ~10 h: **Seasons** (winter surge, summer trauma), regional network prestige, cosmetic wings. No hard gate; chapter gate = reputation reached by play.

## 7. Crises / events

Visible warning (5-30 in-game minutes) wherever possible:
- **Flu surge week** (forecast banner; arrivals x1.6).
- **Multi-vehicle crash** (burst of 8-12 arrivals, mixed severities).
- **Power outage** (generator capacity decision: ICU/OR first).
- **Scanner breakdown** (queue shift, patient transfers).
- **Staff sick calls** (burnout-linked), **shift-change gap**.
- **Norovirus ward closure** (isolation wing; beds lost).
- **Supply delay** (visible stock; reorder lead time).
- **Neighbor hospital diverts to you** (accept for bonus or refuse).
- **VIP patient + press** (priority queue pressure, reputation spike).
- **Heat wave / cold snap** (specific arrival mixes).
- **Inspection day** (clean-room ratio, wait-time audit).
- **IT outage** (paper mode: all tag actions slower).
No gore; red cases use sirens and urgent colors, not injury art.

## 8. Map / diorama + art direction

- **Camera:** 2.5D isometric cutaway; roof off the focused department; corridors always visible as a light-up flow map (heat colors for congestion).
- **Blender buildings:** clinic, ED block, wards, OR suite, lifts, helipad, ambulance bay, parking; modular rooms so chapters reuse parts; GLB per module.
- **Meshy characters/organics:** patients (various ages and body types, stylized), nurses/doctors/porters/cleaners (uniform color by role), paramedics, trees/hedges for the grounds. Run through the optimization pipeline.
- **Tone:** clean pastel clinical palette + warm accents; role-colored uniforms for readability; severity tag colors (also have shape icons — color-blind safe).
- **Low-profile plan (prims <= 300k, draws <= 150, 30 FPS):**
  - Visible animated people capped at ~28; beyond that, patients are bed/corridor icons only. Crowd/vehicle tick 15 Hz.
  - Only the focused department has furnished interior; others are exterior boxes with window textures. Prop LOD-hide on zoom-out.
  - No dynamic lights (siren glow = emissive texture swap), no shadows (blob decals), MSAA off, 3D scale 0.85, per-vertex lighting, no specular.
  - Flow heat-map drawn as a single texture overlay (0 extra draw, <= +1 allowed with justification).
  - Medium/High only: ambulance light bloom, extra background staff, glass reflections (conditional).

## 9. Onboarding steps (show-and-do, `games/CLAUDE.md` §9)

Time is paused until each step is done; wrong taps repeat the step; "Skip" always visible; replay in Settings.
1. "Hi, nurse. A patient is waiting. Tap the patient."
2. "Choose the color. This one is green — mild. Tap Green." (wrong colors explained: "A little more serious? Try again.")
3. "Send them to the room. Tap the free bay." (patient walks)
4. "Great. The patient is treated. Tap Discharge."
5. "Now the bed is dirty. Tap the cleaner to clean it." (bed turns clean)
6. "An ambulance! Tap Accept." (first red tag; the coach explains "red goes first")
7. "Open the scanner: tap the X-ray room." (first diagnostic queue)
8. "Earn money. Buy a second porter." (shop arrow)
9. "Something is blocking the corridor. Tap the red line." (bottleneck lens teaches discharge)
10. "Shift is ending. Tap Handover." (first handover)
- Later tips on first unlock: capacity board, diversion, specialist track, OR schedule, surge plan.
- Firebase: `onboarding_step`, `onboarding_complete`, `onboarding_skip`. Headless tests per step; 10-language no-overflow; smoke shows step 1 on first launch.

## 10. Monetization (IAP_STANDARD.md) — no trial, no subscription, no wall

Product ids `com.coddykit.chargenurse.<slug>` (package to be confirmed in P0). 4 PPP tiers.

| Layer | Slug | Ref USD | Gives | ad_free? |
|---|---|---|---|---|
| Base | `noads` | 2.99 | no interstitial; rewarded paid out without video | **YES** |
| Premium | `premium` "Chief of Medicine Edition" | 8.99 | noads + gold scrubs/signage/ambulance cosmetics + 1 save slot + a 1-day starter supply stock | **YES** |
| Convenience | `speed` | 2.99 | 4x speed + auto-skip shift summary | no |
| Convenience | `automation` "Charge Team" | 4.49 | auto-triage rules assistant (optional), auto-assign porters | no |
| Convenience | `offline_plus` | 3.99 | night-shift cap 4 h -> 12 h, rate 50% -> 80% | no |
| Convenience | `slots` | 1.99 | +3 slots | no |
| Cosmetic | `theme_modern`, `theme_retro`, `theme_pediatric` (3) | 1.49 each | hospital visual themes | no |
| Cosmetic | `uniforms_pack`, `ambulance_pack` | 1.49 each | uniforms; ambulance/helicopter liveries | no |
| Cosmetic | `cosmetics_all` | 4.99 | all (~40% off) | no |
| Pass | `chief_pass` "Chief Resident Pass" (permanent) | 4.99 | 2nd surge-plan slot + extra bottleneck-lens detail + weekly extra drill | **YES** |
| Content | `scenarios` | 2.99 | 5 hand-made scenarios (flu week, highway pile-up, blackout night, heat wave, summer festival) | no |
| Offer (once) | `starter` | 1.99 | day 3-7: small cash + 1 porter; does NOT remove ads | no |
| Offer (event) | `surge_kit` | 2.99 | consumable: extra surge team + stock after a failed surge | no |
| Offer (event) | `comeback` | 2.99 | 3+ days away: consumable cash | no |
| Consumable | `cash_s/m/l/xl` | 0.99 / 2.99 / 6.99 / 14.99 | 1 / 2.5 / 5 / 10 days of income, scaled | no |
| Bundle | `bundle` "Complete Hospital Collection" | 17.99 | premium + speed + automation + offline_plus + cosmetics_all + chief_pass | **YES** |

**ad_free list (to add to `games/CLAUDE.md` §8 after owner approval):** `noads`, `premium`, `chief_pass`, `bundle`. Cheap items never remove ads.
**Health-ads policy note:** medical themes can limit ad auctions (see pharmacy-rush) — UA suitability medium-low; plan organic + ASO. Keep content non-graphic, no medication brands, no medical claims in store text.

**Rewarded (AdMob only, opt-in, caps per `REWARDED_ADS_STRATEGY.md`; single <= 1/5 of cash_s, daily <= 1/2, UTC-day cap + cooldown):**
1. **Night-shift x2** on return (offline).
2. **Float nurse** — +1 team member for 10 in-game minutes.
3. **Retry one failed surge** once (restore patients' pre-surge state).
4. **Daily bonus** (small supplies).
5. **Crisis last resort — "Call neighbor hospital"**: 5 patients transfer out free (one per crisis).
Never rewarded: permanent speed, cosmetics, premium items, ad removal. `ad_free` players take them without video. Firebase `rewarded_*` events. House ads only for interstitial/banner; hidden in onboarding/boot/purchase; no interstitial during an active crisis or surge; native in-app review at "first full shift cleared" (~4 min).

## 11. ASO keyword set (Play)

Demand/difficulty **unverified** (no Atlas export in raw for this slice); run ASO Atlas in P7.
- **Core EN:** hospital manager, hospital tycoon, hospital simulator, hospital management game, ER manager, nurse manager game, clinic manager, patient flow game, ambulance manager, doctor tycoon.
- **Long tail:** triage game, hospital idle manager, hospital builder, medical manager, emergency room game.
- **Title:** "Charge Nurse: Hospital Manager" (29 chars). Short description: "Run the ER, find the bottleneck, keep the beds moving."
- **10 languages:** tr "hastane yonetimi / hastane simulatoru", de "Krankenhaus Manager / Klinik Simulator", es "juego de hospital / gestion de hospital", fr "jeu de gestion d'hopital", pt-BR "jogo de hospital / gerenciar hospital", ru "симулятор больницы", ja "病院経営ゲーム", ko "병원 경영 게임", hi "अस्पताल मैनेजर गेम". Verify with Haiku subagents.
- Keep "Empire Tycoon - Idle" and Codigames-style naming out of title and listing.

## 12. Cannibalization with OUR games

| Our game | Overlap | How this differs |
|---|---|---|
| pharmacy-rush | medical keywords, flu surge, queue | Pharmacy is a retail counter; hospital is a multi-department capacity map; pharmacy appears only as a supplier department; flu-week is one crisis among 12 |
| disaster-command (evacuation center) | surge/mass casualty events | City-scale evacuation vs one building's beds; ours is steady-state flow, not a single disaster scenario |
| lifeguard-hq | rescue/dispatch | Outdoor dispatch vs indoor corridor pipeline; no rescue minigames |
| sky-tower-ops | lifts/vertical flow | Lifts appear in chapter 3+ as a constraint only; horizontal campus is the core |
| bureaucracy-office | pipeline of stations | Patients are physical, severity-driven; discharge "paperwork" is just a stage |
| airport-baron | arrival waves | Air traffic vs patients |
Net: closest is pharmacy-rush by keyword and Disaster Command by event type; keep the store messaging on "beds, flow, shifts" rather than "rush".

## 13. Risks

- **Closest external concept:** "The Ward: ER Hospital Drama" (iOS, triage + flow). Verify its Android status in P0; differentiate with campus-wide capacity, handover and bottleneck lens.
- **Ads policy:** medical/health content constrains UA and some ad categories; plan organic + ASO.
- **Tone risk:** no graphic injury; test with 12+ content rating questionnaire (IARC) early.
- **Complexity:** pipeline + teams + diversion; mitigate with one new system per chapter and optional auto-rules.
- **Perf:** corridor crowd; strict visible-agent cap.
- **Smaller niche than hotel** (1M+ for the reference): moderate ceiling; do not over-invest in art scope vs hotel.
- **Review evidence thin** (3 samples): scrape 100+ newest in P0.

## 14. MVP scope

Chapters 1-3 (clinic, town hospital, city general), triage tags + capacity board + bottleneck lens, 6 staff teams, 3 diagnostic rooms, OR in chapter 3, handover mechanic, 8 crises, offline night shift, 10 languages, onboarding (10 steps), catalog = noads/premium/speed/starter/cash_s-xl/bundle + 3 themes, 4 rewarded placements, house ads, Low-profile perf gate green. Chapters 4-6, scenarios, chief_pass and seasons = v1.1.

## 15. P0-P8 phase TODO

- [ ] **P0 Strategy:** Play + trademark check; verify "The Ward" Android status; scrape 100+ newest reviews of reference; ASO Atlas numbers; owner sign-off on ad_free list.
- [ ] **P1 Core sim:** patient state machine, triage rules, bed/ward capacity model, corridor graph + throughput, shift/handover, burnout, economy, crises; headless balance (30 min / 2 h / 7 days; no wall); save/load + offline night shift; tests.
- [ ] **P2 Diorama/art:** modular Blender hospital kit; Meshy patient/staff set + optimization; own splash; flow heat-map overlay; Low before/after perf line.
- [ ] **P3 UI + onboarding:** `coddy_ui` kit; capacity board + bottleneck lens UX; 10-step coach; screens en + de, score >= 8.5.
- [ ] **P4 Monetization:** shop template, ad_free = noads/premium/chief_pass/bundle, rewarded 5 placements with caps, house ads, RC ad tracking, Firebase, parity table vs Corrupt.
- [ ] **P5 i18n:** 10 languages via in-session Haiku subagents; medical vocabulary review per language; fonts bundled.
- [ ] **P6 Perf/QA:** `perf_gate` Low, real-device PSS, physics/audio gates, save fuzz, touch-scroll gate, smoke on Mini emulator (single, sequential).
- [ ] **P7 Store assets:** 10-language text without medical claims, icon candidates, full-bleed screenshots, 30 s trailer, Data safety / IARC / privacy, ASO Atlas.
- [ ] **P8 Internal -> production:** Play internal, UMP/GDPR, RC credentials, AdMob link, `ads_app`/`ads_slot`, symbols zip; production on smoke PASS with Patron/owner approval.
