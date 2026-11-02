# Grill Convoy: Burger Truck Manager (Unity 6)

Plan: kökteki `BURGER_GAME_PLAN.md` (#184). Filo kuralları: kökteki `CLAUDE.md`. Şablon: `bottle-route/` (yol ağı, rota
planlayıcı ve kamyon hareketi oradan uyarlandı), kurallar `GAME_TEMPLATE.md`.

**Oyun:** Liman şehri Cinder Bay. Bir eski burger kamyonu ve Office Plaza izniyle başlarsın. Her sabah hangi kamyonun
hangi noktaya (Office Plaza öğle yoğunluğu, Boardwalk sıcak/hafta sonu, Stadium maç günü devre arası izdihamı) gideceğini,
ne kadar köfte / ekmek / yan ürün yükleneceğini, mürettebatın rollerini (ızgaracı, hazırlayıcı, kasiyer, koşucu) ve menü
panosunu seçersin; kamyonun içindeki 4×3 (sonra 5×4) **mutfak yerleşimi** asıl yapı bulmacasıdır: ızgara → tezgâh → pencere
arası yürüme hattı yavaşlatır, tost makinesi / sos rafı tezgâha, içecek dolabı / fritöz pencereye bitişikse bonus verir.
Servis gerçek zamanlıdır: kuyruk, sabır, tükenme (sell-out), fazla yük = gece bozulma (waste). Merkez mutfakta (commissary)
parti hazırlanır, gün içinde ikmal turu atılır. Krizler: devre arası izdihamı, yağmur, sıcak hava, propan bitmesi, yağ
alevlenmesi, rakip kamyon — kısa, önceden duyurulur, oyun bitmez. Gerçek zincir / referans oyun adı yok (özgünlük kuralı).

## Klasörler

| Yol | Ne |
|---|---|
| `unity/` | Unity 6 projesi (URP, Android, portre). `unity/Assets/CoddyKit` = filo Unity kiti (yalnız house-ads tablosuna `grill-convoy` satırı eklendi) |
| `unity/Assets/Scripts/Sim` | `GrillConvoy.Sim` — motordan bağımsız simülasyon (yol ağı, kamyonlar, mutfak hattı + yerleşim, müşteriler, merkez mutfak, krizler, ekonomi, kayıt, denge botu) |
| `unity/Assets/Scripts/Game` | `GrillConvoy.Game` — oturum/kayıt (+ kariyer yuvaları), eylemler, 14 adımlı rehber, mağaza, ödüllü reklam, yorum anı, analitik, metinler |
| `unity/Assets/Scripts/{App,World,UI}` | Unity katmanı: açılış, şehir dioraması, UI Toolkit ekranları (yerleşim editörü dahil) |
| `unity/Assets/Resources` | veri (`data/*.json`), metinler (`i18n/strings.csv`), Blender kiti, palet, arayüz görselleri |
| `unity/Assets/Art` | Meshy kahramanları (şef, kasiyer, taraftar, burger) + geçici ikon |
| `unity/Tools` | `check.sh` (tüm kapılar), `build_iap.py`, `i18n_merge.py`, `make_palette.py`, `i18n/en.json` (kaynak) |
| `tools/meshy`, `tools/art` | Meshy istemcisi + optimizer, Blender kiti (`kit.py`), UI render'ları (`render_ui.py`) |
| `docs/` | `UNITY.md`, `ART.md`, `BALANCE.md`, `MONETIZATION_PARITY.md` |

## Durum (2026-11-02)

- **Doğrulandı (editörsüz):** `sh unity/Tools/check.sh` → yalnız beklenen `EveryLanguageIsComplete` kırmızı (9 dil
  merkezde Haiku ile çevrilecek; `Tools/i18n/<dil>.json` boş). 140 NUnit testinden 139'u yeşil (sim 54, oyun 31, kit 55):
  rehberin 14 adımı (doğru / yanlış eylem / atla / yeniden aç / yapılamayan adım geçilir), kayıt fuzz'ı (el ile pişirilen
  bilet, ikmal turu, alevlenme, yerleşim değişikliği), mağaza kuralları (`ad_free` yalnız noads / premium / convoy_pass /
  bundle), ödül tavanları, 7 / 20 günlük denge. Unity'ye bağlı kod UnityEngine referanslarına karşı derleniyor.
- **Doğrulanmadı:** Unity Editor'da derleme, sahne, cihazda görünüm, perf_gate, emülatör smoke, AAB → `docs/UNITY.md` §2.
- **Boş kimlikler (P8, Patron):** AdMob ödüllü birim + uygulama kimliği, RevenueCat anahtarı, Firebase config.
- **Sahip onayı bekleyen:** CLAUDE.md §8 satırı (`grill-convoy`: noads, premium, convoy_pass, bundle).

## Komutlar

```bash
sh grill-convoy/unity/Tools/check.sh
python3 grill-convoy/unity/Tools/i18n_merge.py && python3 grill-convoy/unity/Tools/build_iap.py
```
