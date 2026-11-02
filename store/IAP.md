# Borderline Empire — in-app purchase (2026-10-10)

One product, both stores, nothing submitted for review.

| | |
|---|---|
| Product id | `com.coddykit.borderlineempire.empire_pass` |
| Type | non-consumable (iOS NON_CONSUMABLE, Play one-time product, RC `is_consumable=false`) |
| Price | USA $3.99, income-tiered (PPP) everywhere else (fleet `pricing-policy.ts`: T1 3.99 / T2 2.49 / T3 1.49 / T4 0.99) |
| Grants | permanent ×1.25 delivery payouts + sponsor bonus without a rewarded video (`data/monetization.json` `pass_delivery_multiplier`) |
| Code | `scripts/monetization/monetization.gd` — purchase grants only after a matching RC entitlement; Restore Purchases in the Shop |
| In-game path | Business tab → "Shop & sponsor bonus" (also Settings → "Shop & sponsor bonus") |

## App Store Connect (ASC IAP 6821093519)

- Reference name "Empire Pass", family sharing off, review note = `store/iap_catalogue.json` `review_note`.
- 10 localizations (en-US, tr, de-DE, es-ES, fr-FR, ja, ko, pt-BR, ru, zh-Hans): names are the game's own
  strings (`translations/strings.csv`), descriptions ≤45 chars translated with nitrorouter gpt-5.6-luna.
- Price schedule: USA base + tier prices (created 2026-10-09 by `src/game-iap-ppp.ts`, 10 manual prices).
- Availability: 175 territories, new territories on.
- App Review screenshot: `store/iap-review-shop.png` (real in-game Shop, Empire Pass + price + Restore Purchases).
- Script: `store-automation/_smuggler_asc.mjs iap` (idempotent), read-back: `… state`.
- First non-consumable of a new app is submitted WITH the app version (Apple refuses it separately through the API) —
  the owner does that together with the app submission.

## Google Play

- One-time product created 2026-10-10 by `SPEC=game-iap/smuggler.json PLATFORM=gp DRY=0 npx tsx src/game-iap-ppp.ts`
  after the billing AAB (versionCode 1791611984) was uploaded to the internal track as a DRAFT release.
  Read-back: purchase option `buy` ACTIVE, 171 regions (ET, AR, AZ, UZ not billable at regions version 2025/03),
  US 3.99 USD · TR 87.99 TRY · IN 110 INR.
- Listings in 10 languages (`_smuggler_play.mts iaplistings`, from `store/iap_catalogue.json`).

## RevenueCat (project proj134e4759 "Borderline Empire")

- Entitlement `empire_pass` ← iOS product (non_consumable) + Play product (one_time, is_consumable=false).
- Offering `default` (current) → package `$rc_lifetime` → both products. Read back 2026-10-10 via V2 API.
- Public SDK keys in `data/monetization.json` (appl_… / goog_…).
- Refund Control: `npx tsx src/refund-control/new-app.ts --project proj134e4759 …` → webhook + policy + Apple
  server notifications + Google RTDN all ✓ (2026-10-10).


# Version 1.2.0 catalog (W5, 2026-10-19) — Google Play + RevenueCat, iOS not yet

17 products, source of truth `data/iap.json` (coddy_iap shop template, `games/tools/shop`; RevenueCat backend `addons/coddy_iap/IapStore.gd`).
Ids `com.coddykit.borderlineempire.<slug>`; the live `empire_pass` id and price are unchanged. No subscription, no trial: the whole game
stays free; **only the ad-free products end interstitials** (fleet rule `games/CLAUDE.md` §3/§8: `noads`, `premium`, `bundle`, `empire_pass` - each grants the `no_ads` feature) and only they claim the rewarded bonuses without the video; cash packs, `starter`, `legacy_pack`, paints and the other looks/conveniences do NOT (they watch the ad). Buyers of anything before this rule keep ad-free via the one-time `legacy_ad_free` migration (`ad_free_migrated` marker in the Iap cache; a later cheap purchase never sets it). Rewarded: one reward <= 1/5 and a UTC day's rewards <= 1/2 of `cash_s` (30 min of reference income), per-placement daily cap + cooldown persisted, ads-config `rewardedEnabled` kill switch (absent = on), the next interstitial waits one more interval after a watched video.

| slug | name | kind | ref. USD | grants |
|---|---|---|---|---|
| `bundle` | Ultimate Bundle | permanent | $14.99 | empire_pass, paint_gold, hq_theme, auto_plus, offline_plus, paint_street, paint_military, paint_luxury, driver_portraits, no_ads |
| `premium` | Kingpin Edition | permanent | $9.99 | empire_pass, paint_gold, hq_theme, auto_plus, no_ads |
| `empire_pass` | Empire Pass | permanent | $3.99 | empire_pass, no_ads |
| `noads` | Remove Ads | permanent | $2.99 | no_ads |
| `automation_plus` | Crisis Manager | permanent | $3.99 | auto_plus |
| `offline_plus` | Longer Offline Earnings | permanent | $2.99 | offline_plus |
| `paint_street` | Street Paint Set | permanent | $1.99 | paint_street |
| `paint_military` | Military Paint Set | permanent | $1.99 | paint_military |
| `paint_luxury` | Luxury Paint Set | permanent | $1.99 | paint_luxury |
| `hq_theme` | HQ Signature | permanent | $1.99 | hq_theme |
| `driver_portraits` | Driver Portraits | permanent | $1.99 | driver_portraits |
| `cosmetics_all` | Cosmetics Collection | permanent | $4.99 | paint_street, paint_military, paint_luxury, paint_gold, hq_theme, driver_portraits |
| `starter` | Quick Start | permanent | $1.99 | pack `starter` |
| `legacy_pack` | Legacy Head Start | consumable | $3.99 | pack `legacy_pack` |
| `cash_s` | Small Cash Pack | consumable | $0.99 | pack `cash_s` |
| `cash_m` | Medium Cash Pack | consumable | $2.99 | pack `cash_m` |
| `cash_l` | Large Cash Pack | consumable | $6.99 | pack `cash_l` |

PPP (store-automation `pricing-policy.ts`, 4 tiers, `.99` rounding; the cash group is a ladder that rises in every tier):
bundle 14.99/8.99/4.99/3.49 · premium 9.99/5.99/3.49/1.99 · empire_pass 3.99/2.49/1.49/0.99 · noads 2.99/1.99/0.99/0.99 · automation_plus 3.99/2.49/1.49/0.99 ·
offline_plus 2.99/1.99/0.99/0.99 · paint_* / hq_theme / driver_portraits / starter 1.99/0.99/0.99/0.99 · cosmetics_all 4.99/2.99/1.99/0.99 ·
legacy_pack 3.99/2.49/1.49/0.99 · cash_s 0.99/0.99/0.99/0.99 · cash_m 2.99/1.99/1.49/1.49 · cash_l 6.99/3.99/2.49/1.99.

What the features do (all optional, looks never touch a number):
- `empire_pass`: ×1.25 delivery pay (`pass_delivery_multiplier`), ads gone (`no_ads`, a Pass is a §8 ad-free product), sponsor bonus without the video — unchanged from 1.1.0; 1.1.0 buyers are migrated from `monetization.json`.
- `auto_plus` (Crisis Manager, 5th automation line, switch in Business): road events are handled with the same choice a driver makes offline, finished daily jobs are claimed.
- `offline_plus`: away time counts up to 8 h instead of 2 h (`offline_plus_cap_seconds`).
- `paint_street` / `paint_military` / `paint_luxury` / `paint_gold`: colour sets in a vehicle's Paint job; `hq_theme`: lit gold sign + six company colours (Settings → HQ Signature); `driver_portraits`: a procedural face per driver.
- Cash packs: 30 / 75 / 150 minutes of the player's reference income (`EconomyManager.ref_income`: best steady income per minute of the last ~2 h, floor 120/min, reset by prestige), delivered as non-income cash (stages/goals unaffected). `starter` (offer on play-day 3-7, once) = 18 min + a skilled driver; `legacy_pack` (offer after a prestige) = max(3 × start fund, 20 min of the finished run's income).
- Offers: never in the tutorial or the first session, at most one per session, 3 days apart.

Store side (all Google Play; App Store untouched): Play one-time products created ACTIVE with 10 listings and 174 regions
(`store/iap/play_apply.json`, script `store-automation/iap_catalog_gp.ts`, `SKIP=empire_pass`); RevenueCat project proj134e4759: one product per
catalog entry (permanent = `is_consumable=false`, cash packs + legacy pack `is_consumable=true`), one entitlement per permanent product (key = slug),
offering `shop` (not current; `default` untouched) (`store/iap/rc_apply.json`, script `store-automation/_smuggler_rc_catalog.ts`).
Regenerate the store catalog with `python3 games/smuggler/store/iap/make_catalog.py`.
