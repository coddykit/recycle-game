# Volt City — P7 QA (2026-10-28, S2 #165, Unity 6000.3.27f1 on the Mac Mini)

One table, every gate with its number. Evidence root: `/Volumes/BuildSSD/game-builds/volt-city/q165/`
(`before/` = the code at the start of #165, `after/` = after the quality pass, `langs/`, `soak/`, `device/`).
Editor frames come from `Tests/PlayMode/VoltCaptureTest.cs` (explicit test: camera + UI Toolkit panel rendered into
render textures at 1080 x 2340, simulated phone insets status 110 px / nav bar 126 px, fresh career per run, the newbie
GuideBot through the guide, then the balance gates' "greedy" operator + buy phase as the player).

| Gate | Result | Number / evidence |
|---|---|---|
| Compile (all assemblies + GMA 11.6.0, RC 9.16.0, Play Review 1.8.3, Firebase 14.1.0 bridges) | PASS | 0 errors |
| EditMode (Unity Test Runner) | PASS | 125 / 125 |
| PlayMode smoke (`CitySmokeTest`: boot, pager, graphics popup, 9-step guide, tab probe ≤ 100 ms, relaunch) | PASS | 1 / 1 |
| `unity/Tools/check.sh` | PASS | core 48/48, sim 26/26, game 46/46, 98 files parse, strings 442 × 10, IAP 23 products, store listing, fonts, EditMode |
| `jenkins/preflight.py --production` | PASS | 13 / 13 |
| Fonts cover every shown character (`Tools/subset_fonts.py --check`) | PASS | Noto Sans 239 chars, JP 741, KR 443, Devanagari 68, Symbols 2+3 — 1.2 MB total |
| 10-language frame check (HUD + guide + first tab, Medium) | PASS | 0 tofu, Hindi conjuncts shaped (advanced text generator for `hi` only) — `langs/medium_<lang>_0[1-4]_*.png` × 10 |
| Perf gate Low (world draws / triangles, start + late) | PASS | **14 draws, 40,410 → 40,410 tris** (budget ≤ 16–17 draws, ≤ 84k) — unchanged by #165 (fog / fit / UI only) |
| Perf gate Medium | PASS | **14 draws, 53,316 tris** (≤ ~120k) |
| Low: no extra per-frame CPU from #165 | PASS | fog distances written only when the camera distance changes; toasts / pager gate = two bools per frame |
| 30-min soak (Low, editor, bot at 3×) | PASS | 1,800 s, 50,715 frames, 24 shifts, 18 purchases, **0 errors**, managed memory 261 → 262 MB (flat), worst frame 247 ms once (GC); `soak/capture_report.txt` |
| 10-minute play (first 600 s of the soak) | PASS (bot) | shifts 1 → 9, stars 3★ every shift after the guided one (2★), cash dips to $129–$356 after hookups and recovers; no stuck state, no window left open |
| Save safety (relaunch mid-career) | PASS | PlayMode smoke relaunch step + EditMode save tests (hash-identical future, `.bak` / `.unreadable`) |
| Device smoke (emulator, `smoke_android.sh`, 120 s + 300 monkey, dev AAB 41 MB with all 4 bridges) | PASS | **0 Unity errors**; bridges AdMob+UMP, Firebase, PlayReview, RevenueCat; `device2/smoke/after_monkey.png` = hero pager full-bleed, pill Next, no toast. The first run (`device/`) showed RC 9.x `Configure` NRE aborting kit init once the real key was set — fixed (configure after `Purchases.Start`) |
| Onboarding: no toast over the pager, sim paused, first warning held | PASS (code + frames) | `VoltCityMain.IntroOpen` / `ShowToast`; `after/*_01_pager.png` has no toast (before: "Old Harbor switched off" over page 1) |
| Pill radius (UI Toolkit scales an oversized radius per axis) | PASS | `--radius-pill` 999 → 42 px; bars / handle / chips / badges own half-height radii; `after/*_01_pager.png` Next button |
| Safe area uses the top / bottom bands | PASS | HUD background runs under the status bar, tab bar under the nav bar, pager hero full-bleed |

## Before / after (1080 x 2340)

| Item | Before | After |
|---|---|---|
| City crisp + bright (Low) | `before/low_en_03_hud.png` (orange exponential fog over the whole diorama, 360 px-effective curtain) | `after/low_en_03_hud.png` |
| City (Medium) | `before/medium_en_01_pager.png` | `after/medium_en_03_hud.png`, heroes `after/hero_1..3.jpg` |
| Next button ellipse | `before/low_en_01_pager.png` | `after/low_en_01_pager.png` |
| Toast over onboarding | `editor-bringup-1028/smoke2/after_monkey.png` (device) | `after/low_en_01_pager.png`, `device/smoke/*.png` |
| Dark top band | `before/low_en_01_pager.png`, `before/low_en_02_guide.png` | `after/low_en_01_pager.png`, `after/low_en_02_guide.png` |

What made the city murky / blurry, and the fix:
1. **Exponential fog** at a top-down camera 800–2,600 m away hazed every pixel (dusk: pink-orange) → **linear fog that
   starts at 1.3 × the view distance** (`VcLightCurves.SetViewDistance`, called by the rig on a distance change only).
2. **Camera fit** centred for the unclamped distance and clamped afterwards: a small band aimed the camera off the
   city → centred at the clamped distance (`VcCameraFit.Fit`).
3. **UI textures** (curtain, splash, icons) were mipmapped and obeyed the Low tier's `globalTextureMipmapLimit = 1`:
   the 720 px curtain drew at 360 px → UI textures: no mips, `ignoreMipmapLimit`, bilinear, CompressedHQ
   (`VcArtImport`). The pager now shows **real hero frames** from the running loop (Medium, 1080 x 2340, ~205 KB each).
4. The tab sheet took 42 % of the screen and left a 20 % city band → 36 %.
URP settings were already right (Medium/High render scale 1, MSAA 2/4; Low 0.85, MSAA off — the fleet policy keeps it).

## Open toward release
- The 10-minute play is the bot's; a human 10-minute play with notes (W66) is still owed before production.
- Frame times above are editor numbers; device frame time / vitals come from Play's pre-launch report after the first
  internal AAB.
- Two pager hero frames are close in look (same dusk moment, 12 s apart): a night / storm capture would vary them.

## Release device pass (2026-10-28, Patron release agent, 1.0.0 vc1793218190 = the internal AAB)
Smoke `smoke_android.sh` 200 s + 300 monkey **PASS** (0 Unity errors, 0 FATAL/ANR; the real rewarded unit loads), frames
`/Volumes/BuildSSD/game-builds/smoke/volt-city-1.0.0-vc1793218190-20261028-2341/`. Real adb taps
(`/Volumes/BuildSSD/game-builds/volt-city/release-1.0.0-sdk36/taps/`, 1080 x 2400 emulator, Low): intro Next 1/3 → 2/3 → 3/3,
Start, graphics OK, guide step 1 → 2, needle, Skip guide, Crews tab, Shop tab — each fired **once**; nav-bar frame
(gesture nav) clear of HUD / tab bar / house banner; no Unity splash or logo. **Open before production** (S2):
1. The plant row keeps `22 / 45 MW · Set to 5/10` after `+` (supply does rise) — the Grid list row does not refresh.
2. While a guide tip waits, the sim clock stops, so the queued `+` order is not applied and the tip asks "Tap + again".
3. The needle tip card ("Supply … Keep the needle in the green band") stays on screen after Skip guide.
4. At night (00:49) the Grid tab lists no plant and supply reads 0 MW (Old Smoky gone from the list) — check sim vs list.
5. Shop with billing unavailable shows only the header: no product rows and no "store unavailable" line.
6. Copy: "1 trucks free" (plural). Low tier at dusk: the river reads brown-orange (`taps/small_06_guide.png`).

## #170A / #171 / #172 (S2, 2026-10-29) — device-pass fixes, layout gate, plurals, copy gate, a real play

Evidence: `/Volumes/BuildSSD/game-builds/volt-city/q170/` and `q172/`.

| Finding | Cause → fix | Test / frame |
|---|---|---|
| 1 Grid row kept "Set to 5/10" after `+` · 4 Grid tab empty at 00:49 | the list's "finger down" counter leaked when a pill's button captured the PointerUp → the list never refreshed again in that session. Pointer ids, ups caught at the panel root, 3 s stale guard (`UiKit.List`) | PlayMode `ListHoldTest`; `q172/play/turn_003.png` (row "→ 27 MW in 6 s" under the tip) |
| 2 guide tip asked "Tap + again" | orders queued while the clock is paused never applied, so the assist's projection missed them → applied at once while paused (`Simulation.ApplyPendingNow`, same step index = replay unchanged) | EditMode `PausedClock_PlayerOrderStillLands` |
| 3 needle tip stayed after Skip guide | the explainer was a toggle → closes after 8 s, on Skip and on guide end | code + `q172/play` frames |
| 5 Shop showed only its header without billing | no line when the store has no prices → "The store is not available right now." + Try again (`shop:retry`) | EditMode `ShopTab_BillingUnavailable_SaysSo_WithRetry`; `q170/heroes/low_en_04_tab_shop.png` |
| 6a "1 trucks free" | → real plural rules: `Loc.N` + CLDR one / few / many / other, `count.*` keys × 10 languages ("1 fault", "3 поломки", "5 поломок") | EditMode `Counts_PluralRules_TenLanguages`; report `q172/play/turn_028.png` ("21 dark minutes · 1 fault · 0 sheds") |
| 6b Low dusk river brown-orange | per-vertex diffuse took the orange low sun's hue → water takes its brightness, not its hue (Low only, a few ALU) | `q170/heroes/low_en_03_hud.png` vs `release-1.0.0-sdk36/taps/small_06_guide.png` |
| guide bubble hid the fault card | bubble went right above its target inside the sheet → above the whole sheet | `q172/play/turn_007.png` |
| #172a white pill by the clock | the capture tools composited two textures and washed out the translucent chip; the game was right (device frames) → tools render camera + UI into one texture | `q172/play/turn_002.png` ("Evening peak") |
| #172c "Stadi/um/Row" (German tablet) | a chip + a long pill squeezed the name → name ≥ 40 %, the side drops to its own line; the layout gate now flags word-splits | `q172/layout5/3x4_de_hud.png` |
| #172d "free power when windy" | → "cheap power" / "no fuel cost" / "without paying" / "ready trucks" in 10 languages; `Tools/copy_gate.py` in check.sh (10 languages, "ad-free" allowed) | `COPY_GATE: 0 hits` |
| close chip ✕ was a box | the font subsetter did not scan the kit's C# → scans it; ✕ in Symbols 2 | `q172/play/turn_007.png` |
| "→ 40 MW in 0 s" | → "Ramping to 40 MW" in the last half second | code |
| 4 "Tap + again" tips in a row (play) | the assist asked one tap at a time, each stopping the clock → one tip with the count ("Tap + on Old Smoky 2 more times", plural × 10) | `q172/play/state_004.txt` |

**P5 layout gate** (`Tests/PlayMode/VoltLayoutTest.cs`, in the PlayMode suite): 10 languages × 4 aspects (9:16, 9:19.5,
9:21, 3:4 tablet), pager 1–3, graphics, 5 tabs half + full, Settings, Language; checks off-screen, clipped (incl.
ellipsis), cut, word-split and stale-after-language-switch: **640 screens, 47,283 texts, 0 problems** (first run: 7,197 —
card lines ended in "…"). Frames `q172/layout5/<aspect>_<lang>_{pager,hud}.png`.

**Pager heroes**: page 1 day, page 2 storm, page 3 night (`q170/heroes/hero_1..3.jpg`, from the running loop on Medium).

### 10-minute play (S2 played it, turn by turn in the editor: `Tests/PlayMode/VoltPlaySession.cs`, `q172/play/`)
Fresh career, Medium, 44 turns, 594 s of game time, 0 errors in the log.
- Pager → graphics → guide: clear; the steps read well; the dim + ring show the one thing to tap.
- Step 1 + assist: short 11 MW needs 3 `+` → was 4 separate "Tap + again" tips each freezing the clock (**fixed**: one tip
  with the count). Under a tip the row now shows "→ 27 MW in 6 s" (finding 2 fixed, seen live).
- Step 3/4 fault: the bubble covered the card (**fixed**, above the sheet now); the card reads "3,800 homes dark · storm",
  the truck row, Send.
- First shift 2★ (lights 96.6 %), profit $884; the report reads well, plurals right.
- "Too much power. Tap −" at +4.6 % surplus in the night felt fussy but follows real demand drops — kept.
- Speed button in the guided first shift does nothing and says nothing (open: a "after the guide" toast).
- Steps 6–7 build line + gas peaker: clear, prices + lines readable; step 8 peaker at the evening peak: good moment.
- Open: step 9 (tap Hilltop) not reached by the harness (a card target, not a named element); en "1 more time" is
  correct but plain.

## #173 (S2, 2026-10-29)

| Item | Result | Evidence |
|---|---|---|
| White pill by the HUD clock | **Not in the game.** The real 1.0.0 release build on the emulator (Patron device pass) shows the "Evening peak" phase chip; the pill was the capture tool compositing two textures (fixed 10-29: camera + UI in one texture) | `release-1.0.0-sdk36/taps/16_plus_free_t17.png` vs `q172/after/low_en_04_tab_city.png` vs `q172/play/turn_002.png` |
| Speed taps in the guided shift | answer: the pill pulses + toast "Speed-up unlocks after the guide." (10 languages); outside the guide a change pulses the pill | `q173/play/turn_003.png` |
| City "$2,000" on grey | not possible yet = a status chip (lock icon + the reason: "Between shifts only" / "Not enough cash yet" / "City rating too low"), the price in the line; possible = the active price pill | EditMode `CityHookup_SaysWhyNot_OrOffersTheActivePrice`; `q173/play/turn_045.png` |
| Volt soak | in check.sh (game EditMode suite, dotnet + Unity): 30 game min, seeded fuzz (33,770 actions incl. junk ids), 5,705 tab renders, 10 save -> fresh-session reload cycles (hash + cash equal), 0 exceptions, needle bounded | `GameTests.Soak_ThirtyGameMinutes_RandomFuzz_SaveLoadCycles` |
| Play tool taps list cards | `target <id>` (resolved by `GameScreen.FindTarget`, a real pointer down / up) | `VoltPlaySession.cs` |

### Play 3 (guide to the end + after it), `q173/play/`, 78 turns, 1,182 s game time, 0 errors
- The guide ran to step 9 ("Next goal: Hilltop") through the real controls (the Hilltop card via `target city.hilltop`).
- The tap-count assist works: "3 more times" -> "2" -> "1" as taps land.
- **Bug found + fixed:** with every district dark demand is 0 and `NeedlePm` exploded (9,496,000 ‰; the Grid tab line
  would read "+949,600 %") -> clamped to +-100 % in the sim (every threshold is inside; sim 27/27, balance gates green).
- **Feels bad, fixed:** after the guide, a night demand drop left +10-27 % surplus; a running plant trips after 20 s over
  8 % and the city went dark — 3 post-guide shifts in a row ended 1 star with only the red needle as a signal. Now a
  warning toast halfway to the trip, on every tab ("Too much power: a plant trips soon. Turn it down on the Grid tab."),
  at most every 30 s, 10 languages.
- My post-guide turns ran 12 s blind (a harness pace, not a player's); a person reacting to the warning keeps the
  stars — to re-check in the next play.

## #174 (S2, 2026-10-29)

**Store set `voltcity-play-2026-10-29-fb`** (`/Volumes/BuildSSD/store-assets/voltcity-play-2026-10-29-fb/`, NOT uploaded):
10 locales x (8 phone + 8 tablet-7 + 8 tablet-10 + feature) = 250 files, `fullbleed_validate.py` **FULLBLEED_PASS** (min game
cover slides 0.880 / feature 0.868, glyphs ja / ko / hi / ru PASS). Sheets `sheets/sheet-<locale>.jpg`. C3b icon in the bands
+ feature; feature title = each listing's localized store title (two lines at the colon when long). Raws:
`voltcity-play-2026-10-29-fb-raw` (Medium, earned states, timed slides 18:08-19:26 = dusk). Capture: camera + UI in one
texture (no white pill: checked on 01-peak / 03-balance / 07-shed in en + de). Behind a modal sheet (04-forecast, 08-report) the
shared texture came back flat; `ScrimProbe` showed the camera draws the city there (frame `q174/scrim/d_open_camera_only.png`),
so those two slides composite the camera's own frame under the scrim's real alpha (0.42) — documented in the test.

**turn_045 bugs**: (a) two drag handles — the tab sheet's handle is hidden while a modal sheet is open. (b) black map behind a
card — not the night: probe frames a/b/c (scrim made transparent: still flat) vs d (camera alone: the city); the card scrim
is lighter now (0.62 -> 0.42) and night has a moonlight floor (moon .42 -> .56, ambient +~0.05; constants, 0 draws, Low
same cost). Open: a device frame with a card open (the editor's shared-RT render is not proof of the device look).

**Warning toast vs stars** (`q174/play_before`, `play_hints`, `play`; a player reacting only to the game's own toasts /
assists, 4 s per reaction, guide skipped):
| run | change | stars (4 shifts) | lights on |
|---|---|---|---|
| 1 | oversupply warning only | 1 1 1 1 | 22-45 % (districts the grid shed stayed dark all shift) |
| 2 | + "X is dark. Tap + on Old Smoky to bring it back" (Grid-tab assist + toast elsewhere), warning at 1/4 of the trip | 1 1 1 1 | 11-80 % |
| 3 | + balance: shed_after_s 10 -> 18, over_trip_after_s 20 -> 35 (sim 27/27 incl. balance gates) | 1 **2** 1 1 | 75-99 % |
Remaining 1-star shifts lose 2-3 districts each to sheds when demand ramps faster than one "+" per 4 s; to re-check with a
human play before more tuning (2 stars need 95 % lights on, 3 stars 99 %).

## #174b (S2, 2026-10-29)
- **Slide 04 "Heatwave ahead"**: the map under the Forecast card is the camera's own frame under the scrim's real alpha in
  all 10 languages (map-band brightness 34-38 vs ~10 when black); the earlier report's contact sheets were stale (made before
  the fix) — regenerated, plus `sheets/check-04-forecast-10-languages.jpg`.
- **Dusk sea**: purple / brown in the game itself (water mirrored the orange horizon, took the orange sun's hue, pink dusk
  fog) → water mirrors the sky above, takes the sun's brightness not its hue on every tier, dusk fog mauve-blue (constants +
  ALU, 0 draws, Low same cost). Store set re-shot in place: `voltcity-play-2026-10-29-fb`, 250 files, FULLBLEED_PASS.
- **Stars** (owner decision 2 >= 88 %, 3 >= 96 %; shed 18 s, trip 35 s kept). Measured with headless players, shifts 1-6 after
  the guided one, 3 seeds (`GameTests.Stars_CarefulThree_TypicalTwo_MistakeOne`, in the suite):
  | player | lights on | stars at 88 / 96 | stars at 88 / **98.5** |
  |---|---|---|---|
  | careful (operator every 1 s) | 99.9-100 % | 3 x 18 | 3 x 18 |
  | typical new (looks every 4 s, 2 in 3 times, one tap when the needle is red, follows the restore tip) | 95.8-100 %, median ~98 % | 3 in 17 / 18 (misses "typical = 2") | median **2**, mean 2.4, never 1 |
  | mistake (away 60 s at each peak) | 36-99 %, mostly < 88 % | median 1 (13 / 18) | median 1 (13 / 18) |
  Final: **2 stars >= 88 %, 3 stars >= 98.5 %** (balance.json `stars.two_pm` 880, `three_pm` 985). No player text names the
  thresholds (report shows "Lights on N %" + the stars), so no string change.

## #183 (S2, 2026-10-30) — bot play: 3 profiles x 6 shifts x 3 seeds, first 10 minutes

Tool: `Tests/Game/PlayDiagnostics.cs` (explicit; per-shift events with real-time minute at 1x, UNFAIR + BORING detectors,
first-10-minute pacing). Reports `/Volumes/BuildSSD/game-builds/volt-city/q183/diag1.txt` (before) · `diag-final.txt` (after).
Profiles: **cautious** = the balance gates' operator every 1 s · **typical** = looks every 4 s, 2 in 3 times, one tap when the
needle is red, sends a crew half the time, follows the restore tip · **reckless** = looks every 2 s, two taps at a small
offset, toggles the peaker, sheds by hand when short, never restores.

**Stars (shifts 2-7 after the guided one, seeds 3 / 7 / 11), final:**
| profile | sh 2 | sh 3 | sh 4 | sh 5 | sh 6 | sh 7 | mean | lights on |
|---|---|---|---|---|---|---|---|---|
| cautious | 3/3/3 | 3/3/3 | 3/3/3 | 3/3/3 | 3/2/3 | 3/3/3 | 2.94 | 99.8 % |
| typical | 2/3/3 | 2/2/2 | 2/2/2 | 3/2/3 | 2/2/3 | 2/2/2 | 2.28 | 97.5 % |
| reckless | 1/1/1 | 1/1/1 | 1/1/1 | 1/1/1 | 1/1/1 | 1/1/1 | 1.00 | 13.9 % |
Thresholds kept (2 stars >= 88 %, 3 stars >= 98.5 %): the data matches the targets (cautious 3, typical 2 never 1, mistakes 1);
the suite's `Stars_CarefulThree_TypicalTwo_MistakeOne` still passes with the livelier events (typical now sends crews too).

**Unfair / boring moments found (shift, minute at 1x, cause):**
| # | kind | where | cause | status |
|---|---|---|---|---|
| 1 | BORING | first ~30 min: shifts 2-6 of chapter 1 had **0 incidents in 18 / 18 shifts** (only the demand curve); first 10 min = 0-1 events per minute | `events.json` caps: no random event for 3 shifts, chapter-1 chances halved (x0.5) → ~17 % per shift with one steam plant | **fixed (constants)**: `tutorial_shifts` 3 -> 2, `ch1_scale` 0.5 -> 2.0 (gentle ones only: squirrel 25 % partial / 6 s, balloons line / 4 s, steam trip); now 5 / 18 shifts carry one; sim + balance gates 27/27 |
| 2 | BORING | 8 of 18 cautious shifts, minute 0.4-2.2 (game 08:00-15:00), 66-89 s with nothing to decide | the mid-day plateau of the demand curve | **fixed (pacing feedback)**: one-time hint at a quiet mid-day moment at 1x — "Quiet hours: tap 1× at the top to run the shift at 2× or 3×" (10 languages); **open** proposal below |
| 3 | UNFAIR | cautious seed 7 shift 6 (balloons line fault): 3 -> 2 stars although the crew was sent at once | the dark minutes while the truck drives count against 98.5 % | **open** — proposal below (not changed silently: scoring) |
| 4 | UNFAIR-feeling | reckless every shift: 13.9 % lights on, 1 star | hand-shed districts stay dark all shift (manual shed = player intent, never auto-restored) + double taps tripping plants | **fixed (feedback)**: toast on any tab when a hand-shed district could be powered again — "{district} is still switched off and there is power for it now. Restore it in the City tab." (10 languages); the earlier over-power warning (#173/#174) covers the trips |
| 5 | (checked) | reckless seed 11 shift 2 min 1.6: Riverside shed 7 s after a plant trip | its own over-supply trip | not unfair (self-inflicted); cautious / typical never shed within 10 s of a cause |

**First 10 minutes (typical):** guided shift (4 min, every step taught, 11-13 decisions), shift 2 (5-12 decisions), shift 3
starts — clear and readable, but too quiet before #1: no incident until minute 8-9 on 2 of 3 seeds. With #1 a gentle fault can
now land in shift 3 (minute 8-12).

**Open (proposals, need the owner / a bigger change):**
- **A. One gentle incident per chapter-1 shift** (planner: if no random event rolled after the guided shift, place a squirrel or
  balloons fault at 10:00-15:00) — fills the mid-day lull for real instead of hinting at 2× / 3×; touches the planner + the
  balance gates.
- **B. Fair third star**: count a fault's dark time against the stars only until a crew is dispatched (or beyond its normal
  repair time) — a flawless player then keeps 3 stars through an unavoidable fault (#3).
- **C. Reckless recovery**: after a hand shed, auto-restore once there is room for 30 s (today manual sheds wait for the
  player by design; the new toast is the soft version).

## #183-2b (S2, 2026-10-30) — owner decisions A + B (C: no auto-restore, the toast stays)

- **A. One gentle incident per chapter-1 shift** (from shift 3, i.e. after the 2 tutorial shifts): `ShiftPlan.GentleGuarantee`
  places a squirrel (25 % of a district, 6 s repair) or balloons (line, 4 s repair) at 10:00-14:59 when no gentle incident was
  rolled; own random stream (other draws of a plan unchanged), counts towards `caps.max_negative`. Shifts carrying an incident:
  5 / 18 -> 17 / 18; boring stretches (>= 60 s nothing to decide, cautious) 8 / 18 -> 2 / 18.
- **B. Fair third star**: `Grid` no longer counts the demand a crew is already on — a district's dark part through an open
  transformer fault with a crew, a line-fault outage once every open line fault has a crew. Dark time before the dispatch
  still counts (a late crew costs as before).
- **No sudden death (found while measuring A)**: the over-supply trip used to take any running thermal unit — in chapter 1 the
  only one, blacking out the whole city for 30-90 s (typical player 29-41 % lights on in the shifts with a guaranteed
  incident). Now the ONLY running unit **runs itself back** to what the city draws (toast "Too much power for too long:
  {plant} turned itself down to stop a trip.", 10 languages); with 2+ units one still trips.

**Stars after A + B + runback** (shifts 2-7, seeds 3 / 7 / 11; diagnostics `q183/diag-183b.txt`):
| profile | sh 2 | sh 3 | sh 4 | sh 5 | sh 6 | sh 7 | mean | lights on |
|---|---|---|---|---|---|---|---|---|
| cautious | 3/3/3 | 3/3/3 | 3/3/3 | 3/3/3 | 3/3/3 | 3/3/3 | **3.00** (18 / 18) | 99.9 % |
| typical | 2/3/3 | 1/2/2 | 1/2/2 | 3/3/3 | 2/2/3 | 2/3/2 | 2.28 | 95.8 % |
| reckless | 1 x 18 | | | | | | 1.00 | 13.8 % |
Suite test `Stars_CarefulThree_TypicalTwo_MistakeOne` (its own typical / mistake players): careful 3 in 18 / 18 (now
asserted), typical median 2, never 1; **mistake moved: median 2, 1 star in 5 / 18 (was 13 / 18)** — its 1-star shifts were
mostly the one-plant over-supply blackout the runback now prevents. Thresholds unchanged (88 / 98.5).
**Open:** the repair rebound — when a faulted district comes back its demand returns at once; a slow-reacting player can be
shed for ~1 min (diagnostic typical seed 3, shifts 3-4: 83 % lights on). Proposal: the restored district ramps back over
~10 s, or the restore assist fires on the repair event.

## #183-2c — repair rebound (cold-load pickup)

- A repaired district (transformer part or line corridor) takes its load back over ~10 s: 30 % at once, then linear to 100 %
  (`Grid.PickOff`, `DPickAt/DPickPart/DPickProg`, saved as `d_pick_*`; older saves load with no pickup). No tip.
- The pickup advances only while the supply carries it; the relay (auto-shed) clock waits while a pickup settles. The first
  10 s of held load are excused; after that the held share counts as unserved and shows on the needle, so the player sees it.
- Test `PlayDiagnostics.Typical_NoOneStarFromRepairRebound` (runs in check.sh): 0 / 18 typical shifts get 1 star from a shed
  within 30 s of a repair (before: 2 / 18, seed 3 shifts 2-3, ~83 % lights on).
- Star table (diagnostic): cautious 3.00 (99.9 %) · typical 2.17 (96.3 %) · reckless 1.00 (13.8 %). The one remaining
  typical 1-star (seed 3 shift 3) is a line fault left without a crew for 40 s, not a rebound.
