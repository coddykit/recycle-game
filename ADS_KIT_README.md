# games/tools/ads — CoddyAds template for the Godot games

`CoddyAds.gd` is the template every Godot game copies (autoload `Ads`, after the store). Rules: `games/CLAUDE.md`
§3 (ad-free scope), §4 (rewarded), §5 (AdMob), §5b (RevenueCat ad tracking). Poing Studios AdMob plugin:
`sh install_poing_admob.sh games/<game>`.

- **HOUSE ONLY (owner 2026-10-25, Emir #125):** interstitial + banner show only our other games; there is NO AdMob
  interstitial / banner (no loader, no AdView, MobileAds starts only for a rewarded unit). Empty pool / spent budget /
  images not cached yet = no ad. AdMob = rewarded only. Kit tests assert it (`test_rc_ads`: source has no
  InterstitialAdLoader / AdView, fake AdMob request count 0).
- **Interstitials:** house only (our other games from `ads-config`, counted on-device). The card is
  `CoddyInterstitial.gd` (copy next to CoddyAds.gd): hero frames (`heroImageUrls` ≤ 3, else `interstitialImageUrl`;
  ≤ 512 px, disk cache, aspect kept), cross-fade + slow pan on Medium+, one still frame on Low (`Ads.low_end`), icon +
  title + `sloganI18n` + translated Install (utm referrer) + 3 s countdown close. Tests `tests/test_interstitial.gd`,
  frames `interstitial_capture.gd`. Pacing: install
  grace, session grace, `MIN_GAP_S`, and `quiet_until` (set after a completed rewarded video).
  `Ads.ad_free_check` = the `ad_free` right only (Remove Ads / big packs / legacy buyers — never "bought anything").
- **Rewarded videos (W45, 2026-10-20):** opt-in only. `rewarded_limits[placement] = {daily, cooldown_s}`,
  `rewarded_budget = {single, daily}` in reward units (single ≤ 1/5, daily ≤ 1/2 of the cheapest consumable pack),
  `analytics_event` → Firebase. `rewarded_ready(placement, wanted)` for the button, `show_rewarded(placement, wanted,
  on_earned(units), on_closed)`: the reward only after earned AND closed, capped, counted per UTC day in
  `user://coddyads/rewarded.json` (survives a restart). `ad_free` players: same capped reward without a video, not affected
  by ads-config `rewardedEnabled: false` (which hides everyone else's offers). Events `rewarded_offer_shown /
  _offer_accepted / _completed / _skipped_paid_user` (`placement`, `reward_amount`, `daily_index`). Nothing is loaded
  before `MobileAds.initialize` completed. Units: `[coddy_ads] admob_rewarded_android/_ios` (debug = Google test unit).
- **Bottom banner (owner 2026-10-23, games/CLAUDE.md §2):** copy `CoddyBanner.gd` next to `CoddyAds.gd`; CoddyAds creates it
  (`Ads.banner`). House only (no AdMob banner since 2026-10-25): our house banner from ads-config `houseAds.banner`
  (icon + title + subtitle + Install, rotates every 45 s; **two games side by side in landscape / ≥ 600 dp wide**, each tile its
  own tap target, impression `slot`, click and perAppBudget — Emir #125; impression/click tracked as `format: banner, level 3`,
  never promotes the game itself). **Standard (owner 2026-10-26): every non-ad-free player sees it as soon as the
  first-run onboarding is over — no play-time wait (the 3-minute rule is gone).** Hidden for `ad_free` (at the next 0.5 s
  check after the right arrives), kids games (`[coddy_ads] kids = true`), boot / splash / loading screens, and whenever the
  game's `Ads.banner_allowed` returns false (onboarding pager + guide / tutorial, purchase & payment dialogs, modals).
  **Size (S2 10-24):** the house bar is AdMob-sized — ~58 dp (dpi → physical px → canvas px), never > 12 % of the
  canvas height, Install ≥ 44 dp when that cap allows; never assume a fixed height (`banner.house_height()` / the signal).
  The game lifts its UI: connect `Ads.banner_height_changed(px)` and use `max(bottom safe inset, px)` as the content
  bottom inset (pilot: kaiten-sushi / canal-control `_bind_banner` + `_layout_safe_area`). Tests: `tests/test_banner.gd`.
  **Reserved slot (#165, 2026-10-28):** `CoddyBanner.reserve_slot` is ON by default — on a phone a NEW bar waits for a calm
  moment (no finger down, 3 s without input; taps within 1.5 s of it appearing are swallowed), and while it waits the kit
  already reports its height, so the game's UI is lifted at once and nothing jumps when the bar fills (also right after a
  window / shop / pager closes). A game may set `Ads.banner.reserve_slot = false` (bridged: `banner_host.banner`).
  **Side safe area (#165):** in landscape the phone's 3-button navigation bar sits at the SIDE — the tiles / Install keep
  `side_gaps()` (coddy_ui `UiSafeArea.edges` left / right) free, the background still runs to the edge. Desktop frames:
  with `UiSafeArea.simulate()` / `CODDY_SAFE_SIM` the bar keeps off the simulated bars (bottom and sides) like on that phone.
  **v3 (Patron #110):** the subtitle is ONLY the creative's `sloganI18n[game language]` (≤ 34 chars, ja/ko/zh ≤ 17, no
  free/trial/store, no "…") — otherwise the title stands alone, vertically centred; `subtitleI18n` is not used by the
  banner. Install label = the game's own `Install` translation, else the built-in table (en de tr fr es pt ru ja ko zh hi).
  Language = `Ads.ui_lang()` (the same source as `_pick_i18n`). Text the game's bundled SUBSET font cannot draw is never
  shown as tofu: title → English, slogan → none (see the recipe, step 6).
- **Relatedness rotation (owner 2026-10-26):** banner AND interstitial show our other games **most related first**, one
  step further on every banner rotation / launch (persisted `user://coddy_banner_index.txt`, fresh install = the most
  related) and every interstitial shown (persisted `count`). `CoddyHouseRelated.gd` (copy next to CoddyBanner.gd /
  CoddyAds.gd) is the one data file: per host an ordered list, GENERATED by `house_related.py` from the games' Play
  listing tags (edit tags there → `python3 games/tools/ads/house_related.py` → sync). `apply(config, host)` pools the
  backend's banner + interstitial lists, drops the host + kids games, orders by the table and keeps them ALL (TOP_N = 0
  since 2026-10-28: every one of our games in every other game; ids missing from an older table rank last, never cut)
  — in place on `_config`, so a bridged
  game's own interstitial layer follows the same order without code changes. Installed games are NOT skipped (Godot
  cannot query installed packages). The interstitial COUNT stays capped: perAppBudget × min(sequence, 10) per cycle
  (`CoddyAds.HOUSE_CAP_GAMES`; merged by hand into every game's interstitial layer), so a 20-game list does not double
  the per-host interstitials. The banner just rotates through the longer list (perAppBudget per game, unchanged).
  Tests: `tests/test_related.gd`, `python3 -m unittest tests/test_house_related.py`.
- **Sync:** `bash games/tools/ads/sync.sh <game>…` copies CoddyInterstitial / CoddyBanner / CoddyBannerHost where the
  game has them and puts CoddyHouseRelated.gd next to every CoddyBanner / CoddyAds copy (`--check` = equality gate).
  CoddyAds.gd itself is merged by hand (games keep their debug test-unit rule).

## Banner yayılım tarifi (Patron #110 — pilot kaiten-sushi + canal-control)

Banner'ı alacak oyunlar: kids HARİÇ (block-forge = `games/lego`, cinnamon-cappadocia — `[coddy_ads] kids=true`, ASLA).
CoddyAds kopyası olanlar (`src/services/CoddyAds.gd`): bureaucracy-office, chaiwala-rush, disaster-command, doner-night,
lifeguard-hq, pest-patrol, pharmacy-rush, sky-tower-ops (+ pilotlar). Kendi reklam katmanı olanlar (önce CoddyAds'e geçiş
ya da banner'ı kendi `Ads` autoload'una bağlama işi): aircraft (parts-rush), airport-baron, corrupt-tycoon, moneytycoon,
skiresort, smuggler, trading-sim, train-baron, zombi (last-haven).

1. **CoddyBanner.gd** — `cp games/tools/ads/CoddyBanner.gd games/<g>/src/services/` (değiştirmeden; oyun kopyası = kit).
2. **CoddyAds.gd kit ile senkron** — 8 oyunun kopyası kitten 4 noktada farklı (tarih 2026-10-24): debug'da HER ZAMAN Google
   test birimi (`if OS.is_debug_build():` ×2, kit: yalnız birim boşsa), başlık yorumu, timer yorumu. Bunlar oyunun
   bilinçli yerel farkı — koru. Birleştirme: `git merge-file <g>/src/services/CoddyAds.gd <base> games/tools/ads/CoddyAds.gd`
   (`<base>` = oyunun kopyaladığı kit sürümü: `git show <o commit>:games/tools/ads/CoddyAds.gd > base.gd`; commit'i
   `git log --format=%h -1 -- games/<g>/src/services/CoddyAds.gd` ile bul ve o tarihteki kit commit'ini seç), çakışma
   çıkarsa yerel debug kuralını tut. Sonra `diff` ile kalan farkın yalnız bu 4 nokta olduğunu doğrula. Kit tarafındaki
   banner parçaları: `banner_height_changed`, `banner_allowed`, `banner`, `banner_height`, `_ready`'de CoddyBanner
   yaratma, `ui_lang()` (10-25: AdMob banner/interstitial parçaları kaldırıldı; `CoddyInterstitial.gd` de kopyalanır).
3. **main.gd kapısı** — `_bind_banner()` (pilot: kaiten `src/game/main.gd` `_bind_banner`/`set_banner_inset`):
   `Ads.banner_allowed = func(): return modal == null and onboarding/pager kapalı and mağaza çekmecesi/satın alma
   penceresi kapalı`. Oyunun her tam ekran modal'ı, onboarding adımı, IAP/ödeme ekranı bu kapıda olmalı.
4. **Alt güvenli alan** — `Ads.banner_height_changed.connect(set_banner_inset)`; içerik alt boşluğu
   `max(safe-area alt, banner_inset)` (kit/UiKit alt inset). İlk değer: `set_banner_inset(Ads.banner_height)`.
5. **Testler** — `tests/test_shared_banner.gd` = `tests/test_banner.gd`'nin `res://src/services/…` yollu kopyası
   (`sed 's#res://CoddyAds.gd#res://src/services/CoddyAds.gd#; s#res://CoddyBanner.gd#res://src/services/CoddyBanner.gd#g'`
   + `use("kaiten-sushi")` → oyunun paleti); `tests/services_isolated.gd` (ya da oyunun servis koşucusu) listesine ekle.
6. **Font alt kümesi** — `python3 games/tools/ads/house_ad_text.py --install <g>` (`data/house_ad_text.json`: 20 oyunun
   başlık + slogan metni), sonra oyunda `python3 tools/i18n/subset_fonts.py /Volumes/BuildSSD/game-deps/font-src`
   (fontTools; kaiten'in TC kaynağı sources.json URL'sinden, sha doğrulanır). Yoksa ja/ko/zh/hi başlık İngilizceye
   düşer, slogan çıkmaz (tofu asla). Yeni oyun / slogan eklenince yeniden çalıştır.
7. **Doğrulama** — `sh tools/check.sh` yeşil + `ADMOB_SRC=<g>/addons/admob sh games/tools/ads/tests/run_tests.sh`;
   kare: `BANSHOT_CFG=<ads-config.json> BANSHOT_SLOGANS=games/tools/ads/proposals/banner_slogans_*.json godot --path <g>
   … -s games/tools/ads/banner_capture.gd -- <out> <kaiten|canal> 540 960 play [lang]` (banner_capture pilot'a özgü
   Main kurulumu içerir; yeni oyun için `_run`'daki sahne/ayar kısmını uyarla). Pencereli çağrılar shot-lock altında.
8. **Kids oyunu** — hiçbir adım uygulanmaz; `[coddy_ads] kids=true` banner'ı zaten kapatır ama kopyalama da yapma.

## Kendi reklam katmanı olan oyunlar — `CoddyBannerHost.gd` (Emir #112)

Oyunun reklam katmanı kitten eski bir CoddyAds kopyası ya da tamamen kendi kodu ise (banner'sız) **CoddyAds'i birleştirme**:
interstitial / rewarded / save / IAP / ekonomi koduna dokunmadan `CoddyBanner.gd` + `CoddyBannerHost.gd`'yi yan yana kopyala
(değiştirmeden; ör. `scripts/ads/`). Köprü, CoddyBanner'ın `ads` nesnesinden okuduğu her şeyi (`_config`, `_state.play_s`,
`_ad_free`, `_mobile`, `_platform`, `_app_id`, `_store_url`, `_open_store`, `_track`, `_texture_from`, `_pick_i18n`/`ui_lang`,
`banner_allowed`) oyunun kendi katmanına (`host.source`) yönlendirir; kaynakta yoksa kendisi sağlar (ads-config'i aynı uç +
`X-App-ID` ile kendisi çeker). **AdMob banner dalı kapalı** (`_admob_inited` hep false): bu oyunlarda banner birimi yok →
yalnız house. Kaynağın `_showing`'i (interstitial/rewarded ekranda) true iken banner gizli.

```gdscript
const BannerHost := preload("res://scripts/ads/CoddyBannerHost.gd")
var banner_host = BannerHost.new()
banner_host.source = get_node_or_null("/root/Ads")       # oyunun reklam katmanı (yoksa null: köprü config'i kendi çeker)
banner_host.gate = banner_ok                              # false: modal / onboarding / pager / mağaza / ödeme / tutorial
# isteğe bağlı: banner_host.ad_free = func(): return Store.ad_free()   (vars. source._ad_free())
#               banner_host.play_s  = func(): return <toplam oyun sn>  (vars. source._state.play_s)
#               banner_host.lang    = func(): return I18n.lang         (vars. TranslationServer)
banner_host.banner_height_changed.connect(set_banner_inset)   # alt boşluk = safe area + banner
add_child(banner_host)
```

Oyun başına adımlar (uygulananlar: train-baron `scripts/ui/hud.gd`, airport-baron `scripts/Main.gd`, corrupt-tycoon
`src/ui/UI.gd` — hepsinde `_attach_banner` / `banner_ok` / `set_banner_inset`):
1. Kopya: `cp games/tools/ads/CoddyBanner.gd games/tools/ads/CoddyBannerHost.gd games/<g>/<scripts>/ads/`.
2. Köprüyü kök UI'ya (HUD/main) ekle; `gate` = oyunun tüm modal/onboarding/pager/mağaza/ödeme durumları (mevcut
   `modal_open()` benzeri fonksiyon varsa onu kullan). Ad-free = oyunun Remove Ads/büyük paket bayrağı (çoğunda zaten
   `Ads.ad_free_check` → `source._ad_free()` yeter).
3. Alt boşluk: oyunun alt sekme/menü kabı banner yüksekliği kadar yukarı (ör. alt margin = safe-area + `banner_inset`),
   sonra relayout. Sağ/sol kenar kolonları (zoom vb.) üst bar ile banner arasına sığmalı (train-baron `_fit_zoom_above_banner`).
4. `python3 games/tools/ads/house_ad_text.py --install <g>` → `data/house_ad_text.json`; CJK subset varsa yeniden üret
   (`games/tools/fonts/subset_cjk.py --game games/<g> --src games/airport-baron/store/deck/public/fonts/store --out <fonts>
   --metrics-from <ana font>`; fontTools: `/Volumes/BuildSSD/tmp/c2-canal-venv/bin/python`). subset `data/**/*.json`'u okur.
   Kendi font aracı olan oyun (corrupt-tycoon `tools/make_fonts.py`) house metnini de okuyacak şekilde genişletilir.
   Kit teması değişken fontu ince varsayılan ağırlıkta çiziyorsa (airport-baron) `banner_host.font = <oyunun FontVariation'ı>`.
5. Test (`tests/test_banner.gd`): banner görünür + alt sekmenin alt kenarı ≤ banner üstü; her modal/mağaza/onboarding'de gizli
   ve inset 0; ad_free oyuncuda yok. Sonra oyunun `tools/check.sh` (ya da test komutu) + perf gate yeşil.
6. Kare: `games/tools/ads/banner_capture.gd`'yi oyunun Main kurulumuna uyarla (train-baron `tests/tool_banner_shots.gd`)
   → `/Volumes/BuildSSD/game-builds/banner/rollout/<g>-{after,modal}.png`, pencereli çağrı shot-lock altında.

Kalan 6 oyun (2026-10-24 ölçümü, origin/main):

| oyun | reklam katmanı (source) | not / beklenen zorluk |
|---|---|---|
| aircraft (parts-rush) | `scripts/autoload/Ads.gd` (eski CoddyAds, tüm iç alanlar var) | dikey 720×1280, `scripts/ui/hud.gd`; kolay-orta |
| moneytycoon | `scripts/services/coddy_ads.gd` (autoload değil, bir düğümün çocuğu → `source` = o düğüm) | dikey; HUD büyük, alt sekme + şehir kamerası; orta |
| skiresort | autoload `Ads` = `ads_manager.gd` (iç alan YOK) → `source` = `/root/CoddyAds` (`scripts/core/coddy_ads.gd`, tam) | `ad_free` = `Perk.ad_free()`; tutorial `Game.tutorial.active` kapıda; orta |
| smuggler | `scripts/monetization/coddy_ads.gd` (autoload `Ads`, tam) | dikey, `scripts/ui/hud.gd`, `tools/check.sh`; kolay-orta |
| trading-sim | `src/game/ads.gd` (autoload `Ads`, tam) | dikey, `src/ui/main.gd` + `hud.gd`, `tools/check.sh`; kolay-orta |
| zombi (last-haven) | `Ads` = yalnız rewarded (`scripts/business/ads.gd`, iç alan YOK); house interstitial `scripts/business/interstitials.gd` (`_config`/`_state`/`_showing`/`_store_url`/`_open_store` var, `_track`/`_ad_free` YOK) | `source` = interstitials düğümü, `ad_free` callable ŞART (VIP), banner tıklama telemetrisi gitmez (köprü `_track` yoksa sessiz); en zor |

Köprü testleri: `games/tools/ads/tests/test_banner_host.gd` (kit koşucusu: `sh games/tools/ads/tests/run_tests.sh`).

## RevenueCat ad tracking (W51d)

Copy `games/tools/godotx_revenue_cat_src/gd/RcAdTracking.gd` **unchanged** into the game's scripts alongside the template
(Godot registers its `RcAdTracking` class). The helper is required; the native RC plugin is optional at runtime:
missing / pre-W36 plugin = silent no-op, ads and rewards continue. For release, use the shared W36-or-later `.aar` and
RevenueCat runtime **10.22.1** (plugin export defaults and any export-preset override must agree). See the
[bridge README](../godotx_revenue_cat_src/README.md) and [tracking design](../REVENUECAT_AD_TRACKING.md).

Both interstitial and rewarded loaders create a context per load and bind the helper before show. `loaded` comes from
the load callback; `displayed` only from `on_ad_impression`; `opened` only from `on_ad_clicked`; `revenue` only from
`on_ad_paid` with the callback's micros, currency and precision unchanged. The placement is attached when the game
selects the ad. Load failures carry the requested unit and SDK error code. No paid callback means **no revenue event**:
record that limitation in the game's release report. House promotions and `ad_free` video-less rewards send nothing
to RC. Do not manually send Firebase `ad_impression`.

`ads-config.rcAdTracking` defaults to true and is applied from both cached and live config, independently of `adsEnabled`
and `rewardedEnabled`. Disabling it also drops queued startup events. The helper deduplicates callbacks and holds a
bounded queue while RC configures; the template flushes on loads, enabled config updates and every 30 seconds of
unpaused mobile play. IAP continues to own RC configuration and identity: call `Ads.rc_ads.set_user(id)` **before**
switching RC users (drops the old user's queue), then `Ads.rc_ads.flush()` after RC is configured. This template never
initializes RevenueCat or changes Billing. Games with custom Billing must follow §5b's observer/device-test gate.

## Yeni oyun — yayın kontrol listesi

- [ ] **RC reklam takibi bağlı mı?** Her AdMob türünde `loaded / displayed / opened / failed_to_load`, SDK paid
  destekliyorsa `revenue`; ortak helper + uyumlu RC eklentisi/runtime kurulmuş, mevcut IAP kimliği kullanılıyor.
- [ ] `rcAdTracking: false` önbellek/canlı ayarda olayları durduruyor; house ve videosuz ödüller RC'ye gitmiyor;
  Firebase `ad_impression` elle gönderilmiyor. Paid desteği yoksa yayın raporunda açıkça yazıyor.
- [ ] Headless şablon/oyun testleri yeşil; Patron'un yayın öncesi cihaz/smoke kontrolünde gerçek callback zinciri ve
  RC paneli doğrulanmış. Kendi Billing'ini kullanan oyunda observer bayrağı cihaz testi geçmeden açılmıyor.

## Init lint

`python3 games/tools/ads/check_admob_init.py [games/ | games/<oyun>]` — one `MobileAds.initialize` call site per game and
every ad load gated on the init callback (directly or through its callers). Run by `games/jenkins/run.sh <game> test`.

## Tests

Tests: `sh games/tools/ads/tests/run_tests.sh` (Godot 4.7.2; copies the template + a game's Poing addon into a temporary
project with the canonical RC helper, `ADMOB_SRC` to override; exit 0 = pass). Uses fake ads and a fake RC plugin:
no device, native SDK initialization, live ads or network calls. Covers the W45 reward rules plus both RC callback
chains, placement/units, deduplication, missing/old plugin, missing paid events, config kill switch, delayed RC startup
and exclusions. Also run `sh games/tools/godotx_revenue_cat_src/gd/run_tests.sh` for shared helper regressions.
