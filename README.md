# recycle-game — Claude cloud'da yeni oyun çalışma alanı

CoddyApps monoreposu Claude cloud için çok büyük. Bu repo, oradan yalnız yeni bir oyun yazmak için gereken parçaları
taşır:

- **Örnek oyun:** `smuggler/` = **Borderline Empire** (canlı, Godot 4.7.2, Google Play). Filonun kanıtlanmış kalıbı:
  2.5D diorama, kaos içinde süreç yönetimi, onboarding, 10 dil, IAP kataloğu, house reklam + ödüllü AdMob, perf profilleri.
- **Yapılan oyun:** `bottle-route/` (Unity 6; başlangıç: `bottle-route/README.md`). Plan: `RECYCLING_GAME_PLAN.md` = **Bottle Route: Recycling Tycoon** (depozito makineleri dolar; boşaltma
  turu, yakıt ve maliyet dengesi; "Borderline Empire gibi").
- **Kurallar ve standartlar:** kökteki diğer md dosyaları (aşağıda).
- **Sonraki 6 oyun (sahip 11-02: bu repoda Claude cloud yapacak; CoddyApps fabrikasından çıkarıldı):**
  `UNIVERSITY_GAME_PLAN.md` (Semester Rush: Campus Dean), `HOSPITAL_GAME_PLAN.md` (Charge Nurse: Hospital Manager),
  `LAW_FIRM_GAME_PLAN.md` (Docket Day: Law Firm Rush), `DINOSAUR_PARK_GAME_PLAN.md` (Ridgeback Rangers: Dino Sanctuary),
  `BURGER_GAME_PLAN.md` (Grill Convoy: Burger Truck Manager), `COFFEE_GAME_PLAN.md` (Pour & Roast: Cafe Manager).
  Plan sırası/özgünlük kuralları `NEXT_WAVE_PLANS_README.md`'de (o tablodaki 1–5 numaralı oyunlar CoddyApps'te yapılıyor).

Kaynak: `coddykit/CoddyApps` `origin/main` 59c4bcc12c (2026-11-02).

## Okuma sırası

1. `CLAUDE.md` — filo kuralları (sahip kararları; kural kodla çelişirse kural kazanır).
2. `RECYCLING_GAME_PLAN.md` — oyunun tasarımı; bölüm yapısı `NEXT_WAVE_PLANS_README.md`'de.
3. `NEW_GAME_PLAYBOOK.md` — sıfırdan yeni oyun: fazlar (F0, P1–P8), kapılar, kontrol listeleri.
4. `LESSONS_LEARNED.md` — bütün oyunlardan çıkan dersler (performans, kayıt, UI/dil, servisler); kodlamadan önce oku.
5. `smuggler/README.md` — örnek oyunun yapısı, testleri, araçları.

| Dosya | İçerik |
|---|---|
| `CLAUDE.md` | Filo kuralları (`games/CLAUDE.md`) |
| `NEW_GAME_PLAYBOOK.md` | Yeni oyun rehberi |
| `LESSONS_LEARNED.md`, `LESSONS_MATRIX.md` | Dersler + oyun başına uyum matrisi |
| `NEW_GAMES_STRATEGY_2026-10-20.md` | "Kaos içinde süreç yönetimi" oyun stratejisi |
| `RECYCLING_GAME_PLAN.md`, `NEXT_WAVE_PLANS_README.md` | Bottle Route planı + plan şablonu / özgünlük kuralları |
| `UNIVERSITY_`, `HOSPITAL_`, `LAW_FIRM_`, `DINOSAUR_PARK_`, `BURGER_`, `COFFEE_GAME_PLAN.md` | Sonraki 6 oyunun planları (her biri ayrı klasörde yapılır) |
| `IAP_STANDARD.md` | Geniş IAP kataloğu standardı |
| `REWARDED_ADS_STRATEGY.md` | Ödüllü reklam yerleşimleri ve tavanları |
| `REVENUECAT_AD_TRACKING.md` | RevenueCat reklam olay takibi |
| `LOOKDEV_STANDARD.md` | Görsel kalite standardı |
| `UI_KIT_README.md`, `PERF_KIT_README.md`, `AUDIO_KIT_README.md`, `ADS_KIT_README.md` | Ortak kitlerin kullanımı |

## Belgelerdeki yollar bu repoda nerede

Belgeler monorepo yollarını kullanır. Eşleme:

| Belgede | Bu repoda |
|---|---|
| `games/<oyun>/` | `<oyun>/` (örnek: `smuggler/`; yeni oyun kendi klasörüne, ör. `bottle-route/`) |
| `games/CLAUDE.md`, `games/NEW_GAME_PLAYBOOK.md`, `games/LESSONS_LEARNED.md` | kökte aynı adla |
| `games/tools/IAP_STANDARD.md`, `REWARDED_ADS_STRATEGY.md`, `REVENUECAT_AD_TRACKING.md` | kökte aynı adla |
| `games/docs/next-wave-2026-10-31/<plan>.md` / `README.md` | `<PLAN>_GAME_PLAN.md` / `NEXT_WAVE_PLANS_README.md` |
| `games/tools/<ui\|perf\|audio\|ads>/README.md` | `<UI\|PERF\|AUDIO\|ADS>_KIT_README.md` |
| `games/tools/ui` (coddy_ui kiti) | `smuggler/addons/coddy_ui/` |
| `games/tools/perf/addons/coddy_perf` | `smuggler/addons/coddy_perf/` |
| `games/tools/ads/` (CoddyAds, interstitial, banner, house sıralaması) | `smuggler/scripts/monetization/` (+ `ads/`) |
| IAP / AdMob / RevenueCat / Firebase eklentileri | `smuggler/addons/` (`coddy_iap`, `admob`, `godotx_revenue_cat`, `godotx_firebase`, …) |

Burada **olmayanlar:** `games/tools/` kaynak ağacı ve `sync.sh`, `games/jenkins/`, `store-automation/`, `tools/store-video/`,
`marketing-data/`, Mac Mini / Jenkins / emülatör. Kitleri `smuggler/`'dan yeni oyuna kopyala; `sync.sh` adımlarını atla.

## Cloud'da çalışma kuralları

- `smuggler/`'a dokunma; yalnız okunacak örnek. Yeni oyun kendi klasöründe (ör. `bottle-route/`).
- Cloud'da yapılan: kod + headless testler + commit/push. **Derleme (AAB), mağaza yüklemesi, AdMob/Play/RevenueCat
  hesap işleri, emülatör smoke burada yapılmaz** — monorepoya taşınınca Patron yapar.
- `smuggler/`'daki kimlikler Borderline Empire'ındır (paket adı, `google-services.json`, `GoogleService-Info.plist`,
  AdMob birimleri, RevenueCat anahtarı) — yeni oyuna **kopyalama**. Kimlikler boş kalır; debug Google test birimlerini
  kullanır (CLAUDE.md §5: id uydurma).
- Repo herkese açık: hiçbir gizli anahtar, keystore, `.p8` ya da `.env` commit'leme.
- Planın başındaki "sahip başla demeden kod yok" notu: sahip bu repoda açıkça istediğinde başlanır.
- Yeni metinler 10 dilde, oturum içinde çevrilir (dış çeviri API'si yok; CLAUDE.md §9).

## Örnek oyunun testleri

```bash
cd smuggler
godot --headless --import                          # ilk çalıştırma
godot --headless res://tests/test_runner.tscn      # bot 45 simüle dakika oynar
godot --headless res://tests/lifecycle_test.tscn   # ilk açılış, yeniden başlatma, dil, onboarding
```

Tam liste: `smuggler/README.md` ve `smuggler/tests/`. Godot `--import` `project.godot`'u yeniden yazabilir; commit'ten
önce istenmeyen değişikliği geri al.
