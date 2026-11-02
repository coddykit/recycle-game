# Semester Rush — monetization parity (fleet §11, P4 acceptance)

Item-by-item against the fleet standard (Corrupt = the live reference; `IAP_STANDARD.md`, `REWARDED_ADS_STRATEGY.md`,
`ADS_KIT_README.md`, `REVENUECAT_AD_TRACKING.md`). "Code" = implemented and tested in this repo; "P8" = needs the
Patron's account work (ids, store products, ads_app registration) before it can work in a release build.

| # | Item (fleet) | Semester Rush | State |
|---|---|---|---|
| 1 | Broad IAP catalogue | 17 products (`unity/Tools/build_iap.py` → `Resources/data/iap.json`): noads, premium (Chancellor Edition), bundle (Complete Campus Collection), speed, automation (Registrar), offline_plus (Long Break), 3 campus themes + all themes, cash S/M/L/XL, starter / exam kit / comeback offers | Code; store products P8. `dean_pass`, `slots`, `scenarios`, gowns / mascots = v1.1 (plan §14) |
| 2 | No trial, subscription, loot box, premium currency; **no paid builders** (plan §10) | none; construction is parallel and free for everyone (`check.sh` gate + `NoTrialOrSubscriptionWordingAnywhere`, `ConstructionTakesGameTimeAndCrewsWorkInParallel`) | Code |
| 3 | `ad_free` only from Remove Ads + big packs (§3) | entitlement `noads` granted by noads / premium / bundle; cheap packs never (`OnlyRemoveAdsAndBigPacksMakeTheGameAdFree`, `BuyingACheapPackDoesNotRemoveAds_RemoveAdsDoes`, starter / exam kit tests) | Code — **§8 row needs the owner's approval** (plan list adds `dean_pass` when it ships) |
| 4 | Legacy buyers stay ad-free | not applicable (new game) | — |
| 5 | Rewarded = AdMob only, opt-in, reward after earned | kit `RewardedController`; `SemesterRewards` | Code; unit id P8 |
| 6 | Nothing sold is given by ads | ×2 break pay (bounded), extra marshals for one bell, alumni supplies, "call the city for help" (ends a crisis) — no speed, automation, themes, ad removal, permanent staff | Code |
| 7 | Caps: single ≤ 1/5, daily ≤ 1/2 of the cheapest pack; daily cap + cooldown persisted | cash_s = 1 day income = 1000 units; single ≤ 200, daily ≤ 500 (kit) + sim caps per UTC day in the save (`KitRewardCapsEqualTheBalanceData`, `DailyCapAndCooldownPersistInTheCareer`, `CashRewardsStayUnderOneFifthOfTheCheapestPack`) | Code |
| 8 | Natural stops | welcome-back card, late-counter sheet (bell change), week report, crisis sheet | Code |
| 9 | ad_free gets rewards without video | `SemesterRewards.Get` → `Video = !AdFree` | Code |
| 10 | Interstitial after rewarded deferred | kit pacing | Code (kit) |
| 11 | Kill switches `rewardedEnabled`, `rcAdTracking` | kit | Code (kit) |
| 12 | Rewarded analytics names | kit + `GameEvents` catalogue gate | Code |
| 13 | Interstitial + banner = house only | kit `HouseAdsService`, provisional `semester-rush` row in `HouseRelatedTable.g.cs`; no AdMob interstitial / banner code (`check.sh`) | Code; `ads_app` + `house_related.py` tags (campus / schedule / education / crowd) P4/P8 |
| 14 | Banner standard 10-26 | `SemesterMain.BannerAllowed` (no pager, no guide, no shop sheet), kit `reserve_slot`, `SafeAreaPadding` | Code — verify on device |
| 15 | Interstitial at natural breaks only | "Next week" after a week report (never in a live bell change or an exam), `Sessions > 1`, not in the guide | Code |
| 16 | RevenueCat purchases + restore | kit `CoddyPurchases` + Settings / Shop "Restore purchases" | Code; RC key P8 |
| 17 | RevenueCat ad tracking (§5b) | kit `RcAdTracker` | Code (kit) |
| 18 | Consumables once per transaction, ≤ 3 packs per UTC day | `Commerce.Deliver` + kit ledger (`ConsumableDeliversOncePerTransaction`) | Code |
| 19 | Offer timing | starter day 3–7 after a week report (once), exam kit in exam week, comeback after ≥ 3 days away; 1 per session, 3 days apart, never in the guide | Code |
| 20 | In-app review at a wow moment, first session ~4 min | `ReviewHook`: first week with zero late students (plan §10), a ≥ 80 % week, a zone opened, a semester closed; kit `ReviewGate` | Code |
| 21 | Firebase events | `onboarding_*`, `save_unreadable`, purchase / offer / crisis / week events through the validated catalogue | Code; `google-services.json` P8 |
| 22 | UMP privacy options in Settings | Settings → Privacy options + policy link | Code; UMP form P8 |

**Owner approval needed** (CLAUDE.md §8 is updated only after it): `semester-rush` — noads, premium (Chancellor
Edition), bundle (Complete Campus Collection); `dean_pass` (Dean's Pass) when it ships in v1.1. Starter, exam kit,
comeback, speed, automation, offline_plus, themes and cash packs do NOT remove ads.
