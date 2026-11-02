# Volt City — IAP catalogue + ads (F0, Patron 2026-10-27)

> Binding: CLAUDE §3 (ad-free scope), §4 (rewarded rules), §5/§5b (AdMob, RevenueCat ad tracking), §8 (ad_free table), §11 (revenue parity
> with Corrupt State), [`../../tools/IAP_STANDARD.md`](../../tools/IAP_STANDARD.md), playbook section 15. Shared code `games/tools/shop/`
> (coddy_iap) with the RevenueCat back end; Play products via `store-automation/iap_catalog_gp.ts` (**Patron** creates them — no slave
> opens Play/AdMob/RC/Firebase). **No trial, no subscription, no loot box, no energy, no pay-to-win wall, no premium currency.**
> Product ids `com.coddykit.voltcity.<slug>`; **an id never changes once live**. Data file in P4: `data/iap.json` (copy the Grid Boss
> schema: `prefix`, `langs`, `ad_free_features`, `ad_free_products`, `groups`, `offers`, `consumable_policy`, `products`).

## 1. Catalogue (24 products)

> **P4 status (2026-10-27, [P4-MONEY.md](P4-MONEY.md)):** 23 sold — `scenarios` is reserved (id kept, not in Play / RC)
> until its content exists; 13 entitlements. Source of every generated file: `unity/Tools/build_iap.py`.

Price = T1 reference (USD). Tiers (4 PPP, Play price templates round): **T1 100 %** (US, CA, UK, DE, FR, JP, KR, AU, Nordics…) ·
**T2 ≈ 60 %** (ES, IT, PT, PL, TR-high, BR-high, MX…) · **T3 ≈ 35 %** (BR, TR, RU, ID, PH, ZA…) · **T4 ≈ 20 %** (IN, PK, EG, NG, BD…).
In the game the price is always the store's `displayPrice`; the fallback catalogue never shows a raw id or an invented price.

| # | Slug | Name (en) | T1 USD | Kind | Grants (RC entitlements) | Removes forced ads? |
|---|---|---|---|---|---|---|
| 1 | `noads` | Remove Ads | 2.99 | permanent | `noads` | **yes** |
| 2 | `premium` | Chief Engineer Edition | 8.99 | permanent | `noads`, `forecast_plus`, `slots`, `theme_gold` (gold control-room UI theme + gold truck livery) | **yes** |
| 3 | `speed` | Rush Hour Speed | 2.99 | permanent | `speed4` (4× + auto-skip the shift report) | no |
| 4 | `dispatcher` | Dispatcher Assistant | 4.99 | permanent | `dispatcher` (auto-baseload, auto-battery, auto-crew toggles early; free path earns them at chapters 3/4/5) | no |
| 5 | `forecast_plus` | Long-Range Forecast | 1.99 | permanent | `forecast_plus` (3 shifts ahead instead of 1) | no |
| 6 | `offline_plus` | Night Crew+ | 2.99 | permanent | `offline_plus` (offline cap 8 h → 16 h, rate ×1.5) | no |
| 7 | `slots` | Extra City Slots | 1.99 | permanent | `slots` (+3 save slots) | no |
| 8 | `scenarios` | Challenge Scenarios | 2.99 | permanent | `scenarios` (5 hand-made scenarios on the same map: Heat Dome Week, Ice Storm Night, Final Match Night, Eclipse Day, Islanded Grid) | no |
| 9 | `livery_sunset` | Sunset Truck Livery | 1.49 | permanent | `livery_sunset` | no |
| 10 | `livery_arctic` | Arctic Truck Livery | 1.49 | permanent | `livery_arctic` | no |
| 11 | `pylons_deco` | Art Deco Pylons | 1.49 | permanent | `pylons_deco` | no |
| 12 | `blades_candy` | Candy Stripe Turbines | 1.49 | permanent | `blades_candy` | no |
| 13 | `skyline_neon` | Neon Skyline Windows | 1.49 | permanent | `skyline_neon` (window light colour set) | no |
| 14 | `room_retro` | Retro Control Room | 1.49 | permanent | `room_retro` (UI accent theme: HUD + sheets; kit palette override only) | no |
| 15 | `cosmetics_all` | Style Collection | 6.99 | permanent | #9-#14 | no |
| 16 | `grid_pass` | Utility Pass | 6.99 | permanent | `noads`, `slots`, `skyline_neon` | **yes** |
| 17 | `bundle` | Complete Grid Keeper Collection | 21.99 | permanent | every permanent entitlement above | **yes** |
| 18 | `starter` | Starter Kit (one time) | 2.99 | permanent | `forecast_plus`, `livery_sunset` | no |
| 19 | `cash_s` | Small Budget | 0.99 | consumable | 1 day of current income | no |
| 20 | `cash_m` | Budget Boost | 2.49 | consumable | 2.5 days | no |
| 21 | `cash_l` | Big Budget | 4.99 | consumable | 5 days | no |
| 22 | `cash_xl` | City Fund | 8.99 | consumable | 10 days | no |
| 23 | `comeback` | Welcome Back Fund | 0.99 | consumable (offer) | 1 day; shown after 3+ days away, ≤ 2 per game | no |
| 24 | `storm_kit` | Storm Recovery Fund | 1.99 | consumable (offer) | 3 days; shown at a shift report after a 1★ storm shift, once per chapter | no |

- **`ad_free` list for the CLAUDE §8 table (P4 adds the row):** `noads`, `premium` (Chief Engineer Edition), `grid_pass` (Utility Pass),
  `bundle` (Complete Grid Keeper Collection). Their descriptions say "ad-free" in all 10 languages. Starter, cosmetics, conveniences and
  every consumable **do not** remove ads, and no text says "any purchase removes ads". In code one right: `Ads.ad_free_check = func(): return Iap.ad_free()`;
  `purchased_any` is never used for an ad decision. No `legacy_ad_free` needed (new game).
- "1 day of income" = 6.7 × the net income of the last completed shift without bonuses (Grid Boss `effect.basis` pattern), floor
  $1,000; consumables are delivered once per transaction id after store confirmation, never restored (store text says so), ≤ 3 per UTC day.
- Every cosmetic is visible on the default model on Medium **and** has its canonical look on Low; `test_every_sold_look_is_visible`
  model (LL D6); a sold look never looks worse than the default.
- Pay-to-win check: `dispatcher` only automates what the player can do by hand (balance bot with and without it finishes chapter 5
  within ±10 %); `speed` changes time only; no product sells reputation stars or skips a district gate.

## 2. Offers

| Offer | Rule |
|---|---|
| `starter` | day 3-7 of play, once per game, never in the tutorial or the first session |
| `bundle` card | from day 20, ≤ 2 times per game |
| `comeback` | first session after ≥ 3 days away, ≤ 2 per game |
| `storm_kit` | at the report after a 1★ storm shift, once per chapter |
| General | ≤ 1 offer per session; the same offer does not return for ≥ 3 days; never during a live shift; tapping a locked paid feature opens the shop on that product; owned = "Owned" |

Restore purchases in the Shop **and** Settings.

## 3. Rewarded ads (AdMob only, opt-in) — 4 placements

Caps from CLAUDE §4: one reward ≤ 1/5 of `cash_s` (≤ 0.2 day of income); all rewards of a UTC day ≤ 1/2 of `cash_s`. Each placement has
a persistent daily cap + cooldown in the save (restart does not reset). Shown only when an ad is loaded and the cap allows; reward only
on the SDK "earned" callback. `ad_free` owners get the same rewards **without video**, same caps (`rewarded_skipped_paid_user`).

| Placement | Reward | Cap / cooldown | Value check |
|---|---|---|---|
| `offline_x2` | double the offline summary | 1 / day | offline at cap = 0.7 shift ≈ 0.10 day → +0.10 day ✔ |
| `shift_bonus` | +50 % of the shift's profit at the report | 3 / day, 10 min | 0.5 × 0.15 day = 0.075 day each ✔ |
| `emergency_import` | 60 s of free imported power from the neighbouring grid during a deficit (no cash) | 2 / day, 15 min | non-cash "last chance", never during the tutorial |
| `crew_overtime` | instant repair of one fault (no cash) | 3 / day, 5 min | non-cash |

Daily cash sum: 0.10 + 3 × 0.075 = **0.325 day ≤ 0.5 ✔**. Never rewarded: anything sold (cosmetics, speed4, dispatcher, forecast_plus,
offline_plus, slots, scenarios, ad-free). The game is never made harder to sell the ad. Remote kill switch `rewardedEnabled`.
Events: `rewarded_offer_shown`, `rewarded_offer_accepted`, `rewarded_completed`, `rewarded_skipped_paid_user` (`placement`,
`reward_amount`, `daily_index`).

## 4. House ads + RevenueCat + services (P4, parity with Corrupt State in `docs/MONETIZATION_PARITY.md`)

- Interstitial + banner = **house only** (`CoddyAds.gd` + `CoddyInterstitial.gd` + `CoddyBanner.gd`, synced by `games/tools/ads/sync.sh`); no
  AdMob interstitial/banner units are created; empty pool = no ad. Pacing INSTALL_GRACE_S / SESSION_GRACE_S / MIN_GAP_S unchanged; deferred
  after a rewarded view. Natural break = shift report only.
- House relevance: `games/tools/ads/house_related.py` gets a `volt-city` entry (families: tycoon, infrastructure/utilities, rescue →
  nearest: disaster-command, sky-tower-ops, canal-control, train-baron, airport-baron) → regenerate `CoddyHouseRelated.gd` → `sync.sh` to all games.
- `MobileAds.initialize` only in the Ads autoload; nothing loads before it completes (`check_admob_init.py`). UMP consent before any ad;
  Settings privacy block.
- **RevenueCat:** one project "Grid Keeper" (Patron, `rc-setup --template`), Android app `com.coddykit.voltcity`, **entitlements** = the grant
  ids in the table (`noads`, `theme_gold`, `speed4`, `dispatcher`, `forecast_plus`, `offline_plus`, `slots`, `scenarios`, `livery_sunset`,
  `livery_arctic`, `pylons_deco`, `blades_candy`, `skyline_neon`, `room_retro`) — 14 entitlements; offering `default` with all 24
  packages; consumables are products without an entitlement. RC ad tracking mandatory (AdMob `paid` → revenue, impression → displayed,
  click → opened, loaded, failed_to_load; house ads not sent; kill switch `rcAdTracking`). "Check credentials" after the first AAB is on Play.
- Firebase: project `coddykit-game-2`, own Android stream (Patron opens), bridge AARs present (`services_preflight --production`).
- ReviewGate "wow" moment (games/CLAUDE.md section 12, owner 2026-10-27 — first-session rule): asked in the **FIRST session** after
  ~4 min of real play (`MIN_SESSIONS = 1`, `MIN_PLAY_S = 240`) at a success moment. The sim emits `review_moment` once per career
  (SIM-API section 4): `first_district_restored` (the first repaired fault — Riverside lighting back up in the guide's shift) and
  `first_shift_completed` (shift 1 ends at ~240 s at 1x; `ready` = play time >= 240 s and not in the guide). Later fallbacks: the
  first 3-star shift with a storm / peak event, the first new district lighting up. Never during the guide, a modal, the shop,
  a purchase or an ad, never in the background; <= 3 attempts, >= 30 days apart; Play In-App Review only (OS sheet), no rating
  gate card, nothing given for a rating.
