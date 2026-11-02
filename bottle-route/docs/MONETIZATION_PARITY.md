# Bottle Route — monetization parity (fleet §11, P4 acceptance)

Item-by-item against the fleet standard (Corrupt = the live reference; `IAP_STANDARD.md`, `REWARDED_ADS_STRATEGY.md`,
`ADS_KIT_README.md`, `REVENUECAT_AD_TRACKING.md`). "Code" = implemented and tested in this repo; "P8" = needs the
Patron's account work (ids, store products, ads_app registration) before it can work in a release build.

| # | Item (fleet) | Bottle Route | State |
|---|---|---|---|
| 1 | Broad IAP catalogue: Remove Ads, edition, pass, starter, consumables, convenience, cosmetics, bundle | 19 products (`unity/Tools/build_iap.py` → `Resources/data/iap.json`): noads, premium (Chief Operator Edition), route_pass (Network Pass), bundle, speed, dispatcher_plus, offline_plus, 4 looks + collection, cash S/M/L/XL, starter / match-day kit / comeback offers | Code; store products P8 |
| 2 | No trial, no subscription, no loot box, no premium currency (§1) | none in catalogue or texts (`check.sh` gate + `NoTrialOrSubscriptionWordingAnywhere`) | Code |
| 3 | `ad_free` only from Remove Ads + big packs (§3) | entitlement `noads` granted by noads / premium / route_pass / bundle; cheap packs never (`OnlyRemoveAdsAndBigPacksMakeTheGameAdFree`, `BuyingACheapPackDoesNotRemoveAds_RemoveAdsDoes`) | Code — **the §8 table row needs the owner's approval** |
| 4 | Legacy buyers stay ad-free (§3) | not applicable: new game, nobody bought before the rule | — |
| 5 | Rewarded = AdMob only, opt-in, reward after earned + closed (§4.1) | kit `RewardedController` (UMP → the one `MobileAds.Initialize` → load); `BottleRewards` | Code; unit id P8 |
| 6 | Nothing sold is given by ads (§4.2) | rewards: ×2 offline (bounded), emergency empty, fuel voucher, baler rescue, morning bonus — no cosmetics, speed, automation, ad removal | Code |
| 7 | Caps: single ≤ 1/5, daily ≤ 1/2 of the cheapest pack; per-placement daily cap + cooldown, persisted (§4.3) | cash_s = 1 day income = 1000 units; single ≤ 200, daily ≤ 500 (kit budget) + sim caps per UTC day in the career save (`KitRewardCapsEqualTheBalanceData`, `DailyCapAndCooldownPersistInTheCareer`, `CashRewardsStayUnderOneFifthOfTheCheapestPack`) | Code |
| 8 | Natural stops (§4.4) | welcome-back card, full machine card, route editor / fleet sheet, baler down card, morning plan | Code |
| 9 | ad_free gets rewards without video, same caps (§4.5) | `BottleRewards.Get` → `Video = !AdFree` (kit logs `rewarded_skipped_paid_user`) | Code |
| 10 | Interstitial after rewarded is deferred (§4.6) | kit pacing (`Ads.RewardedCompleted`) | Code (kit) |
| 11 | Kill switches `rewardedEnabled`, `rcAdTracking` (§4.7, §5b) | kit `HouseAdsService.RemoteChanged` | Code (kit) |
| 12 | Rewarded analytics names (§4.9) | `rewarded_offer_shown / _accepted / _completed / _skipped_paid_user` (kit) + catalogue gate `GameEvents` | Code |
| 13 | Interstitial + banner = house only, no AdMob fallback (§2, 10-25) | kit `HouseAdsService` (house card + banner, related-games order incl. a provisional `bottle-route` row); no AdMob interstitial/banner code (`check.sh` gate) | Code; `ads_app` registration + `house_related.py` tags (logistics / civic / eco) P4/P8 in the monorepo |
| 14 | Banner standard 10-26: after onboarding, never over guide / purchase / loading; slot reserved; hidden for ad_free | `BottleMain.BannerAllowed` (no pager, no guide, no shop sheet), kit `reserve_slot`, `SafeAreaPadding` lifts the bottom bar | Code — verify on device |
| 15 | Interstitial at natural breaks, not session 1, not the guide | "Next day" after a report, `Sessions > 1`, not in the guide; kit pacing decides | Code |
| 16 | RevenueCat purchases + entitlements cached offline, restore | kit `CoddyPurchases` (+ Settings / Shop "Restore purchases") | Code; RC key P8 |
| 17 | RevenueCat ad tracking (§5b) | kit `RcAdTracker` (Unity SDK limitation noted in Volt City's UNITY.md §6 applies) | Code (kit) |
| 18 | Consumables once per transaction, ≤ 3 packs per UTC day | `Commerce.Deliver` (txn in settings) + kit `ConsumableLedger` (`ConsumableDeliversOncePerTransaction`) | Code |
| 19 | Starter / offer timing | starter day 3–7 after a report (once), match-day kit on match days, comeback after ≥ 3 days away; 1 per session, 3 days apart, never in the guide (`OffersAreNeverShownInTheGuideAndOnlyOnTheirDays`) | Code |
| 20 | In-app review at a wow moment, first session ~4 min (§12) | `ReviewHook`: first good day report (≥ 6/9 stars), a good bale sale, a new district; kit `ReviewGate` (≥ 240 s, ≤ 3 asks, 30 days apart, never in guide / window / background) | Code |
| 21 | Firebase events (§9, §14) | `onboarding_*`, `save_unreadable`, purchase / offer / crisis / day events through the validated catalogue | Code; `google-services.json` P8 |
| 22 | UMP privacy options in Settings (§17) | Settings → Privacy options (kit consent provider) + policy link | Code; UMP form in AdMob P8 (§15) |

**Owner approval needed** (CLAUDE.md §8 is updated only after it): `bottle-route` — noads, premium (Chief Operator
Edition), route_pass (Network Pass), bundle (Complete Bottle Route). Starter, speed, dispatcher_plus, offline_plus,
looks and cash packs do NOT remove ads.
