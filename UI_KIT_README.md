# coddy_ui — the fleet game UI kit (W64)

Owner (2026-10-22): *"UI anlamında tasarımlar bok gibi … onboarding, ayarlar vs vs modern kaliteli hale getirmek"* and
*"UIlar çok iyi olmadan canlıya çıkmayacak"*. No game ships to production until its UI passes this guide (target ≥ 8.5/10).
Also the home of the fleet finger-scrolling rule, games/CLAUDE.md §16.

| What | Where |
|---|---|
| Design tokens (single source) | `design/tokens.json` → `python3 design/gen_tokens.py` writes `addons/coddy_ui/ui_tokens.gd`, `ui_icons_data.gd`, `design/tokens.css`, `design/icons.js` (`--check` = stale / contrast gate) |
| Mockups (HTML → PNG) | `design/mockups/screens.html` + `kit.css`; `zsh design/mockups/render.sh` → `/Volumes/BuildSSD/game-builds/w64-design/` (canal = landscape 1600×720, kaiten = portrait 720×1560, `system_sheet.png` = the design system) |
| Godot kit (synced into games) | `addons/coddy_ui/` — never edit a game's copy; `bash games/tools/ui/sync.sh games/<game>` (`--check` in check.sh) |
| Language picker | `addons/coddy_ui/ui_language.gd`: `UiLanguage.row()` (Settings row: current language in its own name) + `UiLanguage.open(host, opts)` — a LIST, "System language" first, native names, current marked, a tap applies + closes. **Never an OptionButton / cycle button for the language** (owner bug 10-24: the popup opens under the finger and the release picks the next row) |
| Kit tests | `godot --headless --path games/tools/ui -s res://addons/coddy_ui/tests/run.gd` (also from a game: same path) |
| Touch regression suite | `addons/coddy_ui/tests/touch_scroll_suite.gd` (device-like input via `Input.parse_input_event`) |
| Safe-area layout test (10-28) | `godot --headless --path games/<game> -s res://addons/coddy_ui/tests/safe_layout_test.gd`: the game's screen (built by its `tests/safe_layout_hooks.gd`) on 4 simulated phones per orientation (status bar + cutout + 3-button bar; landscape: bar right AND left) — no text / button under a system bar, the toast never on the tab bar. `-- --generic` = fleet scan of the main scene (no hooks, never fails). Probe: `tests/safe_layout.gd` |
| §16 static gate | `python3 games/tools/ui/check_touch_scroll.py games/<game>` |
| Gallery (every component, both orientations, any language → PNG) | `addons/coddy_ui/gallery/gallery.gd` (windowed) |
| Before/after captures | `tools/capture_screens.gd` (windowed, any W×H, same screens as the audit) |
| Pilot (reference implementation) | `games/canal-control` (commit "canal-control: W64 UI pilot"), shots `/Volumes/BuildSSD/game-builds/w64-pilot/canal-control/` |

## 1. Design language (decisions)

- **Calm night console.** Dark, hue-tinted surfaces from each game's own base colour (never grey), one warm **primary**
  (the thing to tap), one **accent** (information / "safe"), fleet-wide status colours (success, warning, danger, info).
  Contrast is checked by the generator: text ≥ 7:1, muted text ≥ 4.5:1, text on primary ≥ 4.5:1.
- **Scale.** Canvas px at the fleet baseline: short screen side = 720 px ≈ 411 dp → **1 dp = 1.75 px**.
  Touch target ≥ 48 dp = **84 px** (every kit button, row, tab, segment, icon button). Type: Display 46 / Headline 38 /
  Title 30 / Body large 28 / **Body 25 (= 14 sp, the minimum for text)** / Label 26 bold / **Caption 21 (= 12 sp, absolute
  minimum)** / Overline 21 bold caps. Spacing 4·8·12·16·24·32·48·64. Radius 8·14·20·28·36·pill. Elevation e0–e3 (StyleBoxFlat shadows).
- **Components look like one family**: rounded cards with a 1 px hairline, a 4 px darker "lip" under filled buttons
  (tactile, casual-game feel), icon tiles (Lucide line icons in a tinted rounded square/circle) on rows, products and
  modals, overline section headers, switches instead of "Music: On" buttons, segmented controls instead of chips.
- **Never visible**: raw Godot grey panels, default scroll bars, default font, bare outlined text over the 3D view,
  empty fixed-size modal boxes, text below 21 px, buttons below 84 px.
- **Weak devices**: StyleBoxFlat / StyleBoxEmpty / StyleBoxLine only, no shaders, no blur; icons are rasterised once from
  SVG text (`Image.load_svg_from_string`, 2× supersampled) and cached; tweens only on state changes; nothing per frame.
  CJK / Devanagari / Cyrillic: the game's own bundled Noto files (`UiFonts`, OS fallback off); bold = `variation_embolden`.

## 2. Component list (`addons/coddy_ui`)

| Script | Component |
|---|---|
| `ui_kit.gd` | `UiKit.use(palette)` → Theme; factories: `label(style)`, `button(kind: primary/secondary/tonal/ghost/danger/text, icon)`, `icon_button`, `tab_button`, `card(kind)`, `group()`+`group_add`, `tile`, `icon`, `chip`, `badge`, `stat`, `progress`, `dots`, `empty_state`, `scrim`, `section`, `fit_font`, `settle` |
| `ui_theme.gd` | Theme builder: Label / Button / PanelContainer type variations, scroll bars (no track, 6 px thumb), PopupMenu, OptionButton, CheckButton (switch), CheckBox, ProgressBar, HSlider, LineEdit, separators, tooltip |
| `ui_tokens.gd`, `ui_icons_data.gd` | generated tokens + 55 Lucide icons |
| `ui_icons.gd` | cached icon textures, icon tiles, switch, slider knob |
| `ui_fonts.gd` | bundled font stack per language (configure paths if a game's fonts are not in `res://assets/fonts/`) |
| `ui_toast.gd` | toast / snackbar card (icon + ≤ 2 lines), kinds info / success / warning / danger; `toast.text = …` compatible; quiet during a guide (priority "guide" speaks), `keep_clear(panel)`, hides on a language switch (§2 rules below) |
| `ui_modal.gd` | dialog (landscape) / bottom sheet (portrait): content-sized, header icon + title + close X, body scrolls inside, actions, `columns(n)` |
| `ui_back.gd` | Android Back, one layer at a time (10-28): `UiBack.new()` once under the game root turns `quit_on_go_back` off; Back closes the top UiModal (`back` opt / `on_back`, else its close), then a `UiBack.layer(node, close_fn)` panel, then the game's `game` callable; only the bare main screen saves + quits (or runs `main`, a quit confirm). Tests: `UiBack.send(tree)` = Android's notification + signal, `quit_hook` |
| `ui_pager.gd` + `ui_illustration.gd` + `ui_step_dots.gd` | onboarding pager: illustration, step dots, Skip, big Next, slide/fade. Landscape = centred card; **portrait = modal card over the dimmed live game** (88 % width, ≤ 60 % height, near the bottom, scrim over the whole screen) |
| `ui_safe_area.gd` | `UiSafeArea.rect()` — drop-in for `DisplayServer.get_display_safe_area()` that also clears the Android navigation bar (insets ignoring visibility: Godot 4.7.2 immersive mode reports only the cutout; owner 10-26 Döner tabs under the 3-button bar). `edges(canvas)` / `bottom(canvas)` in canvas px. Every game uses it instead of the engine call; the house banner sits above it. **10-28: rect() / edges() also reserve the status bar at the top (visible or not)** — the top HUD sat under it on phones without a cutout and in landscape; `simulate(screen, insets, status)` keeps the status bar apart from the cutout for tests · **10-30 (owner, Chaiwala: ~56 dp empty strip under the banner): a HIDDEN navigation bar (immersive) is no longer reserved in full** — only while `getInsets(navigationBars)` shows it; hidden → the gesture strip (`mandatorySystemGestures`) or `MIN_EDGE_DP` 8 dp, never more than the bar. Backgrounds run to the edge. `simulate(…, nav_mode)` / `CODDY_SAFE_SIM` 4th field `gesture` / `buttons` = hidden bar; the safe-layout test has 2 such phones per orientation and checks the band + that the banner background reaches the bottom |
| `ui_coach.gd` | coach mark: dim + rounded spotlight ring + pointer bubble beside the target ("Step n of N", hint, Skip guide) |
| `ui_segmented.gd` | segmented control (2–4 options, fits long languages) |
| `ui_row.gd` | list / settings rows: `UiRow.nav`, `UiRow.toggle` (switch), `UiRow.item` (card row with tile, selected state) |
| `ui_settings.gd` | `section(parent, title, rows)`, **`privacy(parent, Ads, labels)`** (see §5), `PRIVACY_URL` |
| `ui_shop.gd` | restyles the shared coddy_iap shop (`IapScreen.build`). **Default = compact** (Patron 10-22): one-line info strip (tap = open / close), overlines, 92 px product rows (tile 48 · title 1 line, auto-shrinks to 21 px · description 1 line · price button 84 px on the right). `compact: false` = large cards with "Best value" badge (only for a full-screen shop with room) |
| `ui_loading.gd` | loading screen (key art, title, progress pill, status) — a game's curtain can `extends` it |
| `touch_scroll.gd`, `ui_scroll.gd`, `ui_scroll_container.gd` | §16 finger scrolling (one per viewport, GUI-picking start, inertia, horizontal, rebuild guard `TouchScroll.held()`), `UiScroll.list()/strip()/adopt()`, scene script |
| `ui_reconcile.gd` | W62/W63 list refresh without eating taps (`UiReconcile.apply(list, fresh, scroll)`) |

### Text rules the kit enforces (10-27, W66)
- **Toast** sizes its text column from the text the label DRAWS (`atr(text)`): handing it an English key is fine.
- **Upper-cased kit text** (`UiKit.label(…, "Overline")`, `section`, `badge`, the coach step) goes through
  `UiKit.upper_text(label, text)`: when upper-casing changed the text, the label's auto-translation is off (an upper-cased
  word that is also a CSV key — "RACE" — was translated back to "Race"). An all-caps key passed as is still translates.
  Upper-case your own labels with `UiKit.upper_text` too.
- **Side buttons** (`list_card_side`, a wrapping Button) keep at most two lines in every script (`coddy_fit_lines` = 2;
  CJK breaks between characters, so a one-glyph column is impossible). Lines a game sets itself (`"Rest\n40 s"`) stay.

### Refits after a resize / redraw (10-27, kit fix 3 — Corrupt State Z Fold7 crash)
- A kit fit that follows a **redraw** (`watch_fit`, `fit_line`, list-card / row title minimum) runs on the **next frame**
  (`UiKit.refit_later(control, kind)`, one pending per control), never `call_deferred` from a `draw` handler: the redraw
  runs inside the deferred-call flush, so a fit that changed the layout re-queued itself in the same flush and a layout
  that flipped back and forth grew the queue until "Message queue out of memory" → SIGSEGV.
- `UiKit.refit(control, kind)` (the resize path): at most 2 changing refits per control per frame; a control whose refits
  alternate A, B, A, B keeps its current result until its text / width / style changes. A freed control is never
  touched; leaving the tree drops its pending refit. Do the same in game code: never answer `draw` with a deferred
  layout change.

### Toast / modal / row rules (10-27, kit round 2 — ~17 games had patched these by hand)
- **Language**: a toast hides when the locale really changes (it remembers the locale it was shown in; Godot 4.7.2's
  one TRANSLATION_CHANGED right after the toast enters the tree keeps it).
- **Guide**: every `UiCoach` (and any card registered with `UiToast.add_guide(node)`) is a guide. While its bubble is
  up an ordinary toast is skipped (`show_toast` returns false, `skipped` counts it) and one already up goes. Warning /
  danger, and a line the game says on purpose — `toast.show_toast(msg, "success", "", "guide")` or
  `show_guide_toast(msg)` — still show, placed clear of the bubble and its spotlight (also from a higher canvas layer:
  lego's toast layer 110 over its guide layer 10). A "guide" line with no clear spot is dropped.
- **Panels**: `toast.keep_clear(control, {"pad": 12, "foot": false})` — the card never covers that control while it is
  visible (a sheet's header + tab strip, an event card, a tip card). It moves to the nearest clear spot (under / over /
  beside it, inside the screen, an upper toast never above its own row unless `setup({"ceiling": px})`); `foot: true` =
  with no room outside, at the panel's foot inside it. A visible toast re-places when a registered control (or a
  parent: a sliding sheet) opens, closes, moves or resizes — event driven, no per-frame work, the fade is not restarted.
  `release(control)` (automatic when it leaves the tree); `keep_clear_rect(id, global_rect)` for a drawn area.
- **Modal columns** (`UiModal.columns(n)`): side by side vs stacked is re-checked on every layout pass (screen-size
  change); a column with nothing visible is hidden so the other one takes the width (a column the game hid stays hidden).
- **Row grey line** (`UiRow` subtitle): wraps between words only; a word wider than the column steps the font down to
  21 px, then ends in "…" on one line ("coddykit.com" in a narrow Settings column was "coddykit.c / om").

## 3. Rollout — exact steps for one game (slave brief)

Own sparse worktree as always (games/CLAUDE.md §6); `git worktree add --no-checkout … origin/main` + sparse
`games/tools/ui games/tools/shop games/<game> games/CLAUDE.md`. Read the game's audit row in
`/Volumes/BuildSSD/dev/i18n-work/tasks/W64_audit.md` and look at its shots first.

1. **Sync the kit**: `bash games/tools/ui/sync.sh games/<game>`.
2. **Delete the old TouchScroll copy** (`src/ui/touch_scroll.gd` / `scripts/ui/TouchScroll.gd` + `.uid`) and every
   `class_name TouchScroll`; point users to `const TouchScroll = preload("res://addons/coddy_ui/touch_scroll.gd")`.
   `TouchScroll.attach(root, allow)` keeps working (one instance per viewport now).
3. **Theme**: in the game's `ui_kit.gd` (or theme script) return `UiKit.use("<game>")`; keep the game's old helper API
   as thin wrappers (see `games/canal-control/src/ui/ui_kit.gd`). Set `UiKit.tap_sound = <Audio>.tap`. If the game's
   palette is not in `design/tokens.json`, add its 4 seeds (base, primary, accent, text) and re-run `gen_tokens.py`.
   Fonts not in `res://assets/fonts/NotoSans*.ttf` → `UiFonts.configure({...})` before `use()`.
4. **Every ScrollContainer** → `UiScroll.list()/make()/strip()` or `UiScroll.adopt(sc)`; in a `.tscn` give it the
   script `res://addons/coddy_ui/ui_scroll_container.gd`. `python3 games/tools/ui/check_touch_scroll.py games/<game>` = 0.
5. **Lists refreshed on a timer** → build into a detached container and `UiReconcile.apply(list, fresh, scroll)`
   (or keep the game's reconcile but gate rebuilds with `TouchScroll.held(list)`).
6. **Screens** (checklist §4): HUD, onboarding (UiPager + UiCoach), settings (UiModal + UiRow + UiSegmented +
   UiSettings.privacy), shop (`UiShop.style(shop, {"best": tr("Best value")})` after every build AND after the shop's own
   rebuilds: connect `Iap.prices_changed` / `entitlements_changed` → call it deferred, see canal `fit_shop`; do not
   re-enable autowrap on its labels afterwards), toasts (UiToast), every dialog (UiModal), loading curtain (UiLoading).
7. **Strings**: every new key in the game's i18n CSV in all 10 languages (games/CLAUDE.md §9: Claude Haiku sub-agents
   in session, no external API); remove keys the game no longer shows; re-run the game's font subset if it has one
   (glyph test).
8. **Tests**: add to `tools/check.sh` (copy from canal-control):
   ```sh
   [ ! -f "$P/../tools/ui/sync.sh" ] || bash "$P/../tools/ui/sync.sh" --check "$P"
   [ ! -f "$P/../tools/ui/check_touch_scroll.py" ] || python3 "$P/../tools/ui/check_touch_scroll.py" "$P"
   run -s res://addons/coddy_ui/tests/run.gd
   grep -q "CODDY_UI_TESTS:.* 0 failures" "$OUT/run.log"
   run -s tests/touch_scroll.gd
   grep -q "TOUCH_SCROLL_TESTS:.* 0 failures" "$OUT/run.log"
   ```
   `tests/touch_scroll.gd` = canal's file adapted: kit fixtures + the game's real lists (drag on a button scrolls and
   presses nothing, a flick coasts, a tap lands, a tap held across the refresh lands once, nothing scrolls under a
   window), **en + de**. Update existing tests that read old node texts (rows carry `title`, icon buttons `tooltip_text`).
   The game's own layout / overflow tests must pass in all 10 languages and every aspect ratio they check.
9. **Shots**: before = the audit folder; after:
   ```sh
   /Volumes/BuildSSD/novareka-dev/shot-lock.sh /Volumes/BuildSSD/godot/4.7.2/Godot.app/Contents/MacOS/Godot \
     --path games/<game> --windowed --resolution 640x360 --audio-driver Dummy \
     --script $PWD/games/tools/ui/tools/capture_screens.gd -- /Volumes/BuildSSD/game-builds/w64-pilot/<game>/after <W>x<H> en,de
   ```
   (landscape 1280x720 and 1600x720; portrait 720x1280 and 720x1560). One windowed Godot at a time; Mini muted.
   `git checkout -- project.godot export_presets.cfg` after any import.
10. Self-score every screen against §4 (write the table into the PR/commit message or the W64 board), commit
    `"<game>: W64 UI (kit)"`, secret scan, `git fetch && git rebase origin/main`, `git push origin HEAD:main`.
    No AAB, no store upload in this step.

## 4. Per-screen checklist + acceptance (each screen ≥ 8.5/10, no hard-fail item)

Hard fails (any one = the screen scores ≤ 6): raw Godot look visible (grey panel, default scroll bar, default font),
text < 21 px, a tap target < 84 px, text cut / overflowing in any of the 10 languages, a modal taller than its content,
a toast without its card, a list that does not follow a finger started on a button, fewer than 3 products on a shop
screen, **a scroll thumb visible while nothing scrolls**.

**Scroll bar rule (Patron 10-22):** content fits → no bar at all; content longer → still nothing until the list moves;
while it scrolls a 6 px thumb appears and it fades out ~0.9 s after the last movement. Every list made with
`UiScroll.list()/make()/strip()/adopt()` does this (ScrollFade); kit test "an overflowing list shows no thumb until it
scrolls". Check the Settings and Shop shots: no thumb in a still screenshot.

| Screen | Must have |
|---|---|
| Loading | game key art, game name, progress pill, translated status (or none), no Godot logo |
| Onboarding intro | UiPager: illustration (icon composition or real art), step dots, ≤ 2 short sentences, Skip + primary Next, no scroll, fits 16:9 → 20:9 / 9:16 → 9:19.5 |
| Guide (coach) | UiCoach spotlight on the real control (scrolled into view), bubble beside it, "Step n of N", Skip guide, wrong tap flashes the ring |
| HUD | figures as `UiKit.stat` (icon + value + caption) on a Glass card; round icon buttons (pause / settings) top-right; tabs = icon + label, selected = primary container; lists in Sheet cards; nothing < 21 px |
| Lists / tabs | cards / rows with icon tiles; primary button = the main action only; selected item visible (row `selected`); refresh never eats a tap |
| Shop | **≥ 3 whole products visible on one screen** in landscape AND portrait, in every language (test it: count `prod_*` rows enclosed by the list rect — canal `tests/p5.gd` `_shop_layout`). Compact rows only (`UiShop.style(shop, {...})` default): icon + name + short description + price/buy on the right; the intro is a one-line strip that opens on tap; restore = ghost. Never the large cards in a side panel or a sheet. |
| Settings | UiModal (landscape: two columns, width ~1180; portrait: bottom sheet); sections Sound (switches), Graphics (segmented + hint), Language (picker row, native names), Help (replay guide), Purchases (restore, owned extras), **Privacy (UiSettings.privacy)**; close X; changes apply in place |
| Dialogs | UiModal content-sized, icon tile + headline, body ≤ 3 lines, actions bottom-right (portrait: stacked full width) |
| Toast | UiToast under the HUD, kind icon, ≤ 2 lines, never over a button the player needs |

Scoring guide: 10 = indistinguishable from the mockups; 9 = kit everywhere, small spacing nits; 8.5 = kit everywhere,
one screen has a non-blocking compromise (e.g. dense list); < 8.5 = any hard fail or a screen still on old widgets.

## 5. Privacy row standard (Patron 10-22)

AdMob GDPR + US state messages are live fleet-wide; the US message opens only from the in-app **Privacy options**
entry. Every game's Settings calls `UiSettings.privacy(column, Ads, {"title": tr("Privacy"), "options": tr("Privacy options"), "policy": tr("Privacy policy")}, [analytics_toggle])`:
- "Privacy options" shows only when UMP `privacyOptionsRequirementStatus == REQUIRED` (`Ads.privacy_options_required()`),
  a tap calls `Ads.show_privacy_options()` (UMP `showPrivacyOptionsForm`). Games on the shared `games/tools/ads/CoddyAds.gd`
  already have both methods; others add them (zombi is the original).
- "Privacy policy" is always shown and opens `https://www.coddykit.com/pages/privacy-policy`.
- 10 languages (canal-control's CSV has the reviewed rows: "Privacy", "Privacy options", "Privacy policy").

## 6. Rules of thumb

- Text comes from the game (`tr()`); the kit never calls `tr()`.
- Autowrap labels: give them a width (inside a VBox / card); measure with `UiKit.settle()` before sizing a floating card.
- **Never a word cut mid-letter (#98/#101, 2026-10-22).** Every `UiKit.button` and `UiKit.chip` text runs
  `UiKit.word_fit` (on resize / redraw, no per-frame work; never before the control is in the tree and laid out, and
  refit when the theme's font / size arrives). A wrapping (WORD_SMART) text keeps a minimum width for its longest
  word at the 21 px text minimum (`FIT_TEXT_MIN`) and steps its font down; it goes below 21 (to `FIT_FLOOR` 18) only
  when a container cannot give that width. A one-line `clip_text` button shrinks until its longest line is whole,
  never below 21. Devanagari / CJK may break between graphemes only at the floor; Latin / Cyrillic never. The game's
  own `font_size` override is the start size. Opt out: `set_meta("coddy_no_fit", true)`. Tests: `_word_fit`,
  `_closing` in `tests/run.gd`. Store screenshot provenance hashes `addons/coddy_ui` (tests / gallery excluded), so a
  kit sync marks the shots STALE. Found as de "Taschentuchsta/nd", ru "Бесплат/ный", de "Bienenkaste/n".
- Prefer one primary button per screen. Danger only for destructive / rescue actions.
- Portrait games: `UiModal` becomes a bottom sheet automatically; tab bars go to the bottom (`TabButton` in a 100 px bar).
- Do not add shaders. If a page needs real art (onboarding), set `page.image` (a Texture2D or a `res://…` path): the picture fills the illustration area as a rounded cover (aspect kept, overflow cropped, no icon / satellites; fades + settles with the page). A missing / unloadable image falls back to the icon + satellites composition. `art` (Texture2D) is the older alias.
- **Onboarding pager taps + motion (owner 2026-10-23).** One tap per page; ONE tap on the last button emits `finished`
  once. A game that keeps its own intro counter must follow the pager (`pager.page_changed.connect(func(i): page = i)`);
  the kit also keeps a host's `page` in step when the host holds the pager as `_pager` (the onboarding.gd games: before
  this the last page needed two taps and the first `finished` sent the pager back a page — "4 pages, 3 dots"). Dots +
  counter always show `pages.size()` (`add_page()` after setup). Swipe left/right turns pages (never finishes; a game's
  own swipe wins). Motion = tweens only (Tokens.MOTION): open fade + settle, art/text slide in from the side the page
  comes from, dots morph, last-button CTA (play icon, one halo), press dip, close fade copy. Buttons never move during a
  transition. `UiKit.reduce_motion = true` snaps all of it (accessibility, still captures). Tests: `_pager_taps`,
  `_pager_counts`, `_pager_motion` in `tests/run.gd`.

## Tab pages + windows — the 2026-10-26 redesign (owner: "tab views and the modals from them still look like shit")

Tokens, not one look: every game keeps its palette; these rules decide the structure. Pilots: pest-patrol,
pharmacy-rush (portrait, kit), corrupt-tycoon (landscape, its own K kit — same rules in its colours). Sheets:
`/Volumes/BuildSSD/game-builds/ui-redesign-2026-10-26/<game>/compare/`; tools `tools/capture_tabs.gd` (factory games),
`corrupt-tycoon/tools/qa/TabShots.gd`, `tools/capture_redesign.sh`, `tools/contact_sheet.py`.

| Piece | Rule (kit API) |
|---|---|
| Page head | `UiKit.page_header(icon, title, close, {sub})` → 52 px tile · Title 30 · ONE summary line (the list's old explainer note moves here) · round close chip · hairline. `page_header_set` / `page_header_sub` on tab change / refresh. Low: tile + hairline hidden, `CloseButtonFlat` |
| Close | `UiKit.close_button()` — always top-right, centred on the title row, 84 px touch, 56 px chip (`CloseButton`); UiModal uses it |
| Card row | `list_card` + ONE call to action on the right: `UiKit.action_pill(text, cb, enabled, kind)` via `list_card_side` (price pills "450 coins", "$400"; tonal for secondary). Never a full-width 84 px slab under every card. Secondary chores under the card = `OutlineButton` / ghost. Medium/High: `card_progress` level bar |
| Sections | `UiKit.section_header(text, trailing)` (overline + 12 px air above) |
| Empty | `UiKit.empty_card(icon, title, body, action, cb, low)` instead of a lone grey line |
| Surfaces | Sheet = palette `background` (one step darker than cards `surface_variant`); bottom sheet rounded at the top only; grab handle = rounded pill; portrait sheets size to their rows (a short list leaves the game visible) |
| Modal | portrait = bottom sheet (`sheet: "auto"`, never forced false upright); `columns(n)` stacks below 420 px per column; a hairline over the action row only while the body scrolls |
| Low tier | UI draws per screen ≤ old + 2: pill rows → `ActionText` (coloured text, regular face, no box) + `ListCardDenseFlat` (no row box); empty card without box / tile / body |

## Tab probe clock — #66, 2026-10-25

**#183 (10-30):** `TouchScroll.held(list)` counts only a finger that went down on that list's area (a tap on the tab bar or a lost release no longer defers the new tab's rows), and `tests/tab_budget.sh <probe log>` fails a check when the probe's `worst` > 150 ms (`TAB_BUDGET_MS`). `addons/coddy_ui/tests/tab_latency_clock.gd`: release→first content acceptance ≤2 frames; milliseconds are diagnostic only. Native uses `Engine.get_frames_drawn`; headless draws zero frames, so it explicitly reports `headless-content` from `Engine.get_process_frames`. This is not device GPU proof. Keep real nonempty/new content checks. `bash games/tools/ui/sync.sh --latency-clock games/<game>` copies only this test helper and UID, leaving parallel card-design work untouched. Cinnamon target is its `game` directory; Travel uses browser rAF.

## Fleet font audit (#70)

`tests/test_house_fonts.gd` in each house-ad game initializes its **real game theme**, checks the source kit theme's
Install glyphs in its ten store locales, and builds a real interstitial with the optional banner helper removed and
an unsupported remote title. Shared implementation: `addons/coddy_ui/tests/font_audit.gd`; `font_probe.gd` recursively
checks FontVariation/base/fallback chains with OS fallback disabled. BlockForge supplies an offline adapter for its
older house renderer. Ordinary game `tools/check.sh` gates include `tests/check_fonts.sh`.

From an **own imported worktree**, run the focused suite and optional real desktop screens:

```sh
python3 games/tools/ui/tools/fleet_fonts.py --out /abs/evidence --render --skip-import
# Append game names to narrow the run. Omit --skip-import for a fresh checkout.
```

The runner serializes games, uses separate saves, restores import-mutated config, fails on script errors as well as
process exit codes, and writes `summary.json`, game `matrix.json`, logs and PNGs (1280×720 landscape; portrait requests 720×1280, clamped by the desktop window manager). GPU captures use shot-lock on
Mac Mini and Dummy audio. Screens are the game's actual shop/settings and locally instantiated house components;
remote text is an offline multilingual fixture, with no impressions, ad clicks or store requests. A missing screen
or missing screenshot is a failure. Driftwood and BlockForge have no banner, so each has 30 rather than 40 frames.
Cinnamon has no house ads and is excluded from this audit (its shared test helpers are still synchronized).

For the shared interstitial, use `bash games/tools/ads/sync.sh [--check] games/<game>...`; this copies only the card and
preserves game-specific ads configuration. See `games/docs/FLEET_TOFU_2026-10-26.md` for findings and the 220-row matrix.
