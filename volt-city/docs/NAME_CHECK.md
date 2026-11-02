# Name / trademark knockout — P6 (Patron, 2026-10-27)

**Decision (game owner): the store name is `Grid Keeper: Energy Tycoon`** (26 chars; launcher label `Grid Keeper`).
The recommended F0 name "Volt City: Power Grid Manager" is **knocked out** (identical registered mark in our classes + a
same-theme game on Google Play). Both F0 backups failed too. Folder `games/volt-city`, package `com.coddykit.voltcity`,
C# namespaces and file names stay (internal codename, never shown to players).

This is a knockout search on public registers, not a legal clearance opinion. Raw evidence (screenshots, captured
register JSON, store search results) outside git: `/Volumes/BuildSSD/game-builds/volt-city/p6/namecheck/`.

## Method

| Source | How | Classes / scope |
|---|---|---|
| **USPTO Trademark Search** (tmsearch.uspto.gov, the TESS replacement) | Playwright headless, temp profile; the term **typed into the search box + Enter** (URL params do not search); the page's own `prod-stage-v1-0-0/tmsearch` response (top 100 by relevance, exact phrase boosted ×5) captured and filtered for live marks in IC 009 / 028 / 041 | all US marks, live + dead |
| **TMview** (EUIPO / TMDN, 80+ offices incl. EM, GB, US, WO, TR, JP, KR, IN, BR, RU) | Playwright headless; term typed into "Type trade mark name" + Enter ("Contains"); then the same `api/search/results` the UI calls, all pages up to 500, filtered for Nice 9 / 28 / 41 and status Registered / Filed | 9, 28, 41 |
| **Google Play** | Playwright, `play.google.com/store/search?c=apps`, en-US / US | exact title + theme phrase |
| **App Store** | iTunes Search API, US, entity software | exact title + theme phrase |

The USPTO site's CloudFront WAF blocked the first burst (403 for ~10 min); the run was repeated with 40 s gaps between
searches and only the UI's own request (no extra API calls).

## Results

| Name | Verdict | Evidence |
|---|---|---|
| Volt City: Power Grid Manager (F0 recommendation) | **REJECTED** | **TMview: `VOLT CITY` EUTM 018162764, Registered, Nice 9 / 28 / 41, Wazdan Innovations Limited (a games company), filed 2019-12-06; same mark UK00918162764 (GB), Registered, 9 / 28 / 41.** Identical mark, identical classes. Google Play: **"Voltcity: Power Puzzle"** (Kibou Games, `com.kibougames.cableconnect`) — same word, same electricity theme. USPTO: no `VOLT CITY` mark, but `VOLT` is live in IC 041 for **Oncor Electric Delivery** (a real utility, sn 87765151) and in IC 009 / 028 for many owners (527 VOLT records). App Store: "Volt Block: City Lights" (already on the IP denylist). |
| Blackout Boss: City Power Sim (backup 1) | **REJECTED** | No `BLACKOUT BOSS` mark anywhere (TMview 0, USPTO none in top 100), no app with that title. But the dominant word is crowded in our classes: USPTO `BLACKOUT` live IC 009 (Survivor 21 LLC sn 88028936, Blackout Technologies sn 79224073, Blackout Records `BLACKOUT!` sn 86487918), IC 028 (Bass Pro ×4, Namero, DQC); TMview `BLACKOUT` EUTM 009355711 + UK00909355711 **FremantleMedia, 9 / 41**; CN 13416707 / 13416708 **Hasbro, 41 / 9**; BX 980815 (9 / 41); `BLACKOUT BINGO` (WCLC, all classes; Skillz app on the App Store); `BLACKOUT TRIVIA`, `BLACKOUT JACKPOT` (28). Too close to too many game marks. |
| Gridline: City Power Tycoon (backup 2) | **REJECTED** | TMview **`GRIDLINE` DE 3020262301762, Registered 2026, Nice 9 / 25 / 28 / 35**; `GRIDline` EUTM 018146557 + UK (9, BOB Patent); `GRIDLINE` UK00003110349 (9 / 11, Ansell Electrical); PL filing (9). Play / App Store: several "Gridline" apps (law-enforcement, puzzles, social, dashboards). |
| Wattville: … | REJECTED | Registers and stores clean for the word itself (USPTO 0, TMview 0, no app), but **`VILLE` is a Zynga mark in 9 / 41 / 25** (EUTM 009774613, UK00909774613, WO 1095825, IL, NZ, IN, MX, MY, CA) and Zynga has enforced the "-ville" suffix against game names (Blingville settlement). Not worth the risk. |
| Megawatt City / Kilowatt City | REJECTED | `MEGAWATT` EUTM 019176226 + UK + US sn 99150429 (filed) **Curieus Games CIC, 28 / 41**; `KILOWATT` UK00004463746 **filed 2026-10-22, 28 / 41** (Watt Next Ltd). |
| Peak Load / Ohmstead / Joule City | clean, not chosen | TMview: no mark in 9 / 28 / 41 (Peak Load 14 records, none in our classes; Ohmstead 1, Joule City 1, both outside). Weaker for players (technical term / hard to spell) and for search. |
| **Grid Keeper: Energy Tycoon** | **CHOSEN — low risk** | **USPTO:** no `GRID KEEPER` / `GRIDKEEPER` mark (`gridkeeper` 0 results; `grid keeper` top 100 = only separate `KEEPER` and `GRID` marks of unrelated owners). **TMview:** `gridkeeper` 0; `grid keeper` 1 record = `SEAKEEPER GRID` (US 50048581, filed, class 9, marine stabilisers) — different mark and goods. **Google Play:** no app named Grid Keeper / Gridkeeper (nearest: "Grid Manager" by MSA Safety, a business tool). **App Store:** none. The word `GRID` alone is diluted (Codemasters' racing "GRID", GRID eSports, Grid.com, Figure AI, RIAA, Sensory Software …); the composite "Grid Keeper" with a power-network meaning is distinct from all of them. `Energy Tycoon` is a generic genre phrase. |

## Why the subtitle is "Energy Tycoon", not "Power Grid Manager"

- `POWER GRID` is a **registered US mark of Grand Vision Gaming LLC in IC 009 (sn 85097799) and IC 041 (sn 86864552)**,
  `POWER-GRID` is Turtle Beach's (EUTM 018579259 / 011406717, UK, CA, MX, class 9), and the board game's official app
  "Power Grid" (Brettspielwelt, `de.brettspielwelt.funkenschlag`) is on Google Play. A capitalised "Power Grid …" in our
  title would sit next to all of them. Store texts use the generic lowercase phrase "power grid" only (IP rule kept).
- "Energy Manager" is a competitor's title (Xombat / Trophy Games "Energy Manager - 2026/2027") — not used.
- "City Power" is the name of a real utility (Johannesburg) and of an EV app — not used in the title.
- ASO: the title carries `energy tycoon` (Medium, difficulty 42) and `grid`; the short description opens with the
  lowercase "power grid manager" (`grid manager` D24, `power grid` D38) — Play matches across title + short + long text.

## Localised titles

Brand `Grid Keeper` stays in Latin script in every locale (like Driftwood); the subtitle is the local genre phrase for
"energy tycoon", ≤ 30 chars (see `store/listing/<locale>.json`).

## Follow-ups (not blocking the listing)

- Player-facing rename done in P6: `VoltGameConfig.ProductName` + `ProjectSettings.productName` = "Grid Keeper" (launcher
  label), bundle IAP "Complete Grid Keeper Collection" ×10 languages (docs/IAP.md, i18n_p4, regenerated iap.json /
  rc-catalog.json / catalog.json), games/CLAUDE.md §8 row. The game strings never name the city, so no UI text changes.
- Icon candidates must not show the word "Volt"; the P2 splash / curtain have no baked text (checked `blender/splash_art.py`).
- RevenueCat project name "Grid Keeper" (not "Volt City") when the Patron runs `rc-setup`; Play app title = this file.
- Re-run the Play search for "Grid Keeper" right before `gp-create-app` (new apps appear daily).
