# Grill Convoy — Unity notes

Unity 6 LTS + URP, Android only, portrait, `com.coddykit.grillconvoy`. Built on the fleet's Unity path (Volt City,
`games/volt-city/docs/UNITY.md` in the monorepo — read it; this file only says what is specific here). Every fleet rule
(`CLAUDE.md`, `LESSONS_LEARNED.md`) applies.

## 1. Assemblies

| Assembly | Engine | What |
|---|---|---|
| `CoddyKit.Core` / `CoddyKit.Runtime` / bridges / `CoddyKit.Editor` | — / yes | the fleet kit, copied verbatim from Volt City (only the house-ads table got a provisional `grill-convoy` row) |
| `GrillConvoy.Sim` | none (`noEngineReferences`) | the simulation + save codec + balance bot |
| `GrillConvoy.Game` | none | session, actions, coach, commerce, rewards, review hook, analytics catalogue, strings, formatting |
| `GrillConvoy.App` | yes | kit config (ids), boot curtain |
| `GrillConvoy.World` | yes (+ Input System) | diorama, crowd, camera rig, picking |
| `GrillConvoy.UI` | yes (+ Input System) | entry point (`GrillMain`), screens, sheets, guide view, audio |
| `GrillConvoy.Editor` | editor | project setup + scenes, Android build, fonts, icons, art import |
| tests | EditMode | `Assets/Tests/EditMode` (sim), `Assets/Tests/Game` (game layer) — **asmdefs to add in the editor** (see §2 step 5) |

## 2. First open (owner / Patron, with the Unity editor)

1. Unity Hub: **Unity 6 LTS (6000.3.x)** with Android Build Support (+ OpenJDK, SDK & NDK). The project pins
   `ProjectSettings/ProjectVersion.txt` = 6000.3.27f1 (the Volt City editor); a newer 6000.3 is fine.
2. `sh grill-convoy/unity/Tools/fetch_packages.sh` (Google Play Review / Core / Common + Firebase tarballs → `Packages/tarballs/`, gitignored).
   Google Mobile Ads + RevenueCat come from OpenUPM (scoped registry already in `Packages/manifest.json`).
3. Open `grill-convoy/unity`. `GrillProjectSetup` runs by itself on the first open: fleet Android policy, URP Low / Medium
   / High assets, quality levels, kit PanelSettings + fonts, palette materials, `Boot` + `Region` scenes, build scenes.
   Menu: **Coddy › Grill Convoy › Apply Project Setup**, **Coddy › Validate Project** (`PROJECT_VALID`).
4. Press Play in `Assets/Scenes/Boot.unity` (or `Region.unity` directly — it boots the kit itself). Expected: curtain →
   Cinder Bay → intro pager → guide step 1 (spotlight on "Open the window", truck #1 at the Office Plaza).
5. Test runner: create two test asmdefs (copy Volt City's): `GrillConvoy.Sim.Tests` (refs `GrillConvoy.Sim`, nunit,
   `UNITY_INCLUDE_TESTS`) in `Assets/Tests/EditMode`, `GrillConvoy.Game.Tests` (refs Sim, Game, CoddyKit.Core,
   `GrillConvoy.Sim.Tests`) in `Assets/Tests/Game`. Then Test Runner → EditMode → Run All (140 expected, as under .NET;
   `EveryLanguageIsComplete` fails until the 9 translations are merged centrally).
6. **One commit** with everything the editor generated: `ProjectSettings/*`, `Packages/packages-lock.json`,
   `Assets/Settings/*`, `Assets/Scenes/*`, `Assets/Resources/materials/*.mat`, `Assets/Resources/ui/fonts/GrillPanelText.asset`,
   all `.meta` files. Never `Library/`, `Temp/`, `Logs/`, `UserSettings/`.

Batch alternative (agent on the Mini):
```sh
U=/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity
$U -batchmode -nographics -quit -projectPath grill-convoy/unity -executeMethod GrillConvoy.EditorTools.GrillProjectSetup.Apply -logFile -
$U -batchmode -nographics -projectPath grill-convoy/unity -runTests -testPlatform EditMode -testResults /tmp/editmode.xml -logFile -
```

**Things to look at first in the editor** (written without one): FBX axis of the Blender trucks (the cab must lead when
driving and the hatch face the crowd: hatches are on both sides, the heading offset is `RegionWorld.KitYaw`), palette
point filter (`GrillArtImport`), USS import warnings, marker placement (`RuntimePanelUtils.ScreenToPanel` with top-left
coordinates), the spotlight on sheet pills / layout tiles (`Target_*` names), the layout grid at 360 dp width (5×4 tiles
of 64 dp), the house banner lifting the bottom bar (`SafeAreaPadding`), Low tier draw count (plan: city map ≤ 80, fleet ≤ 150).

## 3. Verified without the editor (2026-11-02, cloud session)

| Check | Result | How |
|---|---|---|
| Sim + Game + CoddyKit.Core compile netstandard2.1 / C# 9, `-warnaserror` | PASS | `Tools/dotnet_check.sh` (.NET 8) |
| NUnit EditMode tests (sim 54, game 31, kit 55) | **139 / 140** — only `EveryLanguageIsComplete` (translations come centrally) | same |
| Engine-bound code (kit Runtime + App + World + UI) vs UnityEngine 2021.3 reference modules + Input System stubs | PASS except 2 Unity-6-only APIs (`QualitySettings.count`, `SystemLanguage.Hindi`) | `Tools/unity_ref_check.sh` |
| Every `.cs` under Assets parses as C# 9 (editor+tests and Android+bridges define sets) | 84 files, 0 errors | same |
| Static gates (one MobileAds init = the kit's, no AdMob interstitial/banner, no PlayerPrefs, no "bought anything" ad logic, no trial wording, no real burger chain / reference-game names, no secrets, generated files fresh, art present) | PASS | `Tools/check.sh` |

## 4. Release ids and accounts (P8 — Patron only)

`GrillGameConfig.AdMobRewardedAndroid`, `RevenueCatAndroidKey`: empty → no rewarded video and a fake store in release.
`GrillAndroidBuild` reads `GRILLCONVOY_ADMOB_APP_ID`, `GRILLCONVOY_KEYSTORE*`, `GRILLCONVOY_VERSION_*`,
`GRILLCONVOY_RELEASE` (from Volt City's build script) and fails a production build without them. Firebase:
`grill-convoy/config/google-services.json` (not in git). Store / AdMob / RevenueCat / ads_app registration: CLAUDE.md §15.

## 5. Differences from Volt City

- Diorama is procedural (map JSON → roads, blocks, pitch stages) on ONE palette material; Blender kit trucks /
  commissary; Meshy heroes (chef, cashier, fan, burger) only in pre-rendered UI art (the crowd = chunky instanced figures).
- No world-space UI: truck / pitch badges are UI Toolkit markers placed from the camera at 30 Hz (Low 10 Hz).
- The truck layout editor is a UI Toolkit grid (tap a station, tap a tile), not 3D.
- Test asmdefs are not committed yet (they need editor GUIDs only for the nunit reference; see §2 step 5).
