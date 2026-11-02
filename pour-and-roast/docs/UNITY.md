# Pour & Roast — Unity notes

Unity 6 LTS + URP, Android only, portrait, `com.coddykit.pourandroast`. Built on the fleet's Unity path (Volt City,
`games/volt-city/docs/UNITY.md` in the monorepo — read it; this file only says what is specific here). Every fleet rule
(`CLAUDE.md`, `LESSONS_LEARNED.md`) applies.

## 1. Assemblies

| Assembly | Engine | What |
|---|---|---|
| `CoddyKit.Core` / `CoddyKit.Runtime` / bridges / `CoddyKit.Editor` | — / yes | the fleet kit, copied verbatim from Volt City (only the house-ads table got a provisional `pour-and-roast` row) |
| `PourAndRoast.Sim` | none (`noEngineReferences`) | the simulation (ticket rail, stations, staff, stocks, roastery, wholesale, crises, economy) + save codec + balance bot |
| `PourAndRoast.Game` | none | session, actions, coach, commerce, rewards, review hook, analytics catalogue, strings, formatting |
| `PourAndRoast.App` | yes | kit config (ids), boot curtain |
| `PourAndRoast.World` | yes (+ Input System) | cafe diorama (`CafeWorld`), customers + staff (`Crowd`), camera rig, picking |
| `PourAndRoast.UI` | yes (+ Input System) | entry point (`RoastMain`), screen (rail, ticket bar, station markers), sheets, guide view, audio |
| `PourAndRoast.Editor` | editor | project setup + scenes, Android build, fonts, icons, art import |
| tests | EditMode | `Assets/Tests/EditMode` (sim), `Assets/Tests/Game` (game layer) — asmdefs committed (`<NS>.Sim.Tests`, `<NS>.Game.Tests`) |

## 2. First open (owner / Patron, with the Unity editor)

1. Unity Hub: **Unity 6 LTS (6000.3.x)** with Android Build Support (+ OpenJDK, SDK & NDK). The project pins
   `ProjectSettings/ProjectVersion.txt` = 6000.3.27f1 (the Volt City editor); a newer 6000.3 is fine.
2. `sh pour-and-roast/unity/Tools/fetch_packages.sh` (Google Play Review / Core / Common + Firebase tarballs → `Packages/tarballs/`, gitignored).
   Google Mobile Ads + RevenueCat come from OpenUPM (scoped registry already in `Packages/manifest.json`).
3. Open `pour-and-roast/unity`. `RoastProjectSetup` runs by itself on the first open: fleet Android policy, URP Low / Medium
   / High assets, quality levels, kit PanelSettings + fonts, palette materials, `Boot` + `Cafe` scenes, build scenes.
   Menu: **Coddy › Pour & Roast › Apply Project Setup**, **Coddy › Validate Project** (`PROJECT_VALID`).
4. Press Play in `Assets/Scenes/Boot.unity` (or `Cafe.unity` directly — it boots the kit itself). Expected: curtain →
   the quayside cart → intro pager → guide step 1 (spotlight on "Open the doors").
5. Test runner (asmdefs already committed; if the editor complains, compare with Volt City's): `PourAndRoast.Sim.Tests` (refs `PourAndRoast.Sim`, nunit,
   `UNITY_INCLUDE_TESTS`) in `Assets/Tests/EditMode`, `PourAndRoast.Game.Tests` (refs Sim, Game, CoddyKit.Core,
   `PourAndRoast.Sim.Tests`) in `Assets/Tests/Game`. Then Test Runner → EditMode → Run All (135 expected, as under .NET; `EveryLanguageIsComplete` fails until the central translation).
6. **One commit** with everything the editor generated: `ProjectSettings/*`, `Packages/packages-lock.json`,
   `Assets/Settings/*`, `Assets/Scenes/*`, `Assets/Resources/materials/*.mat`, `Assets/Resources/ui/fonts/RoastPanelText.asset`,
   all `.meta` files. Never `Library/`, `Temp/`, `Logs/`, `UserSettings/`.

Batch alternative (agent on the Mini):
```sh
U=/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity
$U -batchmode -nographics -quit -projectPath pour-and-roast/unity -executeMethod PourAndRoast.EditorTools.RoastProjectSetup.Apply -logFile -
$U -batchmode -nographics -projectPath pour-and-roast/unity -runTests -testPlatform EditMode -testResults /tmp/editmode.xml -logFile -
```

**Things to look at first in the editor** (written without one): FBX axis of the Blender kit (fronts must face the camera =
-Z; `CafeWorld.Prop` yaw), the counter / back bar kit scale (`scaleX = width / 5`), Meshy prop scale (sack / plant / cat),
palette point filter (`RoastArtImport`), USS import warnings, station marker placement (`RuntimePanelUtils.ScreenToPanel`
with top-left coordinates), the ticket rail width on a 360 dp phone (5 cards + "+n"), the grinder hold-to-dial-in
(PointerDown/Up on the marker), the spotlight on sheet pills (`Target_*` names), the house banner lifting the bottom bar
(`SafeAreaPadding`), camera fit per venue (`CameraRig.Fit`, metres), Low tier draw count (target ≤ 120, fleet ≤ 150).

## 3. Verified without the editor (2026-11-02, cloud session)

| Check | Result | How |
|---|---|---|
| Sim + Game + CoddyKit.Core compile netstandard2.1 / C# 9, `-warnaserror` | PASS | `Tools/dotnet_check.sh` (.NET 8) |
| NUnit EditMode tests (sim 49, game 31, kit 55) | **134 / 135** (only `EveryLanguageIsComplete`: English only until the central translation) | same |
| Engine-bound code (kit Runtime + App + World + UI) vs UnityEngine 2021.3 reference modules + Input System stubs | PASS except 2 Unity-6-only APIs (`QualitySettings.count`, `SystemLanguage.Hindi`) | `Tools/unity_ref_check.sh` |
| Every `.cs` under Assets parses as C# 9 (editor+tests and Android+bridges define sets) | 82 files, 0 errors | same |
| Static gates (one MobileAds init = the kit's, no AdMob interstitial/banner, no PlayerPrefs, no "bought anything" ad logic, no trial wording, no real coffee chains / reference game names, no secrets, generated files fresh, art present) | PASS | `Tools/check.sh` |

## 4. Release ids and accounts (P8 — Patron only)

`RoastGameConfig.AdMobRewardedAndroid`, `RevenueCatAndroidKey`: empty → no rewarded video and a fake store in release.
`RoastAndroidBuild` reads `POURANDROAST_ADMOB_APP_ID`, `POURANDROAST_KEYSTORE*`, `POURANDROAST_VERSION_*`,
`POURANDROAST_RELEASE` (from Volt City's build script) and fails a production build without them. Firebase:
`pour-and-roast/config/google-services.json` (not in git). Store / AdMob / RevenueCat / ads_app registration: CLAUDE.md §15.

## 5. Differences from Volt City

- Diorama is procedural per venue (`CafeWorld.LayoutFor`: cart on the quay / corner cafe / roastery hall) on ONE palette
  material; Blender kit props (`Resources/kit/*.fbx`) with procedural `MeshKit` fallbacks; Meshy hero props (sack, plant,
  cat) with their own 1024 / 512 textures.
- No world-space UI: station markers and the ticket rail are UI Toolkit, markers placed from the camera at 30 Hz (Low 10 Hz).
- Customers and staff are chunky instanced figures (no GameObjects); Low: ≤ 14 customers at 15 Hz.
- Save slots: `career.json` + `career_<n>.json` (Settings → Save slots; the Edition +1, Extra Saves +3); switching a slot
  reloads the scene.
- Test asmdefs are committed (nunit by precompiled reference name); check they resolve on the first open.

## First editor check: face culling of procedural meshes (2026-11-02)

`MeshKit.Quad` was fixed fleet-wide to wind triangles clockwise (Unity front faces). Helpers that pass their corners in
the opposite order (pour-and-roast's `Cyl` was one, fixed) would now show inside-out. On the first editor open, look at
every procedural prop / building / ground strip on Medium with back-face culling on: a missing or see-through face means
that helper's corner order must be swapped. The Blender kit FBX import with a -90° X root rotation over Z-up vertices:
props must keep the import rotation.
