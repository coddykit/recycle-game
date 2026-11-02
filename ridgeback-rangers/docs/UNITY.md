# Ridgeback Rangers — Unity notes

Unity 6 LTS + URP, Android only, portrait, `com.coddykit.ridgebackrangers`. Built on the fleet's Unity path (Volt City,
`games/volt-city/docs/UNITY.md` in the monorepo — read it; this file only says what is specific here). Every fleet rule
(`CLAUDE.md`, `LESSONS_LEARNED.md`) applies.

## 1. Assemblies

| Assembly | Engine | What |
|---|---|---|
| `CoddyKit.Core` / `CoddyKit.Runtime` / bridges / `CoddyKit.Editor` | — / yes | the fleet kit, copied verbatim from Volt City (only the house-ads table got a provisional `ridgeback-rangers` row) |
| `RidgebackRangers.Sim` | none (`noEngineReferences`) | the simulation + save codec + balance bot |
| `RidgebackRangers.Game` | none | session, actions, coach, commerce, rewards, review hook, analytics catalogue, strings, formatting |
| `RidgebackRangers.App` | yes | kit config (ids), boot curtain |
| `RidgebackRangers.World` | yes (+ Input System) | island diorama (`IslandWorld`), dinosaurs (`Herd`), rangers + visitors (`People`), camera rig, picking |
| `RidgebackRangers.UI` | yes (+ Input System) | entry point (`RidgebackMain`), screens, sheets, guide view, audio |
| `RidgebackRangers.Editor` | editor | project setup + scenes, Android build, fonts, icons, art import |
| tests | EditMode | `Assets/Tests/EditMode` (sim), `Assets/Tests/Game` (game layer) — asmdefs committed (`<NS>.Sim.Tests`, `<NS>.Game.Tests`) |

## 2. First open (owner / Patron, with the Unity editor)

1. Unity Hub: **Unity 6 LTS (6000.3.x)** with Android Build Support (+ OpenJDK, SDK & NDK). The project pins
   `ProjectSettings/ProjectVersion.txt` = 6000.3.27f1 (the Volt City editor); a newer 6000.3 is fine.
2. `sh ridgeback-rangers/unity/Tools/fetch_packages.sh` (Google Play Review / Core / Common + Firebase tarballs → `Packages/tarballs/`, gitignored).
   Google Mobile Ads + RevenueCat come from OpenUPM (scoped registry already in `Packages/manifest.json`).
3. Open `ridgeback-rangers/unity`. `RidgebackProjectSetup` runs by itself on the first open: fleet Android policy, URP Low / Medium
   / High assets, quality levels, kit PanelSettings + fonts, palette materials, `Boot` + `Island` scenes, build scenes.
   Menu: **Coddy › Ridgeback Rangers › Apply Project Setup**, **Coddy › Validate Project** (`PROJECT_VALID`).
4. Press Play in `Assets/Scenes/Boot.unity` (or `Island.unity` directly — it boots the kit itself). Expected: curtain →
   island → intro pager → guide step 1 (spotlight on the Chief's badge).
5. Test runner (asmdefs already committed; if the editor complains, compare with Volt City's): `RidgebackRangers.Sim.Tests` (refs `RidgebackRangers.Sim`, nunit,
   `UNITY_INCLUDE_TESTS`) in `Assets/Tests/EditMode`, `RidgebackRangers.Game.Tests` (refs Sim, Game, CoddyKit.Core,
   `RidgebackRangers.Sim.Tests`) in `Assets/Tests/Game`. Then Test Runner → EditMode → Run All (125 expected, as under .NET; `EveryLanguageIsComplete` fails until the central translation pass).
6. **One commit** with everything the editor generated: `ProjectSettings/*`, `Packages/packages-lock.json`,
   `Assets/Settings/*`, `Assets/Scenes/*`, `Assets/Resources/materials/*.mat`, `Assets/Resources/ui/fonts/RidgebackPanelText.asset`,
   all `.meta` files. Never `Library/`, `Temp/`, `Logs/`, `UserSettings/`.

Batch alternative (agent on the Mini):
```sh
U=/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity
$U -batchmode -nographics -quit -projectPath ridgeback-rangers/unity -executeMethod RidgebackRangers.EditorTools.RidgebackProjectSetup.Apply -logFile -
$U -batchmode -nographics -projectPath ridgeback-rangers/unity -runTests -testPlatform EditMode -testResults /tmp/editmode.xml -logFile -
```

**Things to look at first in the editor** (written without one): `Resources/kit/props.bytes` imports as a TextAsset
(`.bytes`) and `KitBank` logs no warning — buildings, trees, the volcano etc. are merged from it (a missing bank silently
falls back to the boxes); FBX axis of the tram car (now under a yaw pivot, the importer rotation is kept: `IslandWorld.Prop`) and of the Meshy dinosaurs (faces +Z after `glb2fbx.py`; `Herd` adds no yaw — check they walk head first), palette point filter (`RidgebackArtImport`), USS import warnings, marker placement
(`RuntimePanelUtils.ScreenToPanel` with top-left coordinates), the spotlight on sheet pills (`Target_*` names), the
house banner lifting the bottom bar (`SafeAreaPadding`), Low tier draw count (target ≤ 120, fleet ≤ 150).

## 3. Verified without the editor (2026-11-02, cloud session)

| Check | Result | How |
|---|---|---|
| Sim + Game + CoddyKit.Core compile netstandard2.1 / C# 9, `-warnaserror` | PASS | `Tools/dotnet_check.sh` (.NET 8) |
| NUnit EditMode tests (sim 41, game 29, kit 55) | **125 / 125** (P2 polish run; translations merged) | same |
| Engine-bound code (kit Runtime + App + World + UI) vs UnityEngine 2021.3 reference modules + Input System stubs | PASS except 2 Unity-6-only APIs (`QualitySettings.count`, `SystemLanguage.Hindi`) | `Tools/unity_ref_check.sh` |
| Every `.cs` under Assets parses as C# 9 (editor+tests and Android+bridges define sets) | 85 files, 0 errors | same |
| Static gates (one MobileAds init = the kit's, no AdMob interstitial/banner, no PlayerPrefs, no "bought anything" ad logic, no trial wording, no franchise / reference-game names (Jurassic, Codigames…), no secrets, generated files fresh, art present) | PASS | `Tools/check.sh` |

## 4. Release ids and accounts (P8 — Patron only)

`RidgebackGameConfig.AdMobRewardedAndroid`, `RevenueCatAndroidKey`: empty → no rewarded video and a fake store in release.
`RidgebackAndroidBuild` reads `RIDGEBACKRANGERS_ADMOB_APP_ID`, `RIDGEBACKRANGERS_KEYSTORE*`, `RIDGEBACKRANGERS_VERSION_*`,
`RIDGEBACKRANGERS_RELEASE` (from Volt City's build script) and fails a production build without them. Firebase:
`ridgeback-rangers/config/google-services.json` (not in git). Store / AdMob / RevenueCat / ads_app registration: CLAUDE.md §15.

## 5. Differences from Volt City

- Diorama is procedural (map JSON → island, service road, tram rail, paddocks, foliage) on ONE palette material; Blender kit
  buildings + 4 Meshy dinosaur families (one instanced material per species = colour morph).
- No world-space UI: pins, the gate, the tram and empty-slot markers are UI Toolkit buttons placed from the camera at 30 Hz (Low 10 Hz).
- Test asmdefs are committed (nunit by precompiled reference name); check they resolve on the first open.

## First editor check: face culling of procedural meshes (2026-11-02)

`MeshKit.Quad` was fixed fleet-wide to wind triangles clockwise (Unity front faces) — in this game only at the P2 polish
(2026-11-02; the copy here still had the old order). The merge-bank props have a known axis and winding (checked in Blender
with back-face culling on, `tools/art/store_art.py`). Helpers that pass their corners in
the opposite order (pour-and-roast's `Cyl` was one, fixed) would now show inside-out. On the first editor open, look at
every procedural prop / building / ground strip on Medium with back-face culling on: a missing or see-through face means
that helper's corner order must be swapped. The Blender kit FBX import with a -90° X root rotation over Z-up vertices:
props must keep the import rotation.
