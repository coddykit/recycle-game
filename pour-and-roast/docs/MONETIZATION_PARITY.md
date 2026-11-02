# Pour & Roast — monetization parity (fleet §11, P4 acceptance)

Item-by-item against the fleet standard (Corrupt = the live reference; `IAP_STANDARD.md`, `REWARDED_ADS_STRATEGY.md`,
`ADS_KIT_README.md`, `REVENUECAT_AD_TRACKING.md`). "Code" = implemented and tested in this repo; "P8" = needs the
Patron's account work (ids, store products, ads_app registration) before it can work in a release build.

| # | Item (fleet) | Pour & Roast | State |
|---|---|---|---|
| 1 | Broad IAP catalogue | **22 products** (design §10, `unity/Tools/build_iap.py` → `Resources/data/iap.json`): noads, premium (Head Roaster Edition), roast_pass (Roaster's Pass), bundle (Complete Pour & Roast), speed, automation (Head Barista+), slots (Extra Saves), offline_plus, theme_nordic, theme_artdeco, cups, latte_art, aprons, cosmetics_all, cash S/M/L/XL, offers starter / crisis (Milk Run Kit) / comeback / milestone (New-Venue Pack) | Code; store products P8 |
| 2 | No trial, subscription, loot box, premium currency (§1); max item $19.99; noads its own cheap product | `check.sh` gate + `NoTrialOrSubscriptionWordingAnywhere` + `CatalogueHasThePlansTwentyTwoSkusAndNoItemAboveTwentyDollars` | Code |
| 3 | `ad_free` only from Remove Ads + big packs (§3) | entitlement `noads` granted by noads / premium / roast_pass / bundle; cheap packs never (`OnlyRemoveAdsAndBigPacksMakeTheGameAdFree`, `BuyingACheapPackDoesNotRemoveAds_RemoveAdsDoes`) | Code — **the §8 table row needs the owner's approval** |
| 4 | Legacy buyers stay ad-free (§3) | not applicable: new game | — |
| 5 | Rewarded = AdMob only, opt-in, reward after earned (§4.1) | kit `RewardedController` (UMP → the one `MobileAds.Initialize` → load); `RoastRewards` | Code; unit id P8 |
| 6 | Nothing sold is given by ads (§4.2) | 6 placements: ×2 offline (bounded), extra barista (1 h), auto dial-in (90 min), emergency stock, crisis last resort, morning bonus — never speed, automation, slots, cosmetics, ad removal | Code |
| 7 | Caps: single ≤ 1/5, daily ≤ 1/2 of the cheapest pack; per-placement UTC-day cap + cooldown, persisted (§4.3) | cash_s = 1 day income = 1000 units; single ≤ 200, daily ≤ 500 (kit) + sim caps in the career save (`KitRewardCapsEqualTheBalanceData`, `DailyCapAndCooldownPersistInTheCareer`, `CashRewardsStayUnderOneFifthOfTheCheapestPack`) | Code |
| 8 | Natural stops (§4.4) | welcome-back card, bar sheet (helper, dial-in, last resort on a crisis), menu & stock sheet (low milk / ice), forecast sheet (morning bonus) | Code |
| 9 | ad_free gets rewards without video, same caps (§4.5) | `RoastRewards.Get` → `Video = !AdFree` | Code |
| 10 | Interstitial after rewarded deferred (§4.6) | kit pacing | Code (kit) |
| 11 | Kill switches `rewardedEnabled`, `rcAdTracking` | kit `HouseAdsService.RemoteChanged` | Code (kit) |
| 12 | Rewarded analytics names (§4.9) | kit events + `GameEvents` catalogue gate | Code |
| 13 | Interstitial + banner = house only (§2) | kit `HouseAdsService`; provisional `pour-and-roast` row in `HouseRelatedTable.g.cs` (drink + food-rush games first); no AdMob interstitial / banner code (`check.sh`) | Code; `ads_app` registration + `house_related.py` tags (food-service / drinks / cafe) P4/P8 |
| 14 | Banner standard: after onboarding, never over guide / purchase / loading; hidden for ad_free | `RoastMain.BannerAllowed` (no pager, no guide, no shop), `SafeAreaPadding` lifts the bottom bar | Code — verify on device |
| 15 | Interstitial at natural breaks only | "Next day" after a report, `Sessions > 1`, not in the guide | Code |
| 16 | RevenueCat purchases, offline entitlements, restore | kit `CoddyPurchases` (+ Shop / Settings "Restore purchases") | Code; RC key P8 |
| 17 | RevenueCat ad tracking (§5b) | kit `RcAdTracker` | Code (kit) |
| 18 | Consumables once per transaction, ≤ 3 packs per UTC day | `Commerce.Deliver` (txn in settings) + kit ledger (`ConsumableDeliversOncePerTransaction`); the Edition's starter beans once per device (`EditionGivesStarterBeansOnceAndAnExtraSlot`) | Code |
| 19 | Starter / offer timing | starter day 3–7 after a report (once), Milk Run Kit on a late milk van, comeback after ≥ 3 days away, New-Venue Pack on a venue unlock; 1 per session, 3 days apart, never in the guide | Code |
| 20 | In-app review at a wow moment, first session ~4 min (§12) | `ReviewHook`: first ≥ 4 ★ day report, a ≥ 85 roast, a new venue; kit `ReviewGate` (≥ 240 s, never in guide / window) | Code |
| 21 | Firebase events | `onboarding_*`, `save_unreadable`, purchase / offer / crisis / day / roast events through the validated catalogue | Code; `google-services.json` P8 |
| 22 | UMP privacy options in Settings (§17) | Settings → Privacy options + policy link | Code; UMP form P8 |

**Owner approval needed** (CLAUDE.md §8 is updated only after it): `pour-and-roast` — noads (Remove Ads), premium (Head
Roaster Edition), roast_pass (Roaster's Pass), bundle (Complete Pour & Roast). Starter, crisis, comeback, milestone,
speed, automation, slots, offline_plus, looks and cash packs do NOT remove ads.
