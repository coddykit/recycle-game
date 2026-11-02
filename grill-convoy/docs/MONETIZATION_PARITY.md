# Grill Convoy — monetization parity (fleet §11, P4 acceptance)

Item-by-item against the fleet standard (Corrupt = the live reference; `IAP_STANDARD.md`, `REWARDED_ADS_STRATEGY.md`,
`ADS_KIT_README.md`, `REVENUECAT_AD_TRACKING.md`). "Code" = implemented and tested in this repo; "P8" = needs the
Patron's account work (ids, store products, ads_app registration) before it can work in a release build.

| # | Item (fleet) | Grill Convoy | State |
|---|---|---|---|
| 1 | Broad IAP catalogue: Remove Ads, edition, pass, starter, consumables, convenience, cosmetics, bundle | plan §10 MVP, **22 products** (`unity/Tools/build_iap.py` → `Resources/data/iap.json`): noads, premium (Pitmaster Edition), convoy_pass (Convoy Pass), bundle (Complete Grill Convoy), speed, automation (Head Cook+), slots (Extra Saves), offline_plus, livery_retro, livery_neon, uniforms, menuboards, window_awnings, cosmetics_all, cash S/M/L/XL, offers starter (Quick Start) / crisis (Propane Rescue Kit) / comeback / milestone (New-Truck Pack). Nothing above $19.99 (`TheCatalogueIsThePlansTwentyTwoProducts`) | Code; store products P8 |
| 2 | No trial, no subscription, no loot box, no premium currency (§1) | none in catalogue or texts (`check.sh` gate + `NoTrialOrSubscriptionWordingAnywhere`); research waits are never sold, `speed` only raises sim speed | Code |
| 3 | `ad_free` only from Remove Ads + big packs (§3) | entitlement `noads` granted by noads / premium / convoy_pass / bundle; cheap packs never (`OnlyRemoveAdsAndBigPacksMakeTheGameAdFree`, `BuyingACheapPackDoesNotRemoveAds_RemoveAdsDoes`) | Code — **the §8 table row needs the owner's approval** |
| 4 | Legacy buyers stay ad-free (§3) | not applicable: new game | — |
| 5 | Rewarded = AdMob only, opt-in, reward after earned (§4.1) | kit `RewardedController` (UMP → the one `MobileAds.Initialize` → load); `GrillRewards` | Code; unit id P8 |
| 6 | Nothing sold is given by ads (§4.2) | 6 placements: ×2 offline (bounded), extra runner (60 game min), +20 patties, quick prep, crisis rescue, morning bonus — never cosmetics, slots, speed, rule slots, ad removal | Code |
| 7 | Caps: single ≤ 1/5, daily ≤ 1/2 of the cheapest pack; per-placement daily cap + cooldown, persisted (§4.3) | cash_s = 1 day income = 1000 units; single ≤ 200, daily ≤ 500 (kit budget) + sim caps per UTC day in the career save (`KitRewardCapsEqualTheBalanceData`, `DailyCapAndCooldownPersistInTheCareer`, `CashRewardsStayUnderOneFifthOfTheCheapestPack`) | Code |
| 8 | Natural stops (§4.4) | welcome-back card, truck sheet (helper / patties / crisis rescue — crisis "last resort"), commissary sheet (quick prep), morning calendar (bonus) | Code |
| 9 | ad_free gets rewards without video, same caps (§4.5) | `GrillRewards.Get` → `Video = !AdFree` (kit logs `rewarded_skipped_paid_user`) | Code |
| 10 | Interstitial after rewarded is deferred (§4.6) | kit pacing | Code (kit) |
| 11 | Kill switches `rewardedEnabled`, `rcAdTracking` (§4.7, §5b) | kit `HouseAdsService.RemoteChanged` | Code (kit) |
| 12 | Rewarded analytics names (§4.9) | `rewarded_offer_shown / _accepted / _completed / _skipped_paid_user` (kit) + catalogue gate `GameEvents` | Code |
| 13 | Interstitial + banner = house only, no AdMob fallback (§2) | kit `HouseAdsService` (house card + banner, related-first order with a provisional `grill-convoy` row: doner-night, kaiten-sushi, chaiwala-rush, pharmacy-rush, bottle-route, …); no AdMob interstitial / banner code (`check.sh` gate) | Code; `ads_app` registration + `house_related.py` tags P4/P8 in the monorepo |
| 14 | Banner standard 10-26: after onboarding, never over guide / purchase / loading; slot reserved; hidden for ad_free | `GrillMain.BannerAllowed` (no pager, no guide, no shop / welcome-back sheet), kit `reserve_slot`, `SafeAreaPadding` lifts the bottom bar | Code — verify on device |
| 15 | Interstitial at natural breaks, not session 1, not the guide | "Next day" after a day report, `Sessions > 1`, not in the guide; kit pacing decides | Code |
| 16 | RevenueCat purchases + entitlements cached offline, restore | kit `CoddyPurchases` (+ Settings / Shop "Restore purchases"); the Edition's starter pack is granted once per device even after a restore (`EditionGivesItsStarterPackOnceAndAnExtraSlot`) | Code; RC key P8 |
| 17 | RevenueCat ad tracking (§5b) | kit `RcAdTracker` | Code (kit) |
| 18 | Consumables once per transaction, ≤ 3 packs per UTC day | `Commerce.Deliver` (txn in settings) + kit `ConsumableLedger` (`ConsumableDeliversOncePerTransaction`); offer extras: runner, 3 spare propane bottles, 2 batches each in the commissary (`OfferPacksDeliverTheirExtras`) | Code |
| 19 | Starter / offer timing | starter day 3–7 after a report (once), crisis kit when the propane runs out, comeback after ≥ 3 days away, milestone when a truck is bought; 1 per session, 3 days apart, never in the guide; offers live in the shop (no blocking popup) | Code |
| 20 | In-app review at a wow moment, first session ~4 min (§12) | `ReviewHook`: first good day report (≥ 6/9 stars — right after the guide's first day), a new pitch unlocked, a match day beaten; kit `ReviewGate` (≥ 240 s, ≤ 3 asks, 30 days apart, never in guide / window / background) | Code |
| 21 | Firebase events (§9, §14) | `onboarding_*`, `save_unreadable`, purchase / offer / crisis / day / dispatch / prep / layout events through the validated catalogue | Code; `google-services.json` P8 |
| 22 | UMP privacy options in Settings (§17) | Settings → Privacy options (kit consent provider) + policy link | Code; UMP form in AdMob P8 (§15) |
| 23 | Paid convenience works (sold ⇒ real) | Speed Pack 3×/4×, Head Cook+ +2 rule slots, Convoy Pass +1 rule slot + 1 layout preset, Extra Saves / Edition career slots (`GameSession.SwitchSlot`), Offline Plus 75 % / 10 h (`PerksReachTheSim`, `RuleSlotsAreCappedAndGrowWithThePassAndHeadCookPlus`) | Code |

**Owner approval needed** (CLAUDE.md §8 is updated only after it): `grill-convoy` — noads, premium (Pitmaster Edition),
convoy_pass (Convoy Pass), bundle (Complete Grill Convoy). Starter, crisis, comeback, milestone, speed, automation, slots,
offline_plus, looks and cash packs do NOT remove ads.

**Cosmetics are visible but simple** (procedural, every tier, 3 instanced draws): liveries tint the truck (picked in
Settings → Looks), crew outfits recolour the two cooks at the hatch, the awning set swaps the window awning stripes, the
menu board styles swap the sidewalk board to neon. The P2 art pass should give each a proper Blender variant.
