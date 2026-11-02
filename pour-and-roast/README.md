# Pour & Roast: Cafe Manager (Unity 6)

Plan: kökteki `COFFEE_GAME_PLAN.md`. Filo kuralları: kökteki `CLAUDE.md`. Şablon: `bottle-route/` (Bottle Route,
`GAME_TEMPLATE.md`); CoddyUnityKit oradan aynen kopyalandı.

**Oyun:** Yağmurlu Brindle Quay limanında amcandan kalan küçük kahve arabası artık senin. Her sipariş raydaki bir fiş:
hangisi önce yapılacak (kural: ilk gelen / teslim saati önce / müdavimler önce, öncelik bayrağı), kim espresso
makinesinde, kim buhar çubuğunda, aynı içecekler toplu mu yapılsın (batch) — sen seçiyorsun. Öğütücünün ayarı kayar
(acı shot → kötü yorum, basılı tut = ayar), öğütücü aşınır ve sıkışabilir, süt her sabah tedarikçiden gelir (ucuzu daha
sık gecikir), yarısı gece bozulur. Hava tahmini: yağmur ön-siparişi ve sıcak içeceği artırır (sıcak içecek hazırla),
sıcak hava buzlu içecek ve buz ister. Yorumlar yıldızları, yıldızlar müşteriyi belirler. Büyü: **Quayside Cart → Corner
Cafe (masalar, pastane vitrini, buzlu içecekler) → Roastery Hall** (yeşil çekirdek pazarı, tambur kavurucu — ilk çatlakta
"Drop" = tatlı nokta, dinlendirme, harman, iki toptan sözleşme). Eski mekânlar şube olarak kendi kendine kazanır
(sıfırlama yok). Krizler (MVP 6): yağmur, sıcak, süt gecikmesi, öğütücü sıkışması, menşe donu, kötü yorum serisi — kısa,
önceden bildirilir, asla oyun sonu değil; rehberde ve ilk 3 günde yok. Gerçek kahve zinciri / referans oyun adı yok.

## Klasörler

| Yol | Ne |
|---|---|
| `unity/` | Unity 6 projesi (URP, Android, portre). `unity/Assets/CoddyKit` = filo Unity kiti |
| `unity/Assets/Scripts/Sim` | `PourAndRoast.Sim` — motordan bağımsız simülasyon (fiş rayı, istasyonlar, personel, stoklar, kavurma, toptan, krizler, ekonomi, kayıt, denge botu) |
| `unity/Assets/Scripts/Game` | `PourAndRoast.Game` — oturum/kayıt (5 kayıt yuvası), eylemler, 12 adımlı rehber, mağaza, ödüllü reklam, yorum anı, analitik, metinler |
| `unity/Assets/Scripts/{App,World,UI}` | Unity katmanı: açılış, kafe dioraması (`CafeWorld`), UI Toolkit ekranları |
| `unity/Assets/Resources` | veri (`data/*.json`), metinler (`i18n/strings.csv`), Blender kiti + Meshy prop'lar (`kit/`), palet, arayüz görselleri |
| `unity/Assets/Art` | Meshy karakterler (render için), ikon (yer tutucu) |
| `unity/Assets/Tests` | NUnit testleri (EditMode) — aynı dosyalar .NET ile de koşar |
| `unity/Tools` | `check.sh` (tüm kapılar), `build_iap.py`, `i18n_merge.py`, `make_palette.py`, `i18n/*.json` |
| `tools/meshy`, `tools/art` | Meshy istemcisi + optimizer, Blender kiti (`kit.py`), arayüz render'ları (`render_ui.py`) |
| `docs/` | `UNITY.md`, `ART.md`, `BALANCE.md`, `MONETIZATION_PARITY.md` |

## Durum (2026-11-02)

- **Doğrulandı (editörsüz):** `sh unity/Tools/check.sh` → beklenen tek hata `EveryLanguageIsComplete` (yalnız İngilizce
  metin var; 9 dil merkezi Haiku çevirisinde doldurulacak). 135 NUnit testi: sim 49, oyun katmanı 31, CoddyKit 55.
  Unity'ye bağlı kod UnityEngine 2021.3 referanslarına karşı derleniyor (yalnız 2 bilinen Unity-6 API'si farklı).
- **Doğrulanmadı:** Unity Editor'da derleme, sahne kurulumu, cihazda görünüm, perf_gate, emülatör smoke, AAB.
- **Cilalama (bulut, 11-02):** `store/` (en-US listing, 8 ekran görüntüsü planı, data safety CSV, `validate.py` → check.sh),
  feature graphic + 3 ikon adayı (`tools/art/render_store.py`), Blender mekân kabukları (`kit/shell_*`, Düşük'te 0 ek draw),
  tempo: Roastery Hall ~3 sa (`docs/BALANCE.md`), `docs/ART.md`.
- **Boş kimlikler (P8, Patron):** AdMob ödüllü birim + uygulama kimliği, RevenueCat anahtarı, Firebase config.

## Komutlar

```bash
sh pour-and-roast/unity/Tools/check.sh
python3 pour-and-roast/unity/Tools/i18n_merge.py && python3 pour-and-roast/unity/Tools/build_iap.py
blender -b --python pour-and-roast/tools/art/kit.py -- pour-and-roast/unity
blender -b --python pour-and-roast/tools/art/render_ui.py -- pour-and-roast/unity
blender -b --python pour-and-roast/tools/art/render_store.py -- pour-and-roast
python3 pour-and-roast/store/validate.py [--all]
```
