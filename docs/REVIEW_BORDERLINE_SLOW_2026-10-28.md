# REVIEW-BORDERLINE-SLOW — 28 Ekim 2026

Play raporu: TR, 2 yıldız, “Nice game, but it's too slow.” Raporun sürüm alanı **unknown**;
canlı sürüm 1.4.0 bilgisi iş emrinden geliyor. İnceleme, mağazadaki binary üzerinde değil,
`origin/main` kaynak kodu üzerinde yapıldı. Kaynak başlangıcı JSON raporlarındaki `baseline_commit` alanında.

## Ölçüm

Oyunun kendi `tests/test_runner.gd` botu ve gerçek Godot yöneticileri kullanıldı. Beş seed
(12345, 42, 2026, 701, 908), günlük görev ödülü açık/kapalı: her tarafta 10 koşu.
İlk 30 dakika ayrıntılı, mevcut ekonomi kapıları için toplam 60 dakika simüle edildi.
Reklam/IAP yok; test kayıtları her koşuda ayrı geçici HOME içinde.

Eski ölçümde `sim(1.0)` float birikimi yüzünden 11 × 0,1 saniye ilerliyordu. Yeni araç tam
10 tik ilerletiyor. Her iki taraf bu düzeltilmiş saatle ölçüldü. Pacing botu ilk teslimattan
önce harcama yapmayarak öğretici sırasını izliyor; sonra mevcut yatırım/biriktirme politikasını kullanıyor.
Günlük ödül sesinin rastgele perde seçimi gerçek zamanlı bir cooldown kullandığından, bot ölçümünde ses geri bildirimi askıya alındı. Böylece makine yükü ekonomi RNG akışını değiştirmiyor; önce ve sonra aynı sessiz ayarla yeniden ölçüldü. Seed 12345 + günlük ödüllü koşunun tüm rapor satırı ayrı süreçte bire bir tekrarlandı.
Dolayısıyla eski sürümdeki `docs/pacing-quality.json` ile doğrudan zaman karşılaştırması yapılmamalı.

- **Zorunlu boşluk:** sevkiyat, olay kararı veya satın alınabilir temel ilerleme eylemi bulunmayan
  ardışık saniyeler. Para harcayıp teklif yenilemek veya kuyrukta kart taşımak ilerleme sayılmıyor.
- **Bot eylem aralığı:** botun bu olanakları kullanmayıp para biriktirdiği süreyi de içeriyor.
  Oyuncunun kilitlendiği süreyle aynı şey değil.
- **Yeni rota:** daha önce kullanılmamış kaynak–hedef çifti; **ilk açılım:** L2 garaj.
  Yeni bölge ayrıca ölçülüyor.
- Süreler oyun saniyesi, 1 saniyelik karar örneklemesi; insan/cihaz akıcılığı ölçümü değil.

## Önce / sonra

Medyan (en yavaş koşu); “en uzun” satırları bütün koşuların maksimumudur.

| Ölçüt | Önce | Sonra |
|---|---:|---:|
| İlk teslimat | 21 (21) s | 14 (14) s |
| İlk araç yükseltmesi | 21 (21) s | 14 (14) s |
| İlk farklı rota | 32 (32) s | 22 (22) s |
| İlk açılım: L2 garaj | 239 (280) s | 178 (229) s |
| İkinci araç | 269 (300) s | 211,5 (257) s |
| İlk yeni bölge: Green Republic | 437 (503) s | 368,5 (520) s |
| Auto Accept | 1319,5 (1536) s | 1253 (1576) s |
| En uzun zorunlu boşluk, ilk 15 dk | 19 s | 12 s |
| En uzun zorunlu boşluk, ilk 30 dk | 19 s | 12 s |
| En uzun bot eylem aralığı, ilk 15/30 dk | 41 s | 31 s |
| İlk sözleşme gidiş / dönüş | 20 / 8,25 s | 12 / 6 s |
| İlk 30 dk gidiş süresi: koşu medyanlarının medyanı | 28 s | 27,75 s |
| İlk 30 dk dönüş süresi: koşu medyanlarının medyanı | 8,35 s | 7,55 s |
| İlk 30 dk tamamlanan teslimat, medyan | 174 | 180 |
| İlk 30 dk toplam kazanılan para, medyan | 335.682,5 | 358.436,5 |

Önemli sınır: baseline'da da 30 saniyeyi aşan **zorunlu** boşluk bulunmadı. 41 saniyelik
bot eylem aralığının bir kısmı satın alınabilir yükseltmeler varken botun para biriktirmesiydi.
Bu nedenle genel ekonomi hızlandırılmadı; başlangıç beklemesi kısaltıldı ve mevcut öneri
oyuncunun görebileceği hedef kartına taşındı. Aynı seed, zamanlama değişince farklı teklif/olay
akışları üretebiliyor: bölge ve otomasyon kuyruk değerleri iyileşmedi.
30 dakika gelir medyanı yaklaşık %6,8 yüksek; bunun üzerinden uzun vadeli denge garantisi çıkarılmıyor.

## Filo karşılaştırması

Train/Airport'ın mevcut `tools/balance_sim.py` modelleri, aktif oyuncu profili, ilk 30 dakika.
Train başlangıç bölgesi; Airport seed 3/7/11. Oyunların mekanikleri ve modelleri farklı;
aşağıdakiler bire bir oyuncu hızı sıralaması değildir.

| Ölçüt | Train Baron | Airport Baron | Borderline sonrası |
|---|---:|---:|---:|
| İlk ekonomik geri bildirim | İlk ödeme 37 s | İlk 3 varış 40 s | İlk teslimat 14 s |
| İlk yükseltme | İstasyon/vagon 36 s | Kapı 90 s | Araç 14 s |
| Kapasite genişlemesi | İkinci hat 250 s | İkinci kapı 255 s | İkinci araç medyan 211,5 s |
| Sonraki sistem | Araştırma 444 s | İlk sözleşme 345 s | İlk bölge medyan 368,5 s |

Train modeli 952 karar örneğinde 877 birikim/kaynak bekleme örneği bildiriyor; bunlar saniye
cinsinden zorunlu boşluk değildir. Airport'ın satın alma sayısı tabanlı plato göstergesi 0/0/1 dakika;
iniş onaylama gibi oyuncu eylemlerini kapsamaz. Filo için gerçekte ölçülmeyen bir idle-gap sayısı uydurulmadı.

## Değişiklik ve denge sınırları

- İlk öğretici işin temel gidiş tahmini en fazla 12 s; ilk beş teslimat tamamlanana kadar başlangıç bölgesindeki
  temel gidiş tahmini en fazla 20 s (olay gecikmeleri korunur). 900 s oynama süresi, prestij veya offline durumunda yeni işe yardım yok.
- Uygun bir işe çıkarken `intro_trip` kayda yazılır; onun dönüşü en fazla 6 s. Son teslimat
  sayacı artsa da başlamış yolculuk aynı kalır. Eski kayıtlardaki işlerde işaret yoksa eski dönüş uygulanır.
- Normal yolculuk formülü, fiyatlar, ödüller, masraflar, risk, ilerleme maliyetleri ve reklam/IAP değişmedi.
  Daha kısa erken yolculuk geç teslim cezasını önleyebilir; genel gelir çarpanı eklenmedi.
- Mevcut hedef kartı tek dokunuşla Dot önerisini açıyor; üst satırda 10 dilde görünür ipucu var.
  Öneri, başka araç yoldayken hazır bir aracı göndermeyi de buluyor. Yeni HUD node, timer, animasyon
  veya per-frame tarama eklenmedi; öneri sorgusu yalnız kullanıcı açınca çalışıyor.

## Tekrar çalıştırma ve kapılar

Oyun klasöründen:

```sh
python3 tools/qa_pacing.py --out /tmp/borderline-pacing.json
sh tools/check.sh
```

`check.sh` artık 10 koşuluk pacing kapısını ve `early_pacing_test` sınır testini içeriyor:
**ilk 900 saniyede zorunlu boşluk ≤30 s**, ilk ödeme ≤15 s, mevcut bir saatlik beş milestone
bütçesi ve günlük ödül ≤600. `--baseline` yalnız eski davranışı kaydederken yeni iki tavanı devre dışı bırakır.
Sınır testi: ilk işin parasal alanları değişmez; yardım ve dönüş işareti save/load'da korunur;
5 teslimat / 900 saniye / prestij / offline / başka bölge koşullarında normal tahmin aynıdır;
eski işin dönüşü değişmez. Telefon dokunuş testi tek dokunuşun tam bir öneri penceresi açtığını denetler.

Tam `sh tools/check.sh` **exit 0** (8f2a9b777e); ardından yalnız ölçüm fixture ses ayarı değiştiği için önce/sonra pacing kapıları yeniden geçti ve aynı seed tam eşitlikle tekrarlandı. Kalite testi 60/60, gerçek telefon dokunuşu 6/6, dört dil ekran kaydırma testi 248/248, ortak kaydırma 134/134, kit 966/966, oyun 253/253, erken sınır 16/16, lifecycle 90/90, sekme açılışı 68/85 ms (tavan100), layout 72 ekran/0 bulgu, Low4/4 ve font10/10 geçti. [Kapı çıktıları](review-slow-validation.txt). Ham/sıkıştırılmış kanıtlar:
[önce](review-slow-before.json), [sonra](pacing-quality.json), [filo](review-slow-fleet.json).

Açık kapsam: insan veya Android cihaz oynanışı yapılmadı. Uzun vadeli gelir eşitliği iddiası yok;
korunan şey yardım dışındaki ekonomi formülleri ve mevcut test kapılarıdır. Mağaza yazması,
sürüm değişimi, build, AAB, yükleme ve emülatör/simülatör çalıştırma yapılmadı.

## Test düzeni ve ortam notu

İlk tam koşu, pacing çıkışında `getcwd / Operation not permitted` ortam hatasıyla kesildi;
başarılı sayılmadı. `/tmp` içinden worktree dizinine tekrar girilerek erişim düzeldi;
project/export dosyaları değişmemişti. Sonraki UI koşularında değişmeyen kaydırma testlerinde
aralıklı başarısızlıklar görüldü. Uzun testte ikinci dile geçerken serbest bırakılmış bir
ScrollContainer'a erişim de yakalandı.

Kaydırma testinin dört dili EN/TR/DE/JA korunarak `check.sh` içinde ayrı motor süreçlerine
ayrıldı. Sabit seed ve duran gameplay, input ölçülürken iş kartlarının teslimat/olay nedeniyle
silinmesini önlüyor; UI frameleri, animasyonlar ve fling çalışıyor. Ters sürükleme yeniden görünür
bir düğmeden başlıyor (geometrik merkezdeki slider/metin alanı kendi gesture'ını haklı olarak
alabilir). Ayrıca görünür hedef seçilmeden önce scroll hafızası/fling sıfırlanıyor: önceki sıra, görünür düğmeyi seçip sonra listeyi başa alarak hedefi ekran dışına taşıyabiliyordu. Mesafe ve yanlış tıklama eşikleri azaltılmadı; silinen listeye erişim artık açık bir
başarısızlık kontrolü. Tek dil tanılaması: `res://tests/touch_scroll_test.tscn -- --lang=tr`.

12:04 sistem yük ortalaması 87,21 / 81,59 / 81,44 idi. Bu ortamda CPU süreleri kontrollü
karşılaştırma değildir; ek per-frame polling/animasyon eklenmemesi kaynak üzerinden doğrulandı.

## Low render karşılaştırması

| Sahne | Önce 3D / UI draw | Sonra 3D / UI draw | Sonra statik bellek |
|---|---:|---:|---:|
| Başlangıç | 42 / 45 | 43 / 45 | 100 MB |
| Orta oyun | 40 / 66 | 41 / 68 | 103 MB |
| Geç oyun | 96 / 63 | 90 / 66 | 105 MB |
| Mağaza | 114 / 83 | 92 / 86 | 109 MB |

Her iki koşu dört sahnede de mevcut Low kapısından geçti (150 draw, 300k primitif, 250 MB).
Bunlar hareketli, rastgele iş/araç sahneleri; bire bir dondurulmuş draw eşitliği testi değildir.
Başlangıç HUD değeri 45→45; ek UI/3D nesnesi veya per-frame iş eklenmedi. Diğer sahnelerde
küçük UI draw farkları var; CPU sürelerinin büyük farkı makine yükünden ayrıştırılamıyor.
Dolayısıyla sıfır CPU maliyeti veya her karede aynı draw sayısı ölçüldüğü iddia edilmiyor.

Son fetch/rebase sırasında gelen `4fa2d99372` para sınırı düzeltmesi korundu. `check.sh` çakışması, hem dört dil kaydırma döngüsünü hem para regresyon kapısını tutarak çözüldü. Birleşmiş kodda para 5/5, oyun253/253, erken sınırlar16/16 ve pacing10/10 tekrar geçti; on pacing koşusunun bütün rapor satırları kayıtlı sonuçlarla bire bir aynı kaldı.

## Ek — oyuncu hız düğmesi ×1 / ×2 (30 Ekim 2026, review triage D3; C1 alanı, C1 11-04'e kadar yok)

Açılış temposu düzeltmesinden (`e2ed6317f2`) sonra da oyuncunun kendi hız kontrolü yoktu; `GameManager.time_scale`
yalnız hata ayıklama araçları ve testler içindi. Değişiklik bilerek küçük tutuldu:

- **Tek saat:** `GameManager._process` → `dt = min(delta, 0.1) × time_scale × game_speed()`. Ekonomi, ısı, araçlar ve
  yolculukları, sözleşmeler, öğretici / danışman aynı `dt` ile ilerler; araçların görsel konumu `ContractManager.progress`
  ile simden okunduğu için ekranla ekonomi ayrışmaz. `time_scale` (debug / test çarpanı) olduğu gibi kalır.
- **Gerçek zamanda kalanlar (bilerek):** çevrimdışı kazanç ve arka plandan dönüş, kayıt aralığı, reklam / ödüllü / review
  sayaçları, günlük görev saati, ambiyans trafiği. Duraklatma (tam ekran reklam = `get_tree().paused`, arka plan =
  `_suspended`) saati yine tamamen durdurur: `_process` erken döner, hız çarpanı hiç uygulanmaz.
- **Ne zaman:** düğme yalnız öğretici bitince görünür (`TutorialManager.finished` ve `intro_done`); öğretici yeniden
  başlarsa hız kendiliğinden ×1 olur (ayar saklanır, öğretici bitince geri gelir). Seçim `settings.json` → `speed`
  (1 / 2), oturumlar arası kalıcı. Analitik: `game_speed {speed}`.
- **Bilinen etki:** `play_time` ("Patron olarak süre") ve ilk 900 sn'lik kolay sözleşme penceresi oyun zamanını sayar,
  yani ×2'de gerçek zamanda yarı sürede dolar; gelir/dk göstergesi oyun dakikası başınadır. Düşük'te ek kare başı iş yok
  (yalnız bir çarpma; düğme durumu HUD'ın mevcut 0,5 sn zamanlayıcısında güncellenir).
- **Test:** `tests/game_speed_test.tscn` (check.sh): öğreticide gizli ve ×1, bitince görünür, dokunuş ×1↔×2, ayar
  kaydedilip yeniden yükleniyor, ekonomi tiki ×2 (aynı gerçek süre = iki kat oyun zamanı / gelir), duraklatma saati
  durduruyor.
