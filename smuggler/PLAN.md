# Smuggler — eksikler, yapılanlar ve yayın planı

## Güncel yayın çalışması — 13 Ekim 2026

Yeni Android sürümü **1.1.0 (1791874686)** üretildi. Güncel doğrulamalar ve açık yayın engelleri: [Google Play release report](docs/PLAY_RELEASE_2026-10-13.md). Aşağıdaki 9 Ekim notları tarihsel durumdur; canlı ürün/anahtarlar sonraki oturumlarda oluşturuldu. Eski **1791611984** production için kullanılmayacak.

Güncelleme: 9 Ekim 2026. Oyun adı: **Borderline Empire**.

## Son oturum / handoff
Çalışma sahibinin isteğiyle sonlandırıldı. Güncel kapsam, kanıtlar ve kalan işler: [HANDOFF.md](HANDOFF.md). Kayıt/offline düzeltmeleri, günlük görevler, Dot tavsiyesi ve mobil UI iyileştirmeleri ana klasöre aktarıldı. Kalite testi 60/60; masaüstü UI 19/19 senaryo (reset testi sınırlaması handoff içinde); dil kontrolü 595 × 10. Önceki oynanış koşusu 218/218, son kaynakta tam tekrar gerekli. Android debug çıktı alındı; iOS arşivi hesap/provisioning nedeniyle başarısız. Aşağıdaki eski kanıtlar tarihsel kontrol noktalarıdır, son sürümün cihaz doğrulaması değildir.

Son inceleme ve sonraki uygulama turu için İngilizce düzeltme/özellik planı:
[FINAL_REVIEW_AND_IMPROVEMENT_PLAN.md](FINAL_REVIEW_AND_IMPROVEMENT_PLAN.md).
Bu yeni incelemede ek sorunlar bulundu; önceki test geçişleri bu maddeleri tamamlanmış saymaz.

## Başlangıçta ne durumdaydı?
Proje boş değildi: Godot tabanlı lojistik/tycoon oyunu; 8 bölge, 10 araç,
sözleşmeler, risk, otomasyon, prestij ve çevrimdışı kazanç sistemi vardı.
İlk testte 76 kontrol geçti. Eksikler ağırlıklı olarak mobil arayüz, dil kapsamı,
kayıt güvenliği, onboarding ve gelir modeli/yayın hazırlığındaydı.

## Tamamlanan yerel işler
- [x] AGENTS.md oluşturuldu; proje standartlarının asıl kaynağı CLAUDE.md'ye bağlandı.
- [x] İki iş paketi için spec/clarify/plan/tasks/analyze belgeleri ve uygulama kapısı.
- [x] Base ile aynı 10 dil: EN, TR, DE, ES, FR, JA, KO, PT, RU, ZH.
- [x] Eksik çeviriler NitroRouter API üzerinden gpt-6-luna ile tamamlandı.
      595 metin × 10 dil; biçimlendirme değişkenleri ve kaynak kapsamı doğrulandı.
      Model yanıt raporları docs/translation-api-*.json içinde.
- [x] Dil seçici, CJK/Kiril font desteği, açık/koyu/sistem görünümü.
- [x] En az 88×88 mantıksal dokunma alanları; renk seçimi ve kamera kontrolleri büyütüldü.
- [x] Altı adımlı animasyonlu giriş, ilerleme göstergesi, belirgin devam düğmesi,
      şirket adı/renk seçimi ve on adımlı oynanabilir öğretici.
- [x] Klavye açılınca anlatım kartı yukarı taşınıyor; alan ve devam düğmesi görünür.
- [x] Kayıtlar QA'dan ayrıldı; bozuk kayıt sağlam yedeği ezmiyor; kayıt hatası dürüst gösteriliyor.
- [x] Arka planda oyun tikleri durduruldu; çevrimdışı kazançla çift sayım önlendi.
- [x] Blender 5.2.2 ile 82 GLB yeniden üretildi; Godot yükleme kontrolleri geçti.
- [x] Ödüllü reklam ve tek seferlik Empire Pass altyapısı, mağaza ekranı, geri yükleme,
      doğrulanmış hak kontrolü, ödül tekilleştirme ve 10 dakikalık bekleme süresi.
- [x] Empire Pass: teslimat gelirine %25 ek; reklam izlemeden sponsor bonusu;
      prestij/ilerleme sıfırlamasından etkilenmeyen sahiplik.
- [x] Reklam iptali, hatalı/tekrarlı ödül ve doğrulanmamış ödeme hak vermiyor.
- [x] Android AdMob/RevenueCat eklentileri; iOS RevenueCat paketleri yerelde hazır.
      Büyük iOS paketleri git dışı; tools/native/setup_ios.sh checksum ile yeniden kurar.
- [x] CI, çevrimdışı dil doğrulayıcısı, oynanış testleri ve telefon ekranı QA aracı.

## Doğrulama kanıtları
- Godot 4.7.2: **218 kontrol, 0 başarısızlık**. Bozuk JSON testlerinin beklenen
  tanılama mesajları dışında GDScript çalışma/derleme hatası yok.
- UI raporu: docs/qa/report.json; 432×864 telefon boyutunda **80/80 kontrol geçti**
  (10 dil × 8 panel). Rusça üst alan ve kamera düğmesi taşmaları düzeltildi.
- Gerçek UI eylemleri: sözleşme kabulü → sevkiyat → teslimat → motor yükseltme geçti.
  Hem EN/koyu hem TR/açık akış kontrol edildi.
- Android 35 Pixel 7 emülatöründe native APK açılışı, giriş ve klavye yerleşimi görüldü.
  10 dilli native smoke paketinde eksik kaynak/GDScript hatası yok; emülatör kesintisinden kalan
  bozuk kayıt yedekten kurtarıldı. Yeniden açılış kayıt hatası vermedi.
  Son kamera görünürlüğü düzeltmesi masaüstünde kontrol edilip APK yeniden üretildi;
  bu son kozmetik değişiklikten sonra emülatör testi tekrarlanmadı.
- Test APK: build/BorderlineEmpire-debug.apk. SHA256 ve test özeti docs/verification.json içinde.
- Blender üretimi tamamlandı; model envanterinin tamamı test ediliyor.
- Ekran görüntüleri docs/qa altında yerel kanıttır; paketlere/git'e dahil edilmez.

## Sonraya bırakılan canlı gelir kurulumu — sahibin açık kararı
Base skill referansları, kod incelemesi ve somut tamamlama sırası:
[RevenueCat durum raporu](docs/revenuecat-status.md).

- [ ] Uygulamaya özel AdMob Android/iOS app ID ve rewarded unit oluşturulacak.
      Mevcut Google örnek kimlikleri geliştirme içindir. Zorunlu/geçiş reklamı yok.
- [ ] Smuggler'a özel RevenueCat projesi, public SDK anahtarları ve mağaza bağlantıları.
      Başka uygulamanın kimliği/anahtarı kullanılmadı.
- [ ] App Store / Play üzerinde empire_pass non-consumable ürününü oluştur,
      RevenueCat empire_pass entitlement'ına bağla, bölgesel fiyatları doğrula.
- [ ] Sandbox satın alma, bekleyen/iptal ödeme, yeniden kurulumda geri yükleme,
      iade/hak geri alma ve offline sahiplik davranışlarını iki platformda test et.
- [ ] Native reklam onayı, gizlilik seçenekleri, tamamlanan/erken kapatılan reklam ve
      ağ kesintisi testleri. iOS ATT/SKAdNetwork ve gizlilik bildirimlerini son SDK ile doğrula.

## Yayına hazır olmak için kalan zorunlu işler
1. [x] Bundle kimliği: com.coddykit.borderlineempire (ASC 6821004024, Play 4974066353564376708; 9 Ekim 2026).
2. iOS signing team/provisioning ve Android release keystore'u yapılandır.
3. İmzalı Android AAB ve iOS arşivi üret; gerçek iPhone/Android cihazda safe-area,
   geri tuşu, pause/resume, düşük seviye GPU, uzun oturum ve termal test yap.
4. Gerçek reklam/IAP test kanıtını docs/native-sandbox-verification.json içine yaz.
   tools/check_release.py eksik kimlik/test olduğunda bilerek başarısız olur.
5. Kamuya açık gizlilik politikası URL'si, destek iletişimi, yaş derecesi ve mağaza
   veri beyanlarını tamamla. Store metinlerini son gelir modeline göre son kez denetle.
6. Yeni dillerde seslendirme istenirse ayrı onaylı ücretli üretim yap. Şu anda EN/TR
   sesleri var; diğer diller çevrilmiş yazı kullanır, yanlış dilde ses oynatılmaz.
7. İç dağıtım sonrası kamuya yayın/review için ayrıca açık yayın talimatı gerekir.

## Hazırlık tanımı
Yerel uygulama iyileştirmeleri ile mağazaya gönderilebilir sürüm aynı şey değildir.
Canlı gelir kurulumu sahibi tarafından ertelendi; imzalama ve gerçek mağaza/cihaz
kanıtları tamamlanmadan bu sürüm “%100 yayına hazır” olarak işaretlenmez.

## Firebase kaydı — 9 Ekim 2026
- [x] Sahibin belirttiği `coddykit-game` projesinde Android ve iOS uygulamaları oluşturuldu.
- [x] Görünen ad: Borderline Empire (Smuggler); iki platform kimliği: `com.coddykit.borderlineempire`
      (9 Ekim 2026'da `com.borderlineempire.game`'den geçildi; eski Firebase app'leri kullanılmıyor).
- [x] `google-services.json` ve `GoogleService-Info.plist` indirildi; proje/kimlik eşleşmesi
      ve Firebase kayıtlarının ACTIVE durumu API üzerinden tekrar doğrulandı.
- Kanıt: `docs/firebase-registration.json`.
- [x] Native Firebase Core/Analytics entegrasyonu yapıldı; QA analitik göndermez.
- Önceki Android çalışma kanıtı: `docs/firebase-runtime-evidence.txt`. Son debug APK için
  yeni cihaz testi yapılmadı. iOS Analytics ve mağaza veri beyanları açık yayın kontrolleridir.
- Ek ayrıntı (9 Ekim 2026 autostash çalışması):
- Native Firebase Core + Analytics entegrasyonu 9 Ekim 2026 tarihinde eklendi;
  Android/iOS export ayarları ve Android google-services Gradle bağlantısı yapıldı.
- GA4 property `557673345` (coddykit-game, proje genelinde tek property); yeni
  `com.coddykit.borderlineempire` app'lerinin stream'leri: Android `16096275075`, iOS `16096334095`
  (Firebase Management API `analyticsDetails`, 9 Ekim 2026). Eski `com.borderlineempire.game`
  stream'leri (`16095545337` / `16095538108`) kullanılmıyor.
- [ ] Cihazda Firebase/DebugView testi bekliyor: `docs/FIREBASE_DEVICE_TEST.md`.
- `analytics_storage` açık; `ad_storage`, `ad_user_data`, `ad_personalization` kapalı.
  QA oturumları hariç. Oturumlara ek olarak game_ready, screen_view, delivery_completed
  ve tutorial_complete olayları bağlı. Şirket adı/kayıt/ödeme bilgisi gönderilmez.
- Gizlilik açıklaması API ile 10 dile çevrildi (o tarihte toplam 565 metin; güncel 595).
- Yayın öncesi App Privacy ve Play Data Safety analytics beyanlarının kanıtı zorunlu;
  `tools/check_release.py` bunu da denetler. iOS native cihaz doğrulaması halen gerekli.

### Analytics native doğrulama sonucu
Android emülatöründe Firebase Core/Analytics başlatıldı; `first_open`, `session_start`
ve `game_ready` olayları görüldü. Google Analytics yüklemesi HTTP 204 ile onaylandı.
Reklam izinleri kapalı, analytics storage açık olarak logdan doğrulandı.
Kanıt: docs/firebase-registration.json. Bu sonuç iOS cihaz testi yerine geçmez.
