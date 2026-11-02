# 10 yeni oyun — kaos içinde süreç yönetimi (2026-10-20)

> Sahip 2026-10-20: "bu 10 önerinin hepsini yapmak istiyorum; her biri için strateji dokümanı; implementasyona geçme, yalnız TODO/plan."
> **Kod yok.** Bu klasörlerde yalnız `STRATEGY.md` (neden, pazar, rakip, ASO, gelir, UA) ve `PLAN.md` (TODO) var.
> Araştırma (veri + yöntem): [`marketing-data/asoatlas/chaos-game-idea-research-2026-10-20.md`](../marketing-data/asoatlas/chaos-game-idea-research-2026-10-20.md),
> ham veri `marketing-data/asoatlas/chaos-2026-10-20/` (Play US ilk-10 kurulumları `gp_US.json`, ASO Atlas bantları `play*_us.json`).

## Tür tanımı (sahip)

Corrupt State / Airport Tycoon / Train Tycoon / Borderline Empire gibi **2.5D yaşayan diorama**; ekranda aynı anda çok şey olur
(kuyruk, akış, kriz), oyuncu **kaosu süreç olarak yönetir** (yönlendir, önceliklendir, darboğazı aç, otomatikleştir, büyüt).
Seçim ölçütü: **Play'de rekabeti çok düşük niş** + gerçek arama talebi (ASO Atlas, Play US bandı ≥ Medium).

## Portföy

| # | Klasör | Oyun | Play US talep (en iyi) | Temaya uyan en güçlü rakip | Reklamla büyütme (UA) uygunluğu |
|---|---|---|---|---|---|
| 1 | [`chaiwala-rush/`](chaiwala-rush/STRATEGY.md) | Chaiwala Rush — Mumbai çay tezgahı | `tea game` High · `chai game` Medium · IN `chaiwala` High | Istikan Chai 46k | Düşük (IN ARPU) → organik/ASO oyunu |
| 2 | [`disaster-command/`](disaster-command/STRATEGY.md) | Disaster Command — tahliye merkezi | `hurricane game` High + 6 Medium | Disaster Town Tycoon 139k | **1. sıra** |
| 3 | [`lifeguard-hq/`](lifeguard-hq/STRATEGY.md) | Lifeguard HQ — plaj cankurtaran | `lifeguard game` / `lifeguard simulator` Medium | Coast Guard: Beach Rescue 5.6M (2017) | 4. sıra |
| 4 | [`pest-patrol/`](pest-patrol/STRATEGY.md) | Pest Patrol Co. — haşere salgını | `pest control game` Medium | Pest Control Insect World 285k | Düşük (reklam politikası) |
| 5 | [`pharmacy-rush/`](pharmacy-rush/STRATEGY.md) | Pharmacy Rush — grip sezonu eczanesi | `pharmacy game` Medium | Pharmacy Manager Simulator 116k | Düşük (ilaç reklam politikası) |
| 6 | [`kaiten-sushi/`](kaiten-sushi/STRATEGY.md) | Kaiten Sushi Rush — dönen bant | `sushi conveyor` Medium | The Sushi Spinnery 131k | Düşük (yemek açık artırması pahalı) |
| 7 | [`canal-control/`](canal-control/STRATEGY.md) | Canal Control — kanal trafiği | `canal game` / `suez canal` Medium | (trafik yöneten yok) Canal Constructor 947k inşa | **2. sıra** |
| 8 | [`bureaucracy-office/`](bureaucracy-office/STRATEGY.md) | Bürokrasi Dairesi — Doranta | `bureaucracy game` Medium | Bureaucracy Inc 8k | 5. sıra |
| 9 | [`sky-tower-ops/`](sky-tower-ops/STRATEGY.md) | Sky Tower Ops — gökdelen asansörü | `elevator game` / `lift game` Medium | Hotel Elevator 4.8M (2020) | **3. sıra** |
| 10 | [`doner-night/`](doner-night/STRATEGY.md) | Gece Döner Büfesi — İstanbul gece 3 | `kebab tycoon` / `doner game` Medium | Kebab Fever 51k (devler: Kebab World 8.2M) | Düşük (yemek + TR ARPU) |

Play talebi = ASO Atlas tahmini **bant** (Google sayı yayınlamaz); `d` = zorluk 0-100. Kurulumlar = Play sayfasındaki gerçek sayı (2026-10-20).

## Önerilen sıra (karar sahibin)

1. **Reklam kreatif testi ÖNCE** (kod yazmadan): Disaster Command, Canal Control, Sky Tower Ops için 15 sn Blender mockup video ×2–3,
   US Google App Campaign / Meta, fikir başına ~100–200 $ → CTR + CPI. En ucuz CPI → ilk üretilecek UA oyunu. (Harcama sahip onayıyla.)
2. Paralelde **organik/ASO oyunu**: Chaiwala Rush (en boş raf + IN/US High terimler).
3. Kalanlar sırayla; aynı motor/şablon üzerinden (bkz. "Ortak çekirdek").
- Mac Mini kuralı: aynı anda en çok 3 ağır ajan (GPU/Blender) — 10 oyunu aynı anda başlatma.

## Ortak standartlar (her PLAN.md bunlara bağlı — tekrar yazılmadı)

**Motor/sanat**
- Godot **4.7.2**, GDScript 2, Mobile renderer; Android export **`gl_compatibility`** (Vulkan açılış çökmesi).
- Blender 5.2 `blender -b -P blender/<script>.py` → `assets/models/*.glb`; low-poly, okunur siluet, 2.5D izometrik kamera.
- Saf mantık `sim/` katmanı (headless denge simülatörü + testler) — Corrupt State/Airport düzeni.
- **Godot logosu/splash YOK**, her oyuna kendi splash + yükleme perdesi.
- Grafik varsayılanı: güçlü cihaz **Orta**, zayıf/bilinmeyen **Düşük**, asla Yüksek; ilk açılışta "Ayarlar'dan artır" popup'ı.
- Onboarding: 3 sayfalık intro + rehberli koç (aircraft/Parts Rush modeli).
- **10 dil** (en + tr, de, es, fr, pt-BR, ru, ja, ko, hi/ar oyuna göre); çeviri kuralları kök CLAUDE.md / hafıza (OpenRouter yok).

**Gelir** (bkz. hafıza `feedback_games_wide_iap_catalog_standard_2026_10_18`, `feedback_games_rewarded_ads_allowed_2026_10_20`)
- Oyun baştan sona ücretsiz; **trial YOK**, duvar YOK, abonelik YOK, pay-to-win YOK.
- Geniş IAP kataloğu: Reklamları Kaldır · Premium (reklamsız + kozmetik + küçük kolaylık) · kolaylık (hız, otomasyon) · kozmetik paketler ·
  içerik (yeni harita/senaryo) · Hepsi Bir Arada · zamanlı teklifler · sınırlı consumable. 4 PPP fiyat kademesi. Ürün id `com.coddykit.<oyun>.<slug>`.
- Herhangi bir satın alma = bir daha zorunlu reklam yok.
- Reklam: interstitial (önce CoddyAds çapraz tanıtım, sonra AdMob) + **ödüllü reklam (opt-in, 2026-10-20'den beri serbest)**; banner yok.
- Satılan her kozmetik için "gerçekten çiziliyor mu" testi (`test_every_sold_skin_is_visible` modeli).

**Altyapı/yayın**
- Yalnız **Google Play** (iOS şimdilik yok; kod iOS'u kırmasın ama iOS işi açılmaz).
- Firebase Analytics (proje `coddykit-game`, hesap coddykit.com@gmail.com), RevenueCat/Play Billing, AdMob (gerçek id yalnız AdMob hesabından).
- `GAME_SERVICES_MODE=production` olmadan AAB MOCK derlenir (Travel Tycoon tuzağı) — Jenkins job'ında zorunlu.
- Android derlemesi Jenkins **Linux ajanı** (`android-linux`) → AAB → `gp-upload-aab.ts`; Play yüklemesi sahip onayı.
- Production öncesi Mini'de smoke (sırayla, asla paralel, biten emülatör/simülatör kapanır, ses kapalı).
- Play Console: Data safety + "contains ads" + IARC + hedef kitle beyanı.
- Mağaza: coddy devre stili SS standardı; ikon/logo yalnız sahibin onayladığı aday; App Preview/fragman `tools/store-video`.
- ASO: Play başlık/kısa açıklama araştırmadaki terimlerle; yayından sonra ASO Atlas'ta android `track_app` (12 ülke: us,mx,tr,in,fr,es,kr,br,pk,ru,jp,de).
- Steam altyapısı (masaüstü export, `Store` facade) v1'de hazırlanır, yayın sonra (Airport modeli).
