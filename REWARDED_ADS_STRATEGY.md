# Ödüllü reklam stratejisi — oyun başına (W30, 2026-10-20)

> Sahip isteği: "tüm oyunlarda interstitial var. Ödüllü reklam için strateji oluştur her oyuna özel olarak, bazı şeyleri gelir
> kaybı yani IAP'ı etkileyemeyecek şekilde ödüllü reklam tarafında da gelirimiz olsun."
> **Bu yalnız strateji belgesidir — kod değişikliği yok.** Sahip onaylayınca oyun başına uygulama işi açılır.
> Kaynak: koddan çıkarılan olgular (her sayı dosya:satır ile `scratchpad/w30_facts.md` envanterinden; bu belgede özetlendi).
> Cinnamon reklamsız (sahip kararı) → kapsam dışı.

## 0. Filo kuralları (her oyunda aynı)

1. **Satılanı asla reklamla verme.** Kalıcı kozmetik, premium, reklamsız, kalıcı hız/otomasyon/yuva/senaryo ödül OLAMAZ.
   Ödül yalnız **küçük, geçici, sayılı**: tek seferlik az miktar para/kaynak, tek bir beklemeyi kısaltma, çevrimdışı kazancı bir kez artırma.
2. **Tavan oranı:** bir video ödülü ≤ **en ucuz consumable paketinin 1/5'i**; günlük tüm ödüllerin toplamı ≤ en ucuz paketin **yarısı**.
   Böylece en hevesli izleyici bile günde bir "cash_s"ı reklamla toplayamaz → paket satışı yamyamlanmaz.
3. **Günlük tavan + bekleme** her yerleşimde zorunlu (UTC günü; Last Haven'daki `ads.gd` kalıbı: yerleşim başına `cooldown` + `daily`).
4. **Satın alan oyuncu:** herhangi bir satın alma = video yok, ödül doğrudan (IAP_STANDARD madde 6). Last Haven'da şu an yalnız
   `vip_pass` muaf — filo kuralıyla hizalanmalı (aşağıda). Gerekçe: ödeyen oyuncuya reklam göstermek iade/şikâyet riski,
   kazanç tarafında payer'dan alınacak eCPM ihmal edilebilir.
5. **Doğal duraklar:** yalnız oyuncunun zaten durduğu anlar (çevrimdışı kazanç toplama, gün/ay sonu, sözleşme bitişi, kriz "son koz").
   Pay-to-win yok, zorunlu bekleme yaratma yok, oyunu zorlaştırıp reklamı çare yapma yok. Trial yok.
6. **Interstitial ile birlikte:** ödüllü video izleyen oyuncuya o oturumda bir sonraki interstitial en az 1 aralık ertelenir
   (gönüllü izleyeni cezalandırma). House-first (CoddyAds) yalnız interstitial'da; ödüllüde doğrudan AdMob (house ödül vermez).
7. **Ölçüm:** her yerleşimde `Analytics.event` ile `rewarded_offer_shown`, `rewarded_offer_accepted`, `rewarded_completed`
   (`placement`, `reward_amount`, `daily_index`), ayrıca IAP tarafında `purchase` olaylarına `had_rewarded_today` bayrağı →
   yamyamlama ölçülür. Uzaktan aç-kapa: yerleşim başına bayrak (ads-config / oyunun services config'i), varsayılan KAPALI → %10 → %50 → %100.
8. **Gelir tahmini:** eCPM ve izlenme için bu belge RAKAM UYDURMAZ. İlk 14 gün AdMob raporundaki gerçek ödüllü eCPM'i ve
   Firebase'deki `rewarded_completed / DAU` oranını ölçüp karar ver. Karar eşiği (öneri): IAP geliri/DAU %5'ten fazla düşerse yerleşimi kapat.

## 1. Filo özeti ve öncelik ("en çok gelir / en az risk")

| # | Oyun | Bugün ödüllü | Öneri | Risk | İş |
|---|---|---|---|---|---|
| 1 | **Last Haven** (zombi) | 3 yerleşim, tavanlı (en olgun) | muafiyeti "her satın alan"a genişlet, 4. yerleşim: gece raporu "kayıp kurtar" | düşük | S |
| 2 | **Snow Empire** (skiresort) | bonus + dönüş, tavanlı | "fırtına kalkanı" (son koz) + dönüş artışı ölçümü | düşük | S |
| 3 | **Borderline Empire** (smuggler) | sponsor bonusu, **günlük tavan YOK** | günlük tavan + çevrimdışı katlama | orta (tavansız) | S |
| 4 | **Parts Rush** (aircraft) | sözleşme başı +$500, **tavan YOK** | günlük tavan + parça bekleme kısaltma | orta | S |
| 5 | **Airport Baron** | yok | çevrimdışı kazanç ×1,5 (GW'li ikiye katlamaya dokunmadan) | düşük | M |
| 6 | **Train Baron** | yok (bilinçli "IAP only") | Airport ile aynı kalıp | düşük | M |
| 7 | **Corrupt State** | son koz kurtarma (oyun başına 2) | + günlük "rüşvet zarfı" küçük nakit | orta | S |
| 8 | **Money District** | yok | ay sonu "bütçe danışmanı" küçük bonus (consumable yok → tavan kendi ekonomisine göre) | düşük | M |
| 9 | **Trading Sim** | yok | "piyasa bülteni" (bilgi, para değil) | düşük | M |
| 10 | **Travel Tycoon** | günde +1 zümrüt, **payer muafiyeti yok** | payer muafiyeti + çevrimdışı/sezon sonu küçük nakit | düşük | S |
| 11 | **BlockForge** (lego) | yapı başına +50 puan | çocuk modunda zaten kapalı; ÖNERİ: genişletme YOK, Families beyanı netleşene kadar dondur | yüksek (COPPA) | — |

Öncelikli 3 (en hızlı gelir, en az risk): **Last Haven, Snow Empire, Borderline Empire** — üçünde altyapı ve gerçek ödüllü birim id'leri var,
yalnız kural/tavan ayarı + 1 yerleşim.

## 2. Oyun başına

### 2.1 Last Haven (zombi) — referans model
- **Ekonomi / IAP:** yerleşim kaynakları (malzeme/yiyecek/su/yakıt/ilaç); consumable: supplies_small 0.99 (400 mal + 100 yakıt) … stage_pack 3.99;
  kalıcı: vip_pass 4.99, builder_crew, offline_plus, kozmetikler, premium, bundle.
- **Bugün:** `double_offline` (günde 4), `finish_build` (≤120 s, 300 s bekleme, günde 4), `supply_crate` (80 mal + 30 yiyecek + 30 su; 30 dk, günde 3).
  Muafiyet yalnız vip_pass.
- **Öneri:**
  1. Muafiyeti "herhangi bir satın alma"ya genişlet (filo kuralı 4).
  2. `supply_crate` = 80 malzeme ≈ supplies_small'ın 1/5'i — kurala uygun; günlük toplam 3×80 = 240 < 400/2 → uygun. Değiştirme.
  3. `double_offline` günde 4 → **2** (offline_plus 2.99 kalıcı ürünüyle çakışmayı azaltır: reklam = tek seferlik, offline_plus = her gün otomatik %80/12 sa).
  4. Yeni: gece raporunda kayıp varsa "kayıpların yarısını kurtar" (günde 1, son koz).
- **Çakışma:** builder_crew (kalıcı +1 inşa) ↔ finish_build (tek seferlik ≤120 s) → çakışmaz, tavanlı.
- **Ölçüm:** mevcut `ad_reward_*` olayları var; `rewarded_*` adlarıyla eşle.
- **İş:** S — `data/business.json` tavanları + `profile.gd` muafiyet + 1 yerleşim.

### 2.2 Snow Empire (skiresort)
- **Ekonomi / IAP:** günlük kâr; consumable: cash_s 0.99 = 1 günlük kâr … cash_xl 14.99 = 10 gün; storm_kit 2.99; kalıcı: return_plus, auto_ops, speed, kozmetikler.
- **Bugün:** "bonus" 300 nakit (sim günü 3, UTC günü 6; 60 s ara) + "return" bir kez artırma; satın alan videosuz.
- **Öneri:**
  1. Bonus sabit 300 → **günlük kârın %15'i** (en az 300, en çok 0,2 × cash_s), UTC günde 3.
     Gerekçe: geç oyunda 300 anlamsızlaşıyor, ama tavan cash_s'nin 1/5'i.
  2. Yeni "son koz": fırtına hasarı varsa "acil ekip" — **tek** tesisi onarır (storm_kit tümünü onarır + 1,5 gün kâr verir → ondan belirgin küçük), günde 1.
  3. "return" artışı kalsın; return_plus (kalıcı, 16 sa) ile çakışmaz çünkü reklam tek seferlik.
- **Not:** gerçek ödüllü id'ler yalnız export profili `config/services.production.example.json`'da; build betiği enjekte ediyor (doğru).
- **İş:** S.

### 2.3 Borderline Empire (smuggler)
- **Ekonomi / IAP:** sözleşme nakdi, ref gelir 120/dk; cash_s 0.99 = 30 dk gelir, cash_m 75 dk, cash_l 150 dk; empire_pass ×1,25 ödeme, offline_plus.
- **Bugün:** sponsor bonusu 150 × garaj seviyesi, 600 s bekleme, **günlük tavan yok** → günde onlarca izlenebilir, cash_s'yi yamyamlar.
- **Öneri:**
  1. Günlük tavan **5**; ödül = min(150 × garaj, **6 dk ref gelir**) (= cash_s'nin 1/5'i).
  2. Yeni: çevrimdışı kazanç toplama ekranında "×1,5" (günde 1; offline_plus 2 sa → 8 sa kalıcı ürünüyle çakışmaz).
  3. iOS interstitial birimi boş — ödüllü değil ama not.
- **İş:** S — `data/monetization.json` + `monetization.gd`.

### 2.4 Parts Rush (aircraft)
- **Ekonomi / IAP:** sözleşme kârı (ref 1500–8000); cash_s 0.99 = 3 dk kendi kazancı … cash_xl 30 dk; speed, auto_sourcing, stand_plus kalıcı.
- **Bugün:** sözleşme bitişinde sponsor +$500, sözleşme başına 1, **günlük tavan yok**.
- **Öneri:**
  1. Günlük tavan **6**; ödül = min($500, cash_s tutarının 1/5'i). cash_s = 3 dakikalık kendi kazancı (ref gelir 1500–8000 aralığında
     sıkıştırılmış) — uygulamada cash_s'nin o anki gerçek tutarı hesaplanıp sınır ona göre konur (kural 2).
  2. Yeni: AOG acil durumda "parça kuryesi" — **tek** parçanın tedarik süresini yarıya indirir (auto_sourcing kalıcı otomasyonla çakışmaz), günde 2.
- **İş:** S — `data/services.json` + `simulation.gd`.

### 2.5 Airport Baron
- **Ekonomi / IAP:** krediler + Gold Wings (GW); gw_pouch 1.99 = 100 GW …; çevrimdışı 8 sa %50, GW ile ikiye katlama (saat başı 2 GW).
- **Bugün:** ödüllü yok ("interstitial only").
- **Öneri:**
  1. Çevrimdışı toplama ekranında "reklamla ×1,5" (günde 1) — GW ile ×2 katlama satışta kalır, reklam ondan küçük.
  2. GW ASLA reklamla verilmez (premium para birimi = IAP'ın kendisi).
- **İş:** M — ödüllü altyapı yok (Ads autoload'a ödüllü yükleyici + AdMob ödüllü birim id gerekir: **oluşturulmalı, şu an yok**).

### 2.6 Train Baron
- Airport Baron ile aynı yapı (krediler + Golden Spikes; çevrimdışı 2–8 sa). Aynı öneri: çevrimdışı ×1,5 günde 1, GS asla reklamla yok.
- Not: katalogda bilinçli "IAP only — no rewarded anything" kararı var → **sahip kararı gerekir**.
- **İş:** M (AdMob ödüllü birim id gerekir).

### 2.7 Corrupt State
- **Ekonomi / IAP:** temiz/kirli/offshore Ð, "günlük gelir" = son 7 günün ortalaması; cash_s 0.99 = 1 günlük gelir …; election 4.99, crisis 2.99, influence 1.99.
- **Bugün:** yalnız "son koz" (iflas/devrim, oyun başına 2, hiç ödememiş oyuncu).
- **Öneri:**
  1. Son koz kalsın (iyi tasarım).
  2. Yeni: gün kapanışında "zarf" = günlük gelirin **%20'si** (cash_s'nin 1/5'i), günde 2.
  3. Influence/heat ödülü YOK (election/crisis/influence ürünleriyle doğrudan çakışır).
- **İş:** S — `RewardedAds.gd` mevcut, yerleşim eklenir.

### 2.8 Money District
- **Ekonomi / IAP:** aylık finans simülasyonu; **consumable yok** (sahip: "oyun para öğretiyor, para satmıyoruz"); kalıcı: speed, slots, scenarios, kozmetikler.
- **Bugün:** ödüllü yok.
- **Öneri:** para vermek oyunun öğretme ilkesiyle çelişir → ödül **para değil, bilgi**: ay sonunda "danışman analizi"
  (gelecek ayın giderleri/riskleri önizlemesi), günde 2. IAP'ta karşılığı yok → yamyamlama riski sıfır.
  **Sahip kararı gerekir** (oyunun "para satmıyoruz" ruhuna uygun mu).
- **İş:** M (ödüllü altyapı + iOS'ta reklam kapalı).

### 2.9 Trading Sim
- **Ekonomi / IAP:** sermaye $; capital_s 0.99 = sermayenin %10'u …, bailout 2.99.
- **Bugün:** ödüllü yok; Analytics autoload yok (ölçüm için önce eklenmeli).
- **Öneri:** "piyasa bülteni" — bir sonraki gün için 1 hissenin eğilim ipucu (bilgi; pro_tools kalıcı aracından belirgin zayıf), günde 2.
  Sermaye ASLA reklamla verilmez (capital_*/bailout ile doğrudan çakışır).
- **İş:** M (ödüllü + analytics altyapısı).

### 2.10 Travel Tycoon (Three.js)
- **Ekonomi / IAP:** nakit + zümrüt (yavaş kazanılır); hepsi kalıcı (mekân lisansları, premium, kozmetik), consumable yok, noads ürünü yok.
- **Bugün:** dükkânda günde +1 zümrüt; **satın alan muafiyeti yok**; interstitial yalnız QA'da (oyuncuya kapalı).
- **Öneri:**
  1. Satın alan oyuncuya videosuz ver (filo kuralı 4).
  2. +1 zümrüt/gün kalsın (venue_tufmar 25 zümrüt → 25 gün; lisans satışını yamyamlamaz).
  3. Yeni: sezon kapanışında küçük nakit bonusu (günde 1).
- **İş:** S.

### 2.11 BlockForge (lego) — çocuk
- **Bugün:** yapı başına +50 puan ödüllü; `kids_mode` açıkken tüm reklam/IAP kapalı; reklamlar kişiselleştirilmemiş.
- **Risk:** `project.godot` `keep_ad_id=true` ile `store/privacy-data-collection.md` "Advertising ID = No" çelişiyor; Families/hedef kitle beyanı repoda yok.
- **Öneri:** **genişletme YOK.** Önce hedef kitle + Families beyanı ve AD_ID çelişkisi çözülsün (sahip/Play Console).
  Uygulama çocuk hedefliyse mevcut ödüllü de kapatılmalı (Families politikası yalnız sertifikalı reklam SDK'sı ve kurallar ister).

## 3. Sahibe sorulacak kararlar
1. **Satın alana ödüllü:** "her satın alan videosuz ödül alır" filo kuralı mı? (Last Haven şu an yalnız vip_pass'ı muaf tutuyor; Travel hiç tutmuyor.)
2. **Train Baron'daki bilinçli "IAP only" kararı** kalkıyor mu?
3. **Money District'te para yerine bilgi ödülü** uygun mu?
4. **BlockForge:** çocuk hedefli mi? (Evetse ödüllü kapatılır; Families beyanı + AD_ID çelişkisi.)
5. **Airport/Train/Money District/Trading Sim için yeni AdMob ödüllü birimleri** oluşturulsun mu? (Bu belge oluşturmaz.)
6. Açılış sırası: önerilen kademeli açılış (%10 → %50 → %100, 14 gün ölçüm, IAP/DAU %5 düşüş eşiği) kabul mü?
