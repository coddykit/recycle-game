# Volt City — Unity (owner decision 2026-10-27)

> **Volt City** (store name **Grid Keeper: Energy Tycoon** since P6, docs/NAME_CHECK.md; `games/volt-city`, `com.coddykit.voltcity`, portrait, Google Play only) is built in
> **Unity 6 LTS + URP** as a test. The rest of the fleet stays Godot 4.7.2. Every fleet rule (`games/CLAUDE.md`,
> `games/LESSONS_LEARNED.md`) still applies; this file says what is different in Unity and how each rule is kept.
>
> **State (2026-10-27): Unity is NOT installed on the Mini yet.** Everything under `unity/` except the engine-free core is
> **UNCOMPILED** until the editor exists (see §2 for exactly what was checked and how).

## 1. Layout and owners

| Path | Owner | What |
|---|---|---|
| `unity/Packages/manifest.json`, `unity/ProjectSettings/*`, `unity/.gitignore` | Patron (Unity P0) | project skeleton |
| `unity/Assets/CoddyKit/**` | Patron (fleet kit) | **CoddyUnityKit** — the Unity port of the Godot kits (§4) |
| `unity/Assets/Scripts/App/**`, `unity/Assets/Editor/VoltCity/**`, `unity/Assets/Tests/App/**` | Patron | boot scene logic, game kit config, project setup + Android build entry, project tests |
| `unity/Tools/**` | Patron | generators + gates runnable without Unity |
| `unity/Assets/Scripts/Sim/**`, `unity/Assets/Tests/EditMode/**`, `unity/Assets/Resources/data/**` | P1 | engine-free sim (`VoltCity.Sim`, `noEngineReferences`), its tests, data JSON (in `Resources/data`, not StreamingAssets) |
| `unity/Assets/Art/**`, `blender/**` | P2 | models (FBX + GLB twins), URP shaders, `Editor/VcArtImport.cs`, runtime city builder (`docs/P2-ART.md`) |
| `unity/Assets/Scripts/Game/**` (`VoltCity.Game`, engine-free), `Scripts/World/**` (`VoltCity.World` + `World/Editor` scene builder), `Scripts/UI/**` (`VoltCity.UI`), `Tests/Game/**`, `Tests/PlayMode/**`, `Resources/i18n/strings.csv`, `Resources/ui/**`, `Art/Runtime/VoltCity.Art.asmdef`, `Art/Editor/VoltCity.Art.Editor.asmdef` | P3 | playable layer: view models, guide, strings, save wiring, diorama driver, UI Toolkit screens (`docs/P3-UI.md`) |
| `jenkins/**` | Patron | Linux build-only plan (§7) — **not wired** |

Assemblies: `CoddyKit.Core` (engine-free) → `CoddyKit.Runtime` → bridges (`CoddyKit.AdMob`, `CoddyKit.RevenueCat`,
`CoddyKit.PlayReview`, each compiled only when its package is installed) · `CoddyKit.Editor` · `CoddyKit.Tests` ·
`VoltCity.App` · `VoltCity.Editor` · `VoltCity.App.Tests` · P1's `VoltCity.Sim` + `VoltCity.Sim.Tests` · P2's
`VoltCity.Art` (+ `VoltCity.Art.Editor`; asmdefs added in P3 so asmdef assemblies can reference the art) · P3's
`VoltCity.Game` (engine-free: Sim + CoddyKit.Core) → `VoltCity.World` (+ Art, Runtime, Input System) → `VoltCity.UI`
(+ App) · `VoltCity.World.Editor` (City scene builder) · `VoltCity.Game.Tests` (EditMode) · `VoltCity.PlayMode.Tests`.

## 2. What is verified today (no editor)

| Check | Result | How |
|---|---|---|
| `CoddyKit.Core` compiles as **netstandard2.1 / C# 9** (Unity 6's API + language level) | PASS, 0 warnings | `unity/Tools/dotnet_check.sh` (.NET 8 SDK at `/Volumes/BuildSSD/game-deps/dotnet-sdk`) |
| Core tests (NUnit, the same files the Unity Test Runner runs) | **47 / 47 pass** | same script (`Assets/CoddyKit/Tests/*.cs`) |
| Every C# file of the kit, app, editor and tests parses as C# 9 (both define sets: editor+tests, Android+all bridges) | 42 files, 0 syntax errors | Roslyn parse (session check; not a semantic compile) |
| Static gates (one `MobileAds.Initialize`, no AdMob interstitial/banner, no legacy input / double mouse events, `Screen.safeArea` only in `SafeArea.cs`, no `PlayerPrefs`, no trial wording, generated files fresh) | PASS | `unity/Tools/check_unity_sources.py` |
| IP denylist + proof + "nobody is hurt" | 0 matches, proof caught all planted words | `unity/Tools/ip_denylist.py` |
| Preflight | offline 4/4; production 4/12 (blocked by design until P4/P8) | `jenkins/preflight.py [--production]` |
| P3 game layer (`VoltCity.Game`) compiles netstandard2.1 / C# 9, `-warnaserror` | PASS | `unity/Tools/game_dotnet_check.sh` |
| P3 game EditMode tests (strings, view models, actions, guide + newbie audit ×5 seeds, saves, review hook, diorama logic) | **28 / 28 pass** | same script |
| Every C# file (88) parses as C# 9 in both define sets | 0 syntax errors | `unity/Tools/cs_parse_check.sh` |
| Semantic compile of Sim + Core + **Runtime + App + Art/Runtime + Game + World + UI** against Unity **2022.3** engine reference modules (UIElements, Core, Input System stubbed) | 0 errors, 0 warnings in P3 code (session check, not in the repo: third-party reference DLLs) | P3 session, `docs/P3-UI.md` §6 |


### 2a. Verified in the real editor (Unity 6000.3.27f1, 2026-10-28, Mac Mini batchmode)

| Check | Result |
|---|---|
| Full compile, all assemblies incl. vendor bridges (GMA 11.6.0, RC 9.16.0) | 0 errors (fixes: RC 9.x `PurchaseResult` callback + asmdef ref `revenuecat.purchases-unity`; RC `AdTracker` ad events implemented; dev APK path + GMA app-id handling in `VoltAndroidBuild`) |
| `VoltProjectSetup.Apply` | `PROJECT_VALID`, no further file changes |
| EditMode tests (Unity Test Runner) | **125 / 125 pass** |
| PlayMode tests | **1 / 1 pass** |
| `unity/Tools/check.sh` (UNITY_EDITOR set) | `VOLT_UNITY_CHECK: PASS` (sim 26/26, game 46/46, 98 files parse, strings 442 x 10, IAP 23 products, store listing PASS, Unity EditMode pass) |
| Local Android dev build (IL2CPP arm64) | AAB 38.0 MB / dev APK 41.7 MB, build ~1-2 min after import (#165 build with fonts + Firebase: AAB 41 MB) |
| #165 quality pass (2026-10-28) | P7 table in [`P7-QA.md`](P7-QA.md): EditMode 125/125, PlayMode 1/1, check.sh PASS, preflight production 13/13, Low 14 draws / 40k tris, Medium 14 / 53k, 30-min soak 0 errors, 10 languages 0 tofu |
| Emulator smoke (`games/jenkins/smoke_android.sh`, smoke35, 120 s + 300 monkey) | **PASS**, 0 Unity errors after the UMP main-thread fix (1st run: GMA `SetRequestConfiguration` from the UMP callback threw off-main-thread). Frames: city diorama + onboarding readable, safe area OK, no pink shaders. Open: the onboarding `Next` button renders as an ellipse (radius), a city toast shows over onboarding page 1. `/Volumes/BuildSSD/game-builds/volt-city/editor-bringup-1028/smoke2/` |

**Was "needs the editor first" (now done, see 2a):** semantic compile of `CoddyKit.Runtime` / `CoddyKit.Editor` / `VoltCity.*` (UnityEngine,
UI Toolkit, Input System, URP APIs), the three vendor bridges (also need their packages), the USS import (warnings for
any property the installed version does not know), ProjectSetup creating the URP tier assets / PanelSettings / Boot scene,
`VoltProjectTests` (policy validator), P1's and P2's code, the first Android build. Everything is one command: §3.

## 3. Open it the first time (owner / Patron, after Unity Hub installed the editor)

1. Unity Hub: **Unity 6 LTS** with **Android Build Support** (+ OpenJDK + Android SDK & NDK Tools). Activate the licence.
2. Open `games/volt-city/unity` from a **worktree** (never edit the shared checkout; the editor writes files). First import
   takes a while (P2 models + shaders).
3. On the first open `VoltProjectSetup` runs by itself (`[InitializeOnLoad]`, only while `Assets/Settings/URP-Medium.asset`
   is missing): Android player policy, three URP tier assets, quality levels, kit `PanelSettings`, `Assets/Scenes/Boot.unity`,
   build scenes. Menu: **Coddy › Volt City › Apply Project Setup** / **Coddy › Validate Project** (`PROJECT_VALID`).
   Batch alternative for an agent:
   ```sh
   U=/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity
   $U -batchmode -nographics -quit -projectPath games/volt-city/unity -executeMethod VoltCity.EditorTools.VoltProjectSetup.Apply -logFile -
   $U -batchmode -nographics -projectPath games/volt-city/unity -runTests -testPlatform EditMode -testResults /tmp/editmode.xml -logFile -
   UNITY_EDITOR=$U sh games/volt-city/unity/Tools/check.sh
   ```
4. If the installed editor is not 6000.0.x the Package Manager moves URP / Input System / Test Framework to the editor's
   own versions — accept, then commit the rewritten `Packages/manifest.json` + `packages-lock.json`.
5. **One commit** with everything the editor generated: `ProjectSettings/ProjectVersion.txt` (pins the editor for CI),
   the normalised `ProjectSettings/*`, `Packages/packages-lock.json`, `Assets/Settings/*`, `Assets/Scenes/Boot.unity`,
   `Assets/CoddyKit/UI/Resources/CoddyPanelSettings.asset` and **all `.meta` files** (Unity GUIDs; P1/P2 files included —
   tell them). Never commit `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, `UserSettings/` (`.gitignore`).
6. Do **not** install a glTF importer (P2's `.glb` twins would import twice).

## 4. Packages and the CoddyUnityKit

| Package | Version | Why |
|---|---|---|
| `com.unity.render-pipelines.universal` | **17.3.0** (6000.3; was 17.0.4) | URP, per-tier assets |
| `com.unity.inputsystem` | **1.20.1** (was 1.11.2) | Input System only (`activeInputHandler: 1`), EnhancedTouch |
| `com.unity.test-framework` | **1.6.0** (was 1.4.6) | EditMode tests (NUnit) |
| modules | androidjni, audio, imageconversion, jsonserialize, particlesystem, uielements, unitywebrequest(+texture) | nothing else (no physics module: world picks use ray-vs-box maths on P2's district boxes; URP may pull it transitively) |
| **vendor, added at P4 (or as soon as the editor is in)** | | via the OpenUPM scoped registry already in the manifest |
| `com.google.ads.mobile` (Google Mobile Ads Unity + UMP) | **11.6.0** installed 2026-10-28 (+ EDM4U 2.1.0); committed settings keep Google's public sample app id, the real id comes only from `VOLTCITY_ADMOB_APP_ID` at build time | AdMob **rewarded only** + consent; pulls `com.google.external-dependency-manager` (EDM4U). Then Assets › Google Mobile Ads › Settings: Android **app** id (`store-automation/.env`) |
| `com.revenuecat.purchases-unity` | **9.16.0** installed 2026-10-28 | purchases + entitlements |
| `com.google.play.review` 1.8.3 (+ `com.google.play.core` 1.8.5, `com.google.play.common` 1.9.2; needs the built-in `assetbundle` + `unitywebrequestassetbundle` modules) | installed 2026-10-28: Google's tarballs pinned by sha256 in `Tools/fetch_packages.sh` → `Packages/tarballs/` (gitignored), manifest `file:tarballs/…`; CI runs the fetch first | Play In-App Review |
| Firebase App + Analytics 14.1.0 (tarballs, same fetch script; App is 61 MB, never in git) | installed 2026-10-28; `CoddyKit/Bridges/Firebase` registers only when the build carries `google_app_id`; `config/google-services.json` (outside the Unity tree) is copied into Assets by `VoltAndroidBuild` for the build and removed after (all generated files gitignored). Google's EDM 1.2.x comes in next to Unity's EDM 2.1.0 (GMA): builds clean | `rewarded_*`, `onboarding_*`, `save_unreadable`, `gpu_shader_crash` events (until then `CoddyServices.Analytics` is a null sink) |

The bridges compile **only** when their package is present (asmdef `versionDefines` → `CODDY_ADMOB` / `CODDY_RC` /
`CODDY_PLAY_REVIEW` + `defineConstraints`), register themselves at `BeforeSceneLoad`, and only on an Android device —
the editor and a build without the SDK run on safe fakes (feature off, never a crash).

**Kit pieces** (`Assets/CoddyKit`, Godot source in brackets):

| Piece | Where | Rule it keeps |
|---|---|---|
| `SafeArea`, `SafeAreaMath`, `SafeAreaPadding` [ui_safe_area.gd] | Runtime / Core | nav-bar insets **ignoring visibility** (JNI), overlay edges reserve the status bar, content bottom = `max(safe, banner)`, `CODDY_SAFE_SIM` |
| `TapFilter`, `TapInput` [UiKit.is_tap] | Core / Runtime | one tap = one action; release-only, < 12 dp / < 400 ms, pinch never selects; touchscreen present → mouse never read; UI picks block world taps |
| `UiKit` (+ `CoddyTokens.uss` generated from `games/tools/ui/design/tokens.json`, `CoddyKit.uss`, `CoddyRuntimeTheme.tss`) [ui_kit.gd design 2] | Runtime / UI | page header, list card + one action pill, section overline, empty card, close chip, bottom sheet + grab handle, finger-scroll list (`Held()` = no rebuild under a finger), toast card; Low = flat variants |
| `ToastQueue`, `UiToastHost` [ui_toast.gd] | Core / Runtime | 2.5–5.5 s, merge, tap dismiss, always a card |
| `HouseAdsService` + `HouseBannerView` + `HouseInterstitialView` + `HouseImageCache` [CoddyAds / CoddyBanner / CoddyInterstitial / CoddyHouseRelated] | Runtime + Core `HouseAds`, `HouseText`, `HouseRelated(+Table.g.cs)` | same `ads-config` / `ads-track` protocol and headers, related-games order (generated from `games/tools/ads/house_related.py`), pacing 600/300/240 s, rewarded quiet period, perAppBudget × sequence, version reset, banner 58 dp ≤ 12 %, 1 or 2 tiles, 45 s rotation, persisted cursor, calm moment + 1.5 s tap grace, never self / kids, hidden for ad-free / loading / gate, slogan + Install rules, Play referrer |
| `RewardedController`, `RewardedRules`, `RcAdTracker` [CoddyAds rewarded + RcAdTracking.gd] | Core | UMP → the **one** `MobileAds.Initialize` → load; caps per placement + UTC day + cooldown + unit budget (single ≤ 1/5, daily ≤ 1/2 of the cheapest pack); grant once after earned **and** closed; ad-free = reward without video; `rewardedEnabled` / `rcAdTracking` kill switches |
| `CoddyPurchases`, `EntitlementCache`, `ConsumableLedger` [coddy_iap] | Runtime / Core | RC entitlements cached offline; ad-free = `noads` only (§3); consumables once per transaction id, ≤ 3 / UTC day |
| `CoddyReview`, `ReviewGate` [ReviewGate.gd] | Runtime / Core | first session, ≥ 240 s real play, wow moment, never in guide / window / background, ≤ 3 asks ≥ 30 days apart, OS sheet only (IAP.md §4's old "2 sessions + 15 min" line is superseded by CLAUDE §12) |
| `SaveRepository`, `JsonStateFile` [save_repository.gd] | Core | atomic tmp → main, last **good** save in `.bak`, unreadable kept as `.unreadable(.1–.7)`, never deleted; `.tmp` recovery |
| `DeviceTier`, `TierSpec`, `PerfGuard`, `ShaderCrashGuard`, `QualityTiers` [perf_device.gd / perf_profiles.gd / PerfGuard] | Core / Runtime | flagship High / strong Medium / weak Low (same RAM / cores / GPU regexes + IMG BXM), one-time fixes, 10 s < 25 FPS uncapped → one step down once, GPU shader-crash breadcrumb → `safe_shaders` |
| `ProjectSetup`, `ProjectValidator` | Editor | §5 policy applied + checked (CI fails closed) |

**Why UI Toolkit, not uGUI + TextMeshPro:** (1) the 10th language is **Hindi** — TextMeshPro has no complex-script
shaping (Devanagari conjuncts/matras break), Unity 6 UI Toolkit has the Advanced Text Generator (HarfBuzz/ICU);
(2) the fleet tokens map 1:1 onto USS custom properties (generated from the same `tokens.json`); (3) one retained panel
re-tessellates only changed elements — uGUI rebuilds a whole Canvas batch when one label changes (the classic mobile
cost of a ticking money label; fleet LESSONS A9/A10); (4) built-in elastic touch scrolling with inertia where a drag that
starts on a button scrolls (§16). Costs: no world-space UI needed; no box-shadow / text-transform (kit uses surface steps
and upper-cases in C#). **P5 gate:** Hindi glyph + shaping test on device; if the installed editor's advanced generator
does not shape Devanagari, that is a P5 blocker to raise before the string freeze.

## 5. Android player policy (applied by `ProjectSetup`, checked by `ProjectValidator`)

IL2CPP · **ARM64 only** · minSdk **25** (Unity 6000.3 floor) · targetSdk **36** (Play refused 35 on the first upload, 2026-10-28) · AAB · portrait only · Linear colour · HDR off · SRP batcher ·
Input System only · GameActivity entry · fullscreen + render outside safe area (the kit pads) · managed stripping Medium +
engine stripping + IL2CPP "optimize size" · UV0/UV1 vertex compression off (P2 data UVs) · public debug symbols zip ·
**no Unity splash / logo** (`#0A1721` background, own curtain from P2's `Splash/curtain.png`).

**Graphics APIs: Vulkan first, OpenGL ES 3 fallback.** Fleet lessons: Godot's Vulkan path crashed on PowerVR at start
(LL A11) and IMG BXM-8-256 crashed inside the GLES shader compiler on the first 3D frame (Vitals 2026-10-27 §B). In Unity:
put Imagination / PowerVR (incl. BXM) on the **Vulkan deny list** (Unity 6 Player Settings › Android › Vulkan device
filtering) so those GPUs get GLES3; on GLES3 the kit's GPU **breadcrumb** (`QualityTiers.BeginShaderWork/EndShaderWork`
around warm-up and the first 3D frame) turns a crash into `safe_shaders` (Low tier + P2's `*Low` shaders) on the next
launch and reports `gpu_shader_crash`. If the installed version has no Vulkan filter, decide in P3 from a device test
between `[Vulkan, GLES3]` and `[GLES3, Vulkan]`.

**Quality tiers** (`games/CLAUDE.md §10`, `TierSpec`): Low = render scale 0.85, **no MSAA**, no shadows, 30 FPS, crowds ≤ 50 %,
textures ≤ 1024 (mip limit 1), per-vertex Low shaders, no extra effects, props rewritten at 15 Hz · Medium = 1.0, MSAA 2x,
main-light shadows (1 cascade, 1024), 60 FPS · High = 1.0, MSAA 4x, soft shadows 2048, extra effects. Low keeps P2's
**0 extra draws** rule (Low ≤ 17 draws, ≤ 84k prims; every new heavy visual Medium/High-only). The Godot GLES3 MSAA
framebuffer leak is Godot-specific; Low has no MSAA anyway.

## 6. Open risks

| Risk | Note / plan |
|---|---|
| **APK/AAB size** | Unity 6 IL2CPP ARM64 + URP is ~20–30 MB before content; GMA + RC + Play + Firebase add ~8–12 MB. Keep stripping Medium + `link.xml` only where a vendor needs it; measure the first AAB (Play's 150 MB limit is far, the fleet's "reasonable size" rule is not). |
| **Licence / splash** | Unity 6: the splash is optional on every plan incl. Personal; Personal is allowed under the revenue ceiling (USD 200k / 12 months). An older editor (≤ 2023) would force "Made with Unity" on Personal — use Unity 6 only. CI on Linux: Personal = the `Unity_lic.ulf` from one Hub activation as a Jenkins file credential; Plus/Pro = serial (returned after each build). |
| **PowerVR / IMG GPUs** | §5. Needs a device test (Firebase Test Lab / BrowserStack moto g64y class) before production. |
| **House-ad hero frames are WebP** | Unity decodes PNG/JPEG only. `HouseImageCache.WebPDecoder` is the seam: add a WebP decoder package (native libwebp, ARM64) or ask the backend for JPEG twins (`heroImageUrls*`). Until then the interstitial uses its icon layout (big icon on the brand colour) — legal, but less attractive. Icons are PNG (fine). |
| **RevenueCat ad tracking (§5b)** | No RC Unity ad-event API verified offline; `RcPurchasesProvider.TrackAdEvent` returns false and the release report must say "RC ad tracking: unsupported in Unity SDK <v>" until implemented. AdMob `OnAdPaid` values are captured and queued correctly. |
| **Bridge API drift** | GMA / RC / Play Review bridges are written against the APIs as known in 2025-26; compile errors there are expected on first install — fix in the bridge only (`#if` guarded). |
| **Build time** | First Linux import + IL2CPP + Gradle ≈ 20–40 min; keep `Library/` in the Jenkins workspace (incremental ≈ 5–10 min). URP shader variants: strip unused (URP global settings) and watch the variant count. |
| **First-frame shader hitch** | Warm up P2's 5 shaders behind the curtain with a ShaderVariantCollection, inside the breadcrumb (§5). |
| **UI Toolkit maturity** | Touch scroll over buttons, `MeasureTextSize`, `-unity-text-generator` and USS `var()` in shorthands are used — verify on device (P3), fall back to explicit values where an import warning appears. |
| **Mini disk** | BuildSSD is ~92 % full; a Unity `Library/` is several GB per worktree — one editor worktree at a time. |

## 7. Jenkins (`jenkins/`) — written, **NOT WIRED**

`jenkins/Jenkinsfile` (job `Games/volt-city-mobile`, agent `linux-android`, build-only, parameter `WIRED` default false →
stops at "Wiring check"), `jenkins/build_unity_android.sh` (licence → preflight → EditMode tests → `VoltAndroidBuild.Run`
→ `volt-city.aab` + `volt-city.symbols.zip` + `release.json`), `jenkins/preflight.py`. Wiring steps (Patron, after §3):
1. Install the **same** editor version as `ProjectVersion.txt` on the Linux agent with Android modules
   (`unityhub --headless install --version <v> --module android android-sdk-ndk-tools android-open-jdk`) at
   `/opt/unity/editors/<v>/Editor/Unity` (or set `UNITY_EDITOR`). Alternative: GameCI image `unityci/editor:ubuntu-<v>-android-3`.
2. Jenkins credentials: `unity-license-ulf` (file: Personal `Unity_lic.ulf`) + `unity-account` (user/pass) — or switch the
   `withCredentials` block to `unity-serial` for Plus/Pro; `voltcity-upload-keystore` (file), `voltcity-keystore-pass`,
   `voltcity-key-pass` (from `~/.android-keys/volt-city`, never printed, never committed).
3. Create the job from `jenkins/Jenkinsfile`, run once with `RELEASE=dev`, then `internal` once P4 ids exist.
Upload stays the Patron's step (`play_upload.mjs` with `SYMBOLS_ZIP=` in the same edit). Smoke on the Mini emulator as for
every game.

### 7a. How the first release was built (2026-10-28)

Jenkins is still NOT WIRED (no Unity editor / licence on the Linux agent), so the Patron relaxed "Android builds on Jenkins" for Unity: the 1.0.0 internal AAB was built on the Mac Mini in a worktree with the same entry as CI, `VoltAndroidBuild.Run` in `-batchmode -nographics` (headless works; one Unity process on the Mini at a time — wait while another session's editor runs). Env: `VOLTCITY_RELEASE=internal`, `VOLTCITY_VERSION_NAME=1.0.0`, `VOLTCITY_VERSION_CODE=$(date +%s)` (a release below 1.79e9 now fails closed — `ResolveVersionCode`, the committed ProjectSettings value is 1), `VOLTCITY_KEYSTORE*` from `~/.android-keys/volt-city/` (never printed), `VOLTCITY_ADMOB_APP_ID` = `ADMOB_APP_ID_VOLTCITY_ANDROID`. EditMode first (126/126), then ~5–16 min build. After the build: `git checkout` the worktree's ProjectSettings + `Resources/ui/fonts/*.asset` (the editor rewrites the dynamic font atlases). Unity's symbols zip also carries Firebase `.so` for armeabi-v7a/x86/x86_64; the uploaded zip is trimmed to `arm64-v8a` (the AAB's only ABI). Play needs **targetSdk 36** for a new app (35 was refused). Artifacts: `game-builds/release-aabs/com.coddykit.voltcity-1.0.0-vc1793218190-6b49b4d95f.*`.

## 8. What differs from the Godot fleet (quick list)

- Kits are not `sync.sh`-copied from `games/tools/*`: CoddyUnityKit is ported code; the **data** stays single-source
  (house relatedness and UI tokens are generated from the fleet files; `--check` gates catch drift). A kit fix in Godot
  must be mirrored here (and the reverse) — LESSONS rule 0.
- Tests: Unity Test Runner EditMode (+ the engine-free core under dotnet). No `check.sh` Godot runner; `unity/Tools/check.sh`.
- Saves / kit state live in `Application.persistentDataPath/coddykit/` (same file names as the Godot kit's `user://coddyads/*`).
- Export: no `gl_compatibility` — URP on Vulkan/GLES3 (§5). Debug symbols are Unity's own zip (`release_symbols.sh` is Godot-only).
- House ads: the Godot banner's 0.5 s check, calm/grace timing and impression rules are identical; the card is UI Toolkit.
