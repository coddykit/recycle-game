# Borderline Empire (smuggler) - quality pass 2026-10-18

Program: `i18n-work/quality-queue/PROGRAM.md`. Branch `quality/smuggler-2026-10-18`. Godot 4.7.2, Apple M4 (GL Compatibility).

## 1. Audit result (before)
| Rule | State at start |
|---|---|
| No Godot logo | `boot_splash/show_image=false`, but the game started in `main.tscn` directly: no loading curtain, a long white-less black gap while Main (~420 ms) + GLBs built |
| Graphics tiers (phone Medium / weak Low + one-time popup) | already present (`graphics_policy_test` 40 checks, `graphics_notice_test` 6 checks green) |
| Guided onboarding | hand + bubble + highlight ring existed, **no Skip button** |
| Frame cap | none (a 120 Hz phone redrew the scene twice as often) |
| Android renderer | `gl_compatibility` (project.godot + mobile override) already set |
| Free trial | no trial wording anywhere (grep + lifecycle test) |
| Languages | 10 x 563 keys complete; UI text smaller than 11 pt in places (labels of 16-20 units) |

## 2. Fixes
1. **Boot scene + real loading curtain** (`scenes/boot/boot.tscn`, `scripts/boot.gd`, new `run/main_scene`): continues the native launch colour, fades in the game's own icon, real progress bar fed by `ResourceLoader.load_threaded_*` for main.tscn + every model GLB, 6 translated gameplay tips, fades once Main is in the tree and 3 frames in a row are < 40 ms (hard cap 20 s). Headless/QA runs skip it. Evidence: `boot/curtain_tr_540x928.jpg`.
2. **Phones capped at 60 fps** (`Engine.max_fps = 60` on Android/iOS/`mobile`; battery on 120 Hz panels).
3. **Onboarding Skip**: "Skip tutorial" button (touch height 88 units = 44 pt) above the speech bubble, calls `TutorialManager.skip()` (`after/coach_step1_en_skip.jpg`, `coach_step1_de.jpg`, `coach_step1_tr.jpg`, `coach_accept_ja.jpg`).
4. **Crash class: UI call into a freed HUD** during scene reload (language switch / reset / prestige): `UIManager._ui()` guard on every proxy (toast, modal, float_text, coin_burst, open/close panel), `Hud.open_panel` and `Sheet.refresh_now` bail out when not in the tree. The monkey run does 130 language switches + 85 scene reloads + 48 prestiges with 0 script errors.
5. **Text >= 11 pt**: `UIKit.MIN_FONT = 22` canvas units (720-wide canvas, 1 unit ~ 0.5 pt) clamps label/button/pill font size; tab captions use translated `tab.*` keys.
6. **Text overflow in long languages (tr/de/ru/fr/ja)**: contract card (wrap category name, modifier pills in an HFlow, reason label wraps and keeps the button size, ACCEPT/DISPATCH no mid-word break), dispatch vehicle/route buttons (taller, wrap), fleet showroom/upgrade tiles/drivers/hiring board, upgrades (garage chips flow, business "Needs ..." moved from a narrow button to a wrapping line), map region cards, HUD job card wider with ellipsis. Before/after pairs in `before/` + `after/` (e.g. `contracts_de_accept_wrap`, `showroom_ru_overflow`, `dispatch_ru_clipped`, `upgrades_de_tabs_lockbtn`, `vehicle_detail_ru_overflow`).
7. **Untranslated lock reasons**: "Needs <garage level / vehicle kind>" interpolated an English name; now `tr()` of the name (vehicles, business, map depot). Lifecycle test checks the Japanese strings are fully Japanese.
8. **i18n**: 46 new/changed rows (7 region names, 6 curtain tips, 5 `tab.*`, others) x 10 languages by hand; `tools/i18n/validate.py` PASS 609 keys x 10 locales. Extractor knows the Curtain tips.

## 3. Tests (all green)
- `test_runner` PASS (full simulation), `quality_test` 60/0, **`lifecycle_test` 67/0 (new)**: first launch (no save), restart/Continue, background pause/resume, language switch persistence, onboarding Skip, boot curtain, 60 fps cap, Android gl_compatibility, no Godot logo, text >= 11 pt, touch >= 44 pt, no free trial, translations complete.
- `graphics_policy_test` 40/0, `graphics_notice_test` 6/0, `test_release_gate.py` 8 OK, `validate.py` PASS.
- **Text-overflow gate** `tests/tool_layout_scan.gd`: 164 screens (every panel, tab, event modal) x 10 languages = **0 findings** (Jenkins gate runs en de ru pt).
- `jenkins/build.sh test` now also runs lifecycle, graphics_policy, graphics_notice and the layout scan.
- Tools: `tool_monkey.gd` (120 s, seed 7, speed 4: 4683 random actions - taps, drags, panels, 130 language switches, tier switches, Back, save+continue, pause/resume incl. 1 h absence, prestige, cheats: **0 SCRIPT ERROR**, memory ~300-320 MB flat, 0 orphans at end), `tool_perf_tiers.gd`, `tool_untranslated_scan.gd` (ja/ko/zh/ru: clean), `tool_leak_probe.gd`.

## 4. Performance (GL Compatibility, 540x928 / 720x928, `perf/*.json`)
Midgame (9 vehicles, 7 regions), mean frame ms / p95 / draw calls / video MB:
| Tier | 540x928 | 720x928 |
|---|---|---|
| High | 3.0 / 4.5 / 365 / 47 (near 6.1 / 18.2 / 375) | 2.7 / 3.9 / 328 / 51 |
| Medium (scale 0.85) | 3.8 / 5.5 / 336 / 48 | 2.9 / 4.1 / 314 / 52 |
| Low (scale 0.70) | 3.4 / 5.1 / 303 / 41 | 3.7 / 10.4 (one 24 ms frame) / 304 / 43 |
Start of game: 111-210 draw calls, 1.6-3.3 ms. The game is GPU-trivial on an M4; the ranking is by memory/draw calls, not frame time. Android: `gl_compatibility` for desktop + mobile; frame cap 60.

## 5. Leftovers (not done)
- First-time **shader/tier switch hitch** 100 ms the frame after switching to Medium/High in a midgame scene (`switch_to_*_next_frame_ms` in perf JSON); Low 4.5 ms. Only happens when the player changes the tier in Settings.
- **Unlocking region 1 takes ~257 ms** on the main thread (others 3-10 ms); `Main` instantiate 419 ms and scenario build 386 ms (hidden behind the curtain / title).
- Low tier still draws ~300 calls midgame (vs 365 High): the saving is mostly render scale. Merging static city meshes would cut it further; needs visual review (not done: owner rule on scene look).
- No real phone / Android device available: numbers are from the Mac GPU, not from a weak phone.
- Boot curtain only measured visually in tr (`boot/`); the timing lines are printed with `[boot]` prefix.
