# Ridgeback Rangers: Dino Sanctuary (Unity 6)

Plan: kökteki `DINOSAUR_PARK_GAME_PLAN.md` (#184). Filo kuralları: kökteki `CLAUDE.md`. İskelet: `bottle-route/` (Unity
şablonu, `GAME_TEMPLATE.md`); CoddyUnityKit monorepodaki `games/volt-city`'den.

**Oyun:** Ridgeback Adası'nın yeni Baş Bekçisisin. Kurtarılmış tarih öncesi hayvanlar padoklarda yaşar, safari tramvayı
ziyaretçileri bakış noktalarına götürür. Sorunlar diorama üstünde PİN olarak çıkar (aç sürü, kavga, hasta hayvan, gevşek çit,
yola çıkan hayvan, su basmış yol); doğru rol ekibini (Besleyici, Veteriner, Bakıcı, Çit Teknisyeni) zamanında gönderirsin —
ekipler az, öncelik senin. Sabah planı: yem panosu (istasyon başına sabah kasası), tramvay tarifesi. Uyum kartı (diyet ×
biyom × mizaç) hangi hayvanların aynı padokta yaşayabileceğini gösterir; ziyaretçiler çeşitliliği sever. Kazı alanları
(deterministik, şans kutusu yok) → yumurta → kuluçka (sıcaklık kaydırıcısı) → yavru. Kimse yaralanmaz: kaçan pin = para ve
"sakinlik" kaybı, ilerleme kaybı yok. MVP: 3 bölge (Landing Cove, Fern Valley, Dune Mesa), 8 tür, 12 adımlı rehber.
Codigames oyunundan / film serisinden isim, görsel, metin yok; "Jurassic" yok.

## Klasörler

| Yol | Ne |
|---|---|
| `unity/` | Unity 6 projesi (URP, Android, portre). `unity/Assets/CoddyKit` = filo Unity kiti (volt-city'den) |
| `unity/Assets/Scripts/Sim` | `RidgebackRangers.Sim` — motordan bağımsız simülasyon (servis yolu, padoklar, hayvanlar, pinler, ekipler, tramvay, ziyaretçiler, kuluçka, olaylar, ekonomi, kayıt) |
| `unity/Assets/Scripts/Game` | `RidgebackRangers.Game` — motordan bağımsız oyun katmanı (oturum/kayıt, eylemler, 12 adımlı rehber, mağaza, ödüllü reklam, yorum anı, analitik, 10 dil) |
| `unity/Assets/Scripts/{App,World,UI}` | Unity katmanı: açılış, diorama, UI Toolkit ekranları |
| `unity/Assets/Editor/RidgebackRangers` | proje kurulumu (sahneler, malzemeler, Android politikası), Android derleme, font, ikon, içe aktarma |
| `unity/Assets/Resources` | veri (`data/*.json`), metinler (`i18n/strings.csv`), kit modelleri, palet, arayüz görselleri, fontlar |
| `unity/Assets/Tests` | NUnit testleri (EditMode) — aynı dosyalar .NET ile de koşar |
| `unity/Tools` | `check.sh` (tüm kapılar), `dotnet_check.sh`, `unity_ref_check.sh`, `build_iap.py`, `i18n_merge.py`, `make_palette.py`, `i18n/*.json` |
| `tools/meshy`, `tools/art` | Meshy istemcisi + optimizer (filo aracı), Blender kiti, arayüz render'ları |
| `docs/` | `UNITY.md` (editörde ilk açılış), `ART.md`, `BALANCE.md`, `MONETIZATION_PARITY.md` |

## Durum (2026-11-02)

- **Doğrulandı (editörsüz):** `sh unity/Tools/check.sh` → beklenen tek hata `EveryLanguageIsComplete` (yalnız İngilizce;
  9 dil merkezi çeviride — eski Bottle Route çevirilerinden yalnız metni değişmeyen 86 genel anahtar korundu).
  - 125 NUnit testi (sim 41, oyun katmanı 29, CoddyKit 55), 124 geçti: rehberin 12 adımı baştan sona, yanlış dokunuş /
    yanlış rol, atla/tekrar, yarıda bırakıp yeniden açma (3 senaryo), kayıt güvenliği (dakika dakika fuzz, `.unreadable`
    saklanır), mağaza kuralları (`ad_free` yalnız noads/premium/ranger_pass/bundle), ödül tavanları (= veri), anahtarlar +
    yer tutucular, denge botu 1/7/20 gün.
  - Motor kodu (kit + App + World + UI) UnityEngine 2021.3 referanslarına karşı derleniyor (yalnız bilinen 2 Unity-6 API'si).
- **Doğrulanmadı:** Unity Editor'da derleme, sahne kurulumu, FBX eksenleri, cihazda görünüm, perf_gate (Düşük ≤ 120 draw
  hedefi), emülatör smoke, AAB → `docs/UNITY.md` §2.
- **Sahip kararı bekleyenler:** `ad_free` ürün listesi (§8; ranger_pass "pass" sayılsın mı), kitle 13+ mı (plan R4,
  Families), mağaza adı (Play'de yeniden kontrol), son ikon.
- **Boş bırakılan kimlikler (P8, Patron):** AdMob ödüllü birim, AdMob uygulama kimliği, RevenueCat anahtarı, Firebase
  config. Uydurma kimlik yok (§5).

## Komutlar

```bash
sh ridgeback-rangers/unity/Tools/check.sh
```

```bash
python3 ridgeback-rangers/unity/Tools/i18n_merge.py && python3 ridgeback-rangers/unity/Tools/build_iap.py
```

```bash
blender -b --python ridgeback-rangers/tools/art/kit.py -- ridgeback-rangers/unity
```
