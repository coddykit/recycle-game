# Volt City — UI (F0, Patron 2026-10-27)

> Binding sources: CLAUDE §16 (touch), §17 (kit), [`../../tools/ui/README.md`](../../tools/ui/README.md) (components, per-screen checklist,
> the 2026-10-26 tab/window redesign), playbook sections 8-9. The kit is **frozen inside the game**: `bash games/tools/ui/sync.sh games/volt-city`
> (and `--check` in `tools/check.sh`); never edit `addons/coddy_ui` in this folder. Same for `coddy_perf`, `coddy_anim`, the ads card
> (`games/tools/ads/sync.sh`). Every screen must score **≥ 8.5/10** (en + de frames, Patron looks) before production.

## 1. Orientation and zones (portrait only)

Base canvas 720 × 1280 (short side 720 px = 411 dp, 1 dp = 1.75 px), stretch `canvas_items`, aspect `expand`. Layout must fill
16:9, 19.5:9, 20:9 portrait and a 4:3 tablet (layout test, no empty bands — the camera fits the **real district boxes**, not the scene bounds).

```
┌──────────────────────────────┐  ← UiSafeArea.rect() top inset (notch / status bar)
│ HUD row 1: $cash · ★rating · shift clock/phase · ⚙ │  Glass card, UiKit.stat ×3 + round icon buttons
│ HUD row 2: Balance needle (wide) + goal line        │  goal line always visible, tap = goals panel
│                                                      │
│        3D city diorama (≈ 55-60 % of height)         │  tap = pick district / plant / fault (on release)
│        drag = pan (clamped), pinch = zoom (clamped)  │
│                                                      │
├──────────── grab handle (pill) ──────────────────────┤
│ Bottom sheet (half height default, handle → full)    │  sheet = palette background, rounded top only
│   page_header + list of list_cards                   │
├──────────────────────────────────────────────────────┤
│ Tab bar: Grid · Crews · City · Build · Shop (100 px) │  5 equal tabs, icon above, 21 px label
├──────────────────────────────────────────────────────┤
│ House banner (~58 dp, ≤ 12 % of canvas) — if shown   │  never for ad_free, never in onboarding/shop/boot
└──────────────────────────────────────────────────────┘  ← UiSafeArea bottom inset (Android nav bar)
```

- The diorama is never covered by a full-screen window during a live shift; windows are bottom sheets (`UiModal`, `sheet: "auto"`).
- Status in two rows; a row needs ≥ 160 px height or it hides (flash → toast). Text ≥ 25 px body / ≥ 21 px caption; targets ≥ 84 px.

## 2. Kit design generation 2 (the 2026-10-26 redesign) — set before the first `UiKit.use()`

```gdscript
UiKit.design = 2          # darker sheets, round close chip, pill grab handle, columns stack < 420 px, whole-word row titles
UiKit.use(PALETTE)        # palette generated from the game's base colour (deep navy-teal night, warm amber primary, cyan accent)
```

| Piece | This game |
|---|---|
| Page head | every tab = `UiKit.page_header(icon, title, close, {sub})`, ONE summary line, updated with `page_header_sub` at ≤ 4 Hz. Grid: "Supply 46 MW · Demand 44 MW · +4 %". Crews: "2 faults · 1 truck free". City: "6 of 8 districts lit · 3.2★". Build: "Next: Wind farm I — $1,950". Shop: "Remove Ads and extras" |
| Card rows | `UiKit.list_card` + ONE action on the right via `list_card_side` + `UiKit.action_pill`: plant row → `+` / `−` pills (two small pills are one "stepper" side control, each 84 px) ; fault row → `Send`; district row → `Shed` (danger kind) / `Restore`; investment row → price pill `$1,950` (disabled when short, never hidden) ; shop row → kit `UiShop` compact |
| Sections | `UiKit.section_header`: Grid = "Running" / "Standby"; Build = "Plants" / "Lines" / "Operations"; City = "Lit" / "Dark" / "Not connected" |
| Empty | `UiKit.empty_card("check", "No faults", "The grid is calm. Crews wait at the depot.")` |
| Close | `UiKit.close_button()` top-right on every sheet/modal |
| Low tier | pill rows → `ActionText`, `ListCardDenseFlat`; UI draws per screen ≤ old + 2 (measure with `tools/ui/tools/capture_tabs.gd` UI_DRAWS) |

Other kit pieces: `UiToast` (≤ 2 lines, 2.5-5.5 s, tap dismiss, queue compresses — "Riverside is back on!"), `UiSegmented` for the
battery mode (Charge / Hold / Discharge / Auto) and for graphics Low/Medium/High, `UiRow.toggle` switches in Settings, `UiLanguage.row()`
+ `UiLanguage.open()` (never an OptionButton), `UiSettings.privacy(...)`, `UiLoading` curtain, `UiPager` + `UiCoach` (with `avoid`
rects so the bubble never covers the needle or the spark it explains), `UiReconcile.apply` for every live list, `UiScroll.list()` for every list.

## 3. Tabs (content)

| Tab | Rows | Live updates |
|---|---|---|
| **Grid** | one card per plant (icon tile by type, output bar `card_progress`, "52 / 60 MW", ramp status "→ 58 MW in 6 s"), battery card with `UiSegmented`, Demand Response card (cooldown ring) | labels only, ≤ 4 Hz, never rebuild the list on a money tick |
| **Crews** | faults sorted by customers affected ("Riverside line · 3,400 homes dark"), trucks (free / driving / repairing + ETA) | add/remove rows via `UiReconcile`; rebuild waits while a finger is down (`TouchScroll.held()`) |
| **City** | 8 districts (lit/dark/not connected, demand, priority order handle as ↑/↓ pills — no drag-reorder), hookup goal card | — |
| **Build** | investments grouped Plants / Lines / Operations, locked rows show why ("needs Hilltop"), tapping a paid-feature row opens the shop on that product | — |
| **Shop** | kit compact shop, ≥ 3 whole products visible in every language, Restore (ghost) also in Settings | prices only from the store (`displayPrice`), unloaded = "…" |

Settings (gear, bottom sheet): Sound (Music / Effects switches), Graphics (segmented + hint), Speed default, Language (picker),
Help (Replay guide), Purchases (Restore, owned extras), Privacy (`UiSettings.privacy`), version line.

## 4. Custom input rule — `UiKit.is_tap` (S5 #51; `games/tools/ui/addons/coddy_ui/ui_kit.gd`)

Godot delivers a phone touch as a real `InputEventScreenTouch` **and** an emulated mouse event (and the desktop emulates the reverse);
handlers that took both acted twice (Sky Tower floor tap became pick + Call). In this game:

- Every custom `_gui_input` / `_unhandled_input` (world picking of district / plant / fault, needle tap, goal line tap, sheet grab
  handle, the stepper pills if hand-made) gates on **`UiKit.is_tap(ev, on_release)`** and nothing else. Strips, tiles and **world picks
  act on release** (`on_release = true`); buttons act on press (kit buttons already do).
- World tap vs pan: a pick fires only if the finger moved < 12 dp between press and release and the press lasted < 400 ms; anything
  longer is a pan/zoom and never selects. Pinch never selects. One physical tap = one action (test with real `Input.parse_input_event`
  ScreenTouch + emulated mouse; assert the game effect happened once).
- No hold-to-act controls in v1 (ramping is tap steps). If one is ever added: `tests/hold_button_test.gd` (playbook section 18).
- Lists: drag that starts on a card/button scrolls, short touch clicks (TouchScroll); static gate `check_touch_scroll.py`, regression
  `tests/touch_scroll.gd` + `tests/finger_scroll.gd` (en + de, vertical + horizontal).
- Tab open ≤ 100 ms to the first content row for every tab and Settings (`tests/tab_open_probe.gd -- --limit 100`, also `--phone`).
- FPS cap while a window is open only after 1.5 s without touch (LL A: the 20 FPS cap made scrolling choppy).

## 5. Safe area + house banner

- **All edges from `UiSafeArea.rect()` / `edges()` / `bottom()`** — never `DisplayServer.get_display_safe_area()` (Godot 4.7.2 immersive mode
  reports only the cutout; Döner tabs sat under the 3-button bar, owner 10-26). HUD, tab bar, sheets, toasts, coach bubbles and modal
  scrims all respect it; a layout test runs with a fake 48 dp nav bar and a 32 dp cutout.
- **House banner** (`games/tools/ads/CoddyBanner.gd`, house only, AdMob banner never): bottom edge, **above** the nav-bar inset; the tab
  bar and sheet move up by the banner height (`reserve_slot` reports the height before the bar fills, so the tab bar does not jump);
  never covers the diorama's play area or the tabs. Shown to every non-ad_free player after the onboarding finishes (no play-time wait,
  owner 10-26). **Not shown:** ad_free, boot/splash/loading, onboarding pager + coach, shop and purchase sheets.
- Kit 5a5088f8d9 behaviour is mandatory: a NEW bar waits for a calm moment (no finger down, 3 s without input); taps within 1.5 s of the
  bar appearing or the layout under it moving are swallowed. Narrow portrait = one game per bar; ≥ 600 dp wide = two games side by side.
- Interstitial (house card `CoddyInterstitial.gd`): only at the shift report after `Collect`, never in session 1, never during a live
  shift, deferred after a rewarded view; `Ads.low_end = func(): return quality == LOW` (one still frame on Low).

## 6. HUD details

- `UiKit.stat` ×3 on a Glass card: cash (`$12.4k`, compact format per locale), rating (`3.2★`), shift clock + phase chip
  ("Evening peak"). Speed pill (1× / 2× / 3×, 4× with `speed`) beside the clock — speed values from data are compared with `int(x) == v`
  (LL Driftwood P7: `4 in [1.0, 2.0, 4.0]` is false in Godot 4).
- Balance needle: custom control drawn once into a cached texture + a rotating needle (no per-frame text); green ±3 %, amber ±5 %,
  red beyond; a one-line explainer on tap. Colour is never the only signal (icon + text "Short 4 MW").
- Goal line: compact, always visible ("Goal: hook up Hilltop · 2.0★ · $2,000"), tap opens Goals.
- Fault markers in the world: sparks with a pulsing ring (Medium/High) / static icon (Low); a tap opens the fault card on Crews.

## 7. Screens checklist (P3 exit, per screen en + de, portrait 720×1280 and 720×1560)

Loading curtain · Pager (3) · Coach steps (9) · HUD + each tab (5) · Settings · Language picker · Shift report · Offline summary ·
District card · Fault card · Plant card · Forecast · Goals · Shop · Restore result · Rewarded offer card · Graphics popup (first start) ·
House interstitial (fixture) · House banner (fixture, narrow + wide). Hard fails per the kit README section 4.
