# College Tycoon: Semester Rush — handoff (2026-11-02)

Ortak durum ve ortak adımlar: kökteki `HANDOFF.md`. Plan: `UNIVERSITY_GAME_PLAN.md`. Paket `com.coddykit.semesterrush`, Unity 6 + URP, Android, portre.

## Durum
- `sh semester-rush/unity/Tools/check.sh` → PASS (NUnit testleri, filo kapıları, ref derleme, mağaza denetimi 10 dil).
- Kod: `unity/Assets/Scripts/{App,Game,Sim,UI,World}` (`SemesterRush.Sim` / `SemesterRush.Game` motorsuz), Editor `unity/Assets/Editor/SemesterRush`.
- Belgeler: `docs/UNITY.md` (editörde ilk açılış — önce bunu oku), `docs/ART.md`, `docs/BALANCE.md`, `docs/MONETIZATION_PARITY.md`, `README.md`.
- Ses `unity/Assets/Resources/audio` + `audio/manifest.json`; mağaza `store/` (10 dil, feature graphic, 3 ikon adayı).
- Editörde HİÇ açılmadı: derleme, sahne, cihazda görünüm, perf, AAB doğrulanmadı.

## Oyuna özel notlar
- Geç oyunda para birikiyor (bölüm 4–6 harcama yerleri MVP'de yok) — `docs/BALANCE.md` 'Open'. Mağaza metninde gerçek üniversite kısaltması yasak (kapı var).
- `ad_free` veren ürünler (CLAUDE.md §8'e onayla eklenecek): **noads, premium, bundle** — diğer her ürün reklamı kapatmaz.
- Release env'leri: `SEMESTERRUSH_ADMOB_APP_ID`, `SEMESTERRUSH_KEYSTORE*`, `SEMESTERRUSH_VERSION_*`, `SEMESTERRUSH_RELEASE`; `config/google-services.json` (git dışı). Kimlikler boş.

## Sıradaki iş (sırayla)
1. Editörde ilk açılış (`docs/UNITY.md` §2) → derleme hatalarını düzelt → test asmdef'leri → EditMode testleri.
2. Görsel kontrol: yüz yönü, FBX ekseni, arayüz işaretçileri, koç spotlight, banner yuvası; editörün ürettiği dosyaları tek commit.
3. Düşük profilde perf_gate (draw ≤150, prim ≤300k, bellek ≤250 MB, p95 ≤50 ms) → ekran görüntüleri (8 × 10 dil) → UI puanı ≥8,5.
4. AAB + emülatör smoke → Patron: kimlikler, Play Console, §15 hesap adımları.
