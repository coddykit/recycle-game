# Recycling Tycoon: Bottle Route — handoff (2026-11-02)

Ortak durum ve ortak adımlar: kökteki `HANDOFF.md`. Plan: `RECYCLING_GAME_PLAN.md`. Paket `com.coddykit.bottleroute`, Unity 6 + URP, Android, portre.

## Durum
- `sh bottle-route/unity/Tools/check.sh` → PASS (NUnit testleri, filo kapıları, ref derleme, mağaza denetimi 10 dil).
- Kod: `unity/Assets/Scripts/{App,Game,Sim,UI,World}` (`BottleRoute.Sim` / `BottleRoute.Game` motorsuz), Editor `unity/Assets/Editor/BottleRoute`.
- Belgeler: `docs/UNITY.md` (editörde ilk açılış — önce bunu oku), `docs/ART.md`, `docs/BALANCE.md`, `docs/MONETIZATION_PARITY.md`, `README.md`.
- Ses `unity/Assets/Resources/audio` + `audio/manifest.json`; mağaza `store/` (10 dil, feature graphic, 3 ikon adayı).
- Editörde HİÇ açılmadı: derleme, sahne, cihazda görünüm, perf, AAB doğrulanmadı.

## Oyuna özel notlar
- Bölge 5–7 (Sanayi: balya ihaleleri + toplu sayım merkezi; Riviera: oteller + yaz sezonu ×3; Dağ Köyleri + Ada: büyük uzak makineler + feribot) eklendi, 145 test yeşil; editörde bakılacaklar: `docs/CHAPTERS_5_7_HANDOFF.md`. Denge soruları: `docs/BALANCE.md` 'Known tuning questions' (önerilen rota fazla sürüyor, %80 kuralı bot varyantı, balya gelirinin dalgalılığı).
- `ad_free` veren ürünler (CLAUDE.md §8'e onayla eklenecek): **noads, premium, route_pass, bundle** — diğer her ürün reklamı kapatmaz.
- Release env'leri: `BOTTLEROUTE_ADMOB_APP_ID`, `BOTTLEROUTE_KEYSTORE*`, `BOTTLEROUTE_VERSION_*`, `BOTTLEROUTE_RELEASE`; `config/google-services.json` (git dışı). Kimlikler boş.

## Sıradaki iş (sırayla)
1. Editörde ilk açılış (`docs/UNITY.md` §2) → derleme hatalarını düzelt → Test Runner'da EditMode testleri (asmdef'ler repoda).
2. Görsel kontrol: yüz yönü, FBX ekseni, arayüz işaretçileri, koç spotlight, banner yuvası; editörün ürettiği dosyaları tek commit.
3. Düşük profilde perf_gate (draw ≤150, prim ≤300k, bellek ≤250 MB, p95 ≤50 ms) → ekran görüntüleri (8 × 10 dil) → UI puanı ≥8,5.
4. AAB + emülatör smoke → Patron: kimlikler, Play Console, §15 hesap adımları.
