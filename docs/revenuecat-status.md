# RevenueCat — doğrulanmış durum ve tamamlama sırası

9 Ekim 2026. Bu belge kaynak kod incelemesidir; gerçek mağaza satın alma testi değildir.

## Base referansları

- [new-app-bootstrap](../../../base/.claude/skills/new-app-bootstrap/SKILL.md): uygulamaya özel proje, platform kayıtları ve SDK anahtarları.
- [revenuecat-api](../../../base/.claude/skills/revenuecat-api/SKILL.md): projeye özel V2 API ve ürün tipi doğrulaması.
- [subscription-setup](../../../base/.claude/skills/subscription-setup/SKILL.md): iki mağazada tek seferlik ürün kurulumu.

Smuggler Godot kullanır; base'in React Native katmanı doğrudan kopyalanmaz.
Godot RevenueCat adaptörü aynı satın alma/geri yükleme sorumluluğunu taşır.

## Kodda mevcut

- Native eklenti ve platforma göre SDK başlatma.
- `empire_pass` ürününü sorgulama, mağazanın yerel fiyatını gösterme ve satın alma.
- Geri yükleme, iptal, bekleyen işlem ve zaman aşımı yönetimi.
- Satın alma callback'i tek başına hak vermez; eşleşen entitlement aranır.
- İlerleme sıfırlamasından ayrı sahiplik önbelleği.

## Eksikler

- `data/monetization.json` içindeki iki public SDK anahtarı boş. Bu durumda gerçek satın alma başlatılmaz.
- Uygulamaya özel RevenueCat proje/mağaza/ürün bağlantısının doğrulama kanıtı yok.
- `_on_entitlement(false)` mevcut sahipliği kaldırmıyor. Çevrimdışı erişimi koruyor,
  ancak iade/hak iptali için sunucudan güncel ve başarılı yanıtı önbellek/ağ
  hatasından ayıran bir akış gerekli. Bu tamamlanmadan iade desteği hazır sayılmaz.
- Android ve iOS sandbox satın alma/geri yükleme/iade kanıtı yok.

## Tamamlama sırası

1. [x] `com.coddykit.borderlineempire` için mağaza (ASC 6821004024, Play) ve RevenueCat
   (proj134e4759) kayıtları 9 Ekim 2026'da oluşturuldu.
2. Yalnız public SDK anahtarlarını oyun yapılandırmasına yaz; yönetim anahtarlarını
   ve mağaza özel anahtarlarını export edilen kaynaklara koyma.
3. İki mağazada `empire_pass` kalıcı ürünü oluştur; fiyatları ve satın alınabilirliği doğrula.
4. RevenueCat ürünlerini iki platformda da `non_consumable` olarak tanımla;
   GET ile `one_time.is_consumable == false` kontrol et. `empire_pass`
   entitlement'ına bağla; offering kullanılacaksa `$rc_lifetime` paketini kur.
5. Güncel sunucu sonucuna dayalı hak iptalini uygula; ağ hatasında önbelleği koru.
6. İki platformda satın alma, iptal/bekleyen işlem, yeniden kurulumdan geri yükleme,
   iade ve çevrimdışı davranışı doğrula; kanıtları yayın kontrolüne bağla.

Canlı gelir kurulumu kullanıcının önceki açık talimatıyla ertelenmiştir.
Base skill dosyalarının bulunması, bu kurulumun yapılmış olduğu anlamına gelmez.
