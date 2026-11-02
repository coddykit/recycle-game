# Volt City — store plan (F0, Patron 2026-10-27)

> Binding: playbook sections 5, 6, 20; CLAUDE §1 (no trial wording), §15 (post-release steps). Google Play only. Listing files in P6:
> `store/listing/<locale>.json` (title ≤ 30, short ≤ 80, full ≤ 4,000, UTF-16 length too), `store/.gdignore`, `store/validate.py`
> (copy `games/kaiten-sushi/store/validate.py` / `games/pit-wall/store/`). No "free", trial, offer, price or ranking claims; no competitor names.

## 1. Name candidates + collision note

> **P6 (2026-10-27): superseded — the store name is `Grid Keeper: Energy Tycoon`.** The real knockout search (USPTO, TMview
> 9/28/41, Play, App Store) rejected all three F0 names: [NAME_CHECK.md](NAME_CHECK.md). Final listings: `store/listing/<locale>.json`,
> screenshot plan: `store/SCREENSHOTS.md`. The F0 text below is kept as history.

Method used in F0 (quick, from this session): web search of Play / App Store / Steam / itch for each exact name, plus a reasoning pass
over obvious trademark conflicts. **This is not a legal clearance.** The P6 gate is a real knockout search for the chosen title before
any store art: USPTO (TESS, classes 9 + 41, live marks, exact + phonetic), EUIPO eSearch, WIPO Global Brand Database, Play + App Store
search; result into `store/NAME_CHECK.md` (Grid Boss format).

| # | Title (≤ 30) | Chars | Exact-name hits found | Trademark reasoning | Verdict |
|---|---|---|---|---|---|
| 1 | **Volt City: Power Grid Manager** | 29 | no game "Volt City"; nearest "Volt Block: City Lights" (iOS, ~500 installs, 2026) and unrelated "Volt" games (Steam 2014 puzzle, itch arcade) | "Volt" alone is a GM car mark (class 12, different goods) and a generic unit word; "Volt City" composite not seen in games; "power grid manager" is descriptive. Risk: the descriptive phrase contains "Power Grid", a board-game title (Rio Grande / 2F "Power Grid", de "Funkenschlag") → used only as a lowercase descriptive phrase after our own brand, never alone, never "board game" | **Recommended** — carries `grid manager` (Medium, d24) and `power grid` (Medium, d38) in the title |
| 2 | Blackout Boss: City Power Sim | 29 | no exact app; crowded word: Disney "BLACKOUT!" puzzle (power a city), several "BlackOut" puzzles, "Blackout Bingo" (a registered bingo brand), Call of Duty "Blackout" mode | "Blackout" is heavily used in games; "Boss" series fits Pit Lane Boss, but the head word is weak and confusable | backup 1 |
| 3 | Gridline: City Power Tycoon | 27 | "Gridline" used by some puzzle/grid apps | generic word, weak distinctiveness; `power tycoon` Medium | backup 2 |
| 4 | Lights On: Power Grid Tycoon | 28 | "LightsOn: Move to Build a City" (iOS city builder) | too close in the same category | rejected |
| 5 | Lightkeeper … | — | `com.mic.lightkeeper` is already one of the owner's apps | internal collision | rejected |
| 6 | Grid Boss … | — | the name already collided for Pit Wall (10-25) | — | rejected |

Folder and package stay `volt-city` / `com.coddykit.voltcity` even if backup 1 or 2 wins (the package is not a title).

## 2. ASO keywords (Play US, ASO Atlas 2026-10-27)

Title: `grid manager`, `power grid`. Short description: `power`, `city`, `grid`, `storm`. Full description, naturally (Play reads
the long text; no keyword lists): power grid game, energy tycoon, power tycoon, grid manager, solar, wind farm, battery, blackout,
power outage, storm, heatwave, city lights, electricity, utility, tycoon, manager, simulation. Per-locale titles carry the brand + the
local search word (P6 checks in Atlas per locale: tr `elektrik`, de `Stromnetz`, es `red eléctrica`, fr `réseau électrique`, pt-BR
`energia`, ru `электросеть`, ja `電力`, ko `전력`, hi `बिजली`). Atlas suggestions are ideas, not orders: filter competitor names.
After the Play page is public: `atlas.py track_app` android in us, mx, tr, in, fr, es, kr, br, pk, ru, jp, de.

## 3. English listing draft (P6 starts from this; Haiku sub-agents translate with the glossary)

**Title (29):** `Volt City: Power Grid Manager`

**Short description (≤ 80):** `Keep the city's lights on: balance power, fix storm damage and grow the grid.`

**Full description:**

```
The evening peak is here. Every window in Volt City is switching on, the needle is sliding into the red and a storm is rolling in
from the harbour. You run the city's power control room. Keep the lights on.

BALANCE THE GRID
- Watch the balance needle: supply has to match demand, all day long.
- Ramp up the steam plant, start a fast gas peaker, release water from the dam or discharge the battery park.
- Read the forecast and plan ahead for heatwaves, cold mornings, windless fog and cloudy afternoons.

HANDLE THE CHAOS
Thunderstorms bring down lines, a plant trips at the worst moment, the whole city boils the kettle at half time of the big match,
a squirrel sits on a transformer (the squirrel is fine). Send your repair crews, reroute power through smart substations and,
when there is no other way, choose which district goes dark for a moment. The hospital always stays lit.

GROW A WHOLE CITY
- Start with two districts and connect Hilltop, Market Square, Tech Park, Stadium Row, Hospital Hill and Airport Flats.
- Build 50 investments: wind farms, solar fields, a hydro dam, battery parks, stronger lines, more trucks, smart meters and a
  dispatcher assistant that runs the routine for you.
- Earn three-star shifts, raise the city rating and play five chapters, then take on the weekly grid challenge.

A LIVING CITY AT NIGHT
Watch districts light up in a wave when the power comes back, follow your trucks through the streets and see the skyline glow from
dusk to dawn. A step-by-step guide shows you each action the first time. Choose Low, Medium or High graphics to suit your phone.

GOOD TO KNOW
- Single-player. No account needed. Your progress is saved on your device.
- The game contains ads and optional in-app purchases. Nothing in the game requires payment.
- Only Remove Ads and the packages that explicitly include it (Chief Engineer Edition, Utility Pass, Complete Volt City Collection)
  remove the banner and the full-screen ads. Other purchases do not remove ads.
- Rewarded videos are always optional. With ad removal, the same capped rewards are given without a video.
- Consumable budget packs are not restored on a new device.
- Available in 10 languages.

If you like power grid games, energy tycoons and city manager games, take the control room and keep Volt City shining.

Privacy Policy: https://www.coddykit.com/pages/privacy-policy
Terms of Service: https://www.coddykit.com/pages/terms-of-service
```

(No "free", no trial, no price, no "best/#1", no real company or board-game names.)

## 4. Screenshots (coddy circuit style, owner 10-08 standard)

Per language: phone 1080×1920 portrait ×7 + 7" ×7 + 10" ×7 + feature graphic 1024×500 = 22 files. Frames are **real earned gameplay**
captured on the **weak Android profile** with free default looks (`tools/store/capture.sh` → `render.mjs` → strict `validate.py`:
`STORE_ASSETS_PASS`, `STORE_GLYPHS_PASS`); provenance hash covers the kit (a kit sync marks shots STALE). Layout: dark brand-tinted
circuit background + sparkles, icon + name header, pill chip, two-line localized headline, tilted glowing phone, floating benefit chip.

| # | Scene (real state) | Headline (en) | Benefit chip |
|---|---|---|---|
| 1 | evening peak, all lit, needle green | Keep the city's lights on | Live power grid |
| 2 | storm, Riverside half dark, truck driving to a spark | Send crews when storms hit | 15 chaos events |
| 3 | Grid tab open: plants ramping, battery Discharge | Balance supply and demand | Steam · gas · hydro · wind · sun |
| 4 | forecast card: heatwave tomorrow, battery charging | Plan for heatwaves | 3-day forecast* |
| 5 | Stadium Row lighting up in the ripple | Light up new districts | 8 districts |
| 6 | Build tab: wind farm + solar field purchased, skyline at dusk | Build a cleaner grid | 50 investments |
| 7 | shift report 3★ | Earn three-star shifts | Step-by-step guide |
| F | feature graphic: skyline half dark / half lit with a lightning bolt, title | — | — |

\* Slide 4 must not show a paid feature as if free: capture with the free 1-shift forecast and say "Plan ahead for heatwaves" if the
3-day view is not visible (P6 decides by the frame). No other users' names, no fake ratings/downloads.

## 5. Declarations (P6 drafts; the Patron submits)

- `store/play-data-safety.csv` = what the code does: Firebase Analytics (consented), AdMob rewarded (Ad ID), RevenueCat + Play Billing,
  CoddyKit house cross-promotion (impression/click counters, no PII), crash symbols. "Contains ads" = Yes. Ad ID declaration = Yes.
- `store/IARC_DRAFT.md`: no violence, no gambling (no loot boxes), no user content → expected Everyone / PEGI 3; target audience 13+
  (factory standard), not a Families game.
- `store/PRIVACY_POLICY_ADDENDUM.md`: the game's section for `https://www.coddykit.com/pages/privacy-policy`, equal to the CSV.
- Contact `coddy@coddykit.com`, support `https://coddykit.com/contact`, copyright "2026 Coddykit Inc.". Category: Game → Simulation.
- Icon: ≥ 4 candidates (lightning-bolt-in-window, lit skyline tile, pylon + moon, balance needle) + a 48/96/192/512 sheet, **none
  installed**; legible at 48 px; no colour pair of a real utility logo. Play promo video after W64-quality UI (CLAUDE §15).

## 6. Play setup — live (Patron sub-agent, 2026-10-28; backup `/Volumes/BuildSSD/game-builds/backups/voltcity-play-setup-1028/`)

| Where | What | Read-back |
|---|---|---|
| Play details | contact coddy@coddykit.com / https://coddykit.com, en-US default, free | API OK |
| Play listing | 10 languages from `store/listing` — titles keyword first, brand last (owner ASO rule): `Energy Tycoon: Grid Keeper` etc.; graphics NOT yet (waiting for the validated full-bleed set) | API 10 × OK |
| App content | privacy https://www.coddykit.com/pages/privacy-policy, no login, ads yes, AD_ID yes (advertising, analytics, fraud prevention), 13-15 / 16-17 / 18+, government / financial / health no; Game > Simulation | console "all caught up", dashboard 10/11 (store listing graphics left) |
| Data safety | `store/play-data-safety.csv` = Driftwood / Pit Lane Boss set (7 types incl. User IDs; Firebase is on by default here, so every type is "required") via `store-automation/_voltcity_data_safety.ts` | POST 204 |
| IARC | `gp-content-rating-guided.ts` slug `voltcity` (no violence / fear / gambling / substances; digital purchases yes, no loot boxes) | submitted, status Completed |
| Countries | production track: all 177 + rest of world (same as Driftwood / Pit Lane Boss) | API OK |
| AdMob UMP | app added to the published fleet GDPR (privacy URL set) + US-states messages (`_voltcity_ump.ts`) | 448 apps each, Published |
| AdMob ↔ Play link | `_voltcity_admob_link.ts` — blocked until the Play page is public (search returns 0 rows, page 404) | after production |
