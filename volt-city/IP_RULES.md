# Volt City: Power Grid Manager — IP / content rules (BINDING, P0 2026-10-27; Unity paths 2026-10-27)

> Built from [`docs/DESIGN.md`](docs/DESIGN.md) section 11 (binding F0 decision) and [`docs/STORE.md`](docs/STORE.md) section 1.
> Checked in every phase and every commit by `unity/Tools/ip_denylist.py` + `unity/Tools/ip_denylist.txt` (run by
> `unity/Tools/check.sh` and `jenkins/preflight.py`). The game is built in Unity (owner 2026-10-27, `docs/UNITY.md`).
> Template: [`../pit-wall/IP_RULES.md`](../pit-wall/IP_RULES.md), [`../driftwood/IP_RULES.md`](../driftwood/IP_RULES.md). When in doubt: do not use it, ask the Patron.

## 1. Principle

Volt City is a **fictional city** run by a **fictional company** ("Volt City Grid Co.") with fictional districts, plants, crews and
events. Power grids, plants, storms and blackouts are generic, real-world *ideas*; every name, look, text and mechanic name is **ours**.
If a player can say "this is that real utility / that disaster / that other game", the design is rejected.

## 2. FORBIDDEN everywhere (title, store text, ASO keywords, in-game text, data JSON, C# / scene / prefab / asset / file names, FBX/GLB/material names, icon, screenshots, translations)

| Class | Forbidden (full list: `unity/Tools/ip_denylist.txt`) |
|---|---|
| Real utilities / grid operators | National Grid, ConEd / Consolidated Edison, PG&E, Duke Energy, EDF, E.ON, Enel, TEPCO, Iberdrola, ERCOT, Statnett (and RWE, Vattenfall, Engie, SCE, Dominion, Exelon, Entergy). No brand colour pairs or logos of a utility. |
| Real plants, dams, stations | Hoover Dam, Three Gorges, Itaipu, Drax, Battersea, Grand Coulee — no real plant as a model or a name. |
| Disaster names | Chernobyl / Chornobyl, Fukushima, Three Mile Island; no meltdown imagery. |
| The board game "Power Grid" | *Power Grid* (Rio Grande Games; German *Funkenschlag*, 2F-Spiele, Friedemann Friese) is a board-game title. The words appear **only as the lowercase generic phrase "power grid" inside a sentence or after our own brand** ("Volt City: Power Grid Manager"), never alone as a name, never with "board game", never its art (no auction / city-network board look, no resource-market track). |
| Other games | SimCity, Cities: Skylines, Power to the People, Texas PowerGrid Challenge, Energy Manager, Idle Energy, Power Inc, Reactor (Energy Sector Tycoon), Volt Block: City Lights, LightsOn: Move to Build a City, Blackout Bingo — names, art, UI, icon colours, mechanic names. |
| Real people / stadiums / teams | no real executives, engineers, politicians, athletes, stadiums or teams. Crew names are **first names only** (generated names are checked against the real-people list in P1). |
| Store words | competitor names above, "free trial", "try free", "free" as a claim, prices, rankings, real energy-price claims. |

## 3. ALLOWED

- Generic grid vocabulary: power grid (lowercase, in a sentence or after our brand), grid, blackout, brownout, outage, substation, line,
  pylon, transformer, load, demand, peak, base load, peaker, wind farm, solar field, hydro, battery, storm, heatwave, repair crew.
- Real **city names** only in a descriptive store sentence if ever needed; the game's city is Volt City with its own 8 districts.
- A generic lightning bolt (two strokes) as an icon element — never a bolt in a circle badge in a utility's colour pair.

## 4. Content policy (rating target: Play 3+/7+ content, audience 13+ for ads — not Designed for Families)

- **No nuclear plant in v1** (tone + no meltdown imagery).
- **Nobody is hurt.** Outages cost money and rating, never lives. The hospital has its own backup generator and no text ever says
  patients are in danger. Crews are never injured or electrocuted. Squirrels and balloons are always "fine".
- No politics, no real energy-price claims, no climate lecture (CO2 stars are a soft score, not a moral).
- No gambling mechanic, no randomised paid items. No alcohol/tobacco. Kid-safe language in all 10 languages.
- Data/ads: audience 13+ (not Designed for Families); UMP/GDPR as the fleet.

## 5. Denylist test (P0; runs in every `unity/Tools/check.sh` and `jenkins/preflight.py`)

Same spec as Grid Boss / Driftwood, ported to Python so it runs without an engine (`unity/Tools/ip_denylist.py`):
CamelCase split (C# / asset names), lowercase, accent strip (NFKD fold), leetspeak `0->o 3->e 4->a @->a $->s 5->s 7->t`,
separators collapsed; plain entries = word-bounded regex, `~entry` = substring (non-Latin spellings); scans every text file
of `games/volt-city` (C#, USS/UXML/TSS, asmdef, data JSON, Unity YAML scenes/assets/prefabs/materials, shaders/HLSL, i18n,
`store/**` texts, docs, py/sh/Jenkinsfile) and **every file name** (models, textures, scenes); FBX/GLB node and material
names go through `blender/validate_art.py` (P2). Excluded: this file, `docs/DESIGN.md`, `docs/PLAN.md`, `docs/STORE.md`,
`store/NAME_CHECK.md` (they explain the rule), the denylists themselves, vendor SDK folders and **generated folders**
(`unity/Library`, `Temp`, `Obj`, `Logs`, `Build`, `UserSettings`, `android/build`; LL Driftwood #7). A line that only
STATES the rule in a tool header may be listed in the scanner's `ALLOW_LINES` (one entry today: P2's `build_art.py` header).
Result must be **0 matches**; the proof fixture (outside the project) must show planted words are caught (plain, separator,
accent, leetspeak, CamelCase, non-Latin, rule name, asset file name), generated folders are skipped and innocents ("national
park", "hoovering", "icon editor", "sim cities" …) pass. A second check ("nobody is hurt") scans every localisation text
(`i18n` / `Localization` CSV or JSON) that names a person or an animal for harm words (die, hurt, injured …) with its own
planted proof.

## 6. Visual checks (P2 / P6)

- Check on the **rendered pixel** (Medium and Low), not on the hex value: icon and screenshots must not read as a real utility's
  colour pair or logo, nor the *Power Grid* board-game box (network-of-cities board look).
- Icon candidates: Patron picks (owner 10-07 rule); nothing is installed without the Patron's explicit approval.
- A human review of icon, hero frames and store text is part of P6 and P7.

## 7. Trademark process for the title

Recommended title `Volt City: Power Grid Manager` (STORE.md section 1). P6 gate = knockout search in USPTO (classes 9, 41), EUIPO, WIPO
Global Brand Database, Play + App Store → `store/NAME_CHECK.md`. Backups: `Blackout Boss: City Power Sim`, `Gridline: City Power Tycoon`.
