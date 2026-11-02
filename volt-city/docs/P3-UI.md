# Volt City — P3 playable layer (Patron, 2026-10-27) — UNCOMPILED in Unity

> Engine: Unity 6 LTS + URP + UI Toolkit (owner decision 10-27). **The Unity editor is not installed on the Mini yet**:
> everything engine-dependent below is written against Unity 6 APIs and has been syntax-checked (Roslyn, C# 9) and
> semantically compiled against Unity **2022.3** engine reference modules in this session (0 errors) — but never
> imported, run or seen on a screen. Everything engine-free is compiled and tested under .NET 8 (numbers in §6).
> Read with: [PLAN](PLAN.md) P3 · [UI](UI.md) · [DESIGN](DESIGN.md) §3/§9 · [SIM-API](SIM-API.md) · [P2-ART](P2-ART.md) · [UNITY](UNITY.md).

## 1. What is playable "on paper"

A fresh install boots (VoltBoot curtain → City scene) into the 3-page pager, the first-start graphics popup (device class
picks Low / Medium / High), then the guided first shift at 17:30:

1. the diorama at dusk; the needle is short; **step 1** spotlights `+` on Old Smoky (time waits);
2. needle explainer (**step 2**); balance assists keep the spotlight on `+` until the projected supply covers demand;
3. once the needle is back near green a storm spark hits the Riverside line, Riverside goes dark in a ripple (**step 3**:
   tap the spark in the world or its row → fault card), **step 4** "Send crew" → the truck drives the road graph, the
   guide's repair finishes ~2 s after it arrives, Riverside ripples back on, "Riverside is back on!" toast;
4. the shift runs to its end (speed pill 1× / 2× / 3×), **step 5** Collect on the shift report;
5. **step 6** Build tab → Riverside line I ($420), **step 7** gas peaker I ($483), the player starts shift 2;
6. **step 8** in the next deficit / evening peak: Start the peaker; **step 9** City tab → Hilltop goal card → guide done.

After that the full loop is there: 5 tabs (Grid · Crews · City · Build · Shop), live HUD, crew dispatch (tap fault → tap
truck, or "Send crew" = nearest), manual Shed / Restore with Hospital Hill pinned last (+ its 60 s backup countdown),
Demand Response, battery modes, investments with locked reasons, district hookups (chapters), forecast, goals, event cards
with counterplay, shift report → Collect (house interstitial hook from session 2), offline "night crew" summary on resume,
context hints (once each), the first-session review moment (deferred out of the guide, asked at the next own success),
settings (sound, graphics tier, start speed, language picker ×10, replay guide, forecast, restore purchases, privacy,
version), saves (atomic, `.bak`, `.unreadable`, autosave 30 s / shift end / pause). The Shop tab is the entry point only
(P4 fills products; Restore works through the kit).

## 2. Architecture (what lives where)

| Assembly / path | Engine | What |
|---|---|---|
| `Scripts/Game` — `VoltCity.Game` | **none** (noEngineReferences) | `GameSession` (sim + kit `SaveRepository` + autosave + offline + perks) · `GameSettings` (kit `JsonStateFile`: lang, sound, speed, `intro_done`, `coach_done`, `coach_step`, hints, review deferral) · `Loc` (strings.csv, fallback en → key, named placeholders) · `Fmt` (locale separators without CultureInfo) · `Names` · view models `Hud`, `Goals`, `Tabs` (5 tabs), `Cards` (report, offline, event, forecast, district, fault, plant) · `GameActions` (the one action router + `DispatchFlow`) · `Coach` (9 steps + balance assists) · `GuideBot` · `Feed` (event → toast / card / lightning / review / success) · `Hints` · `ReviewHook` · `WorldModel` (layout, sim→art map, change-only diff, `RoadGraph`, `Trucks`, `Traffic`, `Picker`) |
| `Scripts/World` — `VoltCity.World` | Unity | `CityWorld` (P2 `VcCityArt` driver: applies `WorldDiff`, trucks / cars / markers / trees / rotors instancing, light curve moment, lightning, tap → ray → `Picker`, fault screen rect for the spotlight) · `CameraRig` (fit hooked districts into the free band, clamped pan, pinch / wheel zoom, never pans from a UI press) |
| `Scripts/World/Editor` — `VoltCity.World.Editor` | Editor | `VoltCitySceneBuilder`: builds `Assets/Scenes/City.unity` + the 5 tier materials from P2's shaders, registers build scenes. Menu **Coddy › Volt City › Build City Scene** |
| `Scripts/UI` — `VoltCity.UI` | Unity (UI Toolkit) | `VoltCityMain` (scene entry: kit hooks, frame loop, events, navigation, settings / language / graphics sheets) · `GameScreen` (HUD 2 rows, needle, goal line, half/full tab sheet, tab bar, coach wiring) · `RowList` (reconcile by row id, no rebuild under a finger) · `SheetHost` / `CoachOverlay` / `Pager` · `NeedleView` (dial drawn once, needle rotated on change) · `UiParts` (icons, one-tap gesture, stepper / segmented side controls) |
| `Resources/i18n/strings.csv` | data | 390 keys × 10 languages (en tr de es fr pt ru ja ko hi) |
| `Resources/ui/VoltCity.uss`, `Resources/ui/icons/*.png` | data | game styles over the kit theme (design 2 tokens); 34 generated white glyph icons (`Tools/gen_ui_icons.py`, generic shapes only) |
| `Art/Runtime/VoltCity.Art.asmdef`, `Art/Editor/VoltCity.Art.Editor.asmdef` | — | **integration fix:** P2's art had no asmdef (Assembly-CSharp) and asmdef assemblies cannot reference it (UNITY.md §1 asked for this) |
| `Resources/data/roads.json` | data | **integration fix:** regenerated from the art layout (§4) |
| `Tools/road_graph.py`, `gen_ui_icons.py`, `strings_merge.py`, `game_dotnet_check.sh`, `cs_parse_check.sh`; `check.sh`, `check_unity_sources.py` | tools | new P3 gates; `check.sh` runs them; the source gates now also scan the P3 folders |

Rules kept (UI.md / games/CLAUDE.md): kit design 2 (page header + ONE summary line, list cards with ONE side control,
section overlines, empty card, round close chip, bottom sheets, pill handle); Low tier = kit flat variants (`UiKit.Low`:
no icon tiles, pills → coloured text, flat HUD); every edge from the kit `SafeArea` (`SafeAreaPadding` on the screen, the
pager; sheet panels pad by `SafeArea.Bottom` + banner); house banner = kit `HouseBannerView` with `ReserveSlot` and the
**max rule** (content bottom = max(nav bar, banner)), allowed only after pager + guide, never over the shop / a sheet /
the pager; one tap = one action (kit buttons via `clicked`; non-button elements via `TapGesture` = one PointerDown + one
PointerUp registration through the kit `TapFilter`, release-only, < 12 dp / < 400 ms; world taps through the kit
`TapInput`); labels change only when their text changes; lists reconcile by id at ≤ 4 Hz and wait while a finger is down;
no `PlayerPrefs`, no legacy input, no `Screen.safeArea` outside the kit (source gates G3-G5 now scan P3).

## 3. World: sim → art

- **Corridors:** sim `c_harbor..c_airport` → art 0-7 (`ArtMap.Corridor`, test: every sim corridor's district = the art
  corridor's end district); plant ties 8-13 = built with their plant (flow = output share); loops 14 (east, with Tech Park)
  and 15 (west, with Stadium Row). States: normal / overheat (`CHeat` ≥ warn) / down (fault or tripped) / not built.
- **Districts:** lit = hooked and (between shifts, or `Grid.IsLit` and < 50 % dark behind a transformer). Written ONLY
  on change (`WorldModel.Sync` compares 8 bools + 16 small int pairs + level / output tenths + signatures); the first sync
  sets states without the ripple, later changes ripple (P2's 0.6 s coroutine). Smart substations: tie → both endpoint
  districts' substation becomes the smart model.
- **When it syncs:** when the sim step advanced or events arrived — never on an unchanged frame. Trucks and cars: per
  frame on Medium/High, **15 Hz on Low**; trees and rotors written once (Low: every second tree); markers on change.
- **Light curve:** day 6:30-17:30, dusk 17:30-19:30 / 5:00-6:30, night otherwise, storm while a thunderstorm runs (+
  lightning flash on storm sparks). Battery / reservoir globals in twentieths.
- **Trucks:** `Trucks` reads each crew's state / node / `Until`; a leg = road-graph shortest path, start = `Until −
  TravelSteps` (the sim owns the arrival step), position by fractional sim step (`Step + AccUs/StepUs`), progress never
  goes backwards, a retarget starts from where the truck is (projection onto the new path). Test: the visual path length =
  the sim's road distance; the truck is at the fault exactly when the sim says it arrived.
- **Picking:** ray (camera through the tap) → fault spheres (30 m) first, then built plant sites (48 m), then district
  boxes. Guide step 3 accepts the spark or its Crews row.

## 4. Road graph on the art layout (P1 open item #2) + balance re-run

`Tools/road_graph.py` builds `roads.json` from the 13 art road polylines (Douglas-Peucker 3 m, road ends snapped onto the
road they touch ≤ 30 m) and inserts the sim's named nodes on the road: `d_<x>` next to each district substation, `l_<x>` next
to the middle of the district's art corridor, `depot` at the Riverside junction (-55, -30). 42 nodes, 44 edges, `--check`
in check.sh. The art city is about half the size of P1's abstract 1000 × 1600 plan, so trucks arrive sooner:

| depot → | P1 abstract | P3 art layout |
|---|---|---|
| l_riverside / d_riverside | 6.5 s / 9.4 s | 0.9 s / 0.5 s |
| l_harbor / d_harbor | 8.7 s / 12.3 s | 4.1 s / 3.2 s |
| l_hilltop / d_hilltop | 11.1 s / 14.3 s | 4.5 s / 6.6 s |
| l_tech / d_tech | 7.5 s / 9.5 s | 5.6 s / 7.3 s |
| l_airport / d_airport | 10.1 s / 14.4 s | 7.2 s / 10.6 s |

Balance quick gate re-run (`sim_dotnet_check.sh balance quick`): **BALANCE PASSED**, every gate ok — day 7 max 27 / min 26
of 50, everything owned day 26 (100 %), longest purchase gap 1 day, idle cash p95 0.99 day, premium day 7 max 31 / all at
day 22, gas-only / battery-only never finish (mixed bot shift 114, was 115), dispatcher 115, renewables-first 156 / saver 91,
never-shed ≥ 2★ 98.7 % (was 97.1), naive coach 92.6 % (was 92.1), plant matrix wins wind 62 · solar 37 · hydro 21 · gas 8 ·
battery 7 · steam 2. Sim tests 26 / 26 pass; the cross-process determinism hash moved with the data fingerprint:
`hash 77` = `790bb67caee5f745:681` twice (was `cbaffcf6f8dacb01:665`).

## 5. Guide (DESIGN §9) — implementation decisions

- One action per step, advances only on that action after the step was shown; time stops while a step waits; the guide's
  shifts run `TutorialSafe`; Skip always works; Settings → Replay guide (pager + coach) works in any career state (steps
  whose moment passed / item already owned are skipped); a relaunch resumes at the persisted step and re-derives it
  (missing spark → re-scripted; passed phase → skipped). The step's tab is opened for the player.
- Taps outside the spotlight never act and never stall: each physical tap = ONE wrong tap (counter + ring flash); the
  spotlight target and Skip always work.
- **Balance assist (new, Patron decision):** P1's naive-coach gate assumed the coach keeps pointing at `+`/`−` while the
  needle is off; a single "tap + once" step left the newbie at 93-94 % uptime (1★). Between steps the spotlight now asks
  for exactly the taps the projected supply needs (never a tap already on its way, never "turn down" while a district is
  dark). After the guide it continues in chapter 1 as a soft ring (no dim, no pause, no gate).
- **Guide spark:** scripted once the needle is back near green (≤ 20 s after step 2), mended ~2 s after the truck arrives
  ("time runs only for the truck" — an instant repair command on the guide's own fault only).
- **Newbie audit** (EditMode, bot taps only the highlight, 5 seeds): guide done in **248-359 s** of real play (< 10 min),
  shift 1 **2★** every seed (uptime 95.9-97.0 %, profit $882-893 ≈ DESIGN's $930), 0 wrong taps, 7-15 assist taps.
- Review (games/CLAUDE.md §12): the sim's first-session moments both fall inside the guide → deferred (persisted) and
  handed to the kit ReviewGate at the player's next own success after the guide (repair, hand restore, ≥ 2★ shift,
  hookup); never with a sheet / ad open (pending up to 60 s). Test: exactly one ask in the first session, never in the guide.

## 6. Tests and checks run (all on the Mini, .NET 8, no Unity)

| Check | Result |
|---|---|
| `Tools/check.sh` | **VOLT_UNITY_CHECK: PASS** (Unity EditMode step skipped: no editor) |
| CoddyKit.Core tests | 47 / 47 |
| P1 sim tests | 26 / 26 (with the new road graph) |
| P3 game tests (`Tests/Game/GameTests.cs`) | **28 / 28** — strings (CSV parser, 390 keys × 10 languages filled, placeholder parity, every key the code uses exists, $420), Fmt, HUD needle bands, a 14-shift scripted career rendering every tab + card in all 10 languages with no raw key / unfilled placeholder, tab content rules (Hospital never sheddable, shed order, locked rows keep their reason), crews sort + tap-fault-tap-truck dispatch, action router, guide (newbie ×5 seeds, wrong tap once, skip + replay, relaunch mid-guide), review hook, saves (relaunch mid-shift = same hash + identical future, broken main → `.bak` + `.unreadable` + `save_unreadable`, autosave 30 s, offline on resume), world (layout + art map, change-only diff incl. fault ripple and relight, road graph = sim distance, trucks monotone and on time, picking, traffic) |
| Balance quick gate | PASSED (§4) |
| Source gates (now incl. P3 folders) | 0 failures |
| IP denylist + harm check (i18n) | 0 matches, proof caught all, harm texts 0 |
| C# 9 syntax, 88 files, 2 define sets | 0 errors |
| Semantic compile vs Unity 2022.3 reference modules (session only: third-party redistributed DLLs, Input System stubbed) — Sim, CoddyKit Core + Runtime, App, Art/Runtime, Game, World, UI | **0 errors**, 0 warnings outside P2's JsonUtility DTOs |
| Strings | `strings_merge.py --check`: 390 × 10, 0 problems; translations by in-session sub-agents (9 Haiku translators + 1 for the assist lines; no external MT); fr/pt/es "gas peaker" names corrected by hand |

**UNCOMPILED / never run** (needs the editor): `VoltCity.World` + `World.Editor` (scene builder, materials), `VoltCity.UI`
(all screens), `VoltCity.Art(.Editor)` asmdef wiring, the USS (`VoltCity.uss` + kit), the PlayMode smoke
(`Tests/PlayMode/CitySmokeTest.cs`), Unity-6-only API differences (2022.3 was the closest reference), the Input System
paths (stubbed in the compile check), URP shaders (P2), UI Toolkit runtime behaviour (touch scroll over pills, `Painter2D`
dial, `style.rotate`, `-unity-text-generator: advanced` for Hindi).

## 7. First things to do when the editor is installed (in this order)

1. UNITY.md §3 steps 1-5 (open from a worktree, `VoltProjectSetup` runs, accept package versions).
2. Let the importer run `VcArtImport` on `Assets/Art/Models` (check the log for axis fixes / errors).
3. **Coddy › Volt City › Build City Scene** (or `-executeMethod VoltCity.WorldEditor.VoltCitySceneBuilder.Build`):
   expect `CITY_SCENE_OK`; it creates `Assets/Scenes/City.unity` + `Assets/Art/Materials/*.mat` and registers Boot + City.
4. Fix compile errors (expected spots: Input System `ETouch`/`TouchPhase` names, `PointerType`, `Painter2D.Arc` signature,
   `ScrollView.ScrollTo`, `Shader.SetGlobalFloatArray` array sizes) — fix in place, note in this file.
5. EditMode: `-runTests -testPlatform EditMode` → Sim 26 + Game 28 + Core 47 + App tests must pass inside Unity too.
6. PlayMode smoke: `-runTests -testPlatform PlayMode` → `PLAYMODE_SMOKE_PASS` (fresh career, pager, graphics popup,
   guide by bot, tab probe ≤ 100 ms, relaunch).
7. Play it on the Low tier in the Game view (720 × 1280 and 720 × 1560, then 16:9 / 20:9 / 4:3) and with the kit's
   `CODDY_SAFE_SIM` fake 48 dp nav bar + cutout; profiler: Low draws ≤ 17 + UI, no per-frame scripts beyond input and the
   5 instanced submits; capture en + de frames of every screen (UI.md §7) for the ≥ 8.5 score; record the per-district
   material vs MaterialPropertyBlock decision in ART-IDS.
8. Commit everything the editor generated (`.meta` files for ALL P1/P2/P3 assets, scene, materials, PanelSettings).

## 8. Open items (P3 → later phases)

| # | Item | Owner |
|---|---|---|
| 1 | Everything in §6 "UNCOMPILED" + the per-screen UI score ≥ 8.5 (en + de frames) and layout test 16:9 / 19.5:9 / 20:9 / 4:3 | P3 finish (editor) |
| 2 | Onboarding hero frames: the pager loads `Resources/ui/onboarding/hero_1..3` (bot-captured from the real loop, ≤ 1 MB, manifest) and falls back to the P2 curtain until they exist — capture with the editor; then make a missing file a test failure | P3 finish |
| 3 | Pager swipe (Next / Skip buttons only today); grab-handle drag (tap toggles half / full) | P3 finish |
| 4 | District priority **reorder (↑/↓)** from UI.md §3 needs a sim command + save field; v1 shows the fixed shedding order (Hospital Hill pinned last) | P1 owner / Patron decision |
| 5 | Trees on Medium: 556 layout trees > 511 instances per batch (`VcInstancedProps.MaxPerBatch`) → 511 drawn; split into 2 batches (+1 draw on Medium only) | P2 |
| 6 | Shop products, rewarded placements (offline ×2, shift bonus, emergency import, crew overtime), interstitial verification, RC / AdMob ids | P4 |
| 7 | Audio (`settings.music` / `effects` stored; `Feed.Sfx` ids emitted, no player yet) | P4 |
| 8 | Fonts: Noto subsets for CJK / Cyrillic / Devanagari in the PanelSettings text settings; Hindi shaping test (UNITY.md §4) | P5 |
| 9 | Translation review pass (one term per concept per language, lengths on device); "peaker" terms in es/fr/pt event tips | P5 |
| 10 | Dispatcher assistant switches (`Simulation.SetAuto`) have no UI row yet (the assistant is bought in Build; it runs "on") | P3 finish / P4 (paid `dispatcher`) |
| 11 | Late-game plant upgrades pay back slowly (P1 open item 3) — watch in the first long play sessions | Patron |
