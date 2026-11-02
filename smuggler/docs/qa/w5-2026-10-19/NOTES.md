# Borderline Empire 1.2.0 — W5: shop (IAP catalog), house-first interstitials, store rating + feedback (2026-10-19)

Branch `w5/smuggler-iap-2026-10-19` (own worktree), owner standards 7 + 8 and the IAP standard (`games/tools/IAP_STANDARD.md`),
catalog from `GAMES_IAP_STANDARD_2026-10-18.md` §smuggler, owner decision 00:20 (capped cash packs ARE added). Godot 4.7.2.

## What changed

| Area | Change |
|---|---|
| Shop | `addons/coddy_iap` (template `games/tools/shop`) with a **RevenueCat backend** (`IapStore.gd`; the game already shipped `godotx_revenue_cat`, godot-iap would have meant two billing stacks). 17 products in `data/iap.json`, 10 languages. Shop sheet rewritten (grouped cards `prod_<slug>`, price button = the store's own price, "Owned", restore) + Restore purchases in Settings, a locked feature (paint set, Crisis Manager, HQ Signature, portraits) opens the shop on its product. Empire Pass id/price unchanged; 1.1.0 buyers are migrated. |
| Features | `auto_plus` Crisis Manager (5th automation line: events + daily jobs), `offline_plus` (2 h → 8 h), paint sets (street / military / luxury / gold), HQ Signature (lit gold sign + 6 company colours, switch in Settings), procedural driver portraits, Kingpin Edition (Empire Pass + gold paint + HQ Signature + Crisis Manager), Ultimate Bundle. |
| Cash packs | `cash_s/m/l` = 30 / 75 / 150 min of the reference income (1 / 2.5 / 5 play-days of a 30-min-a-day player), floor 120/min, bought cash is not "earned" (stages/goals untouched), Quick Start (offer day 3-7, once) and Legacy Head Start (offer on prestige). |
| Ads | Standard 8: `scripts/monetization/coddy_ads.gd` = template + the three guards of the moneytycoon/train copies; our other games first (`ads-config` host `borderline-empire`), then AdMob (real Android interstitial unit from store-automation/.env, iOS unit left empty; AD_ID stays stripped). AdMob start (UMP form) is lazy: only after intro + tutorial and never for a buyer. Breaks: panel closed after 5 finished trips, welcome-back card. Any purchase = no interstitial, sponsor bonus without the video. Back is ignored while an ad is showing. |
| Rating / feedback | Standard 7: Google Play In-App Review (JavaClassWrapper, `com.google.android.play:review:2.0.2` added by `coddy_firebase_gradle`) at the 2nd promotion / 3rd goal, >=2 sessions, >=15 min, <=3 asks 30 days apart, calm moment only; no own star card, no reward. Settings → Send feedback (modal; `api.coddykit.com/api/feedbacks`, slug `borderline-empire`) and an Android "Rate Borderline Empire" link. |
| Store | Google Play: 16 new one-time products ACTIVE (10 listings, 174 regions, PPP) — `store/iap/play_apply.json`. RevenueCat proj134e4759: 16 products + 13 entitlements + offering `shop` — `store/iap/rc_apply.json`. App Store untouched. |

## Tests (all green)

`jenkins/build.sh test`: gameplay 219 checks, quality 60, lifecycle 67, graphics 2 x, **iap 319** (catalog, texts in 10 languages, gate, fake-RevenueCat
purchase / cancel / pending / restore / replay, features, packs, offers, ads wiring, rating rules, feedback, shop sheet in 10 languages, no trial / no subscription),
layout scan en/de/ru/pt (shop, offers, feedback modal included) 0 findings, i18n validate 636 keys x 10, release-gate unit tests.
Monkey (`tests/tool_monkey.gd -- --paywall`, 90 s, 3.7k actions): 0 errors, 0 orphans.
Shots (windowed GPU, tr / de / ja / ru x 10 screens, `tools/qa_shop_shots.py`): `shots/` — no horizontal overflow, no control under 88 canvas units.

## Open items / owner

1. **Advertising ID stays "No"**: `coddy_firebase_gradle` strips `AD_ID` from the manifest unless `[coddy_ads] keep_ad_id=true` (fleet rule after the Airport / Train
   1.1.0 rejection; not set here). AdMob requests are non-personalised and still fill, only without the ad id. Play Console: Data safety must still be re-imported
   for 1.2.0 (ads interaction events, e-mail + message from the feedback form, a random device id + device model for house-ad tracking, purchases);
   `store/play-data-safety.csv` now also answers e-mail address + other in-app messages (optional, app functionality / developer communications) for the feedback form;
   `store/PRIVACY_POLICY_ADDENDUM.md` is updated, the public policy page still has to be reconciled (as before).
2. **ads-config** (backend, not changed here): host `borderline-empire` exists (house interstitial enabled, budget 2, 14 days) but its sequence lacks money-district, parts-rush
   and ski-resort creatives; Borderline Empire is a creative in every host except `trading-sim` (and the hosts that are disabled: lego, blockforge, cinnamon).
3. **iOS**: not part of this work. The iOS build would show only the Empire Pass (the store lists nothing else), no AdMob interstitial (empty iOS unit), and still has no
   native `CoddyReview` singleton (never asks for a rating on iOS).
4. First real-device purchase / restore / consumable run on the AAB (internal track) is still to be done by the Patron: nothing here proves a Play Billing sale.
5. RevenueCat: every run of `iap_rc_catalog` creates a V2 secret in the dashboard (labels `iap-catalog-borderlineempire-*`, 5 so far) — revoke them in RC → API keys.
6. Empire Pass listing text on Play still says "Permanent x1.25 delivery pay, no-video bonus" (live product left alone); after 1.2.0 it also removes ads.
7. Existing Empire Pass owners who now want Kingpin Edition pay the full 9.99 (no upgrade price).
8. Pending (Ask-to-buy / slow card) consumable purchases are not delivered when they complete later (permanent ones are, via the entitlement sweep on resume).
