# UNIVERSITY — "Semester Rush: Campus Dean" (working title) — game plan #184 (S1, 2026-10-31)

> DOCS ONLY. Fleet rules: `games/CLAUDE.md`. Format model: `games/NEW_GAMES_2026-10-20.md`. Monetization: `games/tools/IAP_STANDARD.md`.
> Originality rule: nothing below copies Codigames names, art, UI, text or mechanics. Codigames facts are market evidence only.
> Clean-room note: a review says the Codigames university has "schedules and competitions" (section 2). We have NOT inspected how those work and must not copy them; our timetable/bell-change design is derived from real campus operations (room-time-professor-student constraints), not from their game.
> Play page fetched 2026-10-31.

## 1. Reference summary (Codigames university game)

- **Title (actual):** "University Empire Tycoon －Idle" (exact Play spelling, full-width dash; C3 raw `/Volumes/BuildSSD/dev/i18n-work/tasks/next-wave-2026-10-31/raw/com.codigames.university.empire.idle.tycoon.json` → `title`) by Codigames SL (package `com.codigames.university.empire.idle.tycoon`; the brief's "Idle University Empire" is not the real title). iOS: https://apps.apple.com/us/app/-/id1550782147
- **Play installs: 1M+**; rating **3.74 from 43,737 ratings** (page: "3.7 / 43.7K reviews") — the **lowest rating of the three Codigames titles in our batch** (hotel 4.39, hospital 4.36); Everyone; "Contains ads"; IAP **$0.99 - $119.99 per item**, "Includes Random Items"; updated **Nov 20, 2025** (older than hotel/hospital's May 2026 updates). Source: https://play.google.com/store/apps/details?id=com.codigames.university.empire.idle.tycoon&hl=en
- **Core loop (Play description, same URL):**
  - Start with a small campus; build classrooms, sports grounds, leisure zones, faculty lounges, student clubs.
  - Open faculties (Maths, Law, Medical, Philosophy, Literature, Physics, Chemistry, Engineering) to raise prestige; prestige brings more tuition-paying students.
  - Staff: maintenance, builders, janitors, office workers, and teachers per subject; hire/fire by workflow.
  - Students' needs: materials, scholarships, after-school activities.
- **Takeaway:** a clear **weakness in the reference** (3.7 rating, stale update, builder bottleneck, complaints about being "not playable" after the first build). A better-designed campus game has real room to win the keyword; ceiling is moderate (1M+ tier).

## 2. What players love / hate

Embedded Play reviews (https://play.google.com/store/apps/details?id=com.codigames.university.empire.idle.tycoon&hl=en, 2026-10-31); 3 extractable, so counts **unverified**; third-party aggregator hints flagged.

| Theme | Evidence |
|---|---|
| Love: nice art style, interactive campus, "entertaining" | Play reviews: "nice artstyle and pretty graphics and they're really interactive"; "The game is quite entertaining, I liked it a lot" |
| Love (about this title vs sibling): in-game engagement via schedules + competitions makes waiting tolerable | Hospital-title review on Play (https://play.google.com/store/apps/details?id=com.codigames.idle.hospital.empire.tycoon&hl=en): "The university tycoon is great. I didn't mind the wait times… in-game engagement with schedules and competitions" |
| Hate: **one build at a time; builders cost real money**; waits are "ridiculous" | Play review: "you can only build one thing at a time. And the only way to get more builders is to pay for it" |
| Hate: students too slow to reach distant buildings even after 3 speed upgrades (late for class) | Play review: "barely make it to class… football stadium, music conservatory… even with the 3 speed upgrades" |
| Hate: "nothing to do" after starting one construction and one research | Play review: "start one construction and a research then there's nothing to do… not playable" |
| Aggregators: suggestions for two free builders, less "free money" from ads | Third-party review summaries, **not primary**: https://justuseapp.com/en/app/1550782147/university-empire-tycoon-idle/reviews and https://marlvel.ai/apps/university-empire-tycoon-idle/reviews (via search snippet) |

Design consequences: movement time is the *game* (bell change is our core), not a pain; parallel work from minute one (no single builder); always something to do (timetable, events); wait states are in-game time.

## 3. OUR game

**Working title:** *Semester Rush: Campus Dean*.
**Name options:** (1) Semester Rush: Campus Dean, (2) Bell Change: Campus Manager, (3) Dean of the Quad.
**Collision check (WebSearch 2026-10-31):** no Google Play listing found for "Semester Rush", "Campus Crunch" or "Dean's List" as a game (results were academic honor pages). Existing near titles: Codigames University Empire Tycoon, "Idle University" (APK mirror), Two-Point Campus (console/PC, not Play). Note: fleet already has several "... Rush" titles (Pharmacy Rush, Chaiwala Rush, Kaiten Sushi Rush, Parts Rush); option 2 or 3 avoids same-suffix fatigue — decide in P0. Final Play-search + trademark glance in P0.

**Pitch.** It is Monday, 08:50. Four hundred students have to be in six different lecture halls in ten minutes, Professor Okafor is double-booked, the cafeteria is closed for repairs and the football final starts at 6 p.m. You are the dean of a young campus: you **build the weekly timetable**, route the **bell-change crowd** across the quad, balance dorm beds, canteen lunch capacity and exam halls, and keep prestige (rankings) rising so that more, better students enroll. It is the "tycoon" with a real puzzle at its core: *who goes where, when, and how long does it take to walk there*.

## 4. Core loop — chaos managed as a process

One in-game **week** ≈ 5 real minutes (days compressed; pause + 1x/2x/3x). A **semester** = 6 weeks, ending with an **exam week**.

1. **Plan (Sunday, paused):** the **timetable board** (days × time slots × lecture halls). Drag courses to rooms; each course needs a professor, a room with enough seats/type (lab, lecture, studio), and a slot that does not collide with its cohort's other courses. Auto-fill suggests; the player edits. Conflicts show red with a one-line reason.
2. **Run the day (live):** at each **bell change** students pour out of buildings and walk the quad to their next class. The player controls **paths and capacity**: open a shortcut gate, schedule bike lanes, shift a course to a nearer hall, ring staggered bells (e.g. 5 minutes offset between faculties), assign marshals. Late students lose attendance, which lowers grades → prestige.
3. **Lunch + housing waves:** cafeteria and dorm capacity are shared constraints — a 12:30 lunch crunch can be eased by moving a lecture or opening a food truck.
4. **Evening events:** clubs, sports fixtures, open days; these spike foot traffic and parking and are scheduled in the same board.
5. **Exam week:** hall capacity, invigilator staffing, quiet-zone rules, resit queues.
6. **Semester close:** grades → prestige and tuition income; choose investments (new faculty, lab, dorm, scholarship fund) and the **next semester's intake** (accept more students with lower thresholds, or fewer with higher).

The mix: **planning (puzzle) + live routing (flow) + prioritizing under crises**. Idle is limited to **offline "break between terms"** (capped).

## 5. Systems

**Staff (as departments; hire/fire is not per-person micromanagement):** professors (by faculty; skill, specialty, workload cap), lab technicians, maintenance, janitors, campus security/marshals, canteen crew, admissions officers, groundskeepers. Professors have **workload (max 3 courses/day)** and **morale** (overbooking → strike risk, a crisis).
**Zones:** lecture halls (small/medium/large amphitheatre), labs (science/engineering/medical), studios (art/music), library, quad + paths, dorms (3 types), canteen, sports stadium + gym, admin building, parking, shuttle stop, research lab (chapter 4+).
**Flow:** students (cohorts by year/faculty), professors, delivery trucks, shuttles, bikes. **Walking time is simulated on a path graph**; building placement matters (the reference's students being too slow becomes a *designed* trade-off).
**Economy:** tuition (per enrolled student × prestige multiplier) + grants (research) + events/sponsors − wages − maintenance − scholarships. **Prestige** (0-1000) is the ranking; it gates chapters and enrollment quality. One soft currency (cash) + **Credit Points** for tech/faculty unlocks (earned via performance milestones, not sold).
**Upgrades:** *Learning* (smart boards, lab equipment, library), *Campus life* (clubs, gym, dorm quality), *Mobility* (paths, bike lanes, shuttle, tunnels), *Admin* (timetable auto-fill quality, conflict warnings). Construction uses **in-game time and parallel crews** (no single-builder wall).
**Prestige reset:** "Merge/Found a new campus" carries accreditation perks into a new map (replayability).
**Differs from bureaucracy-office:** admissions has a minimal paperwork bar only; the heart is time-space scheduling and crowd routing.

## 6. Progression + content map (first ~10 hours)

| Chapter | Campus | Hours | New bottleneck / teach | Unlocks |
|---|---|---|---|---|
| 1 | Community College (1 hall, 1 faculty: Business) | 0-1 | first timetable, bell change basics, late counter | library, second hall |
| 2 | Liberal Arts Quad (Literature, Law, Philosophy) | 1-3 | room-type constraints, professor workload, lunch crunch | canteen v2, dorm A |
| 3 | Science Campus (Physics, Chemistry, Maths) | 3-5 | labs, long walks, staggered bells | shuttle, paths, lab tech |
| 4 | Engineering + Medical Hill (steep map) | 5-7 | multi-site travel, exam halls, research grants | research lab, medical teaching hall |
| 5 | Sports + Arts District | 7-9 | evening events, parking, stadium crowd | stadium, music hall |
| 6 | Research University (global ranking) | 9-10+ | rankings, strikes, accreditation | franchise campus, endless "Years" mode |

After ~10 h: **Years mode** (rolling cohorts, difficulty by ranking), found-a-new-campus prestige, cosmetic campuses. No hard gate; chapter gate = prestige achieved by play.

## 7. Crises / events

Warnings in the planning view when possible:
- **Double-booked professor / room** (auto-flag, but live events can break a plan: sick professor, burst pipe).
- **Power outage during exams** (generator vs reschedule).
- **Snow day / storm** (late arrivals; online-lecture option costs prestige).
- **Dorm flu outbreak** (cohort attendance drop; quarantine wing).
- **Student protest over tuition** (concession vs crowd control; no violence, comic tone).
- **Fire-drill** (evacuation to assembly points; quad capacity).
- **Football final / concert night** (traffic + parking + canteen).
- **Rankings day / accreditation inspection** (hidden audit of conflicts, safety, library hours).
- **Professor poached by another university** (retention bonus decision).
- **Freshman orientation week** (peak crowd, lost-in-campus events).
- **Grant deadline** (lab time vs teaching slot).
- **Exam-paper leak scandal** (prestige hit; investigation mini-decision, not a minigame).
Tone: light satire, no real universities or brands; Everyone / Everyone 10+.

## 8. Map / diorama + art direction

- **Camera:** 2.5D isometric campus diorama; season tint (autumn leaves, snow) as cheap color grade; day/night via tint + baked light.
- **Blender buildings:** brick lecture halls, labs, library, dorms, canteen, stadium, music hall, admin; modular façades; GLB per campus/chapter; cutaway interior shown only for the *focused* building.
- **Meshy characters/organics:** students (varied styles, backpacks, scarves), professors (gowns/cardigans), marshals, trees (oaks, maples), bikes. Use optimization pipeline; budgets enforced.
- **Tone:** warm collegiate palette (brick, ivy, sports colors per faculty); path-flow overlay (arrows + heat) is the signature UI element.
- **Low-profile plan (prims <= 300k, draws <= 150, 30 FPS):**
  - Student crowd: animated max ~30; remaining students as instanced dots/silhouettes on paths at 15 Hz; bell-change density shown as heat ribbon, not 400 meshes.
  - Interiors only for focused building; others sealed boxes with window textures; prop LOD-hide.
  - Trees as billboards/cards at distance; no leaf particles on Low; no shadows (blob), MSAA off, 3D scale 0.85, no specular, per-vertex lighting.
  - Season tint is a color grade only (0 extra pass); no snow particles on Low (static white decal on roofs).
  - Medium/High: leaf/snow particles, extra background students, stadium crowd animation (conditional on tier).
- Every delivery includes the "Low before/after" `perf_gate` line.

## 9. Onboarding steps (show-and-do, `games/CLAUDE.md` §9)

Time paused until each step is done; wrong tap repeats; no loss possible; "Skip" + Settings replay.
1. "Welcome, Dean. Tap the lecture hall." (camera focus)
2. "Open the timetable. Tap Monday 9:00." (board opens; one valid cell highlighted)
3. "Drag Business 101 into this hall." → "A professor is needed. Tap Prof. Lee."
4. "Press Start Day." (clock begins)
5. "Bell rings! Students walk to class. Watch the path." (marker follows one student)
6. "A shortcut! Tap the gate to open a faster path." (late counter drops)
7. "Everyone is on time. Tap Collect tuition."
8. "Spend it: tap the library to build it." (parallel crew explained: "Builders work together.")
9. "Lunch: the canteen is full. Tap Move class to 13:00." (lunch crunch)
10. "End of week. Tap Open the grades." (prestige first rise)
- Later tips on first unlock: staggered bells, exam week, dorm, shuttle, events.
- Firebase: `onboarding_step`, `onboarding_complete`, `onboarding_skip`. Headless test per step; 10 languages without overflow (long German department names; ja/ko/hi fonts bundled).

## 10. Monetization (IAP_STANDARD.md) — no trial, no subscription, no wall

Product ids `com.coddykit.semesterrush.<slug>` (package to confirm in P0). 4 PPP tiers. No paid builders: parallel construction is free for all (explicit differentiation from the reference's "pay for builders").

| Layer | Slug | Ref USD | Gives | ad_free? |
|---|---|---|---|---|
| Base | `noads` | 2.99 | no interstitial; rewarded paid out without video | **YES** |
| Premium | `premium` "Chancellor Edition" | 8.99 | noads + gold gate/banners/gowns + 1 save slot + 1 extra timetable template slot | **YES** |
| Convenience | `speed` | 2.99 | 4x speed + auto-skip week summary | no |
| Convenience | `automation` "Registrar" | 4.49 | timetable auto-fill that also fixes conflicts (optional, can be turned off) | no |
| Convenience | `offline_plus` | 3.99 | break-between-terms cap 4 h -> 12 h, rate 50% -> 80% | no |
| Convenience | `slots` | 1.99 | +3 save slots | no |
| Cosmetic | `theme_ivy`, `theme_modern`, `theme_desert` (3) | 1.49 each | campus themes (façades/paths) | no |
| Cosmetic | `gowns_pack`, `mascots_pack` | 1.49 each | graduation gowns; mascots/team colors | no |
| Cosmetic | `cosmetics_all` | 4.99 | all (~40% off) | no |
| Pass | `dean_pass` "Dean's Pass" (permanent) | 4.99 | extra timetable templates + path heat-map history + weekly extra contract | **YES** |
| Content | `scenarios` | 2.99 | 5 scenarios (orientation week, exam crunch, snow week, football final, accreditation) | no |
| Offer (once) | `starter` | 1.99 | day 3-7: small cash + extra crew; does NOT remove ads | no |
| Offer (event) | `exam_kit` | 2.99 | consumable: invigilator staff + backup generator for one exam week | no |
| Offer (event) | `comeback` | 2.99 | 3+ days away: consumable cash | no |
| Consumable | `cash_s/m/l/xl` | 0.99 / 2.99 / 6.99 / 14.99 | 1 / 2.5 / 5 / 10 days of current income, scaled | no |
| Bundle | `bundle` "Complete Campus Collection" | 17.99 | premium + speed + automation + offline_plus + cosmetics_all + dean_pass | **YES** |

**ad_free list (add to `games/CLAUDE.md` §8 after owner approval):** `noads`, `premium`, `dean_pass`, `bundle`. Cheap packs never remove ads.
**Rewarded (AdMob only, opt-in, caps per `REWARDED_ADS_STRATEGY.md`; single <= 1/5 of cash_s, daily <= 1/2, UTC-day cap + cooldown, persistent):**
1. **Break-between-terms x2** on return.
2. **Extra marshals** for one bell change (10 in-game minutes).
3. **Re-plan once:** undo a timetable conflict day (no money).
4. **Daily bonus** (small supplies).
5. **Crisis last resort — "Call the city for help"** (one per crisis, daily cap).
Never rewarded: permanent speed, cosmetics, ad removal, extra permanent builders/crews. `ad_free` owners claim without video. Firebase `rewarded_*`. House ads only (interstitial + banner), hidden in boot/onboarding/purchase; interstitials only at semester/week summaries; never in a live bell change or exam; native Play In-App Review ~4 min at "first week with zero late students".

## 11. ASO keyword set (Play)

Demand/difficulty **unverified** (no Atlas export in raw for this slice); run ASO Atlas in P7. The reference's 3.7 rating and stale update are an opening in "university tycoon / college tycoon".
- **Core EN:** university tycoon, college tycoon, campus manager, university simulator, school manager game, college management game, dean simulator, campus builder, timetable game, school tycoon.
- **Long tail:** class schedule game, student manager game, university idle manager, rector game, campus tycoon.
- **Title:** "Semester Rush: Campus Dean" (26 chars). Short description: "Plan the timetable. Beat the bell. Grow your campus."
- **10 languages:** tr "universite yonetimi / kampus simulatoru", de "Universitaet Manager / Campus Simulator", es "juego de universidad / gestion de campus", fr "jeu de gestion d'universite", pt-BR "jogo de universidade / gerenciar campus", ru "симулятор университета", ja "大学経営ゲーム", ko "대학 경영 게임", hi "विश्वविद्यालय मैनेजर गेम". Verify with Haiku subagents.
- Avoid the Codigames title pattern ("University Empire Tycoon - Idle") in our store title.

## 12. Cannibalization with OUR games

| Our game | Overlap | How this differs |
|---|---|---|
| bureaucracy-office | schedules/queues/administration | Admin is a small bar; core is timetable constraints + crowd routing; no stamps/desks |
| sky-tower-ops | crowd waves at 08:55 | Vertical elevators vs horizontal campus walking; ours has a planning layer |
| lifeguard-hq | events at a site | Different dispatch type; no rescue loop |
| airport-baron | schedules | Aircraft rotations vs class timetables; tone and map very different |
| pharmacy-rush | flu | Flu is one dorm crisis; no counter queue |
| lastdigit (math tycoon, Unity) | education tone | Math puzzle vs campus tycoon; no number-crunching minigames |
| moneytycoon | tuition/finance | Light finance only; no personal-finance content |
Net: lowest overlap of the three docs by theme; closest is Bureaucracy Office by "schedule/admin" — message in store assets should say "timetable + bell change".

## 13. Risks

- **Complexity:** timetable board on a phone screen must be thumb-friendly (large cells, snap targets, auto-fill). Prototype early in P3 and test with beginners.
- **"Homework" feel:** planning can feel like work. Mitigate: auto-fill default, plan only when you want, short weeks.
- **Perf:** crowd of hundreds. Mitigate: aggregate crowd as heat ribbon + capped animated agents.
- **Originality/cleanliness:** reference mentions schedules/competitions — do not copy; keep a design diary showing our own derivation (P0).
- **Smaller ceiling:** reference is 1M+ (rating 3.7). Keep scope tight; share engine with hotel/hospital docs where possible.
- **Review evidence thin (3 samples):** scrape 100+ newest in P0.
- **Content policy:** satirical protest/leak crises — keep comic, no violence, no real institutions.

## 14. MVP scope

Chapters 1-3 (community college, liberal arts quad, science campus), timetable board + auto-fill, bell-change routing (paths, gates, staggered bells), 6 department staff groups, lunch + dorm capacity, exam week, 8 crises, offline break, 10 languages, 10-step onboarding, catalog = noads/premium/speed/starter/cash_s-xl/bundle + 3 themes, 4 rewarded placements, house ads, Low-profile perf gate green. Chapters 4-6, scenarios, dean_pass and Years mode = v1.1.

## 15. P0-P8 phase TODO

- [ ] **P0 Strategy:** Play + trademark check (title option choice); scrape 100+ newest reviews of the reference; ASO Atlas numbers; owner sign-off on ad_free list; clean-room design diary entry (own derivation of timetable + bell change).
- [ ] **P1 Core sim:** course/professor/room/slot constraint model + auto-fill, student path graph + bell-change simulation, attendance → grades → prestige, enrollment/economy, events, exams; headless balance (30 min / 2 h / 7 days; no wall); save/load + offline; tests.
- [ ] **P2 Diorama/art:** Blender campus kit (modular halls, quad, dorms, canteen, stadium); Meshy student/professor set + optimization; own splash; flow heat-ribbon overlay; Low before/after perf line.
- [ ] **P3 UI + onboarding:** `coddy_ui` kit; thumb-friendly timetable board; 10-step coach; screens en + de, score >= 8.5.
- [ ] **P4 Monetization:** shop template, ad_free = noads/premium/dean_pass/bundle, rewarded 5 placements with caps, house ads, RC ad tracking, Firebase, parity table vs Corrupt.
- [ ] **P5 i18n:** 10 languages via in-session Haiku subagents; academic vocabulary check; fonts bundled; de long-name overflow tests.
- [ ] **P6 Perf/QA:** `perf_gate` Low, real-device PSS, physics/audio gates, save fuzz (mid-semester save/load), touch-scroll gate, smoke on Mini emulator (single, sequential).
- [ ] **P7 Store assets:** 10-language text, icon candidates, full-bleed screenshots (no phone frame), 30 s trailer, Data safety / IARC / privacy, ASO Atlas.
- [ ] **P8 Internal -> production:** Play internal, UMP/GDPR, RC credentials, AdMob link, `ads_app`/`ads_slot`, symbols zip; production on smoke PASS with Patron/owner approval.
