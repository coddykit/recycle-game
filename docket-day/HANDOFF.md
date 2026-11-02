# Law Firm Tycoon: Docket Day — handoff (2026-11-02)

Ortak durum ve ortak adımlar: kökteki `HANDOFF.md`. Plan: `LAW_FIRM_GAME_PLAN.md`. Paket `com.coddykit.docketday`, Unity 6 + URP, Android, portre.

## Durum
- `sh docket-day/unity/Tools/check.sh` → PASS (NUnit testleri, filo kapıları, ref derleme, mağaza denetimi 10 dil).
- Kod: `unity/Assets/Scripts/{App,Game,Sim,UI,World}` (`DocketDay.Sim` / `DocketDay.Game` motorsuz), Editor `unity/Assets/Editor/DocketDay`.
- Belgeler: `docs/UNITY.md` (editörde ilk açılış — önce bunu oku), `docs/ART.md`, `docs/BALANCE.md`, `docs/MONETIZATION_PARITY.md`, `README.md`.
- Ses `unity/Assets/Resources/audio` + `audio/manifest.json`; mağaza `store/` (10 dil, feature graphic, 3 ikon adayı).
- Editörde HİÇ açılmadı: derleme, sahne, cihazda görünüm, perf, AAB doğrulanmadı.

## Oyuna özel notlar
- 'trial' kelimesi mahkeme anlamında serbest, yalnız 'free trial' türü yasak. Denge soruları `docs/BALANCE.md` 'Known tuning questions'.
- `ad_free` veren ürünler (CLAUDE.md §8'e onayla eklenecek): **noads, premium, partner_pass, bundle** — diğer her ürün reklamı kapatmaz.
- Release env'leri: `DOCKETDAY_ADMOB_APP_ID`, `DOCKETDAY_KEYSTORE*`, `DOCKETDAY_VERSION_*`, `DOCKETDAY_RELEASE`; `config/google-services.json` (git dışı). Kimlikler boş.

## Sıradaki iş (sırayla)
1. Editörde ilk açılış (`docs/UNITY.md` §2) → derleme hatalarını düzelt → test asmdef'leri → EditMode testleri.
2. Görsel kontrol: yüz yönü, FBX ekseni, arayüz işaretçileri, koç spotlight, banner yuvası; editörün ürettiği dosyaları tek commit.
3. Düşük profilde perf_gate (draw ≤150, prim ≤300k, bellek ≤250 MB, p95 ≤50 ms) → ekran görüntüleri (8 × 10 dil) → UI puanı ≥8,5.
4. AAB + emülatör smoke → Patron: kimlikler, Play Console, §15 hesap adımları.
