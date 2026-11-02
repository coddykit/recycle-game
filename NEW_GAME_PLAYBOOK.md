# New game from zero - the CoddyApps game fleet playbook

Written 2026-10-25 (S3) for a capable engineer (or a fresh Claude session) who knows nothing about this fleet and is about to
build a new mobile game. It is a **map and a checklist, not a second rulebook**: every rule below has one owner document;
this file tells you the order, the exit gates and where to read the detail. When this file and a source disagree, the source wins
(and `CLAUDE.md` wins over everything else). Disagreements I found between sources are listed in [section 24](#24-where-the-sources-disagree).

Newest end-to-end worked example: **Grid Boss** (`games/pit-wall`, package `com.coddykit.gridboss`) - read its
[STRATEGY](pit-wall/STRATEGY.md), [PLAN](pit-wall/PLAN.md), [IP_RULES](pit-wall/IP_RULES.md) and `pit-wall/docs/*` next to this file.
Verified against `games/CLAUDE.md` at origin/main `1dfbf6982e`.

Notation: **CLAUDE §n** = [`CLAUDE.md`](CLAUDE.md) section n (the single source of fleet game rules, written in Turkish).
**LL A1** = row A1 of [`LESSONS_LEARNED.md`](LESSONS_LEARNED.md) (sections A performance, B data/save, C UI/touch/language,
D services/store, E process). Files outside git are given as plain paths.

## Contents

1. [The 15 non-negotiables](#1-the-15-non-negotiables)
2. [Who decides what, and how work is reported](#2-who-decides-what-and-how-work-is-reported)
3. [Source map](#3-source-map)
4. [Phase overview: F0 and P1-P8 with exit gates](#4-phase-overview-f0-and-p1-p8-with-exit-gates)
5. [F0 - idea, name, IP rules](#5-f0---idea-name-ip-rules)
6. [F0 - demand check with ASO Atlas](#6-f0---demand-check-with-aso-atlas)
7. [P1 - engine, project skeleton, simulation, tests](#7-p1---engine-project-skeleton-simulation-tests)
8. [UI kit (coddy_ui)](#8-ui-kit-coddy_ui)
9. [Orientation and layout](#9-orientation-and-layout)
10. [Graphics tiers and the Low budget](#10-graphics-tiers-and-the-low-budget)
11. [P2 - art pipeline: Blender, Meshy, look-dev](#11-p2---art-pipeline-blender-meshy-look-dev)
12. [Splash and loading screen](#12-splash-and-loading-screen)
13. [P3 - onboarding v2 and the first ten minutes](#13-p3---onboarding-v2-and-the-first-ten-minutes)
14. [P5 - ten languages](#14-p5---ten-languages)
15. [P4 - money: IAP, no trial, ads](#15-p4---money-iap-no-trial-ads)
16. [P4 - services: AdMob, Firebase, RevenueCat, UMP](#16-p4---services-admob-firebase-revenuecat-ump)
17. [Audio](#17-audio)
18. [Tests and gates (what `tools/check.sh` must contain)](#18-tests-and-gates)
19. [P7/P8 - build, smoke, release candidate, Play INTERNAL, production](#19-p7p8---build-smoke-release-candidate-play-internal-production)
20. [P6 - store listing and screenshots](#20-p6---store-listing-and-screenshots)
21. [After the first upload: account steps and vitals](#21-after-the-first-upload-account-steps-and-vitals)
22. [Working rules (git, GPU, disk, shell)](#22-working-rules-git-gpu-disk-shell)
23. [Known traps (one line each)](#23-known-traps)
24. [Where the sources disagree](#24-where-the-sources-disagree)
25. [Day-one checklist](#25-day-one-checklist)

---

## 1. The 15 non-negotiables

Owner decisions; they are not asked again. Detail and sources in the sections named.

1. **Google Play only. iOS: none for now** (owner 10-18). Do not open iOS work, App Store submissions, TestFlight or iOS AdMob units. Keep the code from breaking iOS, nothing more. (CLAUDE §1, section 19.)
2. **Godot 4.7.2, Android export `gl_compatibility`. Never Vulkan** (device-lost crashes, LL A11). (Section 7.)
3. **No free trial anywhere** (owner 09-26). No "try free" text, no trial offers, no store text promising one. Games are free to play, with a wide IAP catalogue. (Section 15.)
4. **Own splash, no Godot logo** on any game. (Section 12.)
5. **Ads:** interstitial and banner = **our own games only (house)** — no AdMob interstitial/banner fallback, empty pool = no ad (owner 2026-10-25); rewarded = the ONLY AdMob format, opt-in, capped; kids games get no house ads. (CLAUDE §2-§4, section 15.)
6. **Forced ads are removed only by Remove Ads and the big bundles** (`ad_free`), never by cheap packs. (CLAUDE §3/§8.)
7. **Fleet UI kit `games/tools/ui` (coddy_ui) on every screen**; the kit is frozen per game: sync scripts only, never edit a game's copy. No raw Godot theme, no raw scrollbar. (CLAUDE §16/§17, section 8.)
8. **Weak-device performance is a release condition**, not a feature: Low profile budgets, perf_gate, no lag from new visuals. (CLAUDE §0/§10, section 10.)
9. **Onboarding for the dumbest possible player + 10 languages** in every game. (CLAUDE §9, sections 13-14.)
10. **Translation = in-session Haiku sub-agents.** Never OpenRouter or nitrorouter; never call Claude through an API when the subscription (this session / a sub-agent) can do the job. (Section 14.)
11. **Original universe**: no real brands, teams, people, venues, no look-alike names, an automated denylist test at 0 matches. (Section 5.)
12. **Every fix is fleet-wide**: a bug found in one game is searched in all of `games/*`, fixed, gated, and added to `LESSONS_LEARNED.md`. (CLAUDE §0.)
13. **Store upload and releases are done by the Patron** (with the owner's authority): other sessions deliver code, tests, a release candidate line and evidence. First release goes to **Play INTERNAL**. (Section 19.)
14. **No release without a fresh smoke test on the real release AAB** (emulator or device frame, never a desktop frame) and a frame the Patron has looked at. (Section 19.)
15. **Never put a secret anywhere**: no API keys, tokens, `.p8` contents, service-account JSON, Jenkins auth in chat, logs, docs or git. Refer to key files by path only.

## 2. Who decides what, and how work is reported

- **Owner** = the human. Gave the rules above. Physical-only steps (a login, a real-device test) are the only things that wait for them.
- **Patron** = the coordinator Claude session. Decides game questions *as the game owner* (name, icon candidate, IARC/audience, Data safety, catalogue, order), uploads to Play, looks at the evidence frames, hands out work. The Patron does not write code. (Owner 10-21; memory `feedback_master_decides_as_game_owner_2026_10_21`.)
- **S1 / S2 / S3** = slave Claude sessions on separate accounts; **C1 / C2** = Codex sessions. They communicate only through the file board at `/Volumes/BuildSSD/dev/i18n-work/slaves/` (tool `tools/slaves/slave.py`, protocol [`../tools/slaves/README.md`](../tools/slaves/README.md)). Codex does not translate; store/account writes are done by the Patron (or by a Claude session holding the owner's quoted authority for that one task).
- If you work directly for the owner with no Patron session, read "evidence to Patron" below as "what you show the owner before the next phase".
- **Factory process** (how the last ten games were built): `/Volumes/BuildSSD/dev/i18n-work/tasks/GAME_FACTORY.md` - parts P1-P8, claim files `GF-<slug>-<P>.claim` (noclobber), a report line per finished part (`BITTI GF-<slug>-<P> - commit ... - tests ... - open ...`), a pipeline rule (a free session starts the next game's P1/P2/P4), and the owner's tasks always pre-empt the factory.
- The most complete brief ever written for one new game is `/Volumes/BuildSSD/dev/i18n-work/tasks/PITWALL_BRIEF.md` (Grid Boss). Copy its structure for yours.

## 3. Source map

| Topic | Read this |
|---|---|
| Fleet game rules (ads, ad-free, rewarded, onboarding, perf, parity, live-game features, audio, save safety, post-release, touch, UI kit) | [`CLAUDE.md`](CLAUDE.md) |
| Every trap learned the hard way (A-E) | [`LESSONS_LEARNED.md`](LESSONS_LEARNED.md); fleet conformity matrix [`docs/LESSONS_MATRIX.md`](docs/LESSONS_MATRIX.md) |
| Standards shared by the ten factory games, market method | [`NEW_GAMES_2026-10-20.md`](NEW_GAMES_2026-10-20.md) (partly superseded, see section 24) |
| Visual quality bar ("the Kaiten sushi bar") | [`docs/LOOKDEV_STANDARD.md`](docs/LOOKDEV_STANDARD.md) |
| Onboarding v2 (real hero frames) | [`docs/ONBOARDING_V2_HERO_PLAN.md`](docs/ONBOARDING_V2_HERO_PLAN.md) |
| Animation (walking people, lanes, "vehicles never overlap") | [`tools/anim/README.md`](tools/anim/README.md), [`docs/W68_ROLLOUT_TABLE.md`](docs/W68_ROLLOUT_TABLE.md) |
| Weak-device audit and perf_gate results | [`SMOOTHNESS_AUDIT.md`](SMOOTHNESS_AUDIT.md), [`tools/perf/README.md`](tools/perf/README.md) |
| Fleet alignment audit (what each game is missing) | [`FLEET_ALIGNMENT.md`](FLEET_ALIGNMENT.md) |
| UI kit: components, 10-step rollout, per-screen checklist, privacy row | [`tools/ui/README.md`](tools/ui/README.md) |
| IAP standard and catalogue skeleton | [`tools/IAP_STANDARD.md`](tools/IAP_STANDARD.md) |
| Rewarded-ads strategy and RC ad tracking | [`tools/REWARDED_ADS_STRATEGY.md`](tools/REWARDED_ADS_STRATEGY.md), [`tools/REVENUECAT_AD_TRACKING.md`](tools/REVENUECAT_AD_TRACKING.md), [`tools/ads/README.md`](tools/ads/README.md) |
| Audio generator | [`tools/audio/README.md`](tools/audio/README.md) |
| Meshy and the Blender model atlas | [`tools/meshy/README.md`](tools/meshy/README.md), [`tools/blender/README.md`](tools/blender/README.md) |
| Jenkins, symbols, smoke | [`jenkins/README.md`](jenkins/README.md) |
| Newest worked example | [`pit-wall/STRATEGY.md`](pit-wall/STRATEGY.md), [`PLAN.md`](pit-wall/PLAN.md), [`IP_RULES.md`](pit-wall/IP_RULES.md), [`docs/P1-VALIDATION.md`](pit-wall/docs/P1-VALIDATION.md), [`docs/P2-ART.md`](pit-wall/docs/P2-ART.md), [`docs/SIM-API.md`](pit-wall/docs/SIM-API.md), [`docs/MONETIZATION_PARITY.md`](pit-wall/docs/MONETIZATION_PARITY.md), [`docs/PLAY_SETUP.md`](pit-wall/docs/PLAY_SETUP.md) |
| Outside git (read them) | `/Volumes/BuildSSD/dev/i18n-work/tasks/GAME_FACTORY.md` (process, balance + screen standard), `.../PITWALL_BRIEF.md` (full brief), `.../RC_1_1_4.md` (release-candidate lines), owner notes in `/Users/mehmet/.claude/projects/-Volumes-BuildSSD-dev-CoddyApps/memory/` (start with `MEMORY.md`) |

Not used as rules here: `FUELBARON_FLEET_IMPROVEMENT_PLAYBOOK.md` (a 2026-10-09 review of a reference game outside the fleet: ideas, not standards) and the 2026-10-09/10 store handoffs (older, partly iOS-era).

## 4. Phase overview: F0 and P1-P8 with exit gates

One game at a time through the factory (parts of the same game may run in parallel). **Every phase ends with: commit + push, a test summary, frames where there is anything to see, and Patron approval before the next phase starts.** The numbers in parentheses are the Grid Boss values, to show the scale.

| Phase | What | Exit gate - must be true | Evidence to Patron |
|---|---|---|---|
| **F0** Decide (before any code) | `STRATEGY.md`, `PLAN.md`, `IP_RULES.md` (+ `tests/ip_denylist.txt`); market + demand + rival shelf; name candidates; orientation; 10th language; content policy | Demand table from ASO Atlas (section 6), rival shelf with ratings counts, a gap statement, name candidates with a collision scan, IP rules written, plan lists P1-P8 as TODO, open decisions listed for the Patron | The three docs committed; list of decisions (name, series/world name, cities/venues, visual tone, orientation, 10th language). Patron answers; write the answers into STRATEGY |
| **P1** Skeleton + sim | Godot project, `sim/` (pure logic, no scenes), data JSON, headless balance runner, saves, ip_denylist, kit sync, own splash stub, Jenkins adapter | `tools/check.sh` green. Unit tests 0 failed (894). Same seed twice = same result **in process and across two processes**. Reopen-lock test (save, close, reopen, continue = never closed). Save fuzz 0 unreadable. Balance gates pass (1,000 races). ip_denylist 0 matches **and** its proof fixture catches planted words. Kit `sync.sh --check` clean. Android preset = `gl_compatibility` | Test counts, balance report (`docs/P1-VALIDATION.md`), sim ASCII debug frame, `docs/SIM-API.md` |
| **P2** Art | Blender -> GLB models, scene dressing, camera, light pools/vignette/grade, own splash + loading curtain, ART-IDS | Deterministic Blender build (two builds = identical manifest hashes); frames at phone size for **Medium and Low**, before/after; **perf_gate Low** inside the budget (Grid Boss: 2 draws, <= 84k primitives); no extra per-frame CPU on Low; rendered-pixel IP/colour check; the difference is visible at first glance (Kaiten bar) | Frames (Medium and Low, 1080x2340 phone portrait or the game's orientation), compare sheets, perf_gate lines, polygon/draw table |
| **P3** Playable | Scenes, game loop, HUD/screens on the kit, onboarding v2 (pager + coach), graphics policy + popup, touch scrolling, tab latency | Full loop playable by a stranger; first 10 minutes audited (a newbie bot that taps only the coach highlight reaches every step); onboarding tests; touch-scroll gate; tab-open probe <= 100 ms; layout test on 16:9, 19.5:9, 20:9, 4:3; perf_gate Low; kit UI self-score >= 8.5 per screen | Gameplay frames (en + de), a 10-minute play record, the per-screen UI score table |
| **P4** Money + services | Catalogue, shop, ads (CoddyAds house-first + rewarded), RC + RC ad tracking, Firebase events, UMP, ReviewGate, audio wiring | `docs/MONETIZATION_PARITY.md` row-by-row equal to Corrupt State (any missing row = P4 not done); every sold cosmetic is drawn (test); rewarded caps and ad_free rules tested; `services_preflight --production` green; no trial text anywhere; accounts opened by the Patron (section 16) | Parity table, catalogue list with ad_free items for the CLAUDE §8 table, preflight output |
| **P5** Languages | 10 languages for game, store text and IAP | All keys in all languages; no overflow (10 languages x 4 aspect ratios); no word broken mid-letter; glyph test; "no old language left after switching" test; IAP language-code gate; denylist green in every language | Test counts + 3-4 frames in a long language (de) and a CJK language |
| **P6** Store | 10-language listing, screenshots (phone + 7" + 10" + feature), icon candidates, Data safety CSV, IARC draft, privacy addendum | `store/validate.py` green; screenshots strict-validated; no "F1/Formula/Grand Prix"-type IP words, no trial/"free"/price/ranking claims; icon candidates **not installed** | Contact sheets, candidate sheet, IARC and Data safety drafts |
| **P7** Full QA | Everything above in one run | All tests, balance, soak, save safety, layout, one-tap, cosmetic visibility, perf_gate Low + Medium, emulator turn, 10-minute play (W66), final IP scan + human IP review, ">= 8.5" UI | One QA table with every gate and its number |
| **P8** Release | Production-mode AAB, symbols, smoke, **Play INTERNAL** | Section 19 in full: preflight, `release.json` with the exact commit + SHA256s, symbols in the same Play edit, smoke 200 s + 300 monkey PASS, last frame looked at, permission list compared | One RC line (section 19) + the smoke frame |

Production is **not** part of the factory phases: it needs the Patron's visual approval, the UI bar (CLAUDE §17: score >= 8.5), a fresh smoke PASS and the post-release account steps (section 21).

## 5. F0 - idea, name, IP rules

**Pick a niche the market leaves open.** Method (details in [`NEW_GAMES_2026-10-20.md`](NEW_GAMES_2026-10-20.md)): a living 2.5D diorama where many things happen at once and the player manages the chaos as a process; Play competition low; real search demand at least "Medium" in ASO Atlas (section 6); say whether paid user acquisition is plausible (food and medicine ad auctions are not). Ten ideas were evaluated that way; Grid Boss came from a Play review.

**`STRATEGY.md` structure** (copy Grid Boss): pitch, demand table, rival shelf (ratings count + year + price model), the gap, core loop, world and look, revenue plan (parity with CLAUDE §11), ad-creative ideas, name + ASO draft, risks, Patron decisions.

**Name and collisions.** Scan the candidate in Play, the App Store, and a quick USPTO/EUIPO search. Real collisions happened three times: "Pit Wall" (5+ similar App Store names -> **Grid Boss: Racing Manager**), "Bureaucracy Inc" (-> **Stamp Office: Bureaucracy Sim**), "Pest Patrol: Turbo Storm" lookalike (-> **Critter Crew: Pest Control**). The folder name may stay the working name. The store title is at most 30 characters; each locale gets its own search words.

**Original universe (binding for any real-world theme; Grid Boss is the template, [`IP_RULES.md`](pit-wall/IP_RULES.md)):**

- Forbidden everywhere (text, data, scene/script names, translations, icon, store text and images, ASO keywords, file names): the real series and bodies, real teams, **look-alike spellings of them** (a misspelling is also forbidden), real people, real venues and corner names, real sponsors and suppliers, series-specific rule names, recognisable emblem associations (a rearing horse, a charging bull), real livery colour schemes.
- Allowed: generic terms, real **city** names for fictional venues, fictional teams/people with abstract emblems, a series name of your own.
- The "F1" keyword is never used in a store text (Play IP policy).
- **Denylist test** `tests/ip_denylist.gd` + `tests/ip_denylist.txt` runs in every `check.sh`: lowercase, accent stripping (NFKD), leetspeak (`0->o 3->e 4->a @->a $->s 5->s 7->t`), separators collapsed (`formula-one`, `formula_1`, `f 1` -> same); plain entries are word-bounded regexes, `~entry` = substring (non-Latin transliterations); scans `data/**/*.json`, `*.tres`, `*.tscn`, `*.gd`, all `i18n/*`, `store/**` texts, asset file names and GLB node/material names (the Blender validator), `docs/ART-IDS.md`. Result must be 0 matches (it prints `file:line`). It also needs a **proof fixture** showing it catches planted words (plain, separator, accent, leetspeak, non-Latin, rule name, asset file name) while innocents pass. Also test generated names against the real-people list.
- **Check IP on the rendered pixel, not only on the hex value** (LL, Grid Boss P2: a "burgundy + gold" livery rendered as red + yellow under the game's light). Sample colours from a Medium and Low render; pairwise distance and distance to the real schemes list.
- Content policy per theme is decided in F0: pharmacy = fictional products, no real drugs, dosing, diagnosis or prescriptions (a ~250-term denylist test); pest control = nothing gets hurt, a test that no kill/poison word appears in any language; kebab shop = no alcohol, no weapons; a human review of icon, livery and text in P6/P7.

## 6. F0 - demand check with ASO Atlas

```sh
python3 ~/.coddy-aso/asoatlas/atlas.py research_keywords '{"terms":["racing manager","race strategy","pit stop game"],"country":"us","platform":"android"}'
```

- Up to 50 terms per call. **New terms come back under `pending` the first time; call the same command again after 30-60 s** (use an until-loop; see `~/.coddy-aso/asoatlas/AGENT_RECIPE.md`). The client spaces calls by a file lock (limit 120/min) and falls back to the Mini if the office network breaks TLS.
- Google Play publishes no numbers: Atlas returns a **band** (Very low ... Very high) and a difficulty `d` 0-100. Target: demand at least Medium and difficulty low (Grid Boss: most terms <= 50). Record the table in STRATEGY (term, demand, difficulty, note).
- Look at the rival shelf yourself too (Play installs on the listing page, App Store rating counts) and write the gap down.
- Atlas suggestions are **ideas, not orders**: a suggestion list contains competitor names and other languages; filter them before they go into a title or keyword field (owner 10-02, `docs/ASO_STRATEGY.md` section 6.1 at the repo root).
- After the Play page is public, register the game in Atlas as Android in 12 countries (us, mx, tr, in, fr, es, kr, br, pk, ru, jp, de): `atlas.py track_app '{"platform":"android","store_id":"<package>","country":"us"}'` (section 21).
- Raw market data from earlier research lives under `marketing-data/asoatlas/` (repo root).

## 7. P1 - engine, project skeleton, simulation, tests

- **Engine:** Godot **4.7.2** (`/Volumes/BuildSSD/godot/4.7.2/Godot.app/Contents/MacOS/Godot`), GDScript 2, **Compatibility renderer, Android export `gl_compatibility`** (no Vulkan; some older games keep `mobile` in `project.godot` and rely on the build script to convert, which is a risk - put `gl_compatibility` in the repo, [`SMOOTHNESS_AUDIT.md`](SMOOTHNESS_AUDIT.md) item 12).
- **The Godot 4.7.2 GLES3 MSAA leak (red trap):** with MSAA on and an internal 3D buffer needed (`scaling_3d_scale != 1.0`, or Environment glow/adjustment/SSAO) the engine opens a new MSAA framebuffer **every frame** and never deletes the old one -> out-of-memory crash on real Android phones (invisible on desktop and in the emulator). Rule: on Android, MSAA only at scale 1.0 and no post effects. Never set MSAA in game code; use `PerfProfiles.apply_core` (calls `guard_msaa`) which switches MSAA off in the leak condition. A patched export template exists at `/Volumes/BuildSSD/godot/templates/4.7.2.coddy-msaafix/` (upstream fix 81a533bb9e) and is pending real-device proof; until the owner's device test passes, the shared guard stays. (CLAUDE §10, LL A-notes.)
- **Folders:** `sim/` (pure logic, RefCounted, no scenes), `src/`, `scenes/`, `data/` (JSON), `blender/`, `assets/`, `i18n/`, `tests/`, `tools/`, `jenkins/`, `store/`. `store/` gets an empty `.gdignore` (Godot otherwise imports the Data safety CSV as a translation).
- **Simulation:** fixed step and a **seeded, deterministic** RNG; drawing interpolates between the last two sim snapshots every frame (the "tik tik" lesson from Canal Control). Never give a seeded RNG methods named like Godot globals (`randf`, `randi`, `seed`) - the class silently calls the global one (Grid Boss). Event handlers use the event's time, not a stale tick clock; replayable state is integer microseconds, not floats.
- **Saves (CLAUDE §14, LL B1):** the codec accepts every state the sim can produce; an unreadable save is **never deleted or overwritten**: shared `src/save_repository.gd` writes atomically (tmp + rename), keeps `.bak`, copies the bad file to `.unreadable` and loads `.bak` if it is valid, sends a `save_unreadable` event (error code + recovered 0/1, no PII). Gate: `games/tools/save_fuzz/save_fuzz.gd` (tutorial phase, purchase moment, offline return, frame-timed save-load) and a "0 unreadable saves" check in `tests/run.gd`; plus `tests/relaunch.gd` (play, save, close, reopen hours later, close the window, tap the next control).
- **Headless balance runner** (`tools/balance.gd`): the standard is in `GAME_FACTORY.md`: a normal player (30 min a day plus overnight offline) has at most 70 % of the investments bought by day 7 and all of them by day 25-30, at least 2 days without a stop, idle money at session end <= about one day of income; premium (all perks) at most 78 % on day 7, everything not before day 19. No walls, no forced waiting. The rewards API is capped (CLAUDE §4), `grant_purchase_funds` is idempotent, `set_commerce_perks` hook exists from P1. Gates are real gates, not printouts. A game-specific rule for variety: no single dominant strategy (Grid Boss measured winners per circuit/weather).
- **Physics:** a game with no physics bodies sets `physics/common/physics_ticks_per_second=30`, `max_physics_steps_per_frame=1`, Dummy 2D/3D engine and has a `tests/physics_gate.gd`; with physics, only the Low tier drops to 30 Hz (CLAUDE §10, LL A1).
- **Version tests** read `application/config/version`, never a literal (LL E, pest 1.1.3).
- **Project hygiene:** `--import` and tests rewrite `project.godot`; `check.sh` copies it and `export_presets.cfg` aside and restores them in a trap. Version files belong to the release train: do not touch them in a feature commit.
- **Jenkins adapter:** `jenkins/game.env`, `jenkins/build.sh test|android`, pipeline Jenkinsfile; the Linux agent checks out only `games/<game>` + `games/jenkins` (+ `games/tools` where the game's preflight needs it), so every `check.sh` call into `games/tools` is guarded `[ ! -f ... ] || ...` (LL E).

## 8. UI kit (coddy_ui)

Owner: "UI must be very good or it does not go live." Everything is built with [`tools/ui`](tools/ui/README.md) (W64 kit). **The kit is frozen in the game**: you copy it with `bash games/tools/ui/sync.sh games/<game>` (and `sync.sh --check` in `check.sh`), you never edit `addons/coddy_ui` inside a game. A kit change is made once in `games/tools/ui`, tested there, and synced to every game in the same pass (LL E); a game whose copy differs fails `check.sh`. Same for `games/tools/perf` (`coddy_perf`) and `games/tools/anim` (`coddy_anim`).

Must-use components (the README has the full list and the 10-step rollout):

- **`UiPager`**: onboarding pager (illustration or real image, step dots, Skip, big Next; one tap per page, one tap on the last button; landscape = centred card, portrait = modal card over the dimmed live game). Keep your own counter in step with `pager.page_changed`.
- **`UiCoach`**: coach mark with spotlight ring + bubble ("Step n of N", hint, "Skip guide"). If the bubble covers an important marker, give it **`avoid`** rects (`UiCoach.avoid`, an `Array[Rect2]`); beware `Rect2.grow(-x)` going negative.
- **`UiReconcile.apply(list, fresh, scroll)`**: refresh a list without eating taps. Never rebuild a list every frame or on every money tick; update only changed labels (<= 4-5 Hz); **while a finger is down the rebuild waits** (`TouchScroll.held()`).
- **`UiKit.list_card(icon, title, opts)`**: the standard list/card row (title, sub lines, badge, selected state, body slot for buttons); use it for lists instead of hand-made rows.
- **Language picker**: `UiLanguage.row()` in Settings + `UiLanguage.open(host, opts)` - a **list** with "System language" first, native names, the current one marked, a tap applies and closes. Never an `OptionButton` or a cycle button (the popup opened under the finger and the release picked the next row, owner bug 10-24).
- `UiModal` (dialog / bottom sheet, content-sized), `UiToast` (card, <= 2 lines; rule: 2.5-5.5 s, shorter at game speed, queue compresses, tap dismisses - check that your kit version implements tap-dismiss), `UiShop` (compact rows, >= 3 whole products per screen), `UiSettings.privacy` (UMP "Privacy options" only when required + policy link), `UiLoading`, `UiSegmented`, `UiRow`.
- **TouchScroll** on every `ScrollContainer` (`UiScroll.list()/make()/strip()/adopt()`): a drag that starts on a button or card scrolls, a short touch clicks, inertia, horizontal lists work, no visible raw scrollbar (a 6 px thumb only while moving; nothing when content fits). Static gate `python3 games/tools/ui/check_touch_scroll.py games/<game>` + a device-like regression `tests/touch_scroll.gd` (en + de, vertical + horizontal).
- **Hard fails** (screen scores <= 6): raw Godot look, text < 21 px (body 25 px = 14 sp), touch target < 84 px (48 dp), text cut or overflowing in any language, a modal taller than its content, a toast without its card, fewer than 3 products on the shop screen, a scroll thumb visible on a still screen. Do not hide a kit node and replace it with your own control (Skiresort lesson: the kit gives the size).
- Acceptance: before/after frame per screen (en + de), the Patron looks, **every screen >= 8.5/10**.

## 9. Orientation and layout

- **Portrait is the default when the game allows it** (owner 10-23): one-handed, matches how people play. Landscape only when the game cannot work otherwise (Lifeguard HQ and Disaster Command are landscape; Chaiwala Rush, Bureaucracy and Pest Patrol were converted to portrait in 1.1.4/1.1.5; Kaiten, Sky Tower, Pharmacy and Doner were portrait from the start). Decide in F0 and write it in STRATEGY. Grid Boss: portrait, track on top, pit-wall panel below.
- **No empty areas.** The conversion lessons: the camera fits the real game boxes (not the bounding box of the scene), portrait needs its own bottom sheet (default half height, handle for full), 5 equal bottom tabs with icon above and a 21 px label, status in two rows, and the world must fill the screen at every aspect ratio - empty bands were the most common Patron complaint on portrait conversions (LL C-section 10-24 "landscape to portrait pattern", chaiwala/bureaucracy/pest 1.1.5 notes).
- **Screen standard** (GAME_FACTORY): stretch `canvas_items` + aspect `expand` (not `keep`), HUD anchored to the edges, safe-area (notch) margins, layout test at **16:9, 19.5:9, 20:9, 4:3** (and the portrait equivalents); a smoke tap must change the frame.
- Base canvas 720 px short side = 411 dp; 1 dp = 1.75 px. Test windows for portrait use a portrait window/SubViewport (a 1280x720 window mis-scales taps at 720x1280 canvas).
- Landscape: shop and long lists >= 45 % of the width. Portrait: a row needs >= 160 px or it is hidden (flash goes to a toast).
- Always-visible goal: the current objective is on the HUD at all times (compact line, tap opens the goals panel); never a goal behind a tap (LL C, airport FR).
- Build-placement games: tap a placed building -> Move -> ghost (green/red) -> Confirm; nothing refunded, nothing counted new (LL C, airport).
- **Animation (owner 10-23/10-24):** people walk (not slide), swimmers float, vehicles stay in lanes, keep following distance and **never drive through each other**: use `coddy_anim` (Gait, MultiMesh gait, RoadGraph, VehicleMover, ParkingLot); a headless test drives the real vehicles and `overlap_probe.gd` asserts `worst_depth == 0`; World rules (wind, numbers, clock) come from one source, never a fixed angle (LL C, windsock).

## 10. Graphics tiers and the Low budget

Single source: CLAUDE §10 and [`tools/perf/README.md`](tools/perf/README.md) (`addons/coddy_perf`, copied by `sync.sh`).

**Default tier by device (owner 10-23; replaces "never High"):** chosen at start from RAM + GPU adapter + core count in `games/tools/perf/addons/coddy_perf/perf_device.gd` (one place, never re-implemented):

| Class | Tier | Android rule (iOS has its own) |
|---|---|---|
| Flagship | **High** | OS RAM >= 7000 MB (an "8 GB" phone shows ~7.2-7.6 GB) + >= 8 cores + Adreno 8xx / 730+, Mali Immortalis, Mali-G720/G725/G9xx, Xclipse 9xx |
| Strong | **Medium** | RAM >= 5.2 GiB + >= 8 cores + a GPU outside the weak families |
| Weak / unknown | **Low** | everything else (a failed `OS.get_memory_info()` returns -1 on Android: treat as unknown) |
| Desktop | High | |

First start shows a short translated popup (what is set, "change in Settings" - owner 10-14 rule, memory `feedback_games_default_low_quality_with_popup`), Settings has Low/Medium/High and applies at once. `PerfGuard` lowers one tier only when the game really is slow (10 s in a row < 25 FPS **uncapped** frames; frames the game itself capped do not count), never raises by itself, never touches a manual choice; a strong device that was auto-lowered is raised back once. A player who picked a tier by hand is never overridden. Dynamic downgrade shows a one-time notice.

**Profiles** (`perf_profiles.gd validate()` checks them):

| | Low | Medium | High |
|---|---|---|---|
| 3D scale / AA | 0.85 (0.80-0.85 allowed), **no MSAA** | 1.0 + MSAA 2x | 1.0 + MSAA 4x (only without the leak condition) |
| Shadows / SSAO / glow | none | simple shadows | full shadows + extra effects |
| FPS | 30 cap | 60 | 60 |
| Crowd / particles | <= 50 % of full, LOD, far hiding | normal | normal |
| Textures | <= 1024 px (512 for Low variants) | | |
| Lit materials | per-vertex, no specular (`low_shading.gd`); flat multi-material models merged to one vertex-colour surface (`low_merge.gd`); MultiMesh written only when changed | per-pixel | per-pixel |

UI always renders at screen resolution (`canvas_items`; `scaling_3d` is 3D only). FXAA does nothing in Compatibility, so AA = MSAA. While a window is open, FPS may not be capped at 20 during interaction (cap only after 1.5 s without touch) - LL A-notes.

**Hard budgets in the gate** (`perf_gate.sh`, CLAUDE §0, windowed under shot-lock, Low profile, game's `tests/perf_gate_hook.gd` scenes, isolated `HOME`): primitives <= 300k, draw calls <= 150, static memory <= 250 MB, 30 FPS, p95 <= 50 ms, <= 1 hitch >= 100 ms per minute; PSS <= 300 MB is measured **on a real device only** (emulator PSS counts SwiftShader buffers; use it only as an A/B). A **new** game that misses them does not ship to production; a **live** game may ship a build that is better than the live one on every metric.

**Rule for new visuals/animation on Low (owner 10-24, binding; replaces the older "Low <= +10 %"):** a new visual or animation improvement must **never** cause lag on a weak device. Default: every heavy new element is Medium/High only (conditional on the quality tier). Only almost-free things enter Low: **extra draw calls 0 (at most +1, justified in the hand-over), primitives <= +5 %, no new shader branch, no extra render pass, no extra per-frame CPU** (traffic, crowds, particles capped and at 15 Hz on Low). **Every hand-over carries a "Low before/after" perf_gate line.** After release, compare Play Android vitals (slow / frozen frames) with the previous version; if it got worse, that change is switched off on Low. (CLAUDE §10; LOOKDEV_STANDARD two-tier table is current. Older text saying "+10 %" is stale - section 24.)

Other budgets from §10: first start < 8 s on a weak device, memory < 400 MB, scene draw calls <= ~150, visible triangles <= ~60k (soft target), sensible AAB size (font subsets, compressed textures). A new heavy look must also pass a 2 GB-RAM emulator low-end run (`games/tools/perf/qa_lowend_report.sh`, needs a debug/QA AAB because the `PERFX` line is debug-only; decide on p95/p99 + draw/prim/memory, not on median FPS which saturates at the 30 cap).

## 11. P2 - art pipeline: Blender, Meshy, look-dev

- **Blender:** deterministic headless build `blender -b -P blender/<script>.py` -> `assets/models/*.glb` (+ `hd/` for Medium/High). **Use `/Volumes/BuildSSD/blender/blender`** (or `/Volumes/BuildSSD/tools/bpy-venv/bin/python` with `bpy`); `/Applications/Blender.app` hangs on the Mini. Run it through the Mini's `shot-lock.sh` with an alarm (`alarm 900`). Pit Wall has the newest kit: `blender/pwkit.py` (vertex colour = palette x baked AO, alpha = material code), `blender/build_art.py`, `blender/validate_art.py` (also scans names for the denylist).
- **GLB import:** new model folders arrive without the import script and render white/pale. Run Godot import -> `python3 tools/glb_import_settings.py` (scans every model folder, also sets texture mipmaps) -> import again. Procedural "textures" must be tileable <= 256 px, sampled by world position, with mipmaps (`filter_linear_mipmap_anisotropic`; `.import` `mipmaps/generate=true`) - per-cell hashes shimmer and look pixelated (LL, Grid Boss).
- **Budgets:** hero character <= 5k triangles, small <= 1.5k, textures <= 1k (512 on Low), no new materials/draws; many repeated decor pieces are budgeted by triangle count before merging (LL A/Corrupt roof kits).
- **Meshy (allowed again since 10-23)**: characters, creatures, organic hero props only; buildings and modular parts stay Blender. Plan 1,100 credits/month; **every paid task is logged in `~/.config/meshy/ledger.json`** and `meshy.py` checks the cap before each call (do not bypass it; tell the owner when ~100 credits remain). Every Meshy model goes through `games/tools/meshy/optimize.py` (weld, pivot bottom-centre/+Y up/faces +Z, 1 unit = 1 m, one game-named material, Decimate to LOD budgets, 1024 and 512 textures). Pilot costs: static prop ~15-20 credits, rigged humanoid ~26-31. Meshy reads **worse** on tiny crowd figures (~25 px) than chunky canonical figures - use it only where the camera shows the character large (>= ~100 px); take an on-screen frame first. Pit Wall used 0 credits for exactly that reason. The key file is read inside `meshy.py` only; never `cat` it.
- **Sold cosmetics must never look worse than the default** and must really be drawn (section 18; LL D6).
- **Look-dev standard** ([`docs/LOOKDEV_STANDARD.md`](docs/LOOKDEV_STANDARD.md)): the environment is what reads. Order per game: (1) surfaces of the big areas from world position in the shared static shader (zero geometry, zero draws, every tier - the single biggest change on Low), (2) dressing as ONE mesh/draw, (3) light pools + vignette + colour grade, (4) camera, (5) only then model detail, Medium only. Take `before/` from a clean `git archive origin/main`, write the Low ceilings down first, show before/after on Low and Medium; if the difference is not visible at first glance, the work is not done. Fake shadows/light: `blend_mix` + `fog_disabled`, falloff from the quad's own `VERTEX`; custom shaders: `if (!OUTPUT_IS_SRGB)` around any sRGB conversion, varyings written only in `vertex()`, a non-compiling ShaderMaterial silently draws flat grey (the tool must stop on `SHADER ERROR`). MultiMesh with custom data needs `use_colors = true` + white per instance (else black figures); never mirror MultiMesh instances.
- Capture and evidence frames: fixed-size `SubViewport` at phone size (a window larger than the screen is silently cropped), check the saved PNG size, free the scene on every code path.

## 12. Splash and loading screen

- **No Godot logo**: either `application/boot_splash/image` = the game's own art with a matching `bg_color`, or (Grid Boss) `boot_splash/show_image=false` so the first thing on screen is the own-art loading curtain; never the engine default. The Android native splash uses the icon + background colour. Verify on a device/emulator, not only the editor. (Owner 10-14, memory `feedback_games_no_godot_splash`.)
- `run/main_scene` = a Boot scene that loads the main scene on a thread; a `Curtain` (starts identical to the native splash, themed animation, real progress, rotating tips) fades out only when the world runs smoothly (`UiLoading` of the kit can be extended; memory `feedback_games_need_a_real_loading_screen`). Splash/curtain art is rendered from the game (P2).
- Smoke check ignores flat/black frames: the loading screen must show an indicator so the "flat colour" check does not pass on it (`games/jenkins/screen_check.py`).

## 13. P3 - onboarding v2 and the first ten minutes

Standard (CLAUDE §9): a person who has never played anything must do the core loop unaided within 5 minutes.

- **One thing per step**, text <= 1-2 short sentences, plain words, no jargon.
- **Show, make them do it:** the target is highlighted (arrow/hand + dimmed spotlight, `UiCoach`); the step advances only when the player really performs *that* action; a wrong tap shows the step again, no penalty. A hidden/self-hiding button must be idempotent (a held button fired `pressed` twice and skipped the last beat - LL, Smuggler).
- **Scope:** the whole core loop in the first session (start -> main action -> see the result -> money/reward -> first upgrade/investment -> staff/automation), and each feature that unlocks later shows its own short context hint the first time.
- **Safe:** the player cannot lose during the tutorial (no time pressure or time stopped; customers/crew arrival paused: first-session walk-out and miss rates were the P7 findings).
- **Skip exists; Settings -> "How to play / Replay guide" always brings it back.** Replay must not leak nodes/resources/objects (a looped tween created per call leaked in Trading Sim: the replay gate counts nodes + resources + objects).
- **Intro cards = `UiPager` with real hero frames (v2, [`docs/ONBOARDING_V2_HERO_PLAN.md`](docs/ONBOARDING_V2_HERO_PLAN.md)):** three small images per game shown through `page.image` (a loadable `res://` path or Texture2D; never replace the kit's art node with your own control). Source = a **real earned game state** (the game's own bot plays to the moment, a native renderer captures the world without HUD under shot-lock and Dummy audio), real models and camera, no AI redraw, no icons, no invented prices/progress/paid looks (pin the capture's settings to the free defaults - Trading Sim showed a paid theme otherwise). Files in `assets/ui/onboarding/`, each <= 768 px, **three images total <= 1 MB**, startup memory <= +10 %, a **missing file fails the check** (no silent icon fallback), cover-crop checked in portrait and landscape, 10 languages, small and tablet screens. Evidence carries a `manifest.json` (`source.commit`, capture-driver sha256, ids, file hashes); evidence folders are per session and new (a copy into an existing folder once installed another session's frames - LL, Money District). Capture the saved state only after `notify_changed()` + 3 frames and compare the loaded state with the saved one.
- **Telemetry:** Firebase `onboarding_step` (step number), `onboarding_complete`, `onboarding_skip` (a kids/Families game has no Firebase: local counter only).
- **Tests:** per step - right action advances, wrong one does not, skip -> replay from Settings; text fits in 10 languages x 4 sizes (card bounds); the last page needs exactly **one tap** (step + save); the first launch smoke frame shows the guide's first step; a **newbie audit** tool (taps only the coach highlight then idles: every step reachable, guide done < 10 min); relaunch in the middle of the guide must never lock the screen (a guide's `_input` trap may not swallow taps while a window is open - LL B).
- **First ten minutes** is where the game is lost: P7 measures first-session quit/miss rates and tunes the start (Lifeguard 26.6 % -> 8.1 % by three starting lifeguards, Critter Crew 11 % -> 5 %). Live-game features that must exist by then: the ever-visible goal line, **ReviewGate** (CLAUDE §12).

## 14. P5 - ten languages

- **Set:** en, tr, de, es, fr, pt-BR, ru, ja, ko **+ a 10th chosen per game for its audience**: hi (Chaiwala, Disaster, Canal, Lifeguard, Pharmacy, Stamp Office, Critter Crew, Doner), zh_TW (Kaiten, Sky Tower), `it` (Grid Boss, motorsport audience); zh also allowed. Game text, store listing and IAP product names use the same set.
- **Who translates:** Claude, **in-session Haiku sub-agents** (owner 10-21), seeded with the game's glossary (`i18n/game.csv` terms, `data/iap.json` product names). **Never OpenRouter or nitrorouter, never an external translation API, never call Claude through an API when this subscription can do it.** Codex does not translate; text written by Codex is reviewed by a Claude sub-agent. The in-game title and series name are one key each. Review the result yourself: lengths, product names, policy URLs, no trial/"free"/price/ranking words.
- **Fonts:** bundle Noto subsets for Latin/Cyrillic/CJK/Devanagari (`games/tools/fonts/subset_cjk.py`); never rely on the system fallback (`allow_system_fallback=false`). Test: uppercase glyphs, native language names in the picker (shown as images or a bundled font), glyph coverage.
- **Texts:** every visible string goes through `tr()`/Loc (lint `python3 games/tools/i18n/check_hardcoded_text.py games/<game>` in `check.sh`). On a language change **everything open re-localises** (open panel buttons, coach card, onboarding); fonts are cached, never freed on a change.
- **Tests that mean something:** compare the **drawn** text (`node.atr(text)`), not `Label.text`, and prove the test is not vacuous by seeing the English UI before the switch (LL C3a); a word never breaks mid-letter (kit `word_fit`; a `wordbreak` gate: longest word fits the width, 10 languages x 8 aspect ratios); overflow tests in every language and aspect; the IAP catalogue passes `games/tools/shop/tools/check_iap_langs.py` (language code `hi`, not `id`); the IP denylist runs on every language.
- Translate **only the ten core languages** (owner note). Tools and audits never write their own text into the game (a "LOADING" finding turned out to be the audit tool's own text, LL C3b).

## 15. P4 - money: IAP, no trial, ads

**Principles.** Free to play start to finish; no wall, no forced waiting, no subscription, no loot boxes, no pay-to-win, **no trial anywhere** (no "try free", no trial offer, no store copy that promises one; the validator checks banned wording in all ten languages). Revenue = a **wide IAP catalogue** plus ads. A new game carries the **whole** proven set (CLAUDE §11); acceptance is a row-by-row table against Corrupt State in `docs/MONETIZATION_PARITY.md` ([Grid Boss example](pit-wall/docs/MONETIZATION_PARITY.md)); a missing row = P4 not finished.

**Catalogue** ([`tools/IAP_STANDARD.md`](tools/IAP_STANDARD.md), shared code `games/tools/shop/` coddy_iap, Play script `store-automation/iap_catalog_gp.ts`): Remove Ads (`noads`), Premium/"Edition" (noads + gold cosmetic + small convenience), convenience (speed, automation, extra save slots, offline+), cosmetic packs (3-6 skins/themes, each 0.99-1.99, plus a collection), content packs, Complete bundle (~35-40 % off), a pass/VIP, one-time `starter` offer, event offers (crisis / comeback / milestone), limited consumables (1 / 2.5 / 5 / 10 days of income, 0.99-14.99). Four PPP price tiers, product id `com.coddykit.<game>.<slug>` (**an existing id never changes**), ten-language names and descriptions, all one-time (Grid Boss: 22 products, 18 entitlements). The factory games ship ~21 products. Rules: offers never in the tutorial or first session, at most one per session, the same offer does not return for >= 3 days; kids modes get no consumables/offers (parental gate); Restore in the shop **and** in Settings; owned items show "Owned"; tapping a locked feature opens the shop on that product.

**Purchases must be readable (LL C6):** the shop row shows a description of at most 2 lines then "..." (1 line if three products would not fit; the full text in the tooltip), name on one line shrinking 25 -> 21 px, then two lines; an unloaded price is a fixed "..."; a loaded price is never cut off; no negative `line_spacing` on labels; the fallback catalogue **never shows a raw product id or an invented price**; at least 3 whole products visible on one screen in portrait and landscape in every language (count the rows in the list rect).

**`ad_free` (CLAUDE §3):** forced ads are removed **only** by Remove Ads and by big bundles that say "ad-free" in their description (full game, edition, pass/VIP, bundle). Money/resource packs, single cosmetics and single conveniences never remove ads. In code the one right is `ad_free`; `purchased_any` / `has_purchased` is never used for an ad decision. `legacy_ad_free` exists only for games that predate the rule (a new game does not need it). Store/in-game texts never say "every purchase removes ads". **Add your game's ad_free product list to the table in CLAUDE §8** (Grid Boss row: noads, premium, paddock_pass, bundle; starter and cheap items do not).

**Ads (CLAUDE §2):**

| Format | Source | Notes |
|---|---|---|
| Interstitial | **CoddyAds house only** (our own fleet); **no AdMob fallback** (owner 2026-10-25): nothing cached / pool empty = no ad | natural breaks only, never while the core loop is live (Grid Boss: never during a live race), deferred after a rewarded view. Card = `games/tools/ads/CoddyInterstitial.gd`: full screen, hero frames (`heroImageUrls` ≤ 3 else `interstitialImageUrl`, ≤ 512 px, disk-cached, aspect kept); 2–3 frames cross-fade on Medium+, ONE still frame on Low; no hero = big icon on the game's colour; icon + title + slogan in the player's language, big translated Install (Play link with utm referrer), 3 s countdown then X; never "free" / "trial". The game sets `Ads.low_end` to its current quality. |
| Banner | **house only** (ads-config `houseAds.banner`), no AdMob banner | ~58 dp, ≤ 12 % of the canvas; **two different games side by side in landscape and on screens ≥ 600 dp wide** (each tile: icon + short title + short slogan + small Install pill, thin divider, its own ≥ 48 dp tap target, own impression `slot` / click / perAppBudget, never the host, never the same game twice), one game on narrow portrait; **not** for ad_free, family/kids games, onboarding / first 3 minutes, purchase screens; UI moves up by the banner height, never covers the play area or bottom tabs; template `games/tools/ads/CoddyBanner.gd` |
| Rewarded | **AdMob only** (house gives no rewards) | on every game, opt-in only |
| Kids / Families | Cinnamon: rewarded only, child-directed tags, no AD_ID/Firebase, no interstitial; BlockForge (lego): no ads at all while `kids_mode`; both outside the house pool | |

- **Rewarded rules (CLAUDE §4):** only a button the player presses, shown only when an ad is ready and the cap allows; reward only after the SDK's "earned" callback (closed/partial = nothing); never auto-play. **Never reward what is sold** (permanent cosmetics, premium, ad-free, permanent speed/automation/slots, premium currency). One reward <= 1/5 of the cheapest consumable; the sum of a day <= 1/2 of it; each placement has a persistent **daily cap (UTC day) + cooldown** that a restart does not reset. Natural breaks only (offline earnings, day/month end, a contract finished, a crisis "last chance"); never make the game harder and sell the ad as the cure. `ad_free` owners get the reward **without video** under the same caps. Remote kill switch `rewardedEnabled` in ads-config. Events (Firebase): `rewarded_offer_shown`, `rewarded_offer_accepted`, `rewarded_completed`, `rewarded_skipped_paid_user` (`placement`, `reward_amount`, `daily_index`). Strategy detail: [`tools/REWARDED_ADS_STRATEGY.md`](tools/REWARDED_ADS_STRATEGY.md). Money District is the one "mixed" exception (CLAUDE §4 item 8).
- **Backend house pool:** ads-config on Supabase gives every host game 10 other games per format (`ourAdShare=100`, interstitial `perAppBudget=3`, banner 2, `resetDays=7`), rotated weekly (Mac Mini launchd `tools/ads/rotate_house_slots.py`). A game enters the pool only after production release **and** registration: `ads_app` (can_show_ads, can_be_promoted, is_kids, 10 language titles, icon, hero, play_url) + `ads_slot` + `ads_app_house` + the `RING` list; without a record `ads-config` returns `adsEnabled=false` and the game shows no ads at all. Verify with the game's `X-App-ID`: `python3 tools/ads/verify_house_config.py`.
- **Revenue parity features** (CLAUDE §12): AdMob rewarded + CoddyAds, **ReviewGate** (Play In-App Review only, no "do you like it" gate card, no reward, never in the first session or the guide, only at a game-specific "wow" moment after >= 2 sessions + 15 min, <= 3 attempts, >= 30 days apart; headless test; model `corrupt-tycoon/src/game/ReviewGate.gd`), RC ad tracking, Firebase events, onboarding, perf, parity, screen standard, own splash, graphics default.

## 16. P4 - services: AdMob, Firebase, RevenueCat, UMP

Accounts and consoles are opened by the **Patron** (with `store-automation` browser automation); you prepare the code, files and the order. A worked "after login" order with every command is in [`pit-wall/docs/PLAY_SETUP.md`](pit-wall/docs/PLAY_SETUP.md) (back up every console state before writing, read back after writing, stop on any failed dependency).

- **AdMob** (CLAUDE §5): account coddykit.com@gmail.com; real ids live in `store-automation/.env` as `ADMOB_{APP_ID,INTERSTITIAL,REWARDED}_<SLUG>_<PLATFORM>`; created by `store-automation/src/admob/setup.ts` with the logged-in `google` Playwright profile on the Mini (the API cannot write; Claude-in-Chrome is not used). **Never invent an id.** Debug builds use Google's test units; if a release id is empty that ad type is off (button hidden). Godot keys: `[coddy_ads] admob_rewarded_android`, `[admob] general/android/app_id` (rewarded is the only AdMob format since 2026-10-25; do not create interstitial / banner units for a new game). **No ad loads before `MobileAds.initialize` completes; the Ads autoload is the only owner of the SDK init** (another module waits for its flag/signal; a second init or an early load = crash, Train Baron 19 crashes). Gate `python3 games/tools/ads/check_admob_init.py games/<game>`. Plugin: next-gen `ads_mobile_sdk` via `games/tools/ads/install_poing_admob.sh` (three older games - cinnamon, lego, zombi - still use the GMS bridge).
- **UMP/GDPR:** the consent request runs before any ad; AdMob "Privacy & messaging" must have the GDPR (and US-state) message **published**; the emulator warning `UMP: no form(s) configured` means it is missing. Settings carries the standard privacy block (`UiSettings.privacy`).
- **Firebase Analytics:** old games = project `coddykit-game`; **new games since 10-21 = `coddykit-game-2`** (the old project hit the 429 app limit), account coddykit.com@gmail.com, one existing property (id 559919694), each game its own Android stream, **no new property**. The Patron opens the stream. The bridge AARs `android/firebase_core` + `firebase_analytics` must be in every game (Doner shipped without and sent nothing): `services_preflight --production` is the gate. Families games: no Firebase. Event names per CLAUDE §4/§9 above.
- **RevenueCat:** one RC project per game (`store-automation` `rc-setup --template ...`); SDK >= 10.22.1 via `games/tools/godotx_revenue_cat_src` (`dist/` .aar). **RC ad tracking is mandatory** ([`tools/REVENUECAT_AD_TRACKING.md`](tools/REVENUECAT_AD_TRACKING.md), CLAUDE §5b): AdMob `paid` -> `revenue`, impression -> `displayed`, click -> `opened`, plus loaded / failed_to_load; house ads are not sent; no invented revenue (if the plugin gives no `paid`, none is sent and it is stated in the report); remote kill switch `rcAdTracking`. Games on their own Billing (godot-iap) use RC **observer mode** only after a real-device test, plus the REST `GET /v1/subscribers/{id}` first-open ping (LL D, C3). After the **first AAB is on Play**: RC **Check credentials** (the Play service account verification does not pass before). Never print the RC/Play credentials; the fleet service-account file is a path in the setup doc, not content.
- **Production-mode trap:** a release must never fall back to mock/test services (Travel Tycoon built MOCK without `GAME_SERVICES_MODE=production`; Parts Rush shipped a mock ad). The release build runs `services_preflight.py --production` and the AAB is checked for live ids/no test ad ids.
- **Play Console setup** (Patron): `GP_CATEGORY=Game npx tsx src/cli.ts gp-create-app --package <pkg> --name "<store title>" --headless` (always `--dry-run` first). "Only the owner can open a Play app" was wrong and cost time (LL D8). Never open `~/.playwright-store-session/*` with a real Chrome (it wipes the Google cookies; only through `session.ts`, or Chrome with `--use-mock-keychain --password-store=basic`; the owner recovers with `npm run login`).

## 17. Audio

CLAUDE §13: every game ships SFX + two music loops (calm / intense) + ambience when needed, generated with [`games/tools/audio`](tools/audio/README.md) (ElevenLabs, `audio/manifest.json`, skip-if-exists, credit floor 15 %). One game is ~30 SFX + 2 x 60 s music + a ~22 s ambience, about 2,100-2,350 credits and ~2 MB of Ogg. Godot side: the `Audio` autoload (`games/tools/audio/godot/Audio.gd`) with `Music` and `SFX` buses, Settings toggles **Music / Effects** (default on, persisted), pause on app pause, **no playback when headless** (tests are silent; the Mini is always muted), `tests/audio.gd` mandatory (weather/ambience also use the headless guard). Look at `disaster-command` and `canal-control` for finished examples; `audio/INTEGRATION.md` maps events to ids.

- **Paid generation needs approval before it runs** (general rule). The 10-21 approval covered the nine factory games only. Run `gen.py games/<game> --dry-run` for the credit estimate and ask the Patron/owner before a batch. The ElevenLabs key is `~/.config/elevenlabs/api_key` (Mac Mini) or `$ELEVENLABS_API_KEY`; there is **one fleet key** (Starter plan, commercial licence); never print it.
- **Voice-over is ON HOLD (owner 10-24):** the planned voice set is the Borderline-style pair **"Lily" (mentor, velvety) + "Callum" (announcer, husky)** in ten languages with a Settings switch, plus narrated trailers in the `docs/GROWTH.md` style; everything waits for the owner's ElevenLabs grant. Do not spend the remaining credits on voice. Until then a new game has SFX + music only.

## 18. Tests and gates

`tools/check.sh` is the one headless gate (copy `pit-wall/tools/check.sh` or `lifeguard-hq/tools/check.sh`): fails on engine errors too (`SCRIPT ERROR|Parse Error|Compile Error|^ERROR:|^FAIL`), restores `project.godot`/`export_presets.cfg` in a trap, never launches an emulator or touches an account. Nothing passes P3/P7 until all of these are green:

| Gate | What it proves |
|---|---|
| `tests/run.gd` (+ per-area suites) | sim, planner, weekend/season, saves, reopen-lock, platform |
| `tests/ip_denylist.gd` | IP words = 0, proof fixture catches plants (section 5) |
| `tests/physics_gate.gd` | no stray physics ticks (A1) |
| `tools/balance.gd` (quick in check, full by hand) | the balance standard (section 7) |
| save: `tests/save_robust.gd`, `games/tools/save_fuzz`, `tests/relaunch.gd` | no unreadable saves; hostile saves; reopen mid-guide |
| `tests/perf.gd` + **perf_gate** (`sh games/tools/perf/perf_gate.sh games/<game>`, last step) | Low profile checks, primitives/draws/static memory; MSAA guard; sold-look visible on Medium and canonical on Low |
| Device tier test: **two** cases | a strong fixture must use a non-flagship GPU (Adreno 660 / Mali-G610) -> Medium; a flagship (Adreno 830, 11 GB) in a separate test -> High (LL E, "strong phone fixture", #78) |
| **Tab-open probe** `tests/tab_open_probe.gd` | taps each bottom tab and Settings through real press+release input and times it until the first content row is drawn. **Current rule: every tab and Settings within 100 ms** (`-- --limit 100`; also `--phone` = Low tier on a handheld, and `--phone --stuck --switch`; commits "#113 tab opens with its rows at once" and "require all tabs and settings within 100ms", 10-25). An empty list is never deferred. Older `check.sh` files still say 150 ms desktop / 300 ms phone - use 100 |
| **Touch scroll:** `check_touch_scroll.py` + `tests/touch_scroll.gd` (kit suite) + `tests/finger_scroll.gd` | every ScrollContainer finger-scrolls; real `Input.parse_input_event` (ScreenTouch + emulated mouse); assert the **game effect**, not a signal count. The kit suite has a **watchdog** (`WATCHDOG_S` 300 s, env `TOUCH_SCROLL_WATCHDOG_S`, 0 = off) because it hung three times in a day with a Godot at 2 % CPU - give every long run `perl -e 'alarm N; exec ...'` as well |
| **Hold-button test** `tests/hold_button_test.gd` (see chaiwala-rush, cinnamon; audit `tools/ui/HOLD_AUDIT_2026_10_25.md`) | a button that works while held: real ScreenTouch >= 1 s measures the game effect, release stops it; state text/pulse while working; reason shown when idle; 10-language layout |
| **Overlap / vehicle-overlap** (`coddy_anim` `overlap_probe`, `tests/anim.gd`, a traffic test) | vehicles and crowd never intersect: `worst_depth == 0`, "stacked pairs / min gap" headless, nobody stuck |
| `tests/audio.gd`, `tests/review.gd`, `tests/anim.gd`, `tests/monkey.gd`, `tests/soak.gd` | audio headless guard; ReviewGate; animation rules; monkey; short bot soak with leak checks (`-- --full` is the 30 min run by hand) |
| Onboarding: guide tests, intro hero test, newbie audit (`tools/newbie_audit.gd` or `first10`) | section 13 |
| `tests/p5.gd`-style language/overflow, `wordbreak`, glyph gate, `check_hardcoded_text.py`, `check_iap_langs.py` | section 14 |
| Shop: sold-cosmetic visibility (`test_every_sold_skin_is_visible` model), `check_gate_copies.py`, ad_free rules, rewarded caps | section 15; **every** sold cosmetic is drawn on the default model |
| `check_admob_init.py`, `services_preflight.py --production`, `check_rc_customer.py` | section 16 |
| Kit gates: `ui/sync.sh --check`, `perf/sync.sh --check`, `anim/sync.sh --check`, kit tests | the copies match the kit |
| `tests/android_export.gd`, `jenkins/placeholder_check.py`, `store/validate.py`, `tools/store/validate.py` | export preset (gl_compatibility), no placeholder text, store files |
| Boot run: `res://scenes/Main.tscn --quit-after 120`, `--quit-after 900` | splash -> curtain -> threaded Main starts without errors (enough frames for the loader to finish) |

Test hygiene that has bitten (LL A/E): measuring tools must use a temporary `HOME`/`XDG` (a real `user://` save from another session turned main red); fixtures must free what they create (a "leaked resources at exit" count that was really fixtures); lambdas on `process_frame`/timers capture `get_instance_id()`, not the node; tests wait for the real condition (an upgrade finished) not a fixed frame count; do not pin `--quality=low` in the full suite; never hardcode a version number.

## 19. P7/P8 - build, smoke, release candidate, Play INTERNAL, production

1. **Build on the Linux agent** (owner 10-07): Jenkins job `Games/<slug>-mobile`, `ANDROID_ACTION=build-only`, `GAME_SERVICES_MODE=production`, agent label `android-linux`; Jenkins is at `http://100.113.101.52:8080` (use `curl -g` for `tree=[...]`). Jenkins builds `origin/main`, not your tree: **commit and push first**. The job runs preflight, tests, builds the AAB, then `games/jenkins/release_symbols.sh` (`SYMBOLS_REQUIRED=1`: a missing zip fails the build) and archives AAB + `symbols.zip` + `version_code.txt` + `release.json` (exact commit + SHA256s). Some older games were built locally on the Mac mini (clean `git archive`, `run.sh android build-only`); Mac does no Android build for new games. Version/`export_presets` bumps belong to the release train.
2. **Symbols:** the zip is uploaded **in the same Play edit** as the AAB (`SYMBOLS_ZIP=<zip>` to `games/jenkins/play_upload.mjs`); Play refuses them afterwards ("version code added previously"). `SYMBOLS: MISSING` in the log = no upload.
3. **Smoke** (`games/jenkins/smoke_android.sh`, on the Mini, one emulator at a time under the global `smoke.lock`, never parallel, close the emulator afterwards, Mini muted): install the **release AAB** (not a debug/QA build, not a desktop frame), 200 s run + 300 monkey events, `monkey --pct-pinchzoom 0`, then force-stop, restart, wait ~10 s and take the last frame; `screen_check.py` rejects black/flat frames; logcat 0 errors; heavy-start games get `SMOKE_SETTLE_S=60`. If the app looks frozen after monkey, first check `dumpsys input` touchingPointers (an unfinished two-finger drag holds a pointer) and try the same seed on the previous release. Banner/ads/IAP evidence must come from the **release AAB on an emulator/device** (a desktop `force_desktop` frame cannot prove a banner appears; Train 1.2.4). If a hung emulator/`netsimd` blocks smoke, kill the stale process and the orphaned `smoke.lock`.
4. **Permissions:** compare the AAB's permission list with the previous release (bundletool); first release prints the **full list**; the Ad ID declaration stays Yes when AD_ID is present.
5. **Release-candidate line** (the Patron's watcher reads this; the prefix is Turkish "READY TO UPLOAD" and must stay literal). One line appended to `/Volumes/BuildSSD/dev/i18n-work/tasks/RC_<version>.md` (the real file for each release):

```
YÜKLEMEYE HAZIR <slug> <versionName> vc<versionCode> <package> AAB=<abs>.aab SYMBOLS=<abs>.symbols.zip NOTES=<abs>.release-notes.json SMOKE_FRAME=<abs>.smoke-frame.png commit=<sha10> perm=same (<N> izin, <previous> ile aynı; bundletool) - <your id>, Jenkins Games/<slug>-mobile #<n> (linux, production), smoke 200 sn + 300 monkey PASS (<smoke dir>); içerik: <what changed, Patron-approved item numbers>; Play SS <10 dil ... STORE_ASSETS_PASS>; notlar 10 dil
```

   The Patron answers with `-> HH:MM production'a yüklendi: <slug> <version> (<package>) - smoke karesi Patron tarafından görüldü`. Release notes (what's new) exist in 10 languages (a Haiku sub-agent). Add a **Low before/after perf line** when the version changes visuals (e.g. `start prim 39788->41688 (+4.8 %) draw 36->36`).
6. **Play INTERNAL first.** A new game goes to internal testing (not production) with the Play app opened by the Patron (`gp-create-app`), RC project, AdMob, Firebase stream, 21+ products ACTIVE. **Visual approval -> production:** only after the Patron has looked at the before/after and smoke frames, the UI bar is met (CLAUDE §17, score >= 8.5), no critical bug is open and a **fresh** smoke passed (owner 10-23: a finished game with a fresh smoke PASS goes to production without asking again; the W64 UI rule of 10-22 still applies). Smoke FAIL = no release.
7. **After the upload:** section 21. Do not upload to stores yourself unless the Patron/owner explicitly gave you that task; never submit iOS.

## 20. P6 - store listing and screenshots

- **Listing** `store/listing/<locale>.json` for the game's ten languages (limits: title 30, short description 80, full 4,000; UTF-16 length too), same set as the game; written in English in-session, the other nine by Haiku sub-agents with the glossary. Title per locale carries the brand + a local search term (CJK locales lead with the word players type). **No IP words, no "free"/trial/offer wording, no price or ranking claims, no competitor names.** The ad-removal sentence ("with ad removal, rewards without video") is tied to all ad_free products in every language. Reference: `games/kaiten-sushi/store/README.md`, `.../validate.py` (the offline checks).
- **Screenshots, coddy circuit style** (owner standard for all apps, 10-08: dark brand-tinted background with circuit lines and sparkles, icon + name header, pill chip, two-line localized headline, tilted glowing phone, floating benefit chip). Per language: **phone + 7" tablet + 10" tablet + feature graphic** (Airport 1.3.4: 7 slides x 3 form factors + feature = 22 files per language; phone 1080x1920 portrait; counts vary, at least 5 slides with real game UI, no mockup-only decks). Frames are **real earned gameplay**, captured with `tools/store/capture.sh` -> `render.mjs` -> strict `validate.py` (`STORE_ASSETS_PASS`, `STORE_GLYPHS_PASS`); a provenance hash covers the kit, so a kit sync or a capture-source change marks the shots **STALE** and they are re-shot in the release round. Take them on the **weak Android profile** (the desktop default shows High and paid-looking items). Never other users' names on a slide; no fake ratings/downloads; pin settings to the free looks. Reference: [`LOOKDEV_STANDARD.md`](docs/LOOKDEV_STANDARD.md) release checklist, `games/kaiten-sushi/tools/store/README.md`.
- **Icon:** produce >= 4 candidates + a 48/96/192/512 sheet and **install none**; the Patron picks as the game owner. An icon file, Play hi-res icon or a build carrying one changes **only** for an explicitly approved candidate (owner 10-07). Check legibility at 48 px (a medical "plus" icon was rejected for a shop game; a tower icon was too thin at 48 px).
- **Declarations drafts:** `store/play-data-safety.csv` (must describe what the code really does: Firebase, AdMob Ad ID, RevenueCat, Play Billing, CoddyKit cross-promotion), `IARC_DRAFT.md` (audience; the factory used 13+), `PRIVACY_POLICY_ADDENDUM.md` (the game's section for `https://www.coddykit.com/pages/privacy-policy`; Data safety and that page must match). Contact: `coddy@coddykit.com`, support `https://coddykit.com/contact`, copyright "2026 Coddykit Inc.".
- **Play promo video** (CLAUDE §15): after the UI is final (W64 for a new game), a 30 s real gameplay trailer: `python3 tools/store-video/games/trailer.py all <game> --fresh`, `trailer.py queue <game>`, then `v2/ops/yt-play.mjs` (see `tools/store-video/games/README.md`) -> YouTube public -> the default-language listing's `video` field. Narration is on hold (section 17).

## 21. After the first upload: account steps and vitals

CLAUDE §15 (owner 10-22), the game is not "done" until the Patron has done or ordered each:

1. **AdMob GDPR/UMP message published** (and the US-state message), privacy URL = the policy page; code requests UMP before ads.
2. **RevenueCat "Check credentials"** passes (needs the first AAB on Play).
3. **Privacy policy:** the game's section is live on the coddykit.com policy page and equals the Data safety CSV.
4. **ASO Atlas:** after the Play page is public: `track_app` android in us + 11 countries; waiting list `~/.coddy-aso/asoatlas/games_pending_play_track.json`.
5. **AdMob <-> Play link** once it is in production.
6. **Ads backend registration** (`ads_app`, `ads_slot`, `ads_app_house`, `RING`) and verification with the game's `X-App-ID`.
7. **Play promo video.**

**Android vitals:** compare slow-frame / frozen-frame and crash/ANR rates with the previous release for every release; a regression is a "Low" switch-off for the change that caused it (CLAUDE §10). The fleet scan of Play Vitals per game runs regularly: a new crash cluster follows the CLAUDE §0 flow. Symbols (section 19) are what make native crashes readable. The first release of a game sets its baseline. Look at 1-star reviews the same way: one game's complaint is checked in every game (owner 10-21).

## 22. Working rules (git, GPU, disk, shell)

- **Sparse git worktree per parallel agent** (never work in the shared checkout; it can be hundreds of commits behind origin: read game code from `origin/main`, e.g. `git archive origin/main games/<game>` into a scratch dir). Template (tested): `git worktree add --no-checkout --detach /Volumes/BuildSSD/dev/wt-<S>-<name> origin/main`, then inside it `git sparse-checkout set --no-cone /games/<game>/ /games/tools/ui/ /games/tools/perf/ /games/tools/anim/ /games/tools/shop/ /games/jenkins/ /games/CLAUDE.md` and `git checkout` (add more `games/tools/*` dirs when a gate needs them; the Linux agent checks out only `games/<game>` + `games/jenkins`). One worktree = one agent; a shared worktree makes `pull --rebase` stop on a dirty tree.
- **No `git stash`** (the stash list is shared by every worktree; a `stash pop` applied another session's 18-conflict stash). For before/after measurement use `git checkout <sha>~1 -- file`, measure, `git checkout HEAD -- file`.
- **Never `git add -A`, never plain `git commit` in a shared place.** Commit **only your own paths**, via a helper that builds the commit from a temporary index on top of the freshly fetched `origin/main` (`GIT_INDEX_FILE` + `read-tree` + `add` of your paths + `commit-tree` + `push`, with a guard that the commit touches only those paths and an `ORIGIN_MOVED` stop if someone changed those paths since your copy; a stale copy once reverted another worker's commit, LL E). **Commit and push about every 30 minutes**; no uncommitted work older than that. Secret and big-file scan first, fetch + rebase/merge-tree, one game per commit, end the message with the co-author line.
- **zsh brace rule** (the Bash tool is zsh): write `"${C}:refs/heads/main"`, `"${B}:games/x"`, never `"$C:refs/..."` (`:r`, `:g`, `:h`, `:t` are modifiers: pushes failed silently four times and one `git show $B:path > file` emptied a file that was then pushed). Use POSIX `sh` scripts for helpers; check `wc -l`/`git diff --stat` before pushing.
- **`rm` with a guarded variable:** `rm -f "${S:?}"/*.png`. A bare `rm $O/*.png` triggers the "dangerous rm" prompt even in bypass mode and blocks the session for minutes.
- **GPU and heavy jobs:** every Godot/Blender/Chrome job that uses the GPU goes through `/Volumes/BuildSSD/novareka-dev/shot-lock.sh` with an alarm (`alarm 900`); **at most 3 heavy jobs at once** (7 parallel GPU agents kernel-panicked the Mini); smoke never parallel; the Mini stays silent (`--audio-driver Dummy`). Start every long run with a timeout (`perl -e 'alarm N; exec ...'`); at the end `pgrep -fl Godot` and kill only your own leftovers.
- **Disk:** BuildSSD >= 25 GB free (>= 12 GB was the older floor), root >= 3 GB; big temporary files on BuildSSD (`TMPDIR=/Volumes/BuildSSD/tmp/<you>/...`); multiple heavy jobs at once hit ENOSPC twice.
- **`project.godot` rewrite:** `--import` and tests rewrite `project.godot` (dropping defaults, reordering); restore it from origin before committing (the check scripts restore it in a trap). Version files and export presets are release-train files.
- **Shared kit/checkout facts:** the Mini's shared checkout is often stale; Jenkins builds origin; the Linux agent has no `games/tools` unless the game's job adds it.
- **Reporting:** a hand-over states the commit(s), the tests with numbers, the Low before/after perf line, what is open. No fake "done": say `blocked` and what unblocks it.

## 23. Known traps

One line each; the row/section in [`LESSONS_LEARNED.md`](LESSONS_LEARNED.md) has what happened, the rule and the guard. Read the whole file before you start (CLAUDE §0).

**Engine, performance, rendering**
- Android GLES3 MSAA + 3D buffer/post effect leaks a framebuffer per frame -> OOM (LL A notes, section 7).
- Vulkan on Android crashes at start (device lost) -> `gl_compatibility` (A11).
- 60 Hz physics + 8 catch-up steps in a game with no physics wastes a frame on weak phones (A1).
- Per-pixel lighting is the Low cost on weak GPUs; per-vertex + no specular (A2) - but measure vertex count first (Corrupt: 1.16 M vertices made it worse, A3).
- Compatibility renderer does not LOD or cull a map-sized MultiMesh: split into 120-128 m cells (A4); write instances only when changed (A5).
- Shader warm-up on a throw-away world crashed the GL thread; warm on the real scene, budgeted (A6/A7, [`perf README`](tools/perf/README.md)).
- SurfaceTool keeps vertices after build; replace it, free unused caches (A8). Rebuilding UI on every money change eats taps (A9); `Label.text` every frame (A10); per-frame material colour writes (A10a); merge flat-colour multi-material models on Low (A10b).
- No native symbols = unreadable crashes, and Play refuses them later (A12).
- A 20 FPS cap while a window is open made every list scroll choppy; PerfGuard mistook the cap for slowness (A notes).
- Emulator PSS is not device memory; median FPS saturates at the cap, use p95/p99 (A notes).
- `use_custom_data` MultiMesh without `use_colors` = black figures; mirrored instances = black faces; `BLEND_MUL` quads render black; varying written outside `vertex()` = flat grey; double sRGB conversion darkens; high `tonemap_white` turns light scenes grey (C/E).
- Baked vertex colours (AO / tint) in a GLB make Godot turn on vertex-colour-as-albedo, and the one-surface merge silently skips the model (Disaster W66: Low 24 -> 85 draws): wire the post-import script per GLB folder via `glb_import_settings.py`, guarded in `check.sh` (A10c).
- A new GLB folder without the import script renders white; `.import` mipmaps default off; per-cell hash "textures" shimmer (E, Grid Boss).
- `(arr[i] as PackedVector3Array).append(v)` appends to a copy (E, W68).
- Godot imports `store/`: add `.gdignore` (E).

**UI, touch, language**
- Drag that starts on a button did not scroll in 12 games (C1); a held, self-hiding button fires `pressed` twice (Smuggler).
- Kit code assumed its parent was a CanvasItem (CanvasLayer crash) - kit uses only `Node` API on hosts (E).
- OptionButton language picker: the release picks the next row (use `UiLanguage`).
- Open panels, coach and onboarding stayed in the old language after a switch (C2); tests that read `Label.text` instead of `atr()` (C3a); audit tools that print their own text (C3b); fonts freed on a repeated language pick (C3).
- Shop row cut descriptions, showed "Loadin..."; negative `line_spacing` hides line 2 (C6). Boxes in landscape too narrow (C7). Empty placeholders on the HUD ("Tip: run - ...") (C8).
- Toast lasting 4-9 s and queueing; must be 2.5-5.5 s, tap to dismiss (C notes).
- Onboarding last button needed two taps because the game kept its own page counter (C notes). Kit-hidden art slot left a 0 px image area (E, Skiresort).
- Relaunch in the middle of the guide locked the screen (B notes).
- Dialog/coach covers the thing it explains; use `UiCoach.avoid` (E, Disaster #115).
- Goal behind a tap = no goal (C, airport); no way to move a placed building (C, airport).
- Test window larger than the screen is cropped silently; portrait needs a portrait test window (C/E).
- Desktop frames show High and paid looks: capture on the weak profile with free defaults (E).

**Data and saves**
- Codec rejected states the sim can produce -> empty start + overwritten save in 5 of 10 games (B1).
- A seeded RNG method named `randf` silently calls the global; event handlers read a stale tick clock; JSON floats do not round-trip bit-exact (E, Grid Boss P1).
- Background save overwrote the slot with the title scene (B2).

**Money, ads, services**
- Satisfied-looking cosmetics that were never drawn were sold for weeks (D6); a new model made sold outfits worse than default (D notes).
- Release AAB built in mock mode (D7); the Firebase bridge AARs missing sent no analytics (D1); AdMob GDPR message never configured (D2); Data safety said "no ads" while code had ads (D3); a wrong language code `id` for Hindi in the catalogue (D5).
- AdMob init race: a second `MobileAds.initialize` or an early load crashes (D4, section 16).
- Banner template tests searched source text; add the banner as its own file with one init call (C10); banner proof from a desktop frame is no proof (E, Train 1.2.4).
- A game missing from `ads_app` shows no ads and is not promoted (D notes).
- REST-only IAP reported only buyers to RevenueCat; observer mode needs a real-device test first (D notes, C3).
- RC "Check credentials" fails until the first AAB is on Play (D9). Trial wording anywhere = fleet violation.

**Art and look**
- Environment reads, characters do not: round 1 of Kaiten spent the budget on character detail and nobody saw it (LOOKDEV).
- Meshy on ~25 px figures read worse (E); Meshy export faces -Z and is stockier (LOOKDEV); a pale "dark scene" hides decor at night (E, Doner).
- A "show more" camera zoom-out broke the readability floor and the Low budget (E, Sky Tower).
- Livery that passed on hex looked like a real team on screen (E, Grid Boss P2).
- A failed evidence check that returned early left a SubViewport alive and doubled the next frames' counts (E).
- Fake shadows/light pools: see section 11. Camera that fits scene bounds instead of game boxes leaves empty bands in portrait (C notes).

**Process and release**
- Smoke "freeze" = an unfinished two-finger drag in monkey, not a game bug (E); WebView/Capacitor emulator surfaces can freeze mid-animation (E).
- Hung Godot processes held the GPU lock for hours; stuck `netsimd` blocked the emulator and `smoke.lock` (E).
- Shared checkout stale vs origin; stale worktree copy reverts another worker's commit (E).
- zsh `"$C:refs/..."` swallows the push (section 22). Bare `rm $O/*` blocks the session (E).
- `check.sh` calls into `games/tools` break on the Linux agent unless guarded (E).
- Version number literal in a test broke a Jenkins run (E). A measuring tool read a real `user://` save (E).
- Evidence folder collisions installed another session's hero frames (E, Money District).
- Do not open `~/.playwright-store-session` with real Chrome (wipes cookies); do not wait for the owner to open the Play app (D8); open the "dangerous rm" prompts only after reading the command (Patron).
- Mac mini kernel panic from 7 parallel GPU agents; max 3 heavy jobs + shot-lock.
- Mini's `/Applications/Blender.app` hangs; use the BuildSSD copy.
- A bug report/Vitals cluster in one game is a fleet task (CLAUDE §0).

## 24. Where the sources disagree

Current-wins reading (newest dated owner decision), so you do not follow an outdated line:

| Topic | Older text | Current (use this) |
|---|---|---|
| Low budget for new visuals | `LESSONS_LEARNED.md` (profile standard note) and [`W68_ROLLOUT_TABLE.md`](docs/W68_ROLLOUT_TABLE.md): "at most +10 % draw/prim/p95" | CLAUDE §10 (owner 10-24, binding): extra draws 0 (max +1 justified), primitives <= +5 %, no new shader branch/render pass, no extra per-frame CPU; LOOKDEV_STANDARD already follows it |
| Default graphics tier | [`NEW_GAMES_2026-10-20.md`](NEW_GAMES_2026-10-20.md), GAME_FACTORY P3 and memory note 10-14: "never High, strong = Medium, weak = Low" | CLAUDE §10 (owner 10-23): flagship = High, strong = Medium, weak/unknown = Low |
| Banner | NEW_GAMES and the 10-20 rewarded-ads note: "no banner" | CLAUDE §2 (owner 10-23): banner allowed, house-first, with the exclusions listed |
| "Any purchase removes ads" | NEW_GAMES and GAME_FACTORY: "any purchase = no forced ads" | CLAUDE §3/§8 (owner 10-20): only Remove Ads and big bundles; a legacy exception for pre-rule buyers |
| Firebase project | NEW_GAMES: `coddykit-game` | GAME_FACTORY (EMIR #25/#35, 10-21): new games use `coddykit-game-2` (existing property, own stream per game); old games stay on `coddykit-game` |
| Onboarding | NEW_GAMES / GAME_FACTORY: "3-page intro + coach" | CLAUDE §9 + §17 + onboarding v2: modal pager with real earned hero frames + step-by-step coach |
| Meshy | memory 10-22: "no Meshy, Blender only" | 10-23 owner: Meshy allowed with ledger and cap, for large characters/organics only |
| Tab-open latency | `check.sh` files: 150 ms desktop / 300 ms phone | Patron rule 10-25: every tab and Settings <= 100 ms (not yet written into CLAUDE.md) |
| Budgets | CLAUDE §0 gate: prim <= 300k, draws <= 150, static <= 250 MB, PSS <= 300 MB (device only) vs §10 "memory < 400 MB, ~60k visible triangles" | Treat §0 numbers as the hard gate; the §10 figures are softer targets |
| Store screenshot style | `STORE_PREP_BRIEF_2026-10-10.md` (iOS-era): a style specific to each game | Owner 10-08 standard + GAME_FACTORY P6: coddy circuit style; the frame source is real gameplay; check the newest finished game's `tools/store/README.md` |
| 10th language | PITWALL_BRIEF: `it`; factory games: hi / zh_TW | Chosen per game in F0 (nine core + one) |
| Kit "frozen" | the kit keeps receiving fixes | Frozen **inside a game**; changes happen only in `games/tools/*` and are synced to all games in one pass |
| Android build host | memory 10-07: always the Linux agent | New games: Jenkins Linux build-only; some older production RCs were exported locally on the Mini (no Jenkins job for airport/train) |
| Production gate | owner 10-22: "no game goes live until UI is very good" vs owner 10-23: "finished + fresh smoke PASS goes to production" | Both apply: factory games stay INTERNAL until the UI bar (>= 8.5, Patron frames) is met; then a fresh smoke PASS is enough, no extra asking |
| Voice | CLAUDE §13 only speaks of SFX + music | Voice-over (Lily + Callum) on hold since 10-24 (memory note) |
| Lesson file vs matrix | `LESSONS_MATRIX.md` dates 10-22 | newer lessons (10-24/25) are not in the matrix yet |

## 25. Day-one checklist

1. Read: `CLAUDE.md` (all), `LESSONS_LEARNED.md` (all), this file, Grid Boss STRATEGY/PLAN/IP_RULES, `PITWALL_BRIEF.md`, `GAME_FACTORY.md`, `tools/ui/README.md`, `tools/perf/README.md`, owner memory `MEMORY.md`.
2. Own sparse worktree from the freshest `origin/main`; scratch under `/Volumes/BuildSSD/tmp/<you>/`; `df -h` shows >= 25 GB free.
3. F0: ASO Atlas demand table, rival shelf, name candidates + collision scan, orientation, 10th language, content policy, IP rules + denylist, `STRATEGY.md` + `PLAN.md` + `IP_RULES.md`. Commit, report, wait for the Patron's decisions.
4. P1: copy the kit-synced skeleton of the newest game (`pit-wall` or the nearest sibling), run `sync.sh` for `ui`, `perf` (and `anim` if needed), `gl_compatibility`, own splash stub, saves + fuzz + reopen-lock, determinism tests, balance runner, denylist with proof fixture, `tools/check.sh`, Jenkins adapter. Commit every ~30 minutes.
5. P2 -> P8 in order (section 4), each with its exit evidence; ask the Patron for visual approval after P2, P3 and P7.
6. Update CLAUDE §8 with the ad_free list, append a `LESSONS_LEARNED.md` row for anything new, and tell the Patron about the accounts only the Patron opens (Play app, AdMob, Firebase stream, RevenueCat project).
