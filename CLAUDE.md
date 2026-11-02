> **New game from zero? Read [NEW_GAME_PLAYBOOK.md](NEW_GAME_PLAYBOOK.md) first.**
> **Operasyon (gece toplu yayın, yükleme araçları, onay akışı, "kalan işlere devam" listesi): [ops/README.md](ops/README.md).**

# Oyunlar — filo kuralları (games/ altındaki HER oyun)

Bu kurallar sahibin verdiği kararlardır; her oyunda aynen uygulanır, **tekrar sorulmaz**. Bir kural bir oyunun koduyla
çelişiyorsa kural kazanır (kodu kurala getir, raporla). Yeni sahip kararı gelince önce BU dosyayı güncelle.
Ayrıntılı gerekçe: `games/tools/REWARDED_ADS_STRATEGY.md` (W30). Son güncelleme: 2026-10-20.

## 0. TECRÜBE KURALI — bir oyunda öğrenilen her ders TÜM oyunlara uygulanır (sahip 10-22)
- Tek kaynak: **`games/LESSONS_LEARNED.md`** (performans, kayıt, UI/dil, servisler, süreç). Yeni oyun yapmadan ve bir oyuna dokunmadan önce oku.
- Bir hata, kötü yorum ya da Vitals kümesi gelince şu sırayla ilerle:
  1. O oyunda düzelt.
  2. Aynı kalıbı bütün `games/*` içinde ara ve düzelt.
  3. Test ya da kapı ekle.
  4. `LESSONS_LEARNED.md`'ye bir satır yaz.
- **Performans kontrolü her oyunda zorunlu.** Düşük segment Android (Hindistan vb.) takılma ve çökmeye 1 yıldızla cevap veriyor. Her sürümden önce Düşük profilde ölçüm yapılır:
  - primitive ≤300k, draw ≤150, statik bellek ≤250 MB (perf_gate, Godot MEMORY_STATIC); PSS ≤300 MB yalnız GERÇEK cihazda ölçülür (emülatör SwiftShader GPU tamponlarını süreç belleğine sayar → emülatör PSS hedef değil, yalnız A/B karşılaştırması);
  - 30 FPS, p95 ≤50 ms, takılma yok.
  Hedefi geçen YENİ oyun production'a çıkmaz. CANLI oyunda, bütün ölçülerde (primitive, draw, PSS, FPS) canlı sürümden iyi olan sürüm çıkabilir; eşikler hedef olarak kalır ve bir sonraki sürümün işi olur (Patron 10-22, Corrupt 1.1.7: PSS 354→333 MB, draw 329→142).

## 1. Genel
- Mağaza: **yalnız Google Play**. iOS işi açma, App Store'a oyun gönderme (sahip 10-18).
- **Trial / ücretsiz deneme yok** — paywall "try free" demez, mağaza metni vaat etmez.
- Gelir = **geniş IAP kataloğu** (reklamsız, premium/tam paket, kozmetik, kolaylık, consumable; keyfi bozmadan) + reklam.
- Godot logosu/splash'i yok (her oyunun kendi splash'i). Telefonda varsayılan grafik (sahip 10-23): amiral gemisi YÜKSEK, güçlü ORTA, zayıf DÜŞÜK (§10).
- Android derlemesi Jenkins Linux ajanında (`android-linux`, build-only → AAB). Mac'te Android derleme yok.
- Production öncesi Mini emülatöründe smoke (asla paralel, biten emülatör kapanır, Mini sessiz). Smoke'suz production yok.
- Mağazaya yükleme / sürüm yayını yalnız Patron (sahip onayıyla). Diğer oturumlar kod + test + commit/push yapar.

## 2. Reklam türleri
- **🔴 YALNIZ HOUSE (sahip 10-25, Emir #125: "şık interstitialler yapıp AdMob yerine kendi oyunlarımızın reklamlarını birbirlerine yapabiliriz, sadece ödüllü reklam için AdMob"):** interstitial + banner = **yalnız kendi oyunlarımız**. AdMob interstitial / banner **YEDEĞİ YOK** — birim yüklenmez, init'te istenmez, AdView / InterstitialAdLoader kodu yok; house havuzu boş / bütçe bitti / görsel henüz önbellekte değilse **o an reklam yok**. **AdMob = yalnız ödüllü** (MobileAds yalnız ödüllü birimi varsa başlar; `[coddy_ads] admob_interstitial_*` / `admob_banner_*` anahtarları yok sayılır). Kendi reklam katmanı olan (köprülü) oyunlarda AdMob interstitial yolu kapatılır, house kartına yönlendirilir; ödüllüye dokunulmaz.
- **Interstitial:** CoddyAds (yalnız house) — `games/tools/ads/CoddyAds.gd` + **`CoddyInterstitial.gd`** (şık kart) kopyası. Kart: tam ekran; oyunun hero görselleri (ads-config `heroImageUrls` ≤ 3, yoksa `interstitialImageUrl`; ≤ 512 px küçültülüp diskte önbellek, en/boy korunur — mağaza görselindeki yazı kırpılmaz) — Orta/Yüksek'te 2–3 kare yavaş çapraz geçiş + hafif pan, **Düşük'te tek statik kare, tween yok**; hero yoksa büyük ikon + oyunun rengi (`brandColor`, yoksa ikonun ortalama rengi); ikon + başlık (tek satır, küçülür → kısa ad, "…" yok) + oyuncunun dilinde tek satır slogan (`sloganI18n`, banner kurallarıyla; sığmazsa gizli) + büyük çevrili "Yükle" (Play sayfası, `referrer` = backend androidReferrer ya da `utm_source=<host>&utm_medium=house_interstitial&utm_campaign=coddyads`) + köşede **3 sn geri sayımlı kapat** (sonra X). "Ücretsiz / deneme" yok. Pacing aynen (INSTALL_GRACE_S / SESSION_GRACE_S / MIN_GAP_S, ödüllüden sonra erteleme). Oyun Düşük'ü bildirir: `Ads.low_end = func(): return <grafik kalitesi == Düşük>` (varsayılan: coddy_perf cihaz sınıfı).
- **Banner (sahip 10-23: "banner reklamı da ekle"):** alt kenarda house banner (ads-config `houseAds.banner`; **AdMob banner yok**, 10-25). Yükseklik ≈ 58 dp, kanvasın ≤ %12'si. **Yatayda ve genişliği ≥ 600 dp dikey/tablette YAN YANA 2 farklı oyun** (Emir #125): her karo = ikon + kısa başlık (tek satır) + kısa slogan (tek satır, sığmazsa gizli) + küçük çevrili "Yükle" hapı, araya ince ayraç; her karo ayrı dokunma hedefi (≥ 48 dp), ayrı gösterim (`slot` 0/1) / tıklama sayımı ve `perAppBudget` (oyun başına; bütçesi kalan oyun karo sayısından azsa yeni tur); iki karo asla aynı oyun, asla host; rotasyon kalıcı indeksle. Dar dikeyde tek oyun. **STANDART (sahip 10-26): Remove Ads / reklamsız paket almamış her oyuncu, ilk açılış onboarding'i bitince banner'ı en altta görür — oyun süresi beklemesi YOK (3 dk kuralı kalktı; house banner için politika gerektirmiyor).** ad_free (hak gelince hemen gizlenir), aile/çocuk oyunları, boot / splash / yükleme ekranı, onboarding / tutorial ve satın alma ekranlarında YOK; arayüz banner yüksekliği kadar yukarı kayar, oyun alanını ve alt sekmeleri kapatmaz; **yuva ayrılmış** (kit `reserve_slot` varsayılan AÇIK, #165 10-28): bar sakin anı beklerken yüksekliği zaten bildirilir → pencere kapanınca arayüz hemen kalkar, bar dolunca hiçbir şey zıplamaz. Yatayda 3 tuşlu gezinme çubuğu YANDA durur: karolar / Yükle onun dışında kalır (kit `side_gaps`). Şablon: CoddyAds #104 (S2).
- **Ödüllü (rewarded):** her oyunda var (sahip 10-20 "ödüllü reklam olacak hepsinde"), yalnız AdMob (house ödül vermez).
- **İlgili oyundan başlayan rotasyon (sahip 10-26: "en ilgili olanlardan başlasın, hep aynı oyun değil, diğer oyunlarımız arasında dönsün"):** banner VE interstitial host'a **en ilgili oyundan** başlar, her banner turunda / açılışta ve her gösterilen interstitial'da bir sonrakine geçer (imleç kalıcı); kendini ve kids oyunlarını asla göstermez. Tek veri dosyası `games/tools/ads/CoddyHouseRelated.gd` (üretici `house_related.py`, Play kategori + listeleme etiketleri: tycoon / ulaşım / yemek-servis / kurtarma vb. aileler); istemci backend'in banner + interstitial listelerini birleştirip bu tabloya göre sıralar, ilk 10'u döndürür. Yüklü oyunu atlamak mümkün değil (Godot paket sorgulayamaz). Yeni oyun: `house_related.py`'ye etiket ekle → üret → `sync.sh` ile tüm oyunlara kopyala.
- **Reklam stratejisi (sahip 10-23: "interstitial reklamlarında diğer oyunlarımızı göstereceğiz; ödüllü reklamdan bizim için önemli olan gelir almamız; interstitial ve banner reklamlarında tüm filodaki oyunlarımızın reklamını yapmamız lazım"):** **Interstitial ve banner = kendi oyunlarımızın tanıtımı (house).** ~~AdMob yalnız house gösterilemiyorsa yedektir~~ → **10-25'ten beri AdMob yedeği YOK** (yukarı bak): house gösterilemiyorsa reklam yok. **Ödüllü = yalnız AdMob, gelir için;** house ödül vermez ve ödüllü house'a asla bağlanmaz. Kids oyunları (block-forge, cinnamon) havuz dışıdır, ads-config onlara house vermez. Backend (Supabase `ads-config`): her oyun host'unda interstitial ve banner için 5 seviyenin hepsinde `ourAdShare=100`. **Sahip 10-28 — TÜM OYUNLAR her oyunda:** her host'un her formatında kendisi hariç **mağazada açık olan TÜM oyunlarımız** vardır (sabit 10 sınırı KALKTI; `--max N` yalnız egress acil freni), sıra = host'a ilgi (`games/tools/ads/house_related.py`, istemcinin `CoddyHouseRelated.gd`'siyle aynı; seviye 1–5 = sıra dilimi, seviye 1 en ilgili); interstitial ve banner AYNI liste; interstitial `perAppBudget=3`, banner `2`, `resetDays=7`; pacing (INSTALL_GRACE_S / SESSION_GRACE_S / MIN_GAP_S) aynen kalır. **Erişilebilirlik otomatik (oyun başına elle adım YOK):** günlük iş her oyunun herkese açık Play sayfasını GET eder — 10 sn arayla İKİ kez: iki kez 200 → `ads_app.can_be_promoted=true`, iki kez 404 → `false`, diğer (bir 200 + bir 404, ağ/5xx/429) → değişmez (10-29: ilk incelemesi süren skiresort'un sayfası 200/404 arası gidip geliyordu, tek şanslı 200 onu açmıştı — kural iki yönde simetrik, `PLAY_RECHECK_S`); ads-config yalnız `can_be_promoted` oyunları servis ettiği için sayfası 200 olmayan oyun her listeden düşer, 200 olunca kendiliğinden girer. Yalnız oyun satırları (`RING`) yazılır; kids (block-forge, cinnamon) asla. Havuz = `ads_app`'te kayıtlı (active, kids değil, mağaza linki + ikon + hero + en başlık) + Play sayfası 200. **Günlük iş:** Mac Mini launchd (`tools/ads/rotate_house_slots.py --apply`, her gün 05:00): önce Play sayfa kontrolü, hemen ardından listeler; değişiklik yoksa hiçbir şey yazmaz (yazım config sürümünü artırır → cihaz sayaçları sıfırlanır). Yeni oyun: `house_related.py`'de etiket + `RING` + `ads_app` kaydı — gerisi otomatik; script değişince `tools/ads/install_rotation_launchd.sh` yeniden çalıştırılır. **İstemci (10-28):** kit `TOP_N=0` (`CoddyHouseRelated.gd` VERSION 2, Travel `house-banner.js`, Unity `TopN`) — gelen listenin TAMAMI host'a ilgi sırasıyla tutulur (tabloda olmayan yeni oyun sona, kesilmez; host/kids asla); interstitial döngü toplamı `perAppBudget × min(liste, 10)` (`HOUSE_CAP_GAMES`, her oyunun reklam katmanına elle işlendi; 30/7 gün sınırı korunur), banner yalnız daha uzun listede döner. Bu kitle derlenmeyen eski sürümler hâlâ en ilgili 10'da keser. Doğrulama: `python3 tools/ads/verify_house_config.py [--show host,…]`.
- **Acente kartı — YALNIZ Travel Tycoon (plan-2027 MO-04, 2026-10-31):** gün sonu molasında, oturumda ≥10 dk oyundan sonra, oturum başına en çok 1 "gerçek Kapadokya" kartı → acente `/r/GAME-TT`; house pacing aynen, `ad_free`'ye hiç; uzak şalter acentenin `/api/public/house-cards` → `travelTycoon` (varsayılan kapalı; site public + TÜRSAB anahtarı + `flags.house_tt` + Kapadokya'da ürün). Metinde tur/transfer ailesi YOK (`banned_words_lint.py`). Başka oyuna eklenmez. Kod `games/travel-agency-game/src/real-cappadocia.js`.
- **Aile/çocuk oyunları (Cinnamon, BlockForge/lego):** AdMob YALNIZ opt-in ödüllü; **interstitial YOK (house da AdMob da), house banner YOK** — ikisi de (lego 10-27: `Ads.interstitial()` no-op, `native_ads.gd` interstitial formatını reddeder, birim kimliği yok; `_child()` yaş cevabından bağımsız hep true). Families uyumlu: AdMob
  `tag_for_child_directed_treatment=true` + `tag_for_under_age_of_consent`, `max_ad_content_rating=G`, kişiselleştirilmemiş,
  aldatıcı düğme yok. Cinnamon'a AD_ID izni ve Firebase EKLENMEZ. lego `kids_mode=true` iken hiç reklam yok.

## 3. Reklamsızlık kapsamı (sahip 10-20)
- Zorunlu reklamı (interstitial) **yalnız** şunlar kapatır: "Remove Ads" ürünü ve onu içeren **büyük/tam paketler**
  (full game / unlock, edition, pass, VIP, premium bundle). Paket açıklamasında "reklamsız" açıkça yazar.
- **Ucuz paketler reklamı KAPATMAZ:** para/kaynak paketleri, tek kozmetik, tek kolaylık/boost.
- Kodda tek hak: `ad_free` = (remove_ads VEYA büyük paket sahibi). "Herhangi bir satın alma" (`purchased_any`,
  `has_purchased`) reklam kararında KULLANILMAZ.
- **Eski alıcılar (sahip 10-20, son karar):** bu kural gelmeden ÖNCE herhangi bir şey almış HERKES reklamsız kalır
  (onlara "her satın alma reklamı kapatır" sözü verildi). Kodda: yeni sürümün ilk açılışında eski "bir şey aldı" bayrağı
  doğruysa kalıcı `legacy_ad_free=true` yazılır (tek seferlik göç işareti ile); sonraki ucuz satın almalar bunu açmaz.
- Oyun içi / mağaza metinleri "her satın alma reklamı kapatır" demez; bu metinler kurala göre düzeltilir (10 dil).
- Oyun başına hangi ürünün `ad_free` verdiği listesi sahibe gösterilip onaylanır, sonra buraya eklenir (§8).

## 4. Ödüllü reklam kuralları
1. **Yalnız opt-in:** oyuncunun bastığı buton. Buton yalnız reklam hazır + tavan/bekleme izin veriyorken görünür.
   Ödül yalnız SDK'nın "earned" geri çağrısından sonra verilir; kapatılan/yarım reklam ödül vermez. Otomatik oynatma yok.
2. **Satılanı verme:** kalıcı kozmetik, premium, reklamsızlık, kalıcı hız/otomasyon/yuva/senaryo, premium para (ör. Golden
   Spikes, Gold Wings) reklamla VERİLMEZ. Ödül küçük, geçici, sayılı olur.
3. **Tavan:** tek ödül ≤ oyunun en ucuz consumable paketinin **1/5**'i; günlük tüm ödüllerin toplamı ≤ **1/2**'si.
   Her yerleşimde **günlük tavan (UTC günü) + bekleme süresi**, cihazda/kayıtta kalıcı (yeniden başlatma sıfırlamaz).
4. **Doğal duraklar:** çevrimdışı kazanç, gün/ay sonu, görev/sözleşme bitişi, kriz "son koz". Oyunu zorlaştırıp reklamı çare yapma yok.
5. **Alıcılar:** yalnız `ad_free` sahibi (§3, eski alıcılar dahil) ödülü **videosuz** alır, aynı tavanlarla. Yeni ucuz paket alan herkes gibi video izler.
6. Ödüllü video izleyen oyuncuya o oturumdaki bir sonraki interstitial en az bir aralık ertelenir.
7. **Uzaktan kapatma:** ads-config `rewardedEnabled` (yoksa AÇIK). Kapalıyken teklif gizlenir; `ad_free` oyuncular etkilenmez.
8. **Money District (moneytycoon) — KARMA (sahip 10-20, son karar):** "Hızlı iş" PARA kalır (emek karşılığı; günde 1–2,
   tek ödül ≤ aylık gelirin ~%5'i). Diğer tüm ödüller **bilgi/ipucu, para yok**: "yokken piyasada ne oldu" özeti +
   yatırım ipucu (çevrimdışı ×2'nin yerine), "bütçe koçu" (gelecek ay uyarısı; "kaçan ödeme cezasını geri al" kaldırıldı),
   yatırım ipucu / piyasa önizlemesi / finans kavram kartı.
9. **Ölçüm (Firebase `coddykit-game`):** `rewarded_offer_shown`, `rewarded_offer_accepted`, `rewarded_completed`,
   `rewarded_skipped_paid_user` (`placement`, `reward_amount`, `daily_index`). 10-20'de eklenen `ad_reward_offer` /
   `ad_reward_earned` adları dokunulduğunda bu adlara taşınır.
10. Açılış: tüm oyunlarda birden (kademeli yüzde yok); ölçüm + uzaktan kapatma kalır.

## 5. AdMob
- Hesap **coddykit.com@gmail.com**, yayıncı `pub-5045495642689387`. Gerçek id'ler: `store-automation/.env`
  (`ADMOB_{APP_ID,INTERSTITIAL,REWARDED}_<SLUG>_<PLATFORM>`). Uygulama/birim oluşturma: `store-automation/src/admob/setup.ts`
  (Mini'deki oturumu açık `google` Playwright profili; Claude in Chrome DEĞİL; API yazamaz). Okuma: aynı profille konsol.
- Id **uydurma**. Debug derleme Google test birimini kullanır; release'te id boşsa o reklam türü kapalıdır (buton gizli).
- Godot: `[coddy_ads] admob_rewarded_android/_ios`; `[admob] general/android/app_id`. (`admob_interstitial_*` / `admob_banner_*` 10-25'ten beri kullanılmaz — eski oyunlarda durabilir, kod okumaz; yeni oyuna interstitial/banner birimi açılmaz.)
- **Hiçbir reklam `MobileAds.initialize` tamamlanmadan yüklenmez** (`_admob_inited`; erken yükleme = çökme, corrupt 1.0.2 Play reddi).
  **SDK'nın TEK sahibi Ads autoload'u**: `MobileAds.initialize` oyunda tek yerde; ödüllü/başka modül kendi init'ini YAPMAZ, Ads'in tamamlandı sinyalini/bayrağını bekler (Poing tek statik init dinleyicisi tutar). Kanıt: Train Baron 1.1.2 Vitals 19 çökme "MobileAds.initialize must be called before…" (2026-10-20). Kapı: `games/tools/ads/check_admob_init.py` (`jenkins/run.sh <oyun> test` çalıştırır).
- Production modu tuzağı: release derleme asla mock/test servise düşmemeli (Travel Tycoon `GAME_SERVICES_MODE`, Parts Rush mock reklam).

## 5b. RevenueCat reklam takibi — HER OYUNDA ZORUNLU (sahip 10-20: "kural olsun geçmiş ve gelecek tüm oyunlar")
- `games/` altındaki **her oyun** (mevcut + yeni) reklam olaylarını RevenueCat'e gönderir: loaded / displayed / opened / revenue / failed_to_load.
  Tasarım: `games/tools/REVENUECAT_AD_TRACKING.md` (W34). Köprü: `games/tools/godotx_revenue_cat_src` (RC SDK ≥10.22.1, `dist/` .aar) +
  `gd/RcAdTracking.gd`; Capacitor/web oyunlarda `purchases-capacitor` + `rc-ad-tracking.js` (travel kalıbı, 4d20b3629d).
- AdMob `paid` olayı (value micros, currency, precision) → `revenue`; impression → `displayed`; click → `opened`. House (CoddyAds) reklamı GÖNDERİLMEZ.
  Firebase `ad_impression` elle yazılmaz (çift sayım yok). Uydurma gelir yok: eklenti paid vermiyorsa revenue gönderilmez, raporda yazılır.
- Uzaktan kapatma: ads-config `rcAdTracking` (canlı, W40; `ads_app.rc_ad_tracking`). Yoksa AÇIK.
- RC SDK'sı olmayan / satın almayı kendi Billing'iyle yapan oyunlar (trading-sim, corrupt-tycoon, zombi): RC **observer** modu
  (`PurchasesAreCompletedBy.MY_APP`, W38) + bayrak `[coddy_rc] observer_enabled` — **gerçek cihaz testi geçmeden bayrak açılmaz**.
- Çocuk/aile oyunları (cinnamon, lego): reklam olayları kişisel veri taşımaz; aynı kural, Families etiketleri değişmez.
- Yeni oyun: şablon (`games/tools/ads/CoddyAds.gd`) RC takip kancasını içerir; oyun ilk yayından önce bağlı olmalı (yayın kontrol listesine madde).

## 6. Çalışma düzeni
- **Mini'deki ortak checkout origin/main'in çok gerisinde olabilir.** Oyun kodunu origin/main'den oku (kendi worktree'n ya da
  `git archive origin/main games/<oyun>`); ortak ağaçta stash/reset/dal değiştirme YOK. Push öncesi fetch + rebase/merge-tree.
- Godot `--import` / testler `project.godot`'u yeniden yazar (varsayılanları düşürür) → commit'ten önce origin sürümüne geri al.
- Sürüm dosyaları (`config/version`, export_presets, sürüm alanları) yayın trenine aittir; özellik işinde dokunma.
- Her oyun ayrı commit; tüm headless testler yeşil; yeni dizeler 10 dilde, oturum içinde çevrilir (dış çeviri API'si yok).

## 7. App Store App Preview videoları (store-video v2, 2026-10-08)
- **Konum (Mac Mini SSD):** `/Volumes/BuildSSD/store-video/v2-pilot/game-packages/train-baron/<locale>-ios.mp4` (sha256 `production-registry.json`), locale: en.
- **Durum:** 1.0 taslağına yüklendi (1/1 locale COMPLETE, 2026-10-08).
- **Yükleme:** `node tools/store-video/v2/ops/asc-upload.mjs train-baron --go` (yalnız düzenlenebilir taslağa; asla göndermez).
- Videolar repoda DEĞİL. Kurallar: `tools/store-video/CLAUDE.md`.

## 8. Oyun başına: `ad_free` veren ürünler (sahip onayı 10-20)
| Oyun | Reklamı kapatan ürünler (+ bunları içeren paketler) |
|---|---|
| airport-baron | noads, premium (Tycoon Edition), bundle, hub_pass |
| train-baron | noads, premium (Baron Edition), bundle, rail_pass |
| corrupt-tycoon | noads, full (Premium), bundle |
| moneytycoon | noads, premium (Wealth Edition), bundle, starter (Remove Ads içerir) |
| aircraft | remove_ads, premium (Gold SLA Edition), bundle |
| smuggler | noads, premium (Kingpin Edition), bundle, empire_pass |
| skiresort | remove_ads, premium (Alpine Edition), bundle |
| lego | noads, premium (Master Builder), bundle, builder_pass |
| zombi | noads, premium (Haven Commander), bundle, vip_pass |
| chaiwala-rush | noads, premium (Premium Tapri), bundle (Patron #36, sahip yetkisi 10-21, 9add5b358a) |
| disaster-command | noads, premium (Command Edition), bundle (Patron #36, sahip yetkisi 10-21) |
| canal-control | noads, premium (Harbor Edition), bundle (Patron EMİR #30, sahip yetkisi 10-21) |
| sky-tower-ops | noads, premium (Premium Tower), bundle (Patron, sahip yetkisi 10-21; GF-sky-tower-ops-P4) — ops_pass reklam KALDIRMAZ |
| lifeguard-hq | noads, premium (Premium HQ), bundle (Patron, sahip yetkisi 10-21; GF-lifeguard-hq-P4) — crew_pack reklam KALDIRMAZ |
| kaiten-sushi | noads, premium (Premium Sushi Bar), bundle (Patron, sahip yetkisi 10-21; GF-kaiten-sushi-P4) — speed_pack / kitchen_pack reklam KALDIRMAZ |
| pharmacy-rush | noads, premium (Premium Pharmacy), bundle (Patron, sahip yetkisi 10-21; GF-pharmacy-rush-P4) — smart_stock / helper_pack reklam KALDIRMAZ |
| bureaucracy-office | noads, premium (Premium Directorate), bundle (Patron, sahip yetkisi 10-21; GF-bureaucracy-office-P4) — digital_reform / efficiency_pack reklam KALDIRMAZ |
| pest-patrol | noads, premium (Premium Fleet), bundle (Patron, sahip yetkisi 10-21; GF-pest-patrol-P4) — lab_boost / efficiency_pack (Busy Depot Pack) reklam KALDIRMAZ |
| doner-night | noads, premium (Premium Kiosk), bundle (Patron, sahip yetkisi 10-21; GF-doner-P4) — fast_spit / efficiency_pack (Busy Night Pack) reklam KALDIRMAZ |
| pit-wall (Grid Boss) | noads, premium (Principal Edition), paddock_pass (Team Patron Pass, kalıcı), bundle (Complete Pit Wall Collection) — C4 EMİR #1 P8 katalog kararı (sahip yetkisi 10-25); P4 teslim/restore doğrulaması bekliyor. Starter ve diğer ucuz ürünler reklam KALDIRMAZ |
| volt-city (Grid Keeper) | noads, premium (Chief Engineer Edition), grid_pass (Utility Pass), bundle (Complete Grid Keeper Collection) (Patron, sahip yetkisi; volt-city P4 e297431f45, Unity `CoddyPurchases.AdFree`) — starter, kozmetik, kolaylık ve bütçe paketleri reklam KALDIRMAZ |
| lastdigit (LastDigit: Math Tycoon) | noads, premium (Bureau Chief Edition), bureau_pass (Bureau Pass), bundle (Complete LastDigit Collection) (Patron, sahip yetkisi; GF-lastdigit-P4, Unity `CoddyPurchases.AdFree` = entitlement `noads`) — starter, temalar, Brass Dials, kolaylıklar (offline_plus, lamp_plus, chief_clerk, profiles, studio), fon/lamba paketleri reklam KALDIRMAZ |
| silent-evidence (Silent Evidence) | se_pro_lifetime (Pro), se_noads (Remove Ads), se_complete (Complete) (Patron, sahip yetkisi; silent-evidence P5 1cd3709b9f, Unity `Commerce.AdFree` = entitlement `pro` veya `noads` + P2 `legacy_ad_free`) — casebook'lar, ofis görünümleri (Collector's Office dahil) ve ipucu paketleri reklam KALDIRMAZ |
Listede olmayan her ürün (para/kaynak paketleri, starter'lar, speed, kozmetik, kolaylık) reklamı KAPATMAZ.
Yeni oyun/ürün: aynı ölçüt (Remove Ads, tam paket, pass, VIP, edition); tabloya ekle.

## 9. Onboarding + 10 dil — HER OYUNDA ZORUNLU (sahip 10-21: "en salak insana bişe adım adım anlatacak onboarding + 10 tane dil desteği")
Geçmiş ve gelecek tüm oyunlar. Ölçüt: oyunu hiç bilmeyen, oyun oynamayan biri ilk 5 dakikada yardımsız temel döngüyü yapabilmeli.
- **Adım adım, tek seferde tek şey:** her temel mekanik ayrı adım; metin en fazla 1–2 kısa cümle, günlük dil, oyun jargonu yok.
- **Göster, yaptır:** her adımda dokunulacak öğe vurgulanır (ok/el + çevresi karartılmış spot); adım ancak oyuncu O EYLEMİ gerçekten yapınca ilerler. Yanlış yere dokunursa adım tekrar gösterilir, ceza yok.
- **Kapsam:** ilk oturumda temel döngünün tamamı (oyuna göre: başlat → ana eylem → sonucu gör → para/ödül → ilk yükseltme/yatırım → ekip/otomasyon). Sonradan açılan her yeni özellik (ilk kez açıldığında) kendi kısa bağlam ipucunu gösterir.
- **Güvenli:** öğretici sırasında oyuncu kaybedemez/batamaz (zaman baskısı yok ya da durdurulmuş).
- **Atla ve tekrar:** "Atla" vardır ama Ayarlar → "Nasıl oynanır / Rehberi tekrar oynat" her zaman geri getirir.
- **Ölçüm:** Firebase `onboarding_step` (adım no) + `onboarding_complete` / `onboarding_skip` olayları (Families/çocuk oyununda Firebase yok → yalnız yerel sayaç).
- **Test:** her adım için headless test (doğru eylem ilerletir, yanlış eylem ilerletmez, atla→Ayarlar'dan tekrar), 10 dilde metin taşmaz; smoke'ta ilk açılış rehberin ilk adımını gösterir.
- **10 dil (asgari):** en, tr, de, es, fr, pt-BR, ru, ja, ko + oyuna göre 10. (hi ya da zh). Oyun içi metin + mağaza metni aynı set; Devanagari/CJK/Kiril fontları oyunla paketli (sistem yedeğine güvenme).
- **Çeviri kimde:** Claude slave oturumları, oturum içi **Haiku alt ajanlarıyla** (sahip 10-21). nitrorouter / OpenRouter / dış çeviri API'si YOK; Codex çeviri yapmaz — Codex'in yazdığı metin varsa Claude slave Haiku ile gözden geçirir.

## 10. Zayıf cihaz optimizasyonu — HER OYUNDA ZORUNLU (sahip 10-21: "dandik cihazı olan da ona göre optimize oynasın, iyi cihazı olan kaliteli oynasın")
- **Cihaz sınıfı otomatik (sahip kararı 2026-10-23, 10-14 "telefon asla Yüksek başlamaz" kuralının yerine):** açılışta RAM + GPU (adaptör adı) + çekirdek sayısı ile sınıf seçilir — **amiral gemisi → YÜKSEK** (Android: OS RAM ≥ 7000 MB ["8 GB" telefon ~7,2–7,6 GB gösterir] + ≥ 8 çekirdek + Adreno 8xx / 730+ · Mali Immortalis · Mali-G720/G725/G9xx · Xclipse 9xx; iOS: RAM ≥ 7000 MB, 15 Pro/16 Pro sınıfı), **güçlü → ORTA** (RAM ≥ 5,2 GiB + ≥ 8 çekirdek + zayıf aile dışı GPU; iOS ≥ 3,4 GiB), **zayıf/bilinmeyen → DÜŞÜK**, masaüstü Yüksek. Kural tek yerde: `games/tools/perf/addons/coddy_perf/perf_device.gd`. Mevcut oyuncu: amiral gemisinde eski otomatik Orta'da duran ve grafiği kendi seçmemiş oyuncu bir kez sessizce Yüksek'e (`PerfDevice.flagship_upgrade`); kimse düşürülmez, elle seçim ezilmez. Koruyucu (PerfGuard) gerçekten yavaşsa (tavansız 10 sn < 25 FPS) bir kademe düşürür.
- **Düşük profil:** gölge yok, 3D çözünürlük ölçeği 0.85, MSAA YOK (W67, 10-23; izin 0.80–0.85; emülatör zayıf profilde p95 +%0; 0.80 + MSAA 2x Android'de Godot 4.7.2 MSAA framebuffer sızıntısını tetikliyordu; eski 0.7 + AA'sız bulanık/tırtıklıydı), 30 FPS tavanı, SSAO/glow kapalı, parçacık ve kalabalık (yaya/araç/müşteri) sayısı ≤ %50, LOD/uzak nesne gizleme, doku ≤ 1024 px. **Orta:** basit gölge, 3D yerel çözünürlük (1.0) + MSAA 2x, 60 FPS hedefi. **Yüksek (amiral gemisinin varsayılanı, diğerlerinde isteğe bağlı):** tam gölge, MSAA 4x, ek efektler. Compatibility renderer'da FXAA/ekran-uzayı AA YOK (görüntüyü değiştirmez) → AA = MSAA. **Godot 4.7.2 Android/Web GLES3 tuzağı:** MSAA + iç 3D tampon (ölçek ≠ 1 ya da Environment'ta glow/adjustment/SSAO) her karede bir framebuffer sızdırır → OOM çökme (Airport/Corrupt 10-23); `PerfProfiles.guard_msaa()` (apply_core çağırır) MSAA'yı orada kapatır, kendi grafik kodu olan oyun da onu çağırır; düzeltmeli export şablonu gelene kadar. UI her zaman ekranın kendi çözünürlüğünde (canvas_items; scaling_3d yalnız 3D). Kurallar `perf_profiles.gd validate()`'te; karşılaştırma aracı `addons/coddy_perf/profile_compare.gd`.
- **🔴 ZAYIF CİHAZDA LAG YOK — yeni görsel/animasyon kuralı (sahip 10-24, BAĞLAYICI; eski "Düşük ≤ +%10" bütçesinin yerine):** yeni bir görsel/animasyon iyileştirmesi zayıf cihazda ASLA takılma yaratmaz. **Varsayılan:** her yeni ağır öğe yalnız Orta/Yüksek'te açılır (quality tier'a bağlı koşullu). Düşük'e yalnız neredeyse bedava olanlar girer: **ek draw call 0** (en çok +1, gerekçesi teslimde yazılı), **prim ≤ +%5**, yeni shader dalı / ek render pası YOK, kare başına ek CPU YOK (trafik / kalabalık / parçacık Düşük'te tavanlı ve 15 Hz). **Her teslimde "Düşük önce/sonra" perf_gate satırı ZORUNLU.** Sürümden sonra Play Android vitals (yavaş / donmuş kare) eski sürümle kıyaslanır; kötüleşen oyunda o değişiklik Düşük'te kapatılır.
- **Dinamik düşürme:** oyun sırasında FPS 10 sn boyunca < 25 ise bir kademe düşür + tek seferlik bildirim ("Ayarlar'dan değiştirebilirsin"); asla kendiliğinden yükseltme.
- **Bütçe:** sahne başına çizim çağrısı ≤ ~150 (Düşük), toplam üçgen ≤ ~60k görünür; ilk açılış < 8 sn (zayıf cihaz), bellek < 400 MB; AAB boyutu makul (font alt kümesi, sıkıştırılmış doku).
- **Fizik ticki (pharmacy-rush 2d767340a5, S2 10-22):** fizik gövdesi/sorgusu/`_physics_process`'i HİÇ olmayan oyun `project.godot`'ta `physics/common/physics_ticks_per_second=30`, `max_physics_steps_per_frame=1` ve `physics/2d|3d/physics_engine="Dummy"` taşır (yavaş karede 8 boş 60 Hz tick koşmasın) + `tests/physics_gate.gd` benzeri kapı; fizik kullanan oyunda genel tick değişmez, yalnız **Düşük** profilde 30 Hz (tick tutulamıyorsa en az `max_physics_steps_per_frame` 2) ve görseli gövdeye bağlıysa `physics/common/physics_interpolation=true`.
- **Düşük = köşe başı aydınlatma + MultiMesh yalnız değişince (doner-night 57d4e6f320, S2 10-22):** Düşük'te 3B görünümün her lit malzemesi `shading_mode=PER_VERTEX` + `specular_mode=DISABLED` (glow/unshaded/yalnız-emissive dokunulmaz; her oyunda malzeme kaydı orijinal değerleri tutar, Orta/Yüksek'e geçişte per-pixel PBR geri gelir; sonradan doğan malzemeler — kozmetik tonlu kopyalar, yeniden kurulan sahne — mevcut ayara uyar). MultiMesh örneği yalnız transform/renk değişince yazılır (örnek başına önbellek, tampon yeniden kurulunca temizlenir; `visible_instance_count` yalnız değişince). Test: Düşük = tüm lit malzeme per-vertex/speküler yok + glow dokunulmamış; Orta per-pixel'i geri getirir; değişmeyen karede 0 MultiMesh yazımı.
- **Test:** headless profil testleri (her sınıf doğru ayarları uygular, dinamik düşürme çalışır) + smoke'ta düşük profil emülatörü (2 GB RAM, düşük çözünürlük) ile FPS/bellek ölçümü raporu; Android export `gl_compatibility`.
- **Native debug sembolleri (W63-12):** her release AAB betiği AAB'den sonra `sh games/jenkins/release_symbols.sh <aab>` çağırır → yanına `<aab adı>.symbols.zip` (resmi Godot sembolleri, build id kontrollü); yükleme `SYMBOLS_ZIP=<zip>` ile `play_upload.mjs`'e AYNI edit'te verilir (commit'ten sonra Play kabul etmez). Yeni oyun = aynı satır.

## 11. Gelir stratejisi eşitliği — yeni oyunlar mevcut oyunların TAMAMINI alır (sahip 10-21)
Yeni her oyun, yayındaki oyunların kanıtlanmış gelir düzenini eksiksiz taşır: `games/tools/IAP_STANDARD.md` (geniş katalog: reklamsız, premium/edition, pass/VIP, starter paketi, consumable para/kaynak paketleri, kolaylık/hız/otomasyon, kozmetikler, bundle), §3 reklamsızlık kapsamı, §4 ödüllü reklam yerleşimleri (çevrimdışı, günlük bonus, başarısızlıkta tekrar, kriz "son koz" — tavanlı, `games/tools/REWARDED_ADS_STRATEGY.md`), interstitial (house-first CoddyAds → AdMob, doğal duraklarda, ödüllüden sonra erteleme), §5b RC reklam takibi, Firebase olayları, starter/teklif zamanlaması, restore. P4 kabul kriteri: Corrupt (canlı en iyi örnek) ile madde madde eşitlik tablosu `docs/MONETIZATION_PARITY.md`; eksik madde = P4 bitmemiş. Trial YOK.

## 12. Her oyunda zorunlu "canlı oyun" özellikleri (sahip 10-21: "admob dahil applerin birbirinin reklamını yapması, oyun içerisinde wow anında puan isteme gibi tüm featurelar")
Yayındaki en iyi oyunun (Corrupt) özellik seti her oyunda; eksik = oyun bitmemiş:
- **AdMob** yalnız ödüllü (§2/§4, 10-25) — çocuk/Families oyununda §4 Families kuralları.
- **Çapraz tanıtım:** `CoddyAds` yalnız house — interstitial kartı + banner (yatay/geniş ekranda 2 oyun); dolmazsa reklam yok; doğal duraklarda.
- **Wow anında puan isteme (sahip 10-27 güncel kural):** Corrupt `src/game/ReviewGate.gd` + `ReviewNative.gd` modeli — yalnız OS'nin kendi sayfası (Google Play In-App Review; oyundan çıkmadan puan), önce "beğendin mi / kaç yıldız" kartı YOK (review gating Google/Apple'da yasak), puan için hiçbir şey verilmez. **İLK OTURUMDA**, ~3–5 dk gerçek oyundan sonra (`MIN_SESSIONS = 1`, `MIN_PLAY_S ≈ 240` sn; oyuna göre 180–300), yeni oyuncunun ~3–5. dakikada yaşadığı bir başarı anında (ilk vardiya/gün/sipariş partisi bitti, ilk seviye/terfi, ilk kontrat/rota/teslimat) — eski "≥2 oturum + ≥15 dk" kapısı KALKTI. Rehber/onboarding sırasında, pencere/mağaza/satın alma/reklam açıkken ya da arka planda ASLA; en çok 3 deneme, aralar ≥30 gün; OS sayfayı yine de göstermeyebilir (sonuç bilinmez, her çağrı deneme sayılır). Yeni oyun (Driftwood dahil) aynı sayılarla gelir. Headless testli.
- §5b RC reklam takibi, Firebase olayları (Families hariç), §9 rehber + 10 dil, §10 performans, §11 gelir eşitliği, ekran standardı, kendi splash (Godot logosu yok), grafik varsayılanı (amiral gemisi Yüksek / güçlü Orta / zayıf Düşük).

## 13. Ses — HER OYUNDA SFX + müzik (Patron, sahip onayı 10-21)
- Her oyun `games/tools/audio` ile (ElevenLabs, manifest `audio/manifest.json`, kredi tabanı %15) SFX + 2 müzik döngüsü (sakin/yoğun) + gerekirse ambiyans taşır; Godot tarafı `Audio` autoload'u (`games/tools/audio/godot/Audio.gd`), Ayarlar'da **Müzik / Efekt** düğmeleri (varsayılan açık, kalıcı), arka planda durur, **headless testlerde ses yok** (Mini hep sessiz); `tests/audio.gd` zorunlu. Örnek: disaster-command, canal-control.

## 14. Kayıt güvenliği — HER OYUNDA ZORUNLU (Patron, 10-22 filo kayıt kaybı taraması)
- Kayıt codec'i simülasyonun **gerçekten üretebildiği her durumu** kabul eder (negatif ara değerler, yoldaki sipariş, kapasite+1 geçişleri, yükseltme anı). Aralık kontrolü sim'in değil codec'in hatası olmamalı.
- Okunamayan kayıt **asla silinmez/üstüne yazılmaz**: ortak `src/save_repository.gd` → `<save>.unreadable` (+ `.bak.unreadable`, eski kopyalar `.unreadable.1…7`), `.bak` geçerliyse ondan yükle; `save_unreadable` analitik olayı (hata kodu + kurtarıldı 0/1, PII yok).
- Her oyunda `games/tools/save_fuzz/save_fuzz.gd` ile fuzz testi (rehber evresi, yatırım/satın alma anı, çevrimdışı dönüş, kare zamanlı kaydet→yükle); 0 okunamayan kayıt kapısı `tests/run.gd`'de.

## 15. Yayın sonrası zorunlu hesap adımları — HER OYUN / HER APP (sahip 10-22)
Her yeni oyun ya da app ilk Play/App Store yüklemesinden sonra, iş "bitti" sayılmadan önce Patron şunları yapar ya da yaptırır:
- **AdMob onay penceresi (GDPR/UMP):** AdMob → Gizlilik ve mesajlaşma bölümünde uygulamanın GDPR mesajı (gerekirse ABD eyalet mesajı da) **yayında** olacak. Gizlilik URL'si uygulamanın politika sayfası. Kodda reklamdan önce UMP isteği yapılacak. Emülatördeki `UMP: no form(s) configured` uyarısı = eksik.
- **RevenueCat "Check credentials":** İlk AAB Play'e çıktıktan sonra Play servis hesabı doğrulaması geçerli olacak.
- **Gizlilik politikası:** Oyunun bölümü https://www.coddykit.com/pages/privacy-policy sayfasında yayında olacak ve Data safety CSV ile birebir tutacak.
- **ASO Atlas:** Play sayfası herkese açılınca ASO Atlas'a Android olarak kaydedilecek (us + 11 ülke; bekleyen liste `~/.coddy-aso/asoatlas/games_pending_play_track.json`).
- **AdMob ↔ Play bağlantısı:** Production'a çıkınca AdMob'da uygulama mağazaya bağlanacak.
- **Reklam backend kaydı (10-23 dersi):** production'a çıkan her oyun Supabase `ads_app` (can_show_ads, can_be_promoted, is_kids, 10 dil başlık, ikon, hero, play_url) + `ads_slot` (kendisi hariç 5 tanıtım, tür halkası) + `ads_app_house` ile kaydedilir; kayıt yoksa `ads-config` `adsEnabled=false` döner ve oyun hiç reklam göstermez. Doğrulama: oyunun `X-App-ID`'siyle `ads-config` çekilir. Çocuk oyunları havuz dışı.
- **Play tanıtım videosu (sahip 10-22):** Her oyunun UI'ı son hâlini alınca (yeni oyunda W64 bittikten sonra) 30 sn gerçek oynanış fragmanı çekilir → YouTube (herkese açık) → Play varsayılan dil listesinin `video` alanı. Tek komut: `python3 tools/store-video/games/trailer.py all <oyun> --fresh` → `trailer.py queue <oyun>` → `v2/ops/yt-play.mjs` (ayrıntı `tools/store-video/games/README.md`). UI sonradan değişirse aynı komutla yeniden çekilir.

## 16. Parmakla kaydırma ve dokunuş — HER OYUNDA ZORUNLU (sahip 10-22; 10-19 telefon raporları)
Geçmiş hata (10-19, 12 oyun): düğme/kart üstünde başlayan sürükleme listeyi kaydırmıyordu; ham scrollbar telefonda işe yaramıyordu. Ayrıca 10-21'de canlı yeniden kurma dokunuşları yuttu (W62 Airport). Kurallar:
- Her `ScrollContainer` (liste, mağaza, ayarlar, sheet, modal, onboarding) ortak **TouchScroll** davranışını kullanır. Düğme/kart/toggle üstünde başlayan sürükleme kaydırır, kısa dokunuş tıklar. Eylemsizlik (inertia) var; yatay listelerde yatay sürükleme çalışır. Kaynak: `games/tools/ui` ortak modülü (W64 kiti). Oyunlara kopya `sync.sh` ile gider, `--check` kapısıyla korunur. Oyun başına elle kopya yazılmaz.
- Görünür ham Godot scrollbar YOK: ince ya da otomatik gizlenen temalı çubuk olur. İçerik sığıyorsa çubuk hiç görünmez (Corrupt onboarding örneği).
- Liste/HUD yenilenirken parmak basılıysa yeniden kurma bekler. Dokunuş kaybolmaz (W62/W63 deseni).
- **Kapı testi (check.sh):**
  1. Statik tarama: TouchScroll'a bağlanmamış ScrollContainer = FAIL.
  2. Cihaz benzeri dokunuş olaylarıyla regresyon: düğme üstünden başlayan sürükleme kayar, dokunuş tıklar, yenileme sırasında dokunuş korunur. Dikey ve yatay, en + uzun bir dil (de).
- Yeni oyun P3/P7'de bu kapı yeşil olmadan geçmez.

## 17. UI standardı — W64 kiti HER OYUNDA ZORUNLU (sahip 10-22: "UI'lar çok iyi olmadan canlıya çıkmayacak")
- Tüm arayüz ortak **`games/tools/ui` (coddy_ui) kiti** ile yapılır; oyuna `sync.sh` ile kopyalanır, `--check` kapısı vardır. Uygulama rehberi `games/tools/ui/README.md`'de: 10 adım, ekran başına kontrol listesi, sert kurallar.
- **Yasaklar:** ham Godot teması (gri panel, varsayılan scrollbar, varsayılan font), 48 dp altı dokunma hedefi, 14 sp altı gövde metni, kartsız toast, durağan ekranda görünen scrollbar.
- **Zorunlu bileşenler:**
  - onboarding pager: çizim + adım noktaları + Atla;
  - koç işareti;
  - ayarlarda standart Gizlilik bloğu: UMP "Gizlilik seçenekleri" + politika bağlantısı;
  - kompakt mağaza: tek ekranda en az 3 ürün;
  - içerik boyutlu modal ve sheet;
  - yükleme ekranı.
- **Kabul:** ekran başına önce/sonra görüntüsü (en + de) çekilir ve Patron bakar. Oyun puanı **≥ 8,5** olmadan production'a çıkılmaz.
- **Yeni oyunlar baştan bu kitle yapılır.** Pilot: canal-control (83d5ef7b80, 7ef66c104c).
