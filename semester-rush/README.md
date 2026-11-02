# Semester Rush: Campus Dean (Unity 6)

Plan: kökteki `UNIVERSITY_GAME_PLAN.md` (oyun planı #184). Filo kuralları: kökteki `CLAUDE.md`. Şablon: `bottle-route/`
(`GAME_TEMPLATE.md`); altyapı (CoddyKit, açılış, mağaza, kayıt) oradan, alan kodu baştan yazıldı.

**Oyun:** Genç bir kampüsün dekanısın. Pazar günü haftalık ders programını kurarsın (ders × gün × saat × salon; salon
türü, hoca aynı anda tek ders ve günde en çok 3 ders, grup aynı anda tek ders). Hafta başlayınca her ders zilinden 10 dk
önce öğrenciler bir önceki salondan / kantinden / kütüphaneden / yurttan ya da otobüs durağından yürür: aynı patikada
kalabalık yavaşlatır, uzun yol geç kaldırır. Kısayol kapısı aç, yurdu salonlara yakın kur, görevli işe al, zilleri
kaydır. 12:00 / 13:30 öğle yemeğinde kantin koltuğu yetmezse aç kalanlar sonraki dersten az öğrenir. Ders bitince harç
gelir (veznedeki paraya dokun), cuma akşamı notlar prestije döner. 6. hafta sınav haftası: dönem kapanır, hibe gelir,
yeni dönem kaydını seçersin (aynı / büyüt / seçici ol). Krizler: hasta hoca, patlayan boru, kar, yurt gribi, harç
protestosu (seçim), yangın tatbikatı, açık gün, hoca transferi (seçim), sınavda elektrik kesintisi. Gerçek üniversite,
marka ya da referans oyunun adı / görseli / metni yok (plan §3 özgünlük kuralı).

MVP kapsamı (plan §14): bölüm 1–3 (Community College, Liberal Arts Quad, Science Campus), program tahtası + otomatik
doldurma, zil değişimi akışı (patika, kapı, kaydırmalı zil, geniş patika, servis), 6 personel grubu, öğle + yurt
kapasitesi, sınav haftası, 9 kriz, çevrimdışı mola, 11 adımlı rehber, mağaza, 4 ödüllü yerleşim, house reklam.

## Klasörler

| Yol | Ne |
|---|---|
| `unity/Assets/Scripts/Sim` | `SemesterRush.Sim` — motordan bağımsız simülasyon (program, yol grafiği + zil akışı, öğle, dersler, ekonomi, krizler, kayıt, denge botu) |
| `unity/Assets/Scripts/Game` | oturum/kayıt, eylemler, 11 adımlı rehber, mağaza, ödüllü reklam, yorum anı, analitik, metinler |
| `unity/Assets/Scripts/{App,World,UI}` | açılış, kampüs dioraması (`CampusWorld`, zil kalabalığı + ısı şeridi), UI Toolkit ekranları |
| `unity/Assets/Resources` | veri (`data/*.json`), metinler (`i18n/strings.csv`), Blender kiti + Meshy meşe, palet, arayüz görselleri |
| `unity/Tools` | `check.sh` (tüm kapılar), `build_iap.py`, `i18n_merge.py`, `make_palette.py`, `i18n/en.json` (kaynak) |
| `tools/art`, `tools/meshy` | Blender kiti (`kit.py`), UI render (`render_ui.py`), Meshy istemcisi + optimizer |
| `docs/` | `UNITY.md`, `BALANCE.md`, `MONETIZATION_PARITY.md`, `ART.md` |
| `store/` | Play listing (`listing/en-US.json`, other 9 locales translated centrally), screenshot plan (`screenshots/en.json`), Data safety CSV, feature graphic + 3 icon candidates (`tools/art/render_store.py`), `validate.py` (run by `check.sh`) |

## Durum (2026-11-02)

- **Doğrulandı (editörsüz):** `sh unity/Tools/check.sh` → `SEMESTER_RUSH_CHECK: PASS` (134 NUnit testi, `STORE_LISTING_PASS`,
  `UNITY_REF_CHECK: PASS`).
- **Cila turu (2026-11-02):** mağaza metni (en) + ekran görüntüsü planı + Data safety + feature graphic + 3 ikon adayı;
  kampüs süslemesi (Blender ağaç / lamba / bank / bisiklet parkı / kapı, bölge ve yol meşlerine birleştirildi, Düşük'te
  0 ek çizim); tempo: Quad ~1 sa, Science ~2,8 sa (`docs/BALANCE.md`, `docs/ART.md`).
- **Doğrulanmadı:** Unity Editor'da derleme, sahne kurulumu, cihazda görünüm, perf_gate, emülatör smoke, AAB → `docs/UNITY.md` §2.
- **Bekleyen:** 9 dilin çevirisi (fleet §9, Haiku), ses (ElevenLabs, sahip onayı), P8 kimlikleri (boş bırakıldı), §8 `ad_free` satırının sahip onayı.

```bash
sh semester-rush/unity/Tools/check.sh
python3 semester-rush/unity/Tools/i18n_merge.py && python3 semester-rush/unity/Tools/build_iap.py
```
