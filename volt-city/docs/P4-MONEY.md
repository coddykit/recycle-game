# Volt City — P4 money + services (Patron, 2026-10-27) — engine code UNCOMPILED in Unity

> Read with: [IAP](IAP.md) (the catalogue contract) · [PLAN](PLAN.md) P4 · [UI](UI.md) section 3/5 · [AUDIO](AUDIO.md) ·
> [ANALYTICS](ANALYTICS.md) · [P3-UI](P3-UI.md). Unity editor still not installed: engine-free parts are compiled and
> tested under .NET 8; the Unity layer was semantically compiled against `UnityEngine.Modules 2021.3.33` (NuGet, session
> only) with 0 errors outside the Input System usings (P3 files, package not in that reference set).

## 1. What is in

| Area | Where | What |
|---|---|---|
| Catalogue | `unity/Tools/build_iap.py` → `Resources/data/iap.json`, `store/iap/rc-catalog.json`, `store/iap/catalog.json` | ONE source (IAP.md) → the game file (Driftwood / Grid Boss schema), the RevenueCat + Play contract (entitlements, 4 PPP tiers, offerings) and the Play catalogue written by the shared `games/tools/shop/tools/iap_store_catalog.py`. 10 languages (`Tools/i18n_p4/*.json`, in-session Haiku sub-agents), Play limits, no trial / subscription wording; `--check` in `check.sh` and `jenkins/preflight.py` |
| Shop | `Scripts/Game/Commerce.cs`, `IapCatalog.cs`, `Offers.cs`, `Looks.cs` | Shop tab: policy strip (ONE tap target, opens / closes in place) → this session's offer → Ad-free and packs → upgrades → looks → budget → your looks (Use / In use) → Restore (ghost). One pill per card: store `displayPrice`, "…" until it arrives / while buying, "Owned" (disabled) when every entitlement is active (a pack covering it counts). Tapping a card opens the product card (whole text + pack contents + the buy button). A locked paid feature (`shop:<slug>`) opens the shop on that product (highlight + scroll) |
| Purchases | kit `CoddyPurchases` (now in `CoddyKit/Core`) on RevenueCat | entitlements cached offline; restore = fresh CustomerInfo (idempotent, refunds remove); consumables delivered once per transaction into `sim.GrantPurchaseFunds` (+ save), ≤ 3 a UTC day as a **pre-purchase** gate, never restored |
| Ad-free | `CoddyPurchases.AdFree` = entitlement `noads` | granted only by noads / premium / grid_pass / bundle; the house banner, house interstitial and the rewarded "no video" path all read it. No `purchased_any` anywhere |
| Offers | `Offers.cs` + VoltCityMain | starter day 3-7 (once), bundle from day 20 (≤ 2), comeback ≥ 3 days away (≤ 2, row on the welcome-back card), storm_kit after a 1★ storm shift (once per chapter, row on the report); never guide / first session / live shift, ≤ 1 per session, 3 days of quiet, never owned; persisted in settings |
| Looks | `Looks.cs`, `CityWorld.ApplyLooks`, `VcCityArt.SetLooks` (P2 hook added), USS `.vc-theme--retro/--gold` | truck livery (`_LiveryA/B`, Medium + Low prop shaders), candy blades, neon skyline + art deco pylons (globals in `VcWorldCore`, every tier), control-room themes (kit palette override). A new look is put on at once if its slot is free |
| City slots | `GameSession.SlotFile`, Settings → Cities | slot 1 free, 2-4 with `slots` (Extra City Slots / Edition / Pass / Collection); `career_N.json`; a refund falls back to slot 1, saves stay |
| Rush Hour Speed | sim `speed4` + Settings → Auto-collect report | 4× in the HUD speed cycle (P3) + optional auto-collect of the shift report (1.5 s, never in the guide) |
| Rewarded | `Scripts/Game/Rewards.cs` (VoltRewards) on the kit `RewardedController` | 4 placements of IAP.md section 3 with BOTH the kit caps (persisted `coddyads/rewarded.json`) and the sim's (career save). Buttons: offline ×2 on the welcome-back card, +50 % on the shift report, emergency import on the Grid tab in a live deficit ("last chance"), crew overtime on a fault card. Video only when loaded; reward after earned + closed; ad_free = same reward, no video, same caps; never in the guide |
| House ads | kit `HouseAdsService` (P0) + P3 wiring | interstitial only after Collect at the shift report (never session 1 / the guide; kit pacing, deferred after a rewarded video), banner with reserved slot after the pager + guide, never over the shop / a sheet; both off for ad_free. **Hero frames are WebP — Unity cannot decode them**: `HouseImageCache` counts a WebP hero as failed and the card uses its icon layout (big icon + brand colour, the Godot "no hero" branch); register `HouseImageCache.WebPDecoder` from a decoder package later to get the frames |
| UMP | kit `RewardedController.Start` (consent first, then the ONE `MobileAds.Initialize`) | Settings → Privacy shows "Privacy options" when UMP says it is required (EEA / UK / US states) |
| Audio | `SoundCues.cs`, `UI/VoltAudio.cs`, `Editor/VoltCity/VoltAudioImport.cs`, `Resources/audio/*.ogg` | no paid generation: 25 existing fleet files (Disaster Command's command-room set, licence in AUDIO.md); music **off by default**, effects on; Low = 3 voices, no ambience |
| Analytics | `Scripts/Game/VoltAnalytics.cs` | one validated catalogue (28 events, Firebase limits), installed before the kit boots; sink = NullAnalytics until the Firebase bridge |
| Review | P3 `ReviewHook` + kit `CoddyReview` | unchanged (first-session wow moment) |

## 2. Decisions (Patron) and deviations from IAP.md

- **23 products sold, not 24:** `scenarios` (5 hand-made scenarios) has no content yet — selling it would sell nothing. It is
  in `iap.json` `reserved` (id kept), not in the Play / RC files; the Collection does not promise it. When the content
  ships: add it to `order`, rebuild, create the product, attach the `scenarios` entitlement to `bundle` in RevenueCat
  (owners get it). Entitlements: **13** (IAP.md's 14 minus `scenarios`).
- `slots` IS sold: the Cities sheet (Settings) was built in P4 so the product does what it says.
- The brief's 4th rewarded moment "daily": IAP.md section 3 (binding) has exactly 4 placements; the 4th is crew overtime
  (fault card). A separate daily-bonus video would break the cash budget check (0.325 of 0.5 day) — not added.
- Music default **off** (P4 brief) — games/CLAUDE.md section 13 says "default on"; the brief wins for this game.
- Kit fix: a pack the store has **charged** is always delivered (the old ledger refused a 4th pack after the charge); the
  daily cap is now checked before the store sheet opens (`CoddyPurchases.ConsumablesAtCap`); `deliver_failed` reported.
- Kit move: `CoddyPurchases.cs` Runtime → Core (pure C#; the game layer and its tests drive the real flow).
- Integration fixes outside P4 files: P1 `GameData` reads `order` (products became a map), `SimTests` 24 → 23; P2
  `VcCityArt.SetLooks` (the cosmetic uniforms existed, the setter did not); P3 `GameSettings` (music default, first_seen,
  slot, auto_collect, looks, offers), `GameSession.SlotFile`, `Tabs/Cards` optional `Extras`, `GameScreen` (product card,
  ScrollTo, SetTheme, ActionRan), `RowList` wrap class, `VoltCity.App` references `VoltCity.Game`, `VoltBoot` installs the
  analytics layer.

## 3. Tests (all on the Mini, .NET 8; `Tools/check.sh` = VOLT_UNITY_CHECK: PASS)

| Suite | Result |
|---|---|
| CoddyKit.Core | 47 / 47 |
| P1 sim | 26 / 26 |
| P3 + P4 game (`Tests/Game/GameTests.cs` + `P4Tests.cs`) | 45 / 45 — P4: catalogue = IAP.md (10 languages, Play limits, no trial, ad-free set, sim amounts), store files cover the catalogue (4 PPP tiers, entitlements), only ad-free products remove ads (+ house interstitial gate), restore idempotent / new phone / offline / refund, consumables once per transaction + daily pre-gate + charged-at-cap delivery, no invented prices + failed purchase unsticks, shop tab (one pill per card, policy strip one tap, Restore ghost, Owned, looks Use, 10 languages, no raw ids), locked feature → product, offers rules + persistence, rewarded caps = sim + budget ≤ 1/5 and 1/2 of cash_s, video only when loaded / grant after earned / early close grants nothing / caps persist, ad_free without video + same caps + never in the guide, offline ×2 + overtime on their cards, sold looks drawn on Low and Medium, city slots, analytics catalogue (every literal event in code) + Firebase validity + doc, audio files + licence rows + music off + Low limits |
| Source gates (G6 now also scans `Resources/data`, `Tools/i18n_p4`, `store/`) · IP denylist · strings (442 × 10) · C# 9 syntax (98 files) · `build_iap.py --check` | all pass |

## 4. UNCOMPILED / never run (needs the editor)

`VoltCity.UI` (shop / settings / cities / audio wiring in `VoltCityMain`, `GameScreen`, `RowList`, `VoltAudio`),
`VoltCity.World` (`ApplyLooks`), `VoltCity.Art` (`SetLooks`), `VoltCity.App` (config, boot), `Editor/VoltCity/VoltAudioImport.cs`,
the USS theme / wrap classes, the bridges (RevenueCat, AdMob + UMP, Play Review) against their real packages, the
`.ogg` import, PlayMode. Semantic check vs Unity 2021.3 reference modules: 0 errors in P4 code (session only).

## 5. What the Patron must create (ids → where they go)

| Account | Create | Put the id into |
|---|---|---|
| Play Console | app `com.coddykit.voltcity` ("Grid Keeper: Energy Tycoon" — docs/NAME_CHECK.md, `gp-create-app --dry-run` first); 23 one-time products from `store/iap/catalog.json` (`store-automation/iap_catalog_gp.ts`, PPP via pricing-policy) | — |
| RevenueCat | project "Grid Keeper" (`rc-setup --template`), Android app, Play service-account credentials; 13 entitlements + product attachments + offerings from `store/iap/rc-catalog.json` (consumables: no entitlement); "Check credentials" after the first AAB | public SDK key `goog_…` → `VoltGameConfig.RevenueCatAndroidKey` (+ `iap.json` `revenuecat.android_key` via `build_iap.py`) |
| AdMob | Android app → **APP id** `ca-app-pub-…~…`; ONE rewarded unit (no interstitial / banner units); UMP GDPR message (+ US states) published with https://www.coddykit.com/pages/privacy-policy; link to Play after production | APP id → `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` (GMA package); unit → `store-automation/.env ADMOB_REWARDED_VOLTCITY_ANDROID` + `VoltGameConfig.AdMobRewardedAndroid` |
| Firebase | project `coddykit-game-2`, Android stream `com.coddykit.voltcity` | `google-services.json` + a Firebase bridge registering `CoddyServices.Analytics` (events: ANALYTICS.md) |
| Unity packages | `com.google.ads.mobile` (GMA + UMP), `com.revenuecat.purchases-unity`, `com.google.play.review` (UNITY.md section 4) | `Packages/manifest.json` (bridges compile on their versionDefines) |
| Fleet files (outside this game's commit scope) | `games/CLAUDE.md` §8 row: `volt-city | noads, premium (Chief Engineer Edition), grid_pass (Utility Pass), bundle (Complete Grid Keeper Collection)`; `games/tools/ads/house_related.py` entry (tycoon / infrastructure / rescue) → regenerate `CoddyHouseRelated.gd` → `sync.sh`, then `unity/Tools/gen_house_related.py` — **DONE in P6 (2026-10-27)**: real tags `ops 3, crisis 2, traffic 2, builder 2, tycoon 2, dispatch 1, transport 1` (nearest: train-baron, canal-control, sky-tower-ops, airport-baron, disaster-command), RING tail, 21 Godot copies synced; backend registration still waits for the store page | — |
| After production | Supabase `ads_app` / `ads_slot` / `ads_app_house` registration, ASO Atlas, privacy-policy section + Data safety (games/CLAUDE.md §15) | — |

RevenueCat ad tracking (§5b): the kit's `RcAdTracker` dedupes and queues AdMob events, but the RC Unity bridge returns
"unsupported" until the installed SDK's ad-event API is verified (`RevenueCatBridge.TrackAdEvent`) — report it as such.
