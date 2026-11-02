# Ridgeback Rangers — monetization parity (fleet §11, P4 acceptance)

Item-by-item against the fleet standard (Corrupt = the live reference; `IAP_STANDARD.md`, `REWARDED_ADS_STRATEGY.md`,
`ADS_KIT_README.md`, `REVENUECAT_AD_TRACKING.md`). "Code" = implemented and tested in this repo; "P8" = needs the
Patron's account work (ids, store products, ads_app registration) before it can work in a release build.

| # | Item (fleet) | Ridgeback Rangers | State |
|---|---|---|---|
| 1 | Broad IAP catalogue: Remove Ads, edition, pass, starter, consumables, convenience, cosmetics, bundle | 17 products (`unity/Tools/build_iap.py` → `Resources/data/iap.json`, plan §10): noads, premium (Chief Warden Edition: golden tram + starter coins), ranger_pass (Ranger Pass), bundle (Complete Sanctuary Collection), speed, offline_plus (Night Shift), 3 tram skins + collection, cash S/M/L/XL, starter (Welcome Crate: coins + 1 incubator) / storm kit / comeback offers. Left out vs the plan: Amber packs (premium currency), `slots`, `automation`, species packs (post-launch per plan §14) | Code; store products P8 |
| 2 | No trial, no subscription, no loot box, no premium currency (§1) | none in catalogue or texts (`check.sh` gate + `NoTrialOrSubscriptionWordingAnywhere`) | Code |
| 3 | `ad_free` only from Remove Ads + big packs (§3) | entitlement `noads` granted by noads / premium / ranger_pass / bundle; cheap packs never (`OnlyRemoveAdsAndBigPacksMakeTheGameAdFree`, `BuyingACheapPackDoesNotRemoveAds_RemoveAdsDoes`) | Code — **the §8 table row needs the owner's approval** |
| 4 | Legacy buyers stay ad-free (§3) | not applicable: new game, nobody bought before the rule | — |
| 5 | Rewarded = AdMob only, opt-in, reward after earned + closed (§4.1) | kit `RewardedController` (UMP → the one `MobileAds.Initialize` → load); `RidgebackRewards` | Code; unit id P8 |
| 6 | Nothing sold is given by ads (§4.2) | rewards (plan §10): Dawn Report ×2 overnight donations (bounded), borrow a ranger for one pin, warm the incubator (−10 min), Storm Shelter (fence repair, last resort), Scout's tip (info) — no cosmetics, speed, permanent slots, ad removal | Code |
| 7 | Caps: single ≤ 1/5, daily ≤ 1/2 of the cheapest pack; per-placement daily cap + cooldown, persisted (§4.3) | cash_s = 1 day income = 1000 units; single ≤ 200, daily ≤ 500 (kit budget; 2×80 + 3×40 + 2×50 + 1×100 + 2×1 = 482) + sim caps per UTC day in the career save (`KitRewardCapsEqualTheBalanceData`, `DailyCapAndCooldownPersistInTheCareer`, `CashRewardsStayUnderOneFifthOfTheCheapestPack`) | Code |
| 8 | Natural stops (§4.4) | welcome-back card, pin card (no free ranger), hatchery, fence / escape pin card, dig chart | Code |
| 9 | ad_free gets rewards without video, same caps (§4.5) | `RidgebackRewards.Get` → `Video = !AdFree` (kit logs `rewarded_skipped_paid_user`) | Code |
| 10 | Interstitial after rewarded is deferred (§4.6) | kit pacing (`Ads.RewardedCompleted`) | Code (kit) |
| 11 | Kill switches `rewardedEnabled`, `rcAdTracking` (§4.7, §5b) | kit `HouseAdsService.RemoteChanged` | Code (kit) |
| 12 | Rewarded analytics names (§4.9) | `rewarded_offer_shown / _accepted / _completed / _skipped_paid_user` (kit) + catalogue gate `GameEvents` | Code |
| 13 | Interstitial + banner = house only, no AdMob fallback (§2, 10-25) | kit `HouseAdsService` (house card + banner, related-games order incl. a provisional `ridgeback-rangers` row (animal-care crews first: pest-patrol, lifeguard-hq…)); no AdMob interstitial/banner code (`check.sh` gate) | Code; `ads_app` registration + `house_related.py` tags (zoo / animals / tycoon) P4/P8 in the monorepo |
| 14 | Banner standard 10-26: after onboarding, never over guide / purchase / loading; slot reserved; hidden for ad_free | `RidgebackMain.BannerAllowed` (no pager, no guide, no shop sheet), kit `reserve_slot`, `SafeAreaPadding` lifts the bottom bar | Code — verify on device |
| 15 | Interstitial at natural breaks, not session 1, not the guide | "Next day" after a report, `Sessions > 1`, not in the guide; kit pacing decides | Code |
| 16 | RevenueCat purchases + entitlements cached offline, restore | kit `CoddyPurchases` (+ Settings / Shop "Restore purchases") | Code; RC key P8 |
| 17 | RevenueCat ad tracking (§5b) | kit `RcAdTracker` (Unity SDK limitation noted in Volt City's UNITY.md §6 applies) | Code (kit) |
| 18 | Consumables once per transaction, ≤ 3 packs per UTC day | `Commerce.Deliver` (txn in settings) + kit `ConsumableLedger` (`ConsumableDeliversOncePerTransaction`) | Code |
| 19 | Starter / offer timing | starter day 3–7 after a report (once), storm kit on an island event, comeback after ≥ 3 days away; 1 per session, 3 days apart, never in the guide (`OffersAreNeverShownInTheGuideAndOnlyOnTheirDays`) | Code |
| 20 | In-app review at a wow moment, first session ~4 min (§12) | `ReviewHook`: first full Park Day with a good report (≥ 6/9 stars), the first hatchling, a new zone; kit `ReviewGate` (≥ 240 s, ≤ 3 asks, 30 days apart, never in guide / window / background) | Code |
| 21 | Firebase events (§9, §14) | `onboarding_*`, `save_unreadable`, purchase / offer / crisis / day / pin / hatch events through the validated catalogue | Code; `google-services.json` P8 |
| 22 | UMP privacy options in Settings (§17) | Settings → Privacy options (kit consent provider) + policy link | Code; UMP form in AdMob P8 (§15) |

**Owner approval needed** (CLAUDE.md §8 is updated only after it): `ridgeback-rangers` — noads, premium (Chief Warden
Edition), ranger_pass (Ranger Pass — the plan lists it as a "pass"; confirm), bundle (Complete Sanctuary Collection). Starter,
storm kit, comeback, speed, offline_plus, tram skins and coin packs do NOT remove ads.

**Audience (plan R4):** declared 13+ (not Families) → house interstitial + banner on, AdMob rewarded only. Dinosaurs attract
children: if Play classifies the app as appealing to children, fleet §2 kids rules apply (no house interstitial / banner, no
AD_ID). Owner decision pending; no kids variant was built.
