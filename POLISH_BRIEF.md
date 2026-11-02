# Cloud polish pass (per game) — store assets → visuals → tempo

Everything here is doable without the Unity editor. Work only inside your game folder `<slug>/`. No git. Fleet rules:
`CLAUDE.md` (summary in `GAME_TEMPLATE.md`). Keep `sh <slug>/unity/Tools/check.sh` fully PASS at the end (pass your scratch
dir as TMPDIR / BR_CHECK_DIR / UNITY_REF_DIR). Read only what you need.

## 1. Store assets (`<slug>/store/`)

- `listing/en-US.json` — format of `tools/store_templates/en-US.json`: `title` ≤ 30 (keyword first, brand after a colon, as
  the plan's §11 ASO section says), `shortDescription` ≤ 80, `fullDescription` ≤ 4000 (sections with CAPS headers + "-"
  bullets like the template; what the player does, the chaos, progression, fair monetization line). **English only** — the
  other 9 locales are translated centrally afterwards. No trial / "free" / price / ranking claims, no real brands, scheme
  names, competitor or reference-game names; name the ad-removing products exactly as in `iap.json`; end with the privacy
  policy URL `https://www.coddykit.com/pages/privacy-policy`.
- `screenshots/en.json` — 8 shots: `{"shots":[{"id":"s1","headline":"≤ 30 chars","sub":"≤ 50 chars","scene":"what the frame
  shows"}], "feature":"feature-graphic tagline ≤ 40 chars"}` (captured later from the running game; this is the plan + copy).
- `play-data-safety.csv` — copy `tools/store_templates/play-data-safety.csv` unchanged (same SDK stack: AdMob rewarded,
  RevenueCat, Firebase Analytics); if your game differs (e.g. kids/Families), adjust and say so.
- `validate.py` — adapt `tools/store_templates/validate.py` to your game (names, banned-words list incl. your plan's
  originality list, the ad-free product names, 1 locale now; it must accept the 10 locales later) and call it from
  `unity/Tools/check.sh`.
- `feature-graphic.png` 1024×500 and `icon-candidates/c1..c3.png` (512×512, full-bleed, readable at 48 px, no text) rendered
  in Blender from your own kit / Meshy models (`<slug>/tools/art/`). Keep the current placeholder in `unity/Assets/Art/Icon`;
  the owner picks a candidate later (`docs/ART.md`: list the candidates).

## 2. Visual quality (Low tier must not get heavier)

Replace the most "placeholder" parts of your diorama with Blender kit models on the palette UVs (bevelled, like
`bottle-route/tools/art/kit.py`): buildings / venue shells / district blocks / key props your plan's §8 names. Rules:
one palette material; each prop ≤ ~600 tris; load from `Resources/kit` with the procedural fallback kept; **Low tier:
0 extra draws** (merge static props into the district mesh or instance them; new animations / particles / extra lights
only on Medium/High, gated by the tier); no per-frame allocation. Meshy only for large / close-up organic things, ≤ 60
more credits (`<slug>/tools/meshy/meshy.py`, never two optimize.py in the same out dir at once). Re-render the UI art
(`render_ui.py`) if the look changed. Update `docs/ART.md` (what changed, tris per prop, Low draw estimate) and the
`check.sh` art presence list.

## 3. Tempo to the plan

Tune `Resources/data/*.json` so the balance bot reaches the plan's chapter / district times (plan §6 content map) within
about ±25 % at 1× (e.g. chapter 2 at ~1–1.5 h when the plan says so), keep day 1 profitable and forgiving, keep the
7-day test green. Update `docs/BALANCE.md` with before/after tables.

## Report (short)

Store files written (title / short description), icon candidates, what visuals changed (tris, Low draws), tempo
before → after, Meshy credits, anything that needs the owner.
