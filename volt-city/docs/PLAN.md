# Volt City (store name: Grid Keeper: Energy Tycoon, P6) — plan / TODO (F0 done by Patron 2026-10-27)

> Status: **F0 done** (this branch, docs only). P0-P8 TODO. Docs: [DESIGN](DESIGN.md) · [UI](UI.md) · [ART](ART.md) · [IAP](IAP.md) · [STORE](STORE.md).
> Package `com.coddykit.voltcity` · folder `games/volt-city` · portrait · Godot 4.7.2 Compatibility, Android `gl_compatibility` (never
> Vulkan) · Google Play only (no iOS work) · 10th language `hi`. Every phase ends with: commit + push, test summary with numbers, frames
> where there is something to see, the Low before/after perf line where visuals changed, and **Patron approval before the next phase**.
> Read first, every phase: `games/CLAUDE.md`, `games/LESSONS_LEARNED.md`, `games/NEW_GAME_PLAYBOOK.md` (section 25 day-one list),
> Grid Boss (`games/pit-wall`) as the newest worked example, Driftwood (`games/driftwood`) as the newest portrait skeleton.

## Decision record (Patron as game owner, 2026-10-27)

All ten 10-20 chaos games plus Pit Lane Boss and Driftwood are built; the factory needs a new title. Chosen: the research's own
reserve pick, **power grid blackout control**, ahead of snow plow (seasonal), laundromat, vet clinic (kids-leaning, Families risk) and taxi dispatch:
1. Demand: 6 Medium terms on Play US (`grid manager` d24, `power grid game` d38, `energy tycoon` d42, `power tycoon` d47, `solar tycoon`, `storm game`); top-10 median 25k installs, only one ≥ 1M.
2. Gap: every rival is an idle clicker or a light-switch puzzle; nobody sells the real-time "keep the lights on" city diorama on mobile.
3. Revenue: global, Western-ARPU theme (US/DE/JP love infrastructure sims), clean ad category → paid UA viable; the lights-out/lights-on ripple is a ready 15 s creative; natural rewarded stops (offline, shift report, last-chance import).
4. Feasibility: static modular city + MultiMesh trucks/cars; the Low tier fits in ~16 draws; reuses Disaster Command (crews on roads), Sky Tower (demand waves), Canal (flow) code patterns.
5. Fleet fit: no overlap with an existing game's loop; strengthens the house-ad "tycoon / infrastructure" family.

## v1 scope

One city (8 districts), 6 plant types × 3 levels, 50 investments, 15 chaos events, 5 chapters + endless + weekly seeded challenge,
24 IAP products, 4 rewarded placements, house interstitial + banner. v1.1 (not in v1, no product sold for it before it exists):
second city (desert solar "Sunmesa" or snow "Frostford"), more scenarios. Voice-over: none (owner hold 10-24).

## P0 — Skeleton (no GPU)
- [ ] Godot project `games/volt-city/` (copy the **structure** of `games/driftwood`, not its content): `sim/ src/ scenes/ data/ blender/ assets/ i18n/ tests/ tools/ jenkins/ store/ docs/`; `store/.gdignore`; `.gitignore` (Grid Boss).
- [ ] `project.godot`: Compatibility renderer, Android `gl_compatibility` in the repo (not converted by a script), portrait, stretch `canvas_items` + `expand`, `boot_splash/show_image=false` (or own image) — **no Godot logo**; no physics → `physics_ticks_per_second=30`, `max_physics_steps_per_frame=1`, 2D/3D engine `Dummy` + `tests/physics_gate.gd`.
- [ ] Kits via scripts only: `games/tools/ui/sync.sh`, `games/tools/perf/sync.sh`, `games/tools/anim/sync.sh`, `games/tools/ads/sync.sh` (+ `--check` in check.sh). `UiKit.design = 2` set in the boot path.
- [ ] Boot scene → threaded load of Main → own `Curtain` (extends `UiLoading`, indicator so smoke's flat-frame check does not pass on it).
- [ ] `src/save_repository.gd` (shared, atomic tmp+rename, `.bak`, `.unreadable` copies, `save_unreadable` event) + `games/tools/save_fuzz` wired + `tests/relaunch.gd` skeleton.
- [ ] `IP_RULES.md` + `tests/ip_denylist.txt` + `tests/ip_denylist.gd` + proof fixture (plain, separator, accent, leetspeak, non-Latin, rule name, asset file name), seeded from DESIGN section 11; scan excludes generated folders (`android/build`, `.godot`, `build`; LL Driftwood #7).
- [ ] `tests/android_export.gd`, `tests/run.gd` shell, `tools/check.sh` (copy `pit-wall/tools/check.sh`: fails on `SCRIPT ERROR|Parse Error|Compile Error|^ERROR:|^FAIL`, restores `project.godot` / `export_presets.cfg` in a trap, guarded `[ ! -f … ] ||` calls into `games/tools`, every long run under `perl -e 'alarm N; exec …'`).
- [ ] Jenkins adapter `jenkins/game.env`, `jenkins/build.sh test|android`, Jenkinsfile (Linux agent `android-linux`, `GAME_SERVICES_MODE=production`, `release_symbols.sh`). Do **not** touch version fields after the first value (release train).
- [ ] `docs/SIM-API.md` stub (the API P1 fills).
- **Exit gate:** `tools/check.sh` green; `--quit-after 120` boot headless with 0 errors; denylist 0 + proof catches all plants; kit `--check` clean ×4; Android preset `gl_compatibility`; physics gate green.
- **Evidence:** check.sh output summary, tree listing, commit id.

## P1 — Simulation core + data + balance (no GPU) — can run in parallel with P2
- [ ] `sim/` pure RefCounted, fixed step 0.25 s game time, seeded RNG (never methods named `randf/randi/seed`), integer microseconds for replayable time, event handlers use the event's time.
- [ ] Demand profiles, weather (forecast + actual), plants (capacity, min output, ramp, fuel, availability), battery, hydro reservoir, balance + auto-shedding by priority (hospital last + 60 s backup), lines (capacity, overheat, trip, reroute with smart substations), faults + crew jobs (road-graph travel time from `coddy_anim` data), revenue, rating, shift report, offline summary (8 h cap), chapters + calendar, 15 events with caps (DESIGN section 5), `tutorial_safe` flag.
- [ ] Player commands = queued orders applied at the next sim step; the UI is told when (LL Pit Lane Boss #133); speeds 1/2/3/4 compared with `int(x) == v`.
- [ ] Data JSON: `districts, plants, lines, investments, events, chapters, weather, balance, iap` (ids only for IAP in P1), `names.json` (crew first names).
- [ ] Save codec accepts every state the sim produces (negatives, mid-ramp, mid-repair, capacity+1, purchase moment) — CLAUDE §14; save fuzz 0 unreadable; reopen-lock test.
- [ ] Rewards API capped (CLAUDE §4) + `grant_purchase_funds` idempotent + `set_commerce_perks` hook (premium/offline_plus/speed4/dispatcher/forecast_plus) from day one.
- [ ] `tools/balance.gd`: 1,000 seeded runs × policies (greedy, saver, renewables-first, gas-only, battery-only, never-shed, naive-coach); gates = DESIGN section 7 (day 7 ≤ 70 %, all by day 25-30, ≥ 2 days without a stop, idle ≤ ~1 day income, premium ≤ 78 % day 7 and not all before day 19, no dominant strategy, never-shed ≥ 2★ in Ch.1-2, naive ≥ 2★ in ≥ 80 % of Ch.1 shifts). Quick mode in check.sh, full by hand.
- [ ] Determinism: same seed + same command list = identical shift hash in process **and across two processes**.
- **Exit gate:** check.sh green; unit tests 0 failed; determinism ×2; reopen-lock; fuzz 0; balance gates pass; denylist 0.
- **Evidence:** test counts, `docs/P1-VALIDATION.md` (balance tables, final economy numbers written back into DESIGN section 7), ASCII debug frame of one shift, `docs/SIM-API.md`.

## P2 — Art (Blender → GLB), camera, splash — GPU, under shot-lock; can run in parallel with P1 after P0
- [ ] Deterministic Blender kit (copy `pit-wall/blender/pwkit.py` pattern): terrain, 8 district merged meshes from the modular kit (ripple distance baked), plants ×6 types ×3 levels, wind blades MultiMesh, pylons + lines merged with corridor ids, trucks, cars, fault markers, trees, sky dome — ART section 2.
- [ ] Shared static shader: world-position surfaces + window ripple (one branch, all tiers) — or the two-material fallback if per-instance values do not work on Compatibility (measure, record in ART-IDS).
- [ ] Light curves (dusk/night/day/storm), light pools, vignette, grade; camera fits district boxes; clamped pan/zoom.
- [ ] `glb_import_settings.py` run on every model folder (guard in check.sh); `validate_art.py` with denylist on node/material/file names.
- [ ] Own splash frame + curtain art; 3 onboarding hero frames are P3 (need the real loop).
- **Exit gate:** two builds = identical manifest hashes; frames 1080×2340 Low + Medium (dusk, night, storm) with before/after sheets; **perf_gate Low ≤ 16-17 draws, ≤ 84k primitives, no per-frame CPU on Low**; rendered-pixel colour check; Kaiten bar (difference visible at first glance).
- **Evidence:** frames, compare sheets, perf_gate lines, polygon/draw table (`docs/P2-ART.md`, `docs/ART-IDS.md`).

## P3 — Playable: scenes, loop, HUD/tabs, onboarding, graphics policy
> **Status 2026-10-28:** compiles in Unity 6000.3.27f1, EditMode 125/125, PlayMode 1/1, check.sh PASS, dev AAB/APK builds (UNITY.md §2a). Was 2026-10-27: code done, uncompiled in the editor (engine-free layer tested under .NET 8) — see
> [`P3-UI.md`](P3-UI.md) for what exists, the test numbers and the first steps once the editor is installed. The Godot-era
> items below map to Unity as listed there (UI Toolkit instead of the Godot kit, kit TapFilter instead of `UiKit.is_tap`).
- [ ] Main scene: diorama + HUD (2 rows, needle, goal line) + bottom sheet + 5 tabs (UI.md), Settings, shift report, offline summary, district/fault/plant cards, forecast, goals — all kit, design 2.
- [ ] Input: every custom handler via `UiKit.is_tap` (world picks on release, < 12 dp / < 400 ms), TouchScroll on every list, `UiReconcile`, no rebuild while a finger is down, tab-open ≤ 100 ms.
- [ ] Trucks/cars on `coddy_anim` RoadGraph + VehicleMover; fractional-time progress, no backward correction (LL Disaster W66); `overlap_probe` worst_depth 0.
- [ ] Onboarding v2: pager with 3 real earned hero frames (bot-captured, manifest, ≤ 1 MB, missing file fails) + 9 coach steps (DESIGN section 9) + context hints; state is a setting not "save exists"; replay without leaks.
- [ ] Graphics policy (`perf_device.gd`: flagship High / strong Medium / weak Low) + first-start popup + PerfGuard; device tier tests (strong fixture Adreno 660 → Medium; flagship Adreno 830 11 GB → High).
- [ ] UiSafeArea everywhere (fake 48 dp nav bar + cutout layout test); banner slot reserved (`reserve_slot`) even before P4 wiring.
- **Exit gate:** full loop playable by a stranger; newbie audit < 10 min and ≥ 2★ shift 1; onboarding tests; touch-scroll gate; tab probe; layout test 16:9/19.5:9/20:9/4:3 portrait; perf_gate Low; kit UI self-score ≥ 8.5 per screen.
- **Evidence:** gameplay frames en + de, 10-minute play record, per-screen UI score table (`docs/P3-UI.md`, `docs/P3-ONBOARDING.md`).

## P4 — Money + services + audio
> **Status 2026-10-27:** code + tests in ([P4-MONEY.md](P4-MONEY.md)); open: Patron accounts (section 5 there), the §8 row +
> `house_related.py` entry (fleet files), audio = existing fleet set (no paid batch; ElevenLabs on hold). Editor compile done 2026-10-28 (GMA 11.6.0, RC 9.16.0; Play Review + Firebase packages not installed yet).
- [ ] `data/iap.json` = IAP.md (24 products, 4 tiers, 10-language names/descriptions in en first), shop on `games/tools/shop` + `UiShop` compact, Restore in Shop + Settings, offers engine, sold-look visibility test, pay-to-win check.
- [ ] House ads (interstitial at shift report, banner per UI.md section 5), AdMob rewarded only (4 placements, caps + cooldowns persisted, ad_free without video), UMP, RC + RC ad tracking, Firebase events (`coddykit-game-2`), ReviewGate (wow moment), `house_related.py` entry + regenerate + sync.
- [ ] CLAUDE §8 row added: `volt-city | noads, premium (Chief Engineer Edition), grid_pass (Utility Pass), bundle (Complete Grid Keeper Collection)`.
- [ ] Audio: `games/tools/audio` manifest (~30 SFX + calm/intense music + city-night ambience); run `gen.py games/volt-city --dry-run`, then **ask the Patron before the paid batch** (the 10-21 approval covered the nine factory games only); no voice.
- [ ] Hand the Patron the account list (Play app via `gp-create-app --dry-run` first, AdMob app + rewarded unit, RC project, Firebase stream) — slaves never open accounts.
- **Exit gate:** `docs/MONETIZATION_PARITY.md` row-by-row equal to Corrupt State; rewarded caps + ad_free tests; `services_preflight --production` green (with the Patron's ids); `check_admob_init.py`; no trial text anywhere.
- **Evidence:** parity table, catalogue list with the ad_free items, preflight output.

## P5 — Ten languages
> **Status 2026-10-28:** fonts done — Noto subsets (Latin/Cyrillic, JP, KR, Devanagari, Symbols) via `unity/Tools/subset_fonts.py`
> (check in check.sh), Hindi on the advanced text generator; 10-language HUD/guide/tab frames 0 tofu (`P7-QA.md`). Open:
> overflow × 4 aspects and the language-switch re-localise gate as tests.
- [ ] en tr de es fr pt-BR ru ja ko hi — game + IAP texts by in-session Haiku sub-agents with the glossary (no OpenRouter/nitrorouter/API); Noto subsets incl. Devanagari + CJK; `check_hardcoded_text.py`, `check_iap_langs.py` (`hi`, not `id`).
- **Exit gate:** all keys × 10; no overflow 10 languages × 4 aspects; wordbreak gate; glyph test; language switch re-localises everything open; denylist green in every language.
- **Evidence:** test counts + frames in de, ja and hi.

## P6 — Store (can start in parallel with P5 once P3 frames exist)
- [ ] **Title knockout search** (USPTO classes 9/41, EUIPO, WIPO, Play, App Store) → `store/NAME_CHECK.md` → Patron confirms the title (STORE.md section 1).
- [ ] 10-language listing from STORE.md section 3; screenshots 7 × phone/7"/10" + feature per language (real earned frames, weak profile, free looks); icon candidates ≥ 4 (not installed); Data safety CSV, IARC draft, privacy addendum.
- **Exit gate:** `store/validate.py` green, `STORE_ASSETS_PASS` + `STORE_GLYPHS_PASS`, no IP/"free"/trial/price/ranking words, icon not installed.
- **Evidence:** contact sheets, icon candidate sheet, declaration drafts.

## P7 — Full QA
> **Status 2026-10-28:** first full table in [`P7-QA.md`](P7-QA.md) (soak, perf Low/Medium, languages, device smoke).
> Open: a human 10-minute play (W66), device vitals after the first internal AAB.
- [ ] All tests, balance full, 30-min soak, save safety, layout, one-tap (`is_tap`) suite, sold-look visibility, perf_gate Low + Medium, 2 GB emulator run (`qa_lowend_report.sh`), emulator turn through the release-like build, 10-minute play (W66) with notes, final IP scan + human review of icon/art/text, UI ≥ 8.5 every screen.
- **Exit gate/Evidence:** one QA table with every gate and its number (`docs/P7-QA.md`).

## P8 — Release to Play INTERNAL
> **Status 2026-10-28 (Patron release agent): 1.0.0 vc1793218190 ON INTERNAL** (track Active, release status completed, symbols in the same edit, notes 10 languages; testers: lists `internal2` + `License testers`). Built **locally** on the Mini (Unity batchmode, UNITY.md §7a) because the Jenkins job is not wired. 23 Play one-time products ACTIVE (174 regions, 10 listings each); RevenueCat credentials Valid. First AAB vc1793210432 was refused by Play at targetSdk 35 → targetSdk 36 (6b49b4d95f). Production still needs the Patron's visual approval + the human 10-minute play (P7-QA) + the account steps below.
- [ ] Patron has opened Play app / AdMob / RC / Firebase; Jenkins `Games/volt-city-mobile` build-only on `android-linux`, production mode; `release.json` (commit + SHA256s); symbols zip in the **same** Play edit; smoke 200 s + 300 monkey PASS on the release AAB (one emulator, `smoke_android.sh` only, never a hand-made lock); permission list printed in full (first release); RC line `YÜKLEMEYE HAZIR volt-city …` to the Patron.
- Production is **not** a factory phase: Patron visual approval + UI ≥ 8.5 + fresh smoke + post-release account steps (playbook section 21: UMP message, RC Check credentials, privacy page, ASO Atlas track, AdMob ↔ Play link, `ads_app` registration).

## What runs in parallel

| Track | Starts after | Can run alongside | Notes |
|---|---|---|---|
| P0 skeleton | now | — | one slave, about one 5 h window |
| P1 sim + balance | P0 | **P2** | no GPU; heaviest thinking |
| P2 art | P0 | **P1** | GPU under shot-lock; ≤ 3 heavy jobs on the Mini |
| P3 playable | P1 SIM-API + P2 GLBs | P4 data file, P6 name knockout | the merge point |
| P4 money/services | P3 tab shell | P5 prep (glossary) | Patron opens accounts in parallel |
| P5 languages | string freeze (P3 + P4) | P6 listing translation | Haiku sub-agents |
| P6 store | P3 frames | P5 | screenshots need the final UI |
| P7 / P8 | all | — | sequential |

## First three slave orders (ready to paste; the Patron assigns S-numbers by board state)

```
python3 tools/slaves/slave.py order S1 "GF-volt-city-P0 (claim GF-volt-city-P0.claim). NEW GAME Volt City: Power Grid Manager (folder games/volt-city, package com.coddykit.voltcity, portrait). Read first: games/CLAUDE.md, games/LESSONS_LEARNED.md, games/NEW_GAME_PLAYBOOK.md, and games/volt-city/docs/{PLAN,DESIGN,UI,ART,IAP,STORE}.md (branch factory/volt-city-plan, merge it to main first if the Patron has not). Do PLAN.md section P0 exactly: Godot 4.7.2 skeleton copied in STRUCTURE from games/driftwood (not its content), gl_compatibility in project.godot, no Godot splash (own curtain extending UiLoading), physics Dummy + 30 Hz + tests/physics_gate.gd, kits by sync.sh only (ui, perf, anim, ads) with --check in tools/check.sh, UiKit.design = 2 in boot, shared src/save_repository.gd + save_fuzz wiring + tests/relaunch.gd skeleton, IP_RULES.md + tests/ip_denylist.{txt,gd} + proof fixture seeded from DESIGN.md section 11 (exclude android/build, .godot, build), tests/android_export.gd, tools/check.sh copied from pit-wall (trap restore of project.godot/export_presets.cfg, guarded games/tools calls, alarm on long runs), jenkins/{game.env,build.sh,Jenkinsfile} for android-linux with GAME_SERVICES_MODE=production and release_symbols.sh, docs/SIM-API.md stub. Own sparse worktree /Volumes/BuildSSD/dev/wt-<you>-volt (never the shared checkout), no git stash, commit only games/volt-city paths, push every ~30 min, secret scan first. Exit: check.sh green, --quit-after 120 boot with 0 errors, denylist 0 + proof, 4 kit --check clean. Report: BITTI GF-volt-city-P0 - commit <sha> - tests <numbers> - open <list>."
```

```
python3 tools/slaves/slave.py order S2 "GF-volt-city-P1 (claim GF-volt-city-P1.claim; start when GF-volt-city-P0 is BITTI). Volt City sim core + data + balance, NO GPU. Read games/volt-city/docs/DESIGN.md sections 3,5,6,7,9 and PLAN.md P1 plus the Grid Boss P1 lessons in games/LESSONS_LEARNED.md (seeded RNG naming trap, event-time handlers, integer microseconds, queued commands applied at the next fixed step = Pit Lane Boss #133 fairness, JSON numbers are floats so compare int(x) == v). Build sim/ as pure RefCounted with fixed 0.25 s step: demand profiles, weather + forecast, 6 plant types x 3 levels with ramp/min/fuel/availability, battery + hydro storage, balance needle + auto-shedding by priority (Hospital Hill last + 60 s backup), lines with capacity/overheat/trip/reroute, faults + crew jobs with road travel time, revenue, rating, shift report, offline summary (8 h cap), 5 chapters + calendar, the 15 events with caps, tutorial_safe flag. Data JSON under data/. Save codec accepts every sim state; save fuzz 0 unreadable; reopen-lock test. Capped rewards API + idempotent grant_purchase_funds + set_commerce_perks hook. tools/balance.gd: 1,000 seeded runs x policies (greedy, saver, renewables-first, gas-only, battery-only, never-shed, naive-coach) as REAL gates per DESIGN section 7. Determinism: same seed + commands = same shift hash in process and across two processes. Exit: check.sh green, 0 failed, gates pass. Evidence: docs/P1-VALIDATION.md (write final economy numbers back into DESIGN.md section 7), ASCII debug frame, docs/SIM-API.md. Own worktree, commit only games/volt-city, push every ~30 min. Report: BITTI GF-volt-city-P1 - commit - tests - open."
```

```
python3 tools/slaves/slave.py order S3 "GF-volt-city-P2 (claim GF-volt-city-P2.claim; start when GF-volt-city-P0 is BITTI, runs in parallel with P1). Volt City art, GPU work ONLY under /Volumes/BuildSSD/novareka-dev/shot-lock.sh with alarm 900, Blender = /Volumes/BuildSSD/blender/blender -b -P (never /Applications/Blender.app), at most 3 heavy GPU jobs on the Mini. Read games/volt-city/docs/ART.md (whole), UI.md section 1, games/docs/LOOKDEV_STANDARD.md, CLAUDE.md section 10 (Low = 0 extra draws rule). Copy the games/pit-wall/blender kit pattern (pwkit.py, build_art.py, validate_art.py with denylist on node/material/file names): terrain_city, 8 merged district meshes from a modular building kit with the light-ripple distance baked, plants (steam, peaker, hydro, wind, solar, battery) x 3 levels, wind blades MultiMesh, pylons + lines merged with corridor ids, crew trucks + cars + fault markers + trees as MultiMesh (use_colors=true, white per instance), sky dome; shared static shader with world-position surfaces + window ripple (one branch on every tier; if per-instance values fail on Compatibility use the two-material fallback and record it). Run glb_import_settings.py on every model folder and guard it in check.sh. Light curves dusk/night/day/storm, camera fits district boxes, own splash frame + curtain art. Meshy: 0 credits. Exit: two Blender builds = identical manifest hashes; frames 1080x2340 Low + Medium (dusk, night, storm) with before/after sheets; perf_gate Low <= 17 draws and <= 84k primitives, no per-frame CPU on Low; rendered-pixel colour check; denylist 0. Evidence: docs/P2-ART.md, docs/ART-IDS.md, perf_gate lines. Own worktree, commit only games/volt-city, push every ~30 min. Report: BITTI GF-volt-city-P2 - commit - perf line - open."
```
