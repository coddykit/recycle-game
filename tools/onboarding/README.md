# Onboarding v2 hero evidence — Borderline Empire (smuggler)

The first-launch story intro (`scripts/ui/onboarding.gd`: Dot, six typed beats with live camera moves, naming the
company, Skip intro, Continue / "That's the one!" / "Start playing", `intro_done` in the save) keeps its texts, ten
languages and flow. The three beats that describe the game now also show one real frame of the game in the story card,
through the kit illustration (`addons/coddy_ui/ui_illustration.gd`, rounded cover picture, 16:9 window):

| Beat (`cam`) | Text | Hero | Moment |
|---|---|---|---|
| 2 `car` | "…this little garage is yours now. One rusty hatchback…" | `01-first-run.png` | core loop: the hatchback on its first contract |
| 3 `map` | "Out there are eight regions full of clients…" | `02-routes.png` | activity: the player's vehicles with their loads at the border crossing between the home valley and the 2nd region |
| 4 `border` | "Every border, every route, every risk…" | `03-empire.png` | growth: home valley (garage level 4) → four regions, depots, docks, border bridges |

`assets/ui/onboarding/manifest.json` records the source commit, the capture driver's sha256, the earned state, the
camera and each file's sha256. A hero file that does not load shows no picture and logs an error (never an icon).

Nothing is staged. `prepare` starts a new game in the real `Main`, taps the title card and the real "Skip intro", then
plays with the game's reference bot (`_bot_step` of `tests/soak_test.gd` / `tests/test_runner.gd`, copied verbatim:
best jobs, events auto-resolved, sensible spending) in fixed 0.25 s `GameManager.tick` steps, seed 20261024, and keeps
three ordinary saves when the moments are earned (`first_run`: the first job ≥ 35 % on its way, 8 game s;
`routes`: ≥ 2 regions, ≥ 3 jobs out and three of the player's moving vehicles within 16 m of each other ≥ 30 m from the
garage; `empire`: garage ≥ 4, ≥ 4 regions, ≥ 2 depots, ≥ 6 vehicles, ≥ 4 jobs out). No cash, unlock, garage, vehicle or
entitlement is set. `hero` loads such a save through the real `Main` (only the save's `time` is refreshed so no offline
earnings are added), checks garage / regions / depots / fleet / businesses / intro_done equal the earned state, taps the
title card away, hides the UI CanvasLayer and the world-space UI (region / town / HQ Label3D, job pins, route ribbon,
event bubbles, selection ring), lets the game run 2 s, renders 1024×768 on High and filters it to 512×384 RGB (lossless
re-encode, pixels verified). Cameras: `first_run` follows the hatchback at distance 26 (the tap-a-vehicle framing);
`routes` aims at the centroid of the player's vehicles right before the shot, distance 48; `empire` 25 % of the way from
the home valley to the newest region, distance 135.

Run from this game directory; every run gets a throw-away HOME (user://), project.godot is restored, the import is
refreshed first, native modes take `novareka-dev/shot-lock.sh` (perf_gate.sh takes it itself), Dummy audio.

```sh
E=/Volumes/BuildSSD/game-builds/onboarding-v2/smuggler
ONBOARDING_MODE=prepare ONBOARDING_OUT=$E/moments-s3 python3 tools/onboarding/capture_heroes.py
ONBOARDING_MODE=heroes ONBOARDING_MOMENTS=$E/moments-s3 ONBOARDING_OUT=$E/heroes-s3 python3 tools/onboarding/capture_heroes.py
# look at the three PNGs (and the *-native.png sources) before installing:
python3 ../tools/ui/prepare_onboarding_heroes.py . $E/heroes-s3 --install
ONBOARDING_MODE=cards ONBOARDING_OUT=$E/cards-s3 python3 tools/onboarding/capture_heroes.py     # 10 languages × 4 sizes × 6 beats
ONBOARDING_MODE=memory ONBOARDING_OUT=$E/memory-s3/after python3 tools/onboarding/capture_heroes.py
ONBOARDING_MODE=tests ONBOARDING_OUT=$E/tests-s3 python3 tools/onboarding/capture_heroes.py     # = jenkins/build.sh test
ONBOARDING_MODE=perf ONBOARDING_OUT=$E/perf-s3 python3 tools/onboarding/capture_heroes.py
```

- `cards`: the shipped intro over a brand-new Low game in a fixed-size SubViewport (720×1280, 720×1560, 960×1280,
  1280×720 → the project's 720×1280 `expand` logical size), all ten languages (`ONBOARDING_LANGS=ru,zh` for a quick
  re-check; the evidence run is all ten). Per beat, after the typewriter line is complete: step label, text, button
  label, Skip intro and the card inside the screen; on beats 2–4 the visible hero is that beat's 512×384 file inside the
  card, on the others no picture. Then ONE real press/release on the beat's button; the last one ("Start playing") must
  finish the intro once, save `intro_done`, give the camera back and free the intro (240 beats, 240 taps, 40 last-beat
  taps).
- `memory`: Low, new game, first-launch intro peak (title → all six beats tapped through) and Replay intro ×5 counts
  (HUD subtree nodes, resources, orphans exact; the whole tree ±4 because ambient traffic comes and goes). For "before",
  run the same driver on a `git archive` copy of the commit before (`$E/memory-s3/before`). Static engine memory, not
  Android PSS.
- `tests/onboarding_hero_test.tscn` (in `jenkins/build.sh test` and `tools/check.sh`): three different imported 512×384
  RGB heroes ≤ 1 MB, manifest sha256 / earned state, every beat's picture (no fallback), texts, real mouse and finger
  taps (touch + its emulated mouse copy), a finger on the card text, Skip on a hero beat, de / ja, Replay ×8 leak.

Evidence of #119 (S3, 2026-10-24) is under `$E/*-s3`.
