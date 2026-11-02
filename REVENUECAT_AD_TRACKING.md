# RevenueCat reklam takibi — araştırma ve tasarım (W34)

**Tarih:** 2026-10-20 · **Hazırlayan:** C2 · **Durum:** tasarım; uygulama kodu ve canlı ayar değişikliği yok.

Sahip isteği: “ayrıca revenuecatin reklam entegrasyonu varsa yap takip edebilmemiz için oyunlarda”.
W34 emri uygulamayı W32 sonrasındaki ayrı emre bırakıyor. Bu belge o uygulamanın kapsamını tanımlar.
Reklam davranışında tek kaynak [games/CLAUDE.md](../CLAUDE.md), özellikle §2–4'tür.

## Karar

**Var.** RevenueCat Ad Monetization; reklam gösterimi, tıklaması ve gösterim bazında geliri IAP verisiyle
birleştirebiliyor. Özellik dokümantasyonda beta; Charts v3 ve proje erişimi gerekiyor. AdMob'da
impression-level ad revenue (ILRD) açık olmalı. AdMob hesap bağlantısı yalnız reklam birimi adlarını
eşleştirmek için kullanılabilir; tek başına cihazdan gelir olayı üretmez. Bu çalışmada erişim/toggle
durumu değiştirilmedi veya canlı reklam gösterilerek doğrulanmadı.
[RC genel bakış](https://www.revenuecat.com/docs/ad-monetization)

**Önerilen yol:** Godot oyunlarında mevcut Poing callback'lerinden ortak bir GDScript eşleyiciye,
oradan `godotx_revenue_cat` Kotlin köprüsüyle RC `AdTracker` API'sine aktarım. İlk kapsam:
loaded/displayed/opened/revenue/failed-to-load. Reklamı sunan ve oyun ödülünü veren mevcut W32 akışı
korunur. RC telemetri hatası satın almayı, reklamı veya hak edilmiş ödülü durdurmaz.

## Sürüm ve API kanıtı

| Konu | Doğrulanan durum | Uygulama sonucu |
|---|---|---|
| İlk altyapı | Android changelog 9.15.0, PR #2728: non-paid revenue reporting altyapısı; 9.15.1 veri nesneleri, 9.15.2 ek olaylar | “Android 8+” genel gereksinimini bu metotların ilk sürümü sayma |
| Deneysel API | 9.18.0, PR #2962 deneysel API işareti; 9.19.4 reklam formatı; 9.22.0 AdMob adapter modülü | Eski deneysel sürümler güncel adapter sözleşmesiyle eşit değildir |
| Filodaki Android | `10.14.1` etiketinde `Purchases.sharedInstance.adTracker` ve beş temel `trackAd*` metodu var; `ExperimentalPreviewRevenueCatPurchasesAPI` işaretli | Temel manuel takip için SDK yükseltmek teknik olarak zorunlu değil; Kotlin opt-in ve yeni köprü gerekir |
| Güncel belgelenen legacy adapter | `purchases-admob` / Purchases **10.19.0+**; GMA **22.0.0+**, minSdk 24, compileSdk 35 | Adapter seçilirse yükseltme gerekir; legacy ve Next-Gen birlikte kurulmaz |
| Deneysel işaretin kalkması | 10.19.0, PR #4068 | Tercih edilen uygulama tabanı en az 10.19.0; tam sürüm sabitlenip W23 regresyonları çalıştırılmalı |
| Sonraki opsiyonel olaylar | 10.24.0 rewarded prompt shown/accepted; 10.27.0 manual earned-unverified | Bu olaylar 10.19 tabanında varmış gibi çağrılmamalı; ilk kapsamda W32 oyun analitiği yeterli |
| Next-Gen adapter | Android 10.23.0+ | Mevcut Poing/legacy GMA'yı bu görev için Next-Gen'e taşımak önerilmiyor |

Kaynaklar:
[Android changelog](https://github.com/RevenueCat/purchases-android/blob/main/CHANGELOG.md),
[10.14.1 AdTracker kaynak kodu](https://github.com/RevenueCat/purchases-android/blob/10.14.1/purchases/src/main/kotlin/com/revenuecat/purchases/ads/events/AdTracker.kt),
[10.14.1 Purchases erişimi](https://github.com/RevenueCat/purchases-android/blob/10.14.1/purchases/src/defaults/kotlin/com/revenuecat/purchases/Purchases.kt),
[adapter kurulum koşulları](https://www.revenuecat.com/docs/getting-started/adapter-sdks/admob).

SDK yükseltme kararı: yalnız manuel temel olaylarda 10.14.1 ile teknik prototip mümkündür.
Üretim uygulamasında deneysel işareti kaldırılmış, test edilmiş **tek sabit 10.19.x veya daha yeni**
sürüm tercih edilir. “En yeniyi otomatik al” veya Gradle `+` sürümü kullanılmaz.
Eklenti derleme bağımlılığı, tüm oyunların `export_plugin.gd` varsayılanı, varsa
`export_presets.cfg` override'ı ve `purchases-ui` aynı sürüm politikasına alınır.
Sürüm dosyaları yayın treniyle çakıştığından bu değişiklik sonraki emirde koordine edilir.

### Android olay sözleşmesi

Giriş: `Purchases.sharedInstance.adTracker`. Veri tipleri:
`com.revenuecat.purchases.ads.events.types.*`.

| Kaynak callback | RC metodu / veri tipi | Anlam |
|---|---|---|
| Reklam yükleme başarı | `trackAdLoaded(AdLoadedData)` | Belleğe reklam geldi |
| Gerçek impression | `trackAdDisplayed(AdDisplayedData)` | Reklam gösterildi |
| Reklam tıklaması | `trackAdOpened(AdOpenedData)` | Reklam açıldı/tıklandı; oyunun “izle” düğmesi değildir |
| AdMob paid callback | `trackAdRevenue(AdRevenueData)` | Gösterim geliri |
| Yükleme hatası | `trackAdFailedToLoad(AdFailedToLoadData)` | Mediatör hata kodu; sahte impression yaratılmaz |

Ortak alanlar: `mediatorName`, `networkName`, `adFormat`, `placement`, `adUnitId`, `impressionId`.
Gelirde ek alanlar: `revenueMicros: Long`, `currency`, `precision`.
Yükleme hatasının alanları farklıdır; bilinmeyen ağ veya impression uydurulmaz.
[RC manuel entegrasyon](https://www.revenuecat.com/docs/ad-monetization/manual-integration)

### REST karşılığı

İncelenen yayımlanmış REST v1/v2 referanslarında bu beş olay için belgelenmiş, bağımsız reklam
geliri gönderme sözleşmesi bulunamadı. SDK kaynağında dahili event taşıması bulunması public API
garantisi değildir. Corrupt'a tahmini bir `POST /ad-revenue` veya receipt içine reklam tutarı eklenmez.
SDK callback yolu seçilir; SDK'sız kalmak zorunluysa RC'den resmî destek/sözleşme alınması ayrı önkoşuldur.

[REST v1](https://www.revenuecat.com/docs/api-v1),
[REST v2](https://www.revenuecat.com/docs/api-v2),
[10.14.1 dahili AdEventsRequest](https://github.com/RevenueCat/purchases-android/blob/10.14.1/purchases/src/main/kotlin/com/revenuecat/purchases/ads/events/networking/AdEventsRequest.kt).

## Repo envanteri ve uygulama önkoşulları

Oyun envanteri `70dcf599d757`, ortak Kotlin kaynak doğrulaması `03d5ff38cc` üzerinde yapıldı.
Aşağıdaki yollar uygulama başlangıcında W32'nin birleşmiş HEAD'inde yeniden kontrol edilmeli;
bu belge oyun dosyası değiştirmez.

- `games/tools/godotx_revenue_cat_src/README.md`: upstream Godot plugin 2.4.0,
  commit `60379736d2c63fb4d6cc9f024a1b24a7c8d8201c`; Android RC 10.14.1.
- **Android kaynakları mevcut:** `03d5ff38cc` Kotlin/Gradle dosyalarını ekliyor.
  `android/revenue_cat/src/main/java/com/godotx/revenuecat/RevenueCatPlugin.kt` içindeki
  `initialize(api_key, user_id, debug)` kullanıcı kimliği alıyor; `customerInfoDict` W23 işlem
  alanlarını üretiyor. `build.gradle.kts` RC ve RC UI için 10.14.1 kullanıyor.
  Reklam metotları henüz yok. Temiz checkout'tan yeniden derlenebilirlik sonraki uygulamada sınanmalı;
  upstream tag'i doğrudan kopyalayıp W23 değişikliklerini kaybetmek kabul edilmez.
- W23 korunacak davranış: `non_subscription_transactions`, gerçek
  `customer_info_changed` sinyali, pending consumable'ın tek seferlik teslimi.
- Poing GDScript köprüsü **zaten paid callback taşıyor**:
  `addons/admob/gdscript/src/api/{InterstitialAd,RewardedAd,AdView}.gd` içindeki `on_ad_paid`.
  `core/AdValue.gd`: `value_micros`, `currency_code`, `precision`.
  `core/ResponseInfo.gd`: `response_id`, `loaded_adapter_response_info`.
  Mevcut AAR'ın bu sinyali gerçekten ürettiği native testte doğrulanmalı.
- Oyun kodunda manuel `ad_impression` veya `on_ad_paid` bağlayan bir üretim çağrısı inceleme
  anında bulunmadı. Addon içindeki callback tanımları, olayın oyunda kullanıldığı anlamına gelmez.
- Travel SDK'sız değil: `package-lock.json` **purchases-capacitor 13.6.1** ve
  **capacitor-community/admob 8.2.0** sabitliyor; `src/iap/store.js` Purchases'ı configure ediyor.
- Corrupt: `src/game/Store.gd` üzerinden godot-iap + RC REST; Cinnamon:
  `game/scripts/autoload/store.gd` ayrı mağaza akışı. İkisi için mevcut RC SDK'lı oyunların köprüsünü
  eklemek ayrıca kimlik ve satın alma sahipliği tasarımı gerektirir.

Upstream Godot bağımlılık kanıtı:
[2.4.0 build.gradle.kts](https://github.com/godot-x/revenuecat/blob/60379736d2c63fb4d6cc9f024a1b24a7c8d8201c/source/android/revenue_cat/build.gradle.kts).

## Önerilen ortak köprü

Sonraki uygulama emrinde `games/tools/ads` altında kopyalanabilir bir `RcAdTracking.gd` ve Kotlin
eklentisine `track_ad_loaded`, `track_ad_displayed`, `track_ad_opened`, `track_ad_revenue`,
`track_ad_failed_to_load` metotları eklenir. Bunlar **önerilen yeni köprü adlarıdır**; bugün var değiller.
Her biri Dictionary kabul edip SDK'nın ilgili veri sınıfına dönüştürür.

Köprünün sözleşmesi:

1. RC bir kez, mevcut IAP kimliğiyle configure edilmiş olmalı. Her reklam için yeni müşteri veya
   Purchases nesnesi oluşturulmaz. Kimlik değişiminde eski reklam bağlamı yeni müşteriye aktarılmaz.
2. Callback'ler reklam nesnesi elde edilince, **show'dan önce** bağlanır. `on_ad_showed_full_screen_content`
   yerine gerçek `on_ad_impression` displayed olayını üretir; `on_ad_clicked` opened olayını üretir.
   Paid callback yalnız revenue üretir. Reward callback oyun ödülünü yönetir.
3. GDScript int → Kotlin Long dönüşümü kayıpsızdır. AdMob `valueMicros` zaten mikro birimdedir;
   tekrar 1.000.000 ile çarpılmaz veya RC'ye gönderirken bölünmez. Currency callback'ten gelir,
   sabit USD yazılmaz.
4. Precision eşlemesi açık tabloyla yapılır; enum ordinal eşitliği varsayılmaz:

   | AdMob | RC |
   |---|---|
   | UNKNOWN (0) | UNKNOWN |
   | ESTIMATED (1) | ESTIMATED |
   | PUBLISHER_PROVIDED (2) | PUBLISHER_DEFINED |
   | PRECISE (3) | EXACT |

5. `mediatorName=AdMediatorName.AD_MOB`; ağ adı, responseInfo'nun yüklenen adapter bilgisinden
   alınır. Ağ bulunamazsa nullable alan kullanılır; yalnızca AdMob mediatörü var diye gerçek
   ağ adı uydurulmaz.
6. Fullscreen reklamda `response_id` mevcutsa aynı reklamın loaded/displayed/revenue/opened
   olayları bu kimlikle ilişkilendirilir. Boşsa yükleme başına üretilen UUID saklanır.
   Yerleşim, reklam birimi kimliği değildir: ör. `offline_bonus`, `contract_retry`.
   Placement gösterim için seçilince bağlama sabitlenir. Gelecekte banner eklenirse yenileme başına
   yeni bağlam gerekir; bugünkü filoda banner eklenmez.
7. Yerel yeniden bağlantılar yüzünden aynı callback'in tekrar bağlanması engellenir.
   Aynı `(app_user_id, ad_unit_id, impression_id, event_type)` için aynı payload tekrar
   gönderilmez. Farklı tutarlı ikinci paid callback'i sessizce ezmek yerine tanılama kaydı üret;
   provider sözleşmesi incelenmeden düzeltme geliri uydurma. SDK'nın kendi taşıma/retry davranışı
   üzerine ayrı, sınırsız bir ağ kuyruğu kurulmaz.
8. RC hazır değilse oyun devam eder. Küçük/süreli bellek kuyruğu ancak müşteri kimliği aynı kalıyorsa
   kullanılabilir; taşma ve kayıp sayacı tutulur. Secret, purchase token veya reklam kimliği loglanmaz.
   Tracking kill switch reklam sunumundan ayrıdır; W32 `rewardedEnabled` davranışı korunur.

Micros ve listener zamanlaması:
[Google ILRD](https://developers.google.com/admob/android/impression-level-ad-revenue).
Precision eşlemesi:
[RC 10.14.1 resmî örnek](https://github.com/RevenueCat/purchases-android/blob/10.14.1/examples/vanilla-ad-tracker-sample/src/main/java/com/revenuecat/sample/vanilla/ui/ads/AdRevenuePrecisionMapping.kt).

**Alternatif adapter yolu:** Poing Kotlin tarafında yüklemeyi `loadAndTrack` ile sarmalamak
mümkün bir sonraki mimaridir. Native reklam nesnelerine erişim ve Poing fork'u gerektirir.
Seçilirse aynı reklam için manuel beş olay kapatılır; adapter + manuel gelir birlikte gönderilmez.
Mevcut Poing callback'leri hazır olduğu için ilk seçim manuel köprüdür.
[RC AdMob adapter davranışı](https://www.revenuecat.com/docs/ad-monetization/admob)

## Oyun başına iş

| Oyun | Entegrasyon noktası / yapılacak iş |
|---|---|
| Borderline / `smuggler` | RC + coddy_iap kimliğini kullan; CoddyAds interstitial ve mevcut rewarded callback'lerini ortak köprüye bağla; W23 teslim ledger'ını koru |
| Parts Rush / `aircraft` | `scripts/autoload/Ads.gd` ve `scripts/services/native_ads.gd`; gerçek mobil callback'leri bağla, mock sponsoru gelir sayma |
| Snow Empire / `skiresort` | RC/coddy_iap ve W32 sonrası rewarded akışı; cooldown/ödül ledger'ına telemetriyi bağlama |
| Money District / `moneytycoon` | `scripts/services/coddy_ads.gd`, `native_ads.gd`; bilgi/ipucu yerleşimleri; para ödülü veya yeni gelir modeli yok |
| Airport Baron / `airport-baron` | `scripts/autoload/Ads.gd` + RC Store; interstitial ve W32 rewarded; initialize tamamlanma koruması sürer |
| Train Baron / `train-baron` | `scripts/autoload/ads.gd` + RC Store; W32'nin yeni rewarded akışı; pending consumable claim mantığı korunur |
| Last Haven / `zombi` | Mevcut RC + Poing akışına köprü; açılıştaki reklam kararı W32'deki anlamıyla korunur |
| BlockForge / `lego` | RC var; W32 sonrası Families etiketli rewarded; `kids_mode=true` olduğunda hiç reklam/RC ad olayı yok |
| Trading Sim / `trading-sim` | `src/game/ads.gd` ve W7/W32 sonrası coddy_iap; gerçek rewarded birimi/akışı hazır olduktan sonra callback bağlantısı |
| Corrupt State / `corrupt-tycoon` | REST mağazasını koruyarak native RC analytics köprüsü seçeneği; aşağıdaki kimlik/observer koşulları gerekli |
| Travel Tycoon / `travel-agency-game` | Mevcut Capacitor RC singleton'ını kullanan küçük native köprü; `src/ads.js` + rewarded adapter callback'leri |
| Cinnamon / `cinnamon-cappadocia` | W32d sonrasında yalnız rewarded; Firebase ve AD_ID eklenmez. RC reklam telemetrisi için Families/veri toplama uyumu ayrıca doğrulanır; interstitial/banner yok |

Tüm oyunlarda yalnız `ad_free` sahibinin videosuz aldığı ödül **reklam gösterimi/geliri değildir**.
Ucuz paket alıcısının gerçekten izlediği reklam normal callback akışından ölçülür.
House tanıtımları üçüncü taraf reklam geliri olarak gönderilmez; mevcut CoddyAds ölçümü korunur.
RC reklam ödül doğrulaması/virtual currencies bu kapsamda açılmaz; W32 ödülünün iki kez verilmesine yol açılmaz.

### Corrupt ve Cinnamon

Corrupt için öneri: native RC SDK'sını mevcut public Android anahtarı ve
`user://revenuecat.json` içindeki **aynı** anonim app_user_id ile configure eden sınırlı köprü.
Satın almayı uygulama tamamlıyor (`PurchasesAreCompletedBy.MY_APP` / observer yaklaşımı);
godot-iap satın alma/acknowledge/consume işlemlerinin tek sahibi kalmalı.
SDK'nın otomatik receipt sync davranışı ve mevcut REST kuyruğuyla etkileşimi pilot öncesi incelenir.
İki Billing istemcisinin transaction bitirmediği, tek receipt'in iki hak vermediği test edilmeden yayımlanmaz.
[Observer mode](https://www.revenuecat.com/docs/migrating-to-revenuecat/sdk-or-not/finishing-transactions)

SDK eklendiğinde ücretsiz kullanıcıların da RC müşteri sayımına girmesi beklenir; W33'teki önceki
receipt temelli seriyle grafik karşılaştırmasında sürüm geçişi işaretlenmelidir.
Cinnamon için aynı seçenek ancak kendi mevcut kimlik deposu/REST akışı okunup Families uygunluğu
doğrulandıktan sonra değerlendirilir. Çocuk oyunu için “anonim” kimlik kullanmak tek başına veri
toplama uygunluğu kanıtı değildir. Bu belgede Firebase/AD_ID ekleme veya RC hesabına yazma yetkisi verilmez.

### Travel: yeni SDK kurmak gerekmiyor

13.6.1 Capacitor Android paketi `purchases-hybrid-common:19.3.1` kullanıyor; o sürümün kaynak
tablosunda Purchases **10.22.1** var. İncelenen Capacitor `src/definitions.ts` reklam takip
metotlarını sunmuyor. Dolayısıyla JS'de olmayan `Purchases.trackAdRevenue` çağrısı uydurmak yerine
mevcut native singleton'a ulaşan küçük Capacitor eklentisi önerilir. Gerçek Gradle resolution
sonraki derlemede doğrulanmalı; RC yeniden configure edilmez.
[Capacitor 13.6.1 Gradle](https://github.com/RevenueCat/purchases-capacitor/blob/13.6.1/android/build.gradle),
[JS API](https://github.com/RevenueCat/purchases-capacitor/blob/13.6.1/src/definitions.ts),
[hybrid-common 19.3.1 sürümleri](https://github.com/RevenueCat/purchases-hybrid-common/blob/19.3.1/android/gradle/libs.versions.toml).

AdMob 8.2.0 native kaynağı paid callback'i `AdImpression` olayına çeviriyor; payload
`valueMicros`, `currencyCode`, `precision`, `adUnitId`, `networkName`, `impressionId`.
Bu olay adı yüzünden onu hem paid gelir hem bağımsız ikinci impression olarak iki kez bağlamamak gerekir.
Görüntüleme/tıklama callback'lerinin JS yüzeyinde ayrı erişimi uygulama sırasında doğrulanır;
yoksa native köprüde eklenir, “show çağrıldı” olayı gerçek impression yerine kullanılmaz.
[AdMob interstitial callback kaynağı](https://github.com/capacitor-community/admob/blob/v8.2.0/android/src/main/java/com/getcapacitor/community/admob/interstitial/InterstitialAdCallbackAndListeners.kt),
[paid payload](https://github.com/capacitor-community/admob/blob/v8.2.0/android/src/main/java/com/getcapacitor/community/admob/models/AdMobRevenueData.kt).

## Firebase ile çift sayımı önleme

Kod taraması manuel Firebase `ad_impression` üretimini göstermedi; bu, otomatik olay olmadığı
anlamına gelmez. Firebase/Analytics ile bağlanmış AdMob uygulamalarında SDK otomatik
`ad_impression` kaydeder. Canlı hesap bağlantısı ve DebugView bu araştırmada denetlenmedi.
[Firebase resmî davranış](https://firebase.google.com/docs/analytics/measure-ad-revenue)

Bu yüzden paid callback'ten **RC'ye** gönderim eklenir; aynı callback'ten ayrıca Firebase
`ad_impression` yazılmaz. Firebase'in otomatik yolu korunur. RC ve Firebase iki ayrı raporlama
hedefidir; bunların parasal toplamları “toplam gelir” diye toplanmaz.
RC'nin gelir olaylarını Firebase'e geri taşıyan başka bir entegrasyon varsa uygulama öncesi
kontrol edilir; aynı ad revenue olayı iki yönden yeniden içeri alınmaz.

W32 oyun olayları `rewarded_offer_shown`, `rewarded_offer_accepted`, `rewarded_completed`,
`rewarded_skipped_paid_user` ayrı oyun davranışıdır. Bunlara reklam gelir tutarı eklenmez.
`rewarded_skipped_paid_user` RC ad olayı üretmez. Cinnamon'da Firebase olmadığı için bu
çalışma Firebase kurmaz; reklam ölçümü AdMob/uygunluğu doğrulanmış RC ile sınırlı kalır.

## Uygulama sırası ve kabul ölçütleri

1. W32 ve reklamsızlık kararlarının birleşmiş HEAD'ini sabitle; W23 Kotlin kaynaklarının
   `03d5ff38cc` değişikliklerini içerdiğini ve temiz checkout'tan yeniden derlenebildiğini göster.
2. Proje başına RC Ad Monetization/Charts v3 erişimi ve AdMob ILRD durumunu kontrol et.
   Açma/bağlama değişiklikleri sonraki yetkilendirilmiş uygulama emrine aittir.
3. Ortak köprü ve callback eşleyicisini hazırla; tek pilot üzerinde teknik doğrulama yap.
   Bu, sahibin tüm filo açılışı kararını değiştiren yüzdesel rollout değildir.
4. Her oyunun reklam/RC kimlik bağlantısını ayrı commit ile ekle. Oyunlar arası RC app key
   karışmasını kontrol et. Secret anahtar istemciye girmez.
5. Aşağıdaki testler ve yayın süreci sonraki emirde uygulanır; W34'te yapılmadı.

| Kabul testi | Beklenen kanıt |
|---|---|
| Değer/precision | 5.000 micros + gelen currency aynı kalır; dört precision eşlemesi; Kotlin Long taşması/kırpılması yok |
| Bir gösterim | Bir loaded, bir gerçek displayed, bir paid revenue; tıklama yoksa opened yok |
| Tekrar callback | Aynı listener iki kez bağlanamaz; aynı paid olayın yeniden iletimi iki gelir yazmaz |
| Ödül ayrımı | Earned ödülü bir kez; dismiss tek başına ödül vermez; ad_free videosuz ödül RC ad olayı üretmez |
| Hata/offline | RC kapalı/erişilemezken oyun, IAP, reklam ve ödül çalışır; bellek/kuyruk sınırlı |
| W23 regresyonu | Pending consumable sonrası teslim, restart, restore ve customer_info_changed tekrarları hak çoğaltmaz |
| Kimlik | IAP ve reklam aynı RC müşteri kimliğinde; logout/veri silme eski bağlamı yeni kullanıcıya taşımaz |
| Firebase | DebugView/raporda otomatik ad_impression korunur, manuel kopya yok; Cinnamon'a Firebase eklenmemiş |
| Kids/kill switch | lego kids_mode ve W32 rewardedEnabled/ad_free kuralları korunur; house/mock/test ödülü gerçek gelir değildir |
| Native/panel | Yeni AAR'ın paid sinyali, RC Ad Events ve test trafiği ayrımı doğrulanır; test reklamlarda 0/UNKNOWN mümkün |

Yeni Kotlin/Capacitor köprüsü native güncelleme ister. Yayın için Patron'un ayrı build-only → tam
commit'te smoke PASS + son kare → onaylı yükleme zinciri geçerlidir. Bu görevde build, emülatör,
simülatör, sürüm artışı veya mağazaya gönderim yapılmadı.

## Risk ve açık noktalar

- **Kaynak/binary eşliği:** W23 Android kaynakları git'te mevcut; yeni derleme, dağıtılan
  AAR'ın W23 davranışını koruduğunu testle göstermeli.
- **Beta/hesap erişimi:** public API'nin mevcut olması her projede Ad Charts'ın açık olduğunu kanıtlamaz.
- **Finansal mutabakat:** callback tutarı ile AdMob'un sonradan düzeltilmiş kesin raporu farklılaşabilir;
  RC görüntüsü gerçek zamanlı analiz, AdMob raporu mutabakat için kullanılır.
- **Bağımlılık uyumu:** RC/UI/export preset/GMA sürümleri, R8 ve mevcut .aar tek paket olarak doğrulanmalı.
- **SDK'sız oyunlar:** observer/satın alma sahipliği ve anonim kimlik geçişi ayrı regresyon riski taşır.
- **Çocuk oyunları:** mevcut Families/consent politikası korunmalı; Cinnamon Firebase ve AD_ID yasağı sürer.
- **Gözlemlenebilirlik:** canlı paid callback ve RC ad grafiği bu salt araştırmada çalıştırılmadı.
  “Entegre edildi” veya “gelir ölçümü testten geçti” sonucu çıkarılamaz.
