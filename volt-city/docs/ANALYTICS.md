# Volt City — analytics event plan (P4)

Firebase comes later (project `coddykit-game-2`, own Android stream — **Patron** opens it; a `CoddyKit.Firebase` bridge
registers `CoddyServices.Analytics` at `BeforeSceneLoad`). Today the layer is complete and the sink is the kit's
`NullAnalytics` — **nothing leaves the phone**. Code: `unity/Assets/Scripts/Game/VoltAnalytics.cs` (one catalogue; this
file is its human copy — `P4Tests.Analytics_*` fails when an event is missing here or in the code).

- `VoltAnalytics.Install()` wraps whatever `CoddyServices.Analytics` is **before** the kit boots (VoltBoot / VoltCityMain),
  so kit events (rewarded, review, GPU breadcrumb) and game events pass the same validator.
- Firebase limits enforced in code: event / param names `^[A-Za-z][A-Za-z0-9_]{0,39}$`, no reserved names
  (`session_start`, `first_open`, `ad_impression`, … and the `firebase_` / `google_` / `ga_` prefixes), ≤ 25 params,
  string values ≤ 100 chars, values long / double / bool / string. An invalid or unknown event is **dropped** and counted
  (`Rejected`), never thrown.
- No PII: product / placement / district ids and small numbers only; profit as a band (`neg`, `lt1k`, `lt10k`, `lt100k`,
  `ge100k`), never raw cash; no device ids (house-ad telemetry is the kit's own `ads-track`, not Firebase).

| Event | Params | Fired by |
|---|---|---|
| `vc_session` | `n` (session count), `tier` (low / medium / high) | VoltCityMain start |
| `onboarding_step` | `step` | Coach (P3) |
| `onboarding_assist` | `target` | Coach balance assist |
| `onboarding_skip` | `step` | Coach Skip |
| `onboarding_complete` | `wrong_taps` | Coach done |
| `shift_started` | `shift`, `speed` | sim `shift_start` |
| `shift_completed` | `shift`, `stars`, `uptime_pm`, `profit_band` | sim `shift_done` |
| `chapter_reached` | `chapter` | sim `chapter` |
| `district_hooked` | `district` | sim `hooked_up` |
| `investment_bought` | `id` | sim `bought` |
| `save_unreadable` | `code`, `recovered` | GameSession (games/CLAUDE.md §14) |
| `save_failed` | `code` | GameSession |
| `gpu_shader_crash` | `stage`, `gpu` | kit QualityTiers breadcrumb |
| `rewarded_offer_shown` | `placement`, `reward_amount`, `daily_index` | kit RewardedController (once per placement per session) |
| `rewarded_offer_accepted` | same | kit |
| `rewarded_completed` | same | kit (after earned + closed, or at once for ad_free) |
| `rewarded_skipped_paid_user` | same | kit (ad_free: reward without video) |
| `shop_open` | `source` (`tab` or the product it opened on) | VoltCityMain |
| `purchase_start` | `product` | Commerce.Buy |
| `purchase_success` | `product`, `kind` | Commerce |
| `purchase_cancel` | `product` | Commerce |
| `purchase_fail` | `product`, `reason` (≤ 40 chars) | Commerce |
| `restore_result` | `ok`, `active` (entitlement count) | Commerce.Restore |
| `offer_shown` | `offer` | Commerce.TryOffer |
| `look_equip` | `look`, `on` | Commerce.ToggleLook |
| `review_prompt` | `moment`, `attempt` | kit CoddyReview (Play In-App Review requested) |
| `settings_change` | `setting` (music / sfx / auto_collect), `value` | Settings |
| `city_slot` | `slot`, `fresh` | Settings → Cities |

Funnels this supports: guide completion (onboarding_*), first-session retention (vc_session n=2), shift quality (stars /
uptime), shop conversion (shop_open → purchase_start → success / cancel / fail), rewarded fill and acceptance per placement,
offer conversion (offer_shown → purchase_success of the same product).
