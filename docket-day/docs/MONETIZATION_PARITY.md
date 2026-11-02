# Docket Day — monetization parity (fleet §11, P4 acceptance)

Item-by-item against the fleet standard (Corrupt = the live reference; `IAP_STANDARD.md`, `REWARDED_ADS_STRATEGY.md`,
`ADS_KIT_README.md`, `REVENUECAT_AD_TRACKING.md`) and plan §10. "Code" = implemented and tested in this repo; "P8" =
needs the Patron's account work (ids, store products, ads_app registration) before it works in a release build.

| # | Item (fleet) | Docket Day | State |
|---|---|---|---|
| 1 | Broad IAP catalogue: Remove Ads, edition, pass, starter, consumables, convenience, cosmetics, bundle | 20 products (`unity/Tools/build_iap.py` → `Resources/data/iap.json`): noads $2.99, premium (Managing Partner Edition) $8.99, partner_pass (Partner's Pass, permanent) $5.99, bundle (Complete Docket Collection) $17.99, speed (Faster Days), automation (Office Manager), overnight_plus (Overnight+), 3 office styles + Suits & Gowns + Courtroom themes + Cosmetic Collection, cash S/M/L/XL (1/2.5/5/10 days), starter / Bridge Funds / Welcome Back offers | Code; store products P8. Plan rows **not** in the MVP: `scenarios` (plan §14: catalogue without scenarios) and `slots` (needs multi-career saves — open question) |
| 2 | No trial, no subscription, no loot box, no premium currency (§1) | none in catalogue or texts (`check.sh` gate + `NoTrialOrSubscriptionWordingAnywhere`) | Code |
| 3 | `ad_free` only from Remove Ads + big packs (§3) | entitlement `noads` granted by noads / premium / partner_pass / bundle; cheap packs never (`OnlyRemoveAdsAndBigPacksMakeTheGameAdFree`, `BuyingACheapPackDoesNotRemoveAds_RemoveAdsDoes`) | Code — **the §8 table row needs the owner's approval** |
| 4 | Legacy buyers stay ad-free (§3) | not applicable: new game | — |
| 5 | Rewarded = AdMob only, opt-in, reward after earned (§4.1) | kit `RewardedController` (UMP → the one `MobileAds.Initialize` → load); `DocketRewards` | Code; unit id P8 |
| 6 | Nothing sold is given by ads (§4.2) | R1 billing boost (+15 % of today's invoices, bounded), R2 case file preview (the next 3 clients' twist — information, no money), offline ×2 (bounded). No speed, automation, pass, cosmetics, trust money | Code |
| 7 | Caps: single ≤ 1/5, daily ≤ 1/2 of the cheapest pack; per-placement daily cap + cooldown, persisted (§4.3) | cash_s = 1 day of income = 1000 units; single ≤ 200, daily ≤ 500 (kit budget) + sim caps per UTC day in the career save (`KitRewardCapsEqualTheBalanceData`, `DailyCapAndCooldownPersistInTheCareer`, `CashRewardsStayUnderTheFleetCaps`, `BillingBoostOnlyOnTheReportBeforeTheBillingRun`) | Code |
| 8 | Natural stops (§4.4) | the day report before the billing run, reception (who's coming next), the welcome-back card | Code |
| 9 | ad_free gets rewards without video, same caps (§4.5) | `DocketRewards.Get` → `Video = !AdFree` (kit logs `rewarded_skipped_paid_user`) | Code |
| 10 | Interstitial after rewarded is deferred (§4.6) | kit pacing (`Ads.RewardedCompleted`) | Code (kit) |
| 11 | Kill switches `rewardedEnabled`, `rcAdTracking` (§4.7, §5b) | kit `HouseAdsService.RemoteChanged` | Code (kit) |
| 12 | Rewarded analytics names (§4.9) | `rewarded_offer_shown / _accepted / _completed / _skipped_paid_user` (kit) + catalogue gate `GameEvents` | Code |
| 13 | Interstitial + banner = house only, no AdMob fallback (§2) | kit `HouseAdsService` (house card + banner, related-games order with a provisional `docket-day` row: bureaucracy-office, silent-evidence, corrupt-state, money-district, lastdigit first); no AdMob interstitial/banner code (`check.sh` gate) | Code; `ads_app` registration + `house_related.py` tags (office / civic / justice / management) P4/P8 in the monorepo |
| 14 | Banner standard 10-26: after onboarding, never over guide / purchase / loading / hearing; slot reserved; hidden for ad_free | `DocketMain.BannerAllowed` (no pager, no guide, no hearing scene, no shop sheet), kit `reserve_slot`, `SafeAreaPadding` lifts the bottom bar | Code — verify on device |
| 15 | Interstitial at natural breaks, never in session 1 / the guide / a hearing | "Next day" after the billing run, `Sessions > 1`, not in the guide; kit pacing decides | Code |
| 16 | RevenueCat purchases + entitlements cached offline, restore | kit `CoddyPurchases` (+ Settings / Shop "Restore purchases") | Code; RC key P8 |
| 17 | RevenueCat ad tracking (§5b) | kit `RcAdTracker` | Code (kit) |
| 18 | Consumables once per transaction, ≤ 3 packs per UTC day, operating cash only | `Commerce.Deliver` (txn in settings) + kit `ConsumableLedger`; never trust money (`ConsumableDeliversOncePerTransactionIntoOperatingCash`) | Code |
| 19 | Starter / offer timing, no "limited time" spam (review theme 4) | starter day 3–7 after a report (once, with an intern day pass), Bridge Funds after a day that ended below zero, Welcome Back after ≥ 3 days away; max 1 offer per session, 3 days apart, never in the guide | Code |
| 20 | In-app review at a wow moment, first session ~4 min (§12) | `ReviewHook`: a won hearing (the guide's first hearing at ~3–4 min, deferred to the guide's end), a ≥ 7/9-star day, a chapter move; kit `ReviewGate` (≥ 240 s, ≤ 3 asks, 30 days apart, never in guide / window / background) | Code |
| 21 | Firebase events (§9, §14) | `onboarding_*`, `save_unreadable`, purchase / offer / crisis / hearing / day events through the validated catalogue | Code; `google-services.json` P8 |
| 22 | UMP privacy options in Settings (§17) | Settings → Privacy options (kit consent provider) + policy link | Code; UMP form in AdMob P8 (§15) |
| 23 | No stored-cash / vault cap (plan §2: a reference-game soft-lock we do not use) | none in the sim (`NoMoneyCapAnywhere`) | Code |

**Owner approval needed** (CLAUDE.md §8 is updated only after it): `docket-day` — noads, premium (Managing Partner
Edition), partner_pass (Partner's Pass), bundle (Complete Docket Collection). Starter, Bridge Funds, Welcome Back, speed,
automation, overnight_plus, office styles, outfits, courtroom themes and cash packs do NOT remove ads.
