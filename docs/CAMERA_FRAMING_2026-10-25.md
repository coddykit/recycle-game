# W66 — aktif bölge kamera çerçevesi (C3 / emir #8)

Baz: `4c1ffeb55b6b1d8836feb611851f7fe65fe6b700`. Kanıt klasörü: `/Volumes/BuildSSD/game-builds/w66/smuggler/camera-1025/`.

## Değişiklik

Başlık ekranının sabit 95 mesafesi ve HQ ofseti aktif yerleşimi ekranın yalnız yaklaşık %38 genişliğine sıkıştırıyordu. Başlık, Tap to start, onboarding harita adımı/bitişi, kayıtlı imparatorluğun açılışı ve bölge açılışı artık aynı çerçeveyi kullanır. Açık bölgelerin gerçek bina mesh köşeleri, garaj, depo ve yol durakları perspektifte merkezlenir. Kilitli arazi/deniz bu hesaba girmez; mevcut kilitli bölgeler komşu kenarlarda görünür.

Hedef yatay pay %80, dikey güvenli alan %18–82. Uzun geç oyun haritasında dikey sınır önceliklidir; sekiz bölgeyi aynı anda göstermek için yatay doluluk %80’in altına düşer. Uzaklaşma sınırı tam aktif çerçevedir; gereksiz ilave %10 uzaklaşma kaldırıldı. Yakınlaşma 14’e kadar; kaydırma yalnız açık bölgelere bağlı ve uzak görünümde merkeze daralır. Dokunma bir kamera tween’ini iptal eder; kaydırma ataleti korunur. Kullanıcının kaydırdığı görünüm ekran oranı değişince zorla sıfırlanmaz. Fit yalnız açılış/bölge/bina/viewport olaylarında hesaplanır; her kare sahne taraması eklenmez.

## Dikey önce/sonra

Gerçek Godot 4.7.2 Compatibility viewport’ları: her ölçüde 1 / 2 / 8 açık bölge, garaj 1 / 3 / 5, sonraki açık bölgelerde depo. Sabit seed 6601025. `tests/camera_evidence.gd` üretim World ve HUD kullanır. Başlığın pop-in animasyonu beklenir. Statik A/B için dünya/işçi zamanlayıcıları dondurulur, hareketli trafik ve oyuncu araçları gizlenir; canlı filo ayrıca normal perf kapısında ölçülür.

`before/<ölçü>/<1|2|8>.png` ve `after/...`: başlık; `*-world.png`: aynı kadrajın HUD’suz ham dünya karesi; `*-play.png`: gerçek Tap to start sonrası HUD. Toplam **36 ana önce/sonra PNG**. `gallery.html` yan yana karşılaştırma. Ham JSON’lar aktif bina/yol bbox ölçülerini ve 3D sayaçlarını içerir.

“Alt boş deniz” = kameradan zemine atılan 72 × (yükseklik/10) örnek ışınla, en alt kenardan başlayıp ilk adaya kadar tamamen deniz kalan satırlar. Köpük/tekne/köprü bu arazi ölçüsüne dahil değildir. “Deniz toplamı” tüm karedeki ada dışı zemin payıdır; HUD’den arındırılmıştır. Aktif genişlik gerçek bina/yol bbox’ıdır, adanın boş arazi köşeleri değildir.

| Boyut | Açık bölge | Mesafe önce → sonra | Aktif genişlik önce → sonra | Alt boş deniz önce → sonra | Deniz toplamı önce → sonra |
|---|---:|---:|---:|---:|---:|
| 720x1280 | 1 | 95.00 → 43.66 | %37.8 → %80.0 | %25.0 → %0.0 | %62.0 → %24.5 |
| 720x1280 | 2 | 95.00 → 115.75 | %87.4 → %80.0 | %25.0 → %19.5 | %62.0 → %57.6 |
| 720x1280 | 8 | 95.00 → 263.20 | %94.3 → %43.3 | %25.0 → %14.1 | %62.0 → %80.4 |
| 720x1560 | 1 | 95.00 → 43.66 | %37.8 → %80.0 | %29.5 → %1.3 | %64.9 → %29.8 |
| 720x1560 | 2 | 95.00 → 115.75 | %87.4 → %80.0 | %29.5 → %25.0 | %64.9 → %62.1 |
| 720x1560 | 8 | 95.00 → 206.69 | %94.3 → %56.9 | %29.5 → %13.5 | %64.9 → %75.3 |

Açılış içerik merkezi her iki boyutta `(0.500, 0.500)`; mesafe 95 → 43,66 (mesafe oranı ≈2,18; perspektifte her evin piksel oranı konumuna göre değişir). İlk alanın evleri ve depo artık okunur. **Geç oyunda tüm harita özeti**, eski HQ’ya bakan sabit açılışla aynı görünür alan değildir: önceki kadrajın dışındaki bölgeler de görünür. Bu nedenle geç oyun deniz toplamının ve draw sayısının yükseldiği satırlar saklanmadı; bu görünümün amacı tüm imparatorluğu göstermek, yakın detay için pinch kullanılabilir.

## Düşük maliyet

| Boyut | Açık bölge | 3D prim önce → sonra | 3D draw önce → sonra | Aynı son kamera: baz prim/draw → yeni prim/draw |
|---|---:|---:|---:|---:|
| 720x1280 | 1 | 32510 → 20030 | 78 → 41 | 20030/41 → 20030/41 |
| 720x1280 | 2 | 39746 → 44898 | 89 → 107 | 44898/107 → 44898/107 |
| 720x1280 | 8 | 62756 → 79764 | 86 → 129 | 79764/129 → 79764/129 |
| 720x1560 | 1 | 40854 → 24082 | 100 → 49 | 24082/49 → 24082/49 |
| 720x1560 | 2 | 48090 → 49690 | 111 → 132 | 49690/132 → 49690/132 |
| 720x1560 | 8 | 79524 → 79764 | 110 → 129 | 79764/129 → 79764/129 |

Son sütun: `before-fixed/` baz kodunu yeni kameranın tam target/mesafe/uzak düzlemiyle çalıştırır. Aynı görünür sahnede ek draw/prim/pas/doku **0**; model, shader veya kalite politikası değiştirilmedi. Açılış zoom’u görünür nesne sayısını azaltır. İki/sekiz açık bölge özeti daha geniş içeriği gösterdiğinden eski sabit HQ kadrajından pahalı olabilir; normal Low kapısı ayrıca zorunludur.

## Diğer oyunlar — ölçüm, düzeltme yok

İki dikey boyutta aynı başlangıç durumu; `audit/<oyun>-<boyut>.png/.json`. Final audit koşuları ayrı `C3-camera-e8-<oyun>` yerel kullanıcı klasöründe, Dummy sesle. İlk keşif koşuları mevcut perf hook’larının yerel QA kullanıcı klasörünü kullandı; ayar/kayıt izolasyonu fark edilip final koşular ayrıldı. Mağaza/DB yazması yok. Zombi perf hook’unun stres mesafesi 120 yerine üretim `world.rebuild_all` başlangıcı 84 kullanıldı. Airport `frame_home()`; Money gerçek başlangıç; Trading tier0. Bbox payı boş piksel yüzdesi değildir: yollar, surlar, parklar ve HUD bbox dışında kalabilir.

| Oyun | Ölçülen kamera | 720×1280 / 720×1560 | Görsel bulgu / sonraki iş |
|---|---|---|---|
| Zombi / Last Haven | Mesafe 84, tavan 150; odak (-2,0,10) | Kurulu bina bbox’ı görünür genişlik %100 / %100; yükseklik %54,1 / %54,1; düşey aralık %12,8–66,9 | Aynı uzak kamera/deniz sorunu yok; binalar yatay kenarlardan taşabiliyor. Alt kısım yol/sur/ön arazi; üst binaların bir kısmını HUD örtüyor. Ayrı odak/HUD incelemesi yapılabilir. |
| Airport | `frame_home` mesafe 99,98; uzaklaşma tavanı 352,60 | Aktif terminal/apron bbox genişliği %69,0 / %69,0; düşey aralık %34,4–64,2 / %37,2–61,7 | Dikeyde belirgin: üst yaklaşık üçte bir ufuk/çim, altta geniş pist/çim. Terminal okunur fakat çerçeve çevreye çok pay bırakıyor; bu dörtlüde ilk düzeltme adayı. Otomatik yön destekli oyunun burada özellikle dikey hali ölçüldü. |
| Money District | Ortho genişlik 44, tavan 63,2; odak (44,0,44) | Görünür bina mesh bbox genişliği %59,0 / %59,0; düşey pay %32,6 / %26,7 | Deniz/boş ekran bandı yok; yol, satış parselleri ve parklar görünümü dolduruyor. Bina bbox’ının sağ kısmı ekran dışında; ev/ofis odağı ayrı iyileştirme konusu olabilir. |
| Trading | Kamera–bakış mesafesi 3,165; FOV 50° | Ofis SubViewport 720×486 / 720×592, iki ekranda da yaklaşık %38 yükseklik; oda görünür bbox %100×%100 | Uzak kamera/boş arazi sorunu yok. Kalan %62 gerçek piyasa/hedef/liste arayüzü; boşluk sayılmadı. |

## Test durumu

- `camera_frame_test.tscn`: 41 kontrol, 0 hata (iki oran, gerçek 1→8 unlock, yeniden kurulan dünya, başlık/Tap to start, onboarding map, gerçek pinch uçları, pan sınırı, tween iptali/atalet).
- İlk tam `check.sh`: fonksiyonel testler geçti; eski +%10 uzaklaşma payıyla son Low sahne 151 draw/150 sınırı nedeniyle başarısız oldu. Bu hata düzeltilmeden tamamlandı denmedi.
- Son tavanla bağımsız `lategame_far`: 82855 prim, 88 draw, 104 MB; PASS. Final `check.sh` (rebase öncesi) exit 0; canlı Low açılış 21508 prim/42 draw, orta 33948/41, geç 83079/91; statik 98/101/103 MB. Rebase sonrası bağımsız sekme kontrolü Mac 77 ms / telefon 62 ms PASS; tam pakette eşzamanlı host yükü nedeniyle 117 ms ve 273 ms eşik aşımları görüldü. Patron #10 (11:42): önceki tam PASS ile teslim, C2 kare-temelli kapısı main’e gelince yalnız Smuggler check.sh tekrar. Son kit eşitlemeli koşu bu emir üzerine durduruldu; bu koşu PASS sayılmadı.
- Masaüstü sanat/kamera/perf kanıtı; Android release smoke veya cihaz FPS/PSS kanıtı değildir. Sürüm/AAB/yükleme/emülatör/ücretli üretim/ikon değişikliği yok.

Rebase notu: güncel ana dalın `check.sh` genel engine-ERROR kapısı kasıtlı bozuk-kayıt fixture’larının `JSON.parse_string` hata logunu reddetti. `SaveManager._read`, kontrollü `JSON.new().parse()` sonucuyla aynı `{}`/yedekten kurtarma akışını korur; bozuk JSON için engine ERROR üretmez. Mevcut `test_runner` kurtarma/bozuk-yedek/ikinci hasar koruma kontrolleri tekrar PASS; `save_unreadable` sinyali ve kurtarma uyarısı korunur. Kontrol kapısı gevşetilmedi. Ortak `ui_shop` kopyası da tek satırlık line-spacing farkı için eşitlendi.

Ortak UI kiti `4fc6894aea` ile eşitlendi (3 dosya). Kamera kanıtı dünyayı ölçer; bu eşitleme kamera/3D geometriyi değiştirmez.
