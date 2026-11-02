# Grid Keeper: Energy Tycoon — Play screenshot plan (P6, Patron 2026-10-27)

Frames come later from Unity (the editor is not installed yet). This file fixes **what** each slide shows and its text, so the
capture run only has to reach the states. Slide texts in 10 languages: `store/screenshots/<lang>.json` (en tr de es fr pt ru ja
ko hi; same ids, same order). Listing texts: `store/listing/<locale>.json`. Name: `docs/NAME_CHECK.md`.

**Standard (owner 10-08, coddy circuit style):** dark brand-tinted circuit background + sparkles, icon + "Grid Keeper" header,
pill chip, localized headline (≤ 2 lines), tilted glowing phone, floating benefit chip. Per language: phone 1080 × 1920 × 8,
7" × 8, 10" × 8 + feature graphic 1024 × 500.

**Frame rules:** real gameplay states reached by playing (a seeded bot run or a real session), never mocked UI; captured on the
**Medium** tier, portrait, guide finished, **no purchase owned** (free looks, free 1-shift forecast, no gold theme), house banner
hidden (no other game's ad in our screenshots), no debug overlay, no fake ratings / download counts / player names. Each frame's
state is written to `raw/scenes.json` next to the finals (outside git, `/Volumes/BuildSSD/store-assets/volt-city-play-<date>/`).
A text on the slide may only promise what the frame or the free game shows.

| # | id | State to capture (real) | Headline (en) | Chip (en) |
|---|---|---|---|---|
| 1 | `01-peak` | Evening peak ~19:00, 4+ districts lit, needle in the green band, HUD visible, skyline at dusk | Keep the city's lights on | Real-time power grid |
| 2 | `02-storm` | Thunderstorm running: a spark on a line, part of Riverside dark, a crew truck on its way along the road, Crews tab half sheet | Send crews when storms hit | 15 kinds of events |
| 3 | `03-balance` | Grid tab open: Old Smoky ramping up (“takes effect in …”), gas peaker started, battery set to Discharge, needle moving back to green | Balance supply and demand | Steam, gas, hydro, wind, sun |
| 4 | `04-forecast` | Forecast sheet showing the **next shift** with a heatwave (the free 1-shift forecast, not Long-Range), battery charging in the background | Plan ahead for heatwaves | Next-shift forecast |
| 5 | `05-ripple` | The lights-on ripple mid-wave in a newly hooked district (Stadium Row or Market Square), "is back on!" toast | Light up new districts | 8 districts |
| 6 | `06-build` | Build tab with a wind farm and a solar field bought, turbines turning in the diorama, late afternoon light | Build a cleaner grid | 50 investments |
| 7 | `07-shed` | A real deficit: the shed / restore list with Hospital Hill pinned last (lock), one low-priority district dark | Decide who stays lit | Hospital Hill comes last |
| 8 | `08-report` | Shift report with 3 stars, uptime %, profit, Collect button (no rewarded-ad button in the frame) | Earn three-star shifts | 5 chapters + endless mode |
| F | feature | Skyline split: left half dark, right half lit, one lightning bolt; "Grid Keeper" wordmark + tagline | Keep the lights on (tagline) | — |

Notes
- Slide 4 must not show a paid feature: if the heatwave is not in the next shift on the capture seed, show a cold snap and keep
  the headline "Plan ahead …" honest (translate the alternative only if the frame changes; the copy files hold the heatwave text).
- Slide 8 chip says "endless mode": it is in the free game after chapter 5 (`chapters.json` `endless`, string `toast.city_done`).
- The weekly "Grid Challenge" from DESIGN.md is **not built** → it is in no slide and no listing text. Same for the reserved
  `scenarios` product.
- Text limits (renderer): headline ≤ 28 chars (ja/ko ≤ 14), chip ≤ 28 (ja/ko ≤ 14), tagline ≤ 32 (ja/ko ≤ 16);
  `python3 games/volt-city/store/validate.py` checks them.
