# Bottle Route — Unity notes

Unity 6 LTS + URP, Android only, portrait, `com.coddykit.bottleroute`. Built on the fleet's Unity path (Volt City,
`games/volt-city/docs/UNITY.md` in the monorepo — read it; this file only says what is specific here). Every fleet rule
(`CLAUDE.md`, `LESSONS_LEARNED.md`) applies.

## 1. Assemblies

| Assembly | Engine | What |
|---|---|---|
| `CoddyKit.Core` / `CoddyKit.Runtime` / bridges / `CoddyKit.Editor` | — / yes | the fleet kit, copied verbatim from Volt City (only the house-ads table got a provisional `bottle-route` row) |
| `BottleRoute.Sim` | none (`noEngineReferences`) | the simulation + save codec + balance bot |
| `BottleRoute.Game` | none | session, actions, coach, commerce, rewards, review hook, analytics catalogue, strings, formatting |
| `BottleRoute.App` | yes | kit config (ids), boot curtain |
| `BottleRoute.World` | yes (+ Input System) | diorama, crowd, camera rig, picking |
| `BottleRoute.UI` | yes (+ Input System) | entry point (`BottleMain`), screens, sheets, guide view, audio |
| `BottleRoute.Editor` | editor | project setup + scenes, Android build, fonts, icons, art import |
| tests | EditMode | `Assets/Tests/EditMode` (sim), `Assets/Tests/Game` (game layer) — **asmdefs to add in the editor** (see §2 step 5) |

## 2. First open (owner / Patron, with the Unity editor)

1. Unity Hub: **Unity 6 LTS (6000.3.x)** with Android Build Support (+ OpenJDK, SDK & NDK). The project pins
   `ProjectSettings/ProjectVersion.txt` = 6000.3.27f1 (the Volt City editor); a newer 6000.3 is fine.
2. `sh bottle-route/unity/Tools/fetch_packages.sh` (Google Play Review / Core / Common + Firebase tarballs → `Packages/tarballs/`, gitignored).
   Google Mobile Ads + RevenueCat come from OpenUPM (scoped registry already in `Packages/manifest.json`).
3. Open `bottle-route/unity`. `BottleProjectSetup` runs by itself on the first open: fleet Android policy, URP Low / Medium
   / High assets, quality levels, kit PanelSettings + fonts, palette materials, `Boot` + `Region` scenes, build scenes.
   Menu: **Coddy › Bottle Route › Apply Project Setup**, **Coddy › Validate Project** (`PROJECT_VALID`).
4. Press Play in `Assets/Scenes/Boot.unity` (or `Region.unity` directly — it boots the kit itself). Expected: curtain →
   town → intro pager → guide step 1 (spotlight on the Fish Market machine).
5. Test runner: create two test asmdefs (copy Volt City's): `BottleRoute.Sim.Tests` (refs `BottleRoute.Sim`, nunit,
   `UNITY_INCLUDE_TESTS`) in `Assets/Tests/EditMode`, `BottleRoute.Game.Tests` (refs Sim, Game, CoddyKit.Core,
   `BottleRoute.Sim.Tests`) in `Assets/Tests/Game`. Then Test Runner → EditMode → Run All (125 expected, as under .NET).
6. **One commit** with everything the editor generated: `ProjectSettings/*`, `Packages/packages-lock.json`,
   `Assets/Settings/*`, `Assets/Scenes/*`, `Assets/Resources/materials/*.mat`, `Assets/Resources/ui/fonts/BottlePanelText.asset`,
   all `.meta` files. Never `Library/`, `Temp/`, `Logs/`, `UserSettings/`.

Batch alternative (agent on the Mini):
```sh
U=/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity
$U -batchmode -nographics -quit -projectPath bottle-route/unity -executeMethod BottleRoute.EditorTools.BottleProjectSetup.Apply -logFile -
$U -batchmode -nographics -projectPath bottle-route/unity -runTests -testPlatform EditMode -testResults /tmp/editmode.xml -logFile -
```

**Things to look at first in the editor** (written without one): FBX axis of the Blender kit (fronts must face the camera =
-Z; `RegionWorld.BuildPoints` yaw), palette point filter (`BottleArtImport`), USS import warnings, marker placement
(`RuntimePanelUtils.ScreenToPanel` with top-left coordinates), the spotlight on sheet pills (`Target_*` names), the
house banner lifting the bottom bar (`SafeAreaPadding`), Low tier draw count (target ≤ 120, fleet ≤ 150).

## 3. Verified without the editor (2026-11-02, cloud session)

| Check | Result | How |
|---|---|---|
| Sim + Game + CoddyKit.Core compile netstandard2.1 / C# 9, `-warnaserror` | PASS | `Tools/dotnet_check.sh` (.NET 8) |
| NUnit EditMode tests (sim 43, game 27, kit 55) | **125 / 125** | same |
| Engine-bound code (kit Runtime + App + World + UI) vs UnityEngine 2021.3 reference modules + Input System stubs | PASS except 2 Unity-6-only APIs (`QualitySettings.count`, `SystemLanguage.Hindi`) | `Tools/unity_ref_check.sh` |
| Every `.cs` under Assets parses as C# 9 (editor+tests and Android+bridges define sets) | 83 files, 0 errors | same |
| Static gates (one MobileAds init = the kit's, no AdMob interstitial/banner, no PlayerPrefs, no "bought anything" ad logic, no trial wording, no real scheme/brand names, no secrets, generated files fresh, art present) | PASS | `Tools/check.sh` |

## 4. Release ids and accounts (P8 — Patron only)

`BottleGameConfig.AdMobRewardedAndroid`, `RevenueCatAndroidKey`: empty → no rewarded video and a fake store in release.
`BottleAndroidBuild` reads `BOTTLEROUTE_ADMOB_APP_ID`, `BOTTLEROUTE_KEYSTORE*`, `BOTTLEROUTE_VERSION_*`,
`BOTTLEROUTE_RELEASE` (from Volt City's build script) and fails a production build without them. Firebase:
`bottle-route/config/google-services.json` (not in git). Store / AdMob / RevenueCat / ads_app registration: CLAUDE.md §15.

## 5. Differences from Volt City

- Diorama is procedural (map JSON → roads, blocks, props) on ONE palette material; Blender kit props + Meshy bales.
- No world-space UI: fill gauges are UI Toolkit markers placed from the camera at 30 Hz (Low 10 Hz).
- Test asmdefs are not committed yet (they need editor GUIDs only for the nunit reference; see §2 step 5).

## First editor check: face culling of procedural meshes (2026-11-02)

`MeshKit.Quad` was fixed fleet-wide to wind triangles clockwise (Unity front faces). Helpers that pass their corners in
the opposite order (pour-and-roast's `Cyl` was one, fixed) would now show inside-out. On the first editor open, look at
every procedural prop / building / ground strip on Medium with back-face culling on: a missing or see-through face means
that helper's corner order must be swapped. The Blender kit FBX import with a -90° X root rotation over Z-up vertices:
props must keep the import rotation.
