# games/LESSONS_LEARNED.md uyum matrisi (2026-10-22, Patron emri: tüm oyunlar)

Kapsam: `games/*` içindeki 22 klasör (fabrika 10 + canlı 11 + travel-agency-game = Capacitor/web, Godot değil).
Yöntem: `origin/main` kodu + filo kapıları (`check_touch_scroll`, `check_hardcoded_text`, `check_admob_init`,
`check_iap_langs`, `save_fuzz`) 22 oyunun hepsinde çalıştırıldı; fizik/renderer ayarları `project.godot` + kaynaktan okundu.
Kapsam dışı (başkası yürütüyor): Blender model turları (Codex), analitik rızası (Codex), kit senkronu (S1).

Gösterim: ✅ bu taramada araçla/kodla doğrulandı · 🔧 açık bulundu, bu taramada düzeltildi · ⚠ açık, bırakıldı (neden notta) ·
❔ yalnız cihazda/Patron smoke'ta doğrulanabilir (tarama kanıtlayamaz) · ➖ uygulanmaz.

## Ders × oyun (özet)

| Oyun | A Performans | B Kayıt | C UI/Dil | D Servis/Mağaza | E Süreç |
|---|---|---|---|---|---|
| aircraft (Parts Rush, canlı) | ✅ 30 Hz Düşük'te çalışma anı + test; perf_gate 3 sahne PASS | ✅ `_quarantine` + .bak | 🔧 hud.gd "PARTS RUSH" lint (marka) | ✅ | ✅ 25 süit PASS |
| airport-baron (canlı) | ✅ 30 Hz/1 adım | ✅ unreadable+.bak | ✅ | ✅ | ✅ |
| bureaucracy-office | ✅ | ✅ fuzz 0 bozuk kayıt | ✅ | ✅ | ✅ |
| canal-control | ✅ | ✅ fuzz 0 | ✅ | ✅ | ✅ |
| chaiwala-rush | ✅ | ✅ fuzz 0 | ✅ | ✅ | ✅ |
| cinnamon-cappadocia (canlı, aile) | ❔ Jolt + interpolasyon, 30 Hz/Düşük elle doğrulanmadı | ✅ | 🔧 4 lint satırı (marka + dev perf yazısı) | ✅ Firebase yok = bilerek (Families) | ⚠ `tools/check.sh` kit-eşitliği kapısında kırmızı (`coddy_ui/ui_tokens.gd` kitten farklı) → S1 kit senkronu |
| corrupt-tycoon (canlı) | ✅ A3/A4/A8 işlendi | ✅ | ✅ | ✅ | ✅ |
| disaster-command | ✅ | ✅ fuzz 0 | ✅ | ✅ | ✅ |
| doner-night | ✅ (Düşük gölge ArtLibrary'de, ortak `low_shading.gd` kopyası yok) | ✅ fuzz 0 | ✅ | ✅ | ✅ |
| kaiten-sushi | ✅ | ✅ fuzz 0 | ✅ | ✅ | ✅ |
| lego (BlockForge, canlı, aile) | ✅ perf_pacer çalışma anı | ✅ | ✅ | ✅ kids_mode | ✅ |
| lifeguard-hq | ✅ | ✅ fuzz 0 | ✅ | ✅ | ✅ |
| moneytycoon (Money District, canlı) | ✅ | ✅ | ✅ | ⚠ **D1: Firebase Analytics köprüsü/autoload yok** ("this game has none yet", README:143). Analitik rızasıyla birlikte (Codex konusu) yapılmalı | ✅ |
| pest-patrol | ✅ | ✅ fuzz 0 | ✅ | ✅ | ✅ |
| pharmacy-rush | ✅ | ✅ fuzz 0 | ✅ | ✅ | ✅ |
| skiresort (canlı) | ✅ | ✅ | ✅ | ✅ | ✅ |
| sky-tower-ops | ✅ | ✅ fuzz 0 | ✅ | ✅ | ✅ |
| smuggler (canlı) | ✅ | ✅ | ✅ | ✅ | ✅ `store/` `.gdignore` yok ama `play-data-safety.csv.import` = keep (res://store/ ikonları) |
| trading-sim | ✅ | ✅ | ✅ | ✅ | ✅ |
| train-baron (canlı) | ✅ | ✅ | ✅ | ✅ | ✅ |
| travel-agency-game (canlı, web) | ➖ Godot değil | ➖ | ➖ | ✅ RC takibi var | ➖ kapılar uygulanmaz (`check_touch_scroll` "no coddy_ui" yanlış alarmı) |
| zombi (canlı) | ✅ 30 Hz | ✅ | ✅ | ✅ | ✅ |

## Ders ders ayrıntı (bulgu)

**A1 fizik.** `physics_ticks_per_second=30` + `max_physics_steps_per_frame=1`: 17 Godot oyunu proje ayarında. aircraft (ayar 60; Düşük
telefonda `main.gd` 30 Hz/2 adım, `tests/performance_test.gd` kanıtlıyor), lego (`perf_pacer.gd`), zombi (30 Hz proje, adım çalışma anı) ve
cinnamon (gerçek fizik, Jolt) çalışma anı kuralı kullanıyor. Proje ayarı değişmedi (canlı oyunlarda grafik varsayılanı yasak).
**A2/A6/A10b** ortak `coddy_perf` (`low_merge.gd`, `perf_warmup.gd`) 21 Godot oyununda var; `perf_gate_hook.gd` + `perf_gate.gd` hepsinde.
**A3–A5, A8–A10, A10a** (geometri/MultiMesh/kare başı yazım) otomatik kapı yok; oyun başına perf testlerinde, kaynak taraması bulgu vermedi.
**A11** Android `gl_compatibility`: her oyunun `build_android.sh`/`mini_build.sh` adımı `rendering_method.mobile` satırını yazıp doğruluyor
(aircraft/lego/zombi/skiresort/smuggler projede de gl_compat). **A12** `release_symbols.sh` her Godot oyununun jenkins/tools betiğinde.

**B1/B2.** `save_fuzz.gd` yalnız `sim/save_codec.gd` olan 10 fabrika oyununda çalışır: 3 tohum × 3000 sn, kare/yatırım/çevrimdışı/rehber
kayıtlarında **0 yüklenemeyen kayıt** (frame ~30k, invest 24–62, offline 15–16, guide 0–1593 kayıt). Diğer Godot oyunlarında codec yok:
bozuk kayıt `.unreadable`/`_quarantine` + `.bak` kodu 20 oyunda mevcut (grep). Arka plan kaydı (`APPLICATION_PAUSED/FOCUS_OUT`) 20 oyunda.

**C1** `check_touch_scroll`: 20 Godot oyunu 0 hata (cinnamon kökten çalıştırılınca "game/addons" yanlış alarmı; `game/` üzerinde 0 hata).
**C2/C3a** `check_hardcoded_text`: aircraft 1 + cinnamon 4 bulgu → 🔧 düzeltildi (marka adı / dev yazısı `# i18n-ok`); diğer 19 oyun PASS.
Dil-değişimi testleri (C2/C3a) oyun başına testlerde; bu tarama tekrar koşmadı (S1 W51).
**C3b/C4/C5** süreç/görsel dersler; kapı yok (W64 sürüyor).

**D1** Firebase AAR'ları: moneytycoon dışında (ve cinnamon = bilerek) hepsinde. **D2** UMP kodu 20 Godot oyununda (aircraft dahil) var.
**D4** `check_admob_init`: tüm Godot oyunları OK (init yeri tek). **D5** `check_iap_langs`: 15 oyun ok; 5 oyun (aircraft, lego, skiresort, travel, zombi)
kendi i18n sistemini kullandığı için araç "skipped" der: aircraft/skiresort IAP dilleri oyun dilleriyle elle karşılaştırıldı (de en es fr ja ko pt ru tr zh) → uyumlu.
**D3/D6/D7/D9/D10** hesap/mağaza/cihaz adımları → ❔ Patron.

**E** `store/.gdignore`: smuggler hariç (bilerek, keep) ve cinnamon (Godot kökü `game/`, `store/` kök dışında) hepsinde var.

## Yapılan düzeltmeler (bu tarama)
- aircraft: `hud.gd` "PARTS RUSH" için lint işareti (`aircraft: …` commit'i).
- cinnamon: 4 satıra `i18n-ok` (`cinnamon: …` commit'i). Yalnız yorum; çalışma anı davranışı ve kayıt biçimi değişmedi.

## Açık kalanlar
1. moneytycoon Firebase Analytics eksik (D1) — analitik rızası işiyle (Codex) birlikte.
2. cinnamon `tools/check.sh` kit-eşitliği kırmızı (S1 kit senkronu bekleniyor); tam süit (~15 dk) bu yüzden koşulmadı.
3. A3–A5/A8–A10 için kare başı yazım/LOD otomatik kapısı yok (perf_gate sayıları kapsıyor ama kalıp taraması yok).
4. cinnamon Düşük fizik 30 Hz doğrulanmadı (❔).
