# Semester Rush — Unity notes

Unity 6 LTS + URP, Android only, portrait, `com.coddykit.semesterrush`. Built on the fleet's Unity path (Volt City,
`games/volt-city/docs/UNITY.md` in the monorepo — read it; this file only says what is specific here). Every fleet rule
(`CLAUDE.md`, `LESSONS_LEARNED.md`) applies.

## 1. Assemblies

| Assembly | Engine | What |
|---|---|---|
| `CoddyKit.Core` / `CoddyKit.Runtime` / bridges / `CoddyKit.Editor` | — / yes | the fleet kit, copied verbatim from Volt City (only the house-ads table got a provisional `semester-rush` row) |
| `SemesterRush.Sim` | none (`noEngineReferences`) | the simulation + save codec + balance bot |
| `SemesterRush.Game` | none | session, actions, coach, commerce, rewards, review hook, analytics catalogue, strings, formatting |
| `SemesterRush.App` | yes | kit config (ids), boot curtain |
| `SemesterRush.World` | yes (+ Input System) | diorama, crowd, camera rig, picking |
| `SemesterRush.UI` | yes (+ Input System) | entry point (`SemesterMain`), screens, sheets, guide view, audio |
| `SemesterRush.Editor` | editor | project setup + scenes, Android build, fonts, icons, art import |
| tests | EditMode | `Assets/Tests/EditMode` (sim), `Assets/Tests/Game` (game layer) — **asmdefs to add in the editor** (see §2 step 5) |

## 2. First open (owner / Patron, with the Unity editor)

1. Unity Hub: **Unity 6 LTS (6000.3.x)** with Android Build Support (+ OpenJDK, SDK & NDK). The project pins
   `ProjectSettings/ProjectVersion.txt` = 6000.3.27f1 (the Volt City editor); a newer 6000.3 is fine.
2. `sh semester-rush/unity/Tools/fetch_packages.sh` (Google Play Review / Core / Common + Firebase tarballs → `Packages/tarballs/`, gitignored).
   Google Mobile Ads + RevenueCat come from OpenUPM (scoped registry already in `Packages/manifest.json`).
3. Open `semester-rush/unity`. `SemesterProjectSetup` runs by itself on the first open: fleet Android policy, URP Low / Medium
   / High assets, quality levels, kit PanelSettings + fonts, palette materials, `Boot` + `Campus` scenes, build scenes.
   Menu: **Coddy › Semester Rush › Apply Project Setup**, **Coddy › Validate Project** (`PROJECT_VALID`).
4. Press Play in `Assets/Scenes/Boot.unity` (or `Campus.unity` directly — it boots the kit itself). Expected: curtain →
   campus → intro pager → guide step 1 (spotlight on the Main Hall).
5. Test runner: create two test asmdefs (copy Volt City's): `SemesterRush.Sim.Tests` (refs `SemesterRush.Sim`, nunit,
   `UNITY_INCLUDE_TESTS`) in `Assets/Tests/EditMode`, `SemesterRush.Game.Tests` (refs Sim, Game, CoddyKit.Core,
   `SemesterRush.Sim.Tests`) in `Assets/Tests/Game`. Then Test Runner → EditMode → Run All (133 expected, as under .NET; `EveryLanguageIsComplete` stays red until the 9 languages are translated).
6. **One commit** with everything the editor generated: `ProjectSettings/*`, `Packages/packages-lock.json`,
   `Assets/Settings/*`, `Assets/Scenes/*`, `Assets/Resources/materials/*.mat`, `Assets/Resources/ui/fonts/SemesterPanelText.asset`,
   all `.meta` files. Never `Library/`, `Temp/`, `Logs/`, `UserSettings/`.

Batch alternative (agent on the Mini):
```sh
U=/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity
$U -batchmode -nographics -quit -projectPath semester-rush/unity -executeMethod SemesterRush.EditorTools.SemesterProjectSetup.Apply -logFile -
$U -batchmode -nographics -projectPath semester-rush/unity -runTests -testPlatform EditMode -testResults /tmp/editmode.xml -logFile -
```

**Things to look at first in the editor** (written without one): FBX axis of the Blender kit (fronts must face the camera =
-Z; `CampusWorld.BuildNodes` yaw), palette point filter (`SemesterArtImport`), USS import warnings, marker placement (rooms / plots / gates / coins)
(`RuntimePanelUtils.ScreenToPanel` with top-left coordinates), the spotlight on sheet pills (`Target_*` names), the
house banner lifting the bottom bar (`SafeAreaPadding`), Low tier draw count (target ≤ 120, fleet ≤ 150).

## 3. Verified without the editor (2026-11-02, cloud session)

| Check | Result | How |
|---|---|---|
| Sim + Game + CoddyKit.Core compile netstandard2.1 / C# 9, `-warnaserror` | PASS | `Tools/dotnet_check.sh` (.NET 8) |
| NUnit EditMode tests (sim 48, game 30, kit 55) | **132 / 133** — only `EveryLanguageIsComplete` (translations pending) | same |
| Engine-bound code (kit Runtime + App + World + UI) vs UnityEngine 2021.3 reference modules + Input System stubs | PASS except 2 Unity-6-only APIs (`QualitySettings.count`, `SystemLanguage.Hindi`) | `Tools/unity_ref_check.sh` |
| Every `.cs` under Assets parses as C# 9 (editor+tests and Android+bridges define sets) | 83 files, 0 errors | same |
| Static gates (one MobileAds init = the kit's, no AdMob interstitial/banner, no PlayerPrefs, no "bought anything" ad logic, no trial wording, no reference-game / real university names, no secrets, generated files fresh, art present) | PASS | `Tools/check.sh` |

## 4. Release ids and accounts (P8 — Patron only)

`SemesterGameConfig.AdMobRewardedAndroid`, `RevenueCatAndroidKey`: empty → no rewarded video and a fake store in release.
`SemesterAndroidBuild` reads `SEMESTERRUSH_ADMOB_APP_ID`, `SEMESTERRUSH_KEYSTORE*`, `SEMESTERRUSH_VERSION_*`,
`SEMESTERRUSH_RELEASE` (from Volt City's build script) and fails a production build without them. Firebase:
`semester-rush/config/google-services.json` (not in git). Store / AdMob / RevenueCat / ads_app registration: CLAUDE.md §15.

## 5. Differences from Volt City

- Diorama is procedural (map JSON → lawns, paths, zone trees) on ONE palette material; Blender kit buildings + one Meshy hero oak (Medium/High only).
- The bell change is analytic (one flow computation per bell, cached); the crowd + heat ribbon are instanced draws from it.
- No world-space UI: room / plot / gate markers are UI Toolkit markers placed from the camera at 30 Hz (Low 10 Hz).
- Test asmdefs are not committed yet (they need editor GUIDs only for the nunit reference; see §2 step 5).
