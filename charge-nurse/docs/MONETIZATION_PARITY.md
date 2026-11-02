# Charge Nurse — monetization parity (fleet §11, P4 acceptance)

Item-by-item against the fleet standard (Corrupt = the live reference; `IAP_STANDARD.md`, `REWARDED_ADS_STRATEGY.md`,
`ADS_KIT_README.md`, `REVENUECAT_AD_TRACKING.md`). "Code" = implemented and tested in this repo; "P8" = needs the
Patron's account work (ids, store products, ads_app registration). Scope = the plan's MVP catalogue (§14); the rest of
the §10 catalogue is listed as **v1.1** (a known parity gap, by plan).

| # | Item (fleet) | Charge Nurse | State |
|---|---|---|---|
| 1 | Broad IAP catalogue | MVP 12 products (`unity/Tools/build_iap.py` → `Resources/data/iap.json`): noads, premium (Chief of Medicine Edition), bundle (Complete Hospital Collection), speed, 3 themes (modern / retro / playful), cash S/M/L/XL, starter offer | Code; store products P8 |
| 1b | v1.1 (plan §10, not in the MVP) | automation (Charge Team), offline_plus (the sim already reads the perk), slots, uniforms_pack, ambulance_pack, cosmetics_all, **chief_pass (ad_free)**, scenarios, surge_kit, comeback | **gap by plan** |
| 2 | No trial, no subscription, no loot box, no premium currency (§1) | none (`check.sh` gate + `NoTrialOrSubscriptionWordingAnywhere`) | Code |
| 3 | `ad_free` only from Remove Ads + big packs (§3) | entitlement `noads` granted by noads / premium / bundle; starter, speed, themes, cash never (`OnlyRemoveAdsAndBigPacksMakeTheGameAdFree`, `BuyingACheapPackDoesNotRemoveAds_RemoveAdsDoes`) | Code — **§8 row needs the owner's approval** |
| 4 | Legacy buyers stay ad-free | not applicable (new game) | — |
| 5 | Rewarded = AdMob only, opt-in, reward after earned | kit `RewardedController`; `NurseRewards` | Code; unit id P8 |
| 6 | Nothing sold is given by ads (§4.2) | rewards: ×2 night pay (bounded), float staff 60 game min, neighbour hospital takes ≤ 5 waiting (once per crisis), morning bonus — no speed, themes, staff or ad removal | Code |
| 7 | Caps (single ≤ 1/5, daily ≤ 1/2 of cash_s; per-placement cap + cooldown, persisted) | cash_s = 1 day = 1000 units, single ≤ 200, daily ≤ 500 + sim caps per UTC day in the save (`KitRewardCapsEqualTheBalanceData`, `DailyCapAndCooldownPersistInTheCareer`, `CashRewardsStayUnderOneFifthOfTheCheapestPack`) | Code |
| 8 | Natural stops (§4.4) | welcome-back card (offline), Board sheet during a busy shift (float staff), crisis "last resort" (neighbour), morning board (bonus) | Code |
| 9 | ad_free gets rewards without video | `NurseRewards.Get` → `Video = !AdFree` | Code |
| 10 | Interstitial after rewarded is deferred | kit pacing | Code (kit) |
| 11 | Kill switches `rewardedEnabled`, `rcAdTracking` | kit | Code (kit) |
| 12 | Rewarded analytics names | kit + `GameEvents` catalogue | Code |
| 13 | Interstitial + banner = house only (§2) | kit `HouseAdsService`, provisional `charge-nurse` row in `HouseRelatedTable.g.cs` (pharmacy-rush, disaster-command, lifeguard-hq first) | Code; `ads_app` + `house_related.py` tags P4/P8 |
| 14 | Banner standard 10-26 | `NurseMain.BannerAllowed` (no pager / guide / shop sheet), `SafeAreaPadding` | Code — verify on device |
| 15 | Interstitial at natural breaks | "Next day" after a report, `Sessions > 1`, never in the guide, **never on a crisis / surge day** (plan §10) | Code |
| 16 | RevenueCat purchases, offline cache, restore | kit `CoddyPurchases` + Restore in Shop / Settings | Code; RC key P8 |
| 17 | RevenueCat ad tracking (§5b) | kit `RcAdTracker` | Code (kit) |
| 18 | Consumables once per transaction, ≤ 3 per UTC day | `Commerce.Deliver` + kit ledger (`ConsumableDeliversOncePerTransaction`); Edition's 1-day supplies once per device (`TheEditionGivesItsStarterSuppliesOncePerDevice`) | Code |
| 19 | Starter / offer timing | starter day 3–7 after a report, once, never removes ads, +1 porter; 1 offer per session, 3 days apart, never in the guide | Code |
| 20 | Review at a wow moment, first session ~4 min | `ReviewHook`: first shift cleared with ≥ 6/9 stars (the guide's shift → deferred to right after), a new wing; kit `ReviewGate` | Code |
| 21 | Firebase events | `onboarding_*`, `save_unreadable`, purchases, `lens_used`, `ambulance_answer`, crisis / day events via the validated catalogue | Code; `google-services.json` P8 |
| 22 | UMP privacy options in Settings | Settings → Privacy options + policy link | Code; UMP form P8 |

**Owner approval needed** (CLAUDE.md §8 only after it): `charge-nurse` — noads, premium (Chief of Medicine Edition),
bundle (Complete Hospital Collection), chief_pass (Chief Resident Pass, v1.1). Starter, speed, themes and cash packs do
NOT remove ads.
