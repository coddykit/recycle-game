# Docket Day: Law Firm Rush (Unity 6)

Plan: kökteki `LAW_FIRM_GAME_PLAN.md` (MVP = §14). Filo kuralları: kökteki `CLAUDE.md`. Şablon: `bottle-route/`
(`GAME_TEMPLATE.md`). Kurgusal şehir Calderport; gerçek büro, mahkeme, kişi, dava ya da referans oyunun (Codigames)
adı / görseli / metni yok. Oyun içi "yalnız hukuk tadı — hukuki tavsiye değildir" satırı Ayarlar → Hakkında'da.

**Oyun:** Garaj önünde küçük bir avukatlık bürosu, karşıda adliye. Her müvekkil tarihli işler getirir (dilekçe, delil,
hazırlık, duruşma). Sayaç beklemek yok: iş, kişilerin günlük saatlerini **Dava Rayı**'na (önümüzdeki 10 gün) sığdırmak.
Resepsiyonda kabul / ret / yönlendir / etik duvar (çıkar çatışması), kişileri görevlere sürükle, aynı saatte iki duruşma =
çarpışma (avukat değiştir, misafir avukat, erteleme), hazırlık masasında delil–iddia eşleştir, uzlaş ya da dava et, duruşma
sahnesi (isteğe bağlı "İtiraz!" vuruşu), akşam fatura turu. Emanet (trust) hesabı harcanamaz; ücretler işletme hesabına
geçer. Krizler: sürpriz dilekçe, elektrik kesintisi, hastalık izni, yedek hâkim. 3 bölüm: Garaj → Ana Cadde → Adliye Sırası.

## Klasörler

| Yol | Ne |
|---|---|
| `unity/` | Unity 6 projesi (URP, Android, portre). `unity/Assets/CoddyKit` = filo Unity kiti (aynen) |
| `unity/Assets/Scripts/Sim` | `DocketDay.Sim` — motordan bağımsız simülasyon (davalar, ray, çarpışmalar, oranlar, duruşmalar, ekonomi, krizler, kayıt, denge botu) |
| `unity/Assets/Scripts/Game` | `DocketDay.Game` — oturum/kayıt, eylemler, 9 adımlı rehber, mağaza, ödüllü reklam, yorum anı, analitik, 10 dil |
| `unity/Assets/Scripts/{App,World,UI}` | Unity katmanı: açılış, diorama (ofis kulesi + sokak + adliye), UI Toolkit ekranları, duruşma sahnesi |
| `unity/Assets/Resources` | veri (`data/*.json`), metinler (`i18n/strings.csv`), Blender kiti + Meshy kedi, palet, arayüz görselleri, fontlar |
| `unity/Assets/Art` | Meshy karakterleri (avukat, müvekkil, hâkim, avukat yardımcısı, ofis kedisi) + yer tutucu ikon |
| `unity/Tools` | `check.sh` (tüm kapılar), `build_iap.py`, `i18n_merge.py`, `make_palette.py`, `i18n/*.json` |
| `tools/meshy`, `tools/art` | Meshy istemcisi + optimizer, Blender kiti (`kit.py`), arayüz render'ları (`render_ui.py`) |
| `docs/` | `UNITY.md`, `ART.md`, `BALANCE.md`, `MONETIZATION_PARITY.md` |

## Durum (2026-11-02)

- **Doğrulandı (editörsüz):** `sh unity/Tools/check.sh` → yalnız beklenen `EveryLanguageIsComplete` kırmızı (9 dil
  merkezi çeviriyi bekliyor; İngilizce kaynak `Tools/i18n/en.json`, 491 anahtar). 133/134 NUnit testi yeşil (sim 45,
  oyun katmanı 34, CoddyKit 55); Unity'ye bağlı kod UnityEngine referanslarına karşı derleniyor (`UNITY_REF_CHECK: PASS`).
- **Doğrulanmadı:** Unity Editor'da derleme/sahne, cihazda görünüm, perf_gate, emülatör smoke, AAB → `docs/UNITY.md` §2.
- **Boş kimlikler (P8, Patron):** AdMob ödüllü birim + uygulama kimliği, RevenueCat anahtarı, Firebase config. Uydurma yok.

## Komutlar

```bash
sh docket-day/unity/Tools/check.sh
python3 docket-day/unity/Tools/i18n_merge.py && python3 docket-day/unity/Tools/build_iap.py
blender -b --python docket-day/tools/art/kit.py -- docket-day/unity
blender -b --python docket-day/tools/art/render_ui.py -- docket-day/unity
```
