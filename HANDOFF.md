# Handoff — 7 Unity oyunu (2026-11-02, bulut oturumu sonu)

Dal: `fix/compassionate-cray-xl63ag`. Her oyunun kendi `<oyun>/HANDOFF.md` dosyası var (oyuna özel durum + sıradaki iş).
Bu dosya ortak durumu ve ortak adımları anlatır.

| Oyun | Klasör | Plan | Paket |
|---|---|---|---|
| Recycling Tycoon: Bottle Route | `bottle-route/` | `RECYCLING_GAME_PLAN.md` | com.coddykit.bottleroute |
| College Tycoon: Semester Rush | `semester-rush/` | `UNIVERSITY_GAME_PLAN.md` | com.coddykit.semesterrush |
| Hospital Manager: Charge Nurse | `charge-nurse/` | `HOSPITAL_GAME_PLAN.md` | com.coddykit.chargenurse |
| Law Firm Tycoon: Docket Day | `docket-day/` | `LAW_FIRM_GAME_PLAN.md` | com.coddykit.docketday |
| Dino Park: Ridgeback Rangers | `ridgeback-rangers/` | `DINOSAUR_PARK_GAME_PLAN.md` | com.coddykit.ridgebackrangers |
| Burger Tycoon: Grill Convoy | `grill-convoy/` | `BURGER_GAME_PLAN.md` | com.coddykit.grillconvoy |
| Cafe Manager: Pour & Roast | `pour-and-roast/` | `COFFEE_GAME_PLAN.md` | com.coddykit.pourandroast |

## Bitti (7 oyunun hepsinde, editörsüz doğrulandı)
- Motordan bağımsız simülasyon + oyun katmanı (rehber, mağaza/`ad_free`, ödüllü tavanlar, ReviewGate, kayıt güvenliği, analitik), NUnit testleri .NET 8'de yeşil.
- Unity katmanı (Boot, diorama, UI Toolkit ekranları, Editor kurulum/derleme betikleri) — UnityEngine 2021.3 referanslarına karşı derleniyor.
- Oyun içi 10 dil, ses (ElevenLabs SFX + 2 müzik, `Resources/audio`), Blender kit + bazı Meshy modelleri, plana göre tempo.
- Mağaza: 10 dilde listing + ekran görüntüsü metinleri, `feature-graphic.png`, `icon-candidates/c1..c3.png`, Data safety CSV.
- Kapı: `sh <oyun>/unity/Tools/check.sh` → `<OYUN>_CHECK: PASS` (testler, filo statik kapıları, ref derleme, `store/validate.py` → ortak `tools/store_templates/store_lint.py`, 10 dilde yasak kelime).

## Ortak sıradaki adımlar (sırayla)
1. **Unity Editor ilk açılış** (Mac Mini / sahibin Unity 6000.3.x'i): her oyunun `docs/UNITY.md` §2 adımları —
   `Tools/fetch_packages.sh` → projeyi aç (ProjectSetup kendiliğinden çalışır) → Coddy › Validate Project → Play.
   Test asmdef'leri editörde oluştur (UNITY.md §2 adım 5) ve EditMode testlerini koş.
2. **İlk bakılacaklar:** derleme hataları (yalnız `QualitySettings.count`, `SystemLanguage.Hindi` Unity 6 farkı bilinir),
   prosedürel mesh yüz yönü (MeshKit.Quad saat yönüne çevrildi; içi dışa görünen yüz = köşe sırası ters), Blender FBX
   ekseni (-90° X kök dönüşü korunmalı), arayüz işaretçi konumu, koç spotlight hedefleri, banner yuvası.
3. **Editörün ürettiği dosyalar tek commit:** `ProjectSettings/*`, `Packages/packages-lock.json`, `Assets/Settings/*`,
   `Assets/Scenes/*`, malzemeler, font asset, `.meta`'lar. `Library/ Temp/ Logs/ UserSettings/` asla.
4. **Performans (CLAUDE.md §0/§10):** Düşük profilde perf_gate — draw ≤150 (hedef ≤120), prim ≤300k, statik bellek ≤250 MB, 30 FPS, p95 ≤50 ms.
5. **Ekran görüntüleri** (`store/screenshots/<dil>.json` planı, 8 kare × 10 dil) + arayüz önce/sonra (en+de), Patron puanı ≥8,5.
6. **AAB** (Jenkins `android-linux`), Mini emülatör smoke, `release_symbols.sh`.
7. **P8 / Patron:** AdMob uygulama + ödüllü birim, RevenueCat anahtarı, Firebase `config/google-services.json`,
   keystore env'leri (`<PREFIX>_ADMOB_APP_ID`, `<PREFIX>_KEYSTORE*`, `<PREFIX>_VERSION_*`, `<PREFIX>_RELEASE`).
   Kimlikler şu an BOŞ (uydurma yok). Sonra §15 hesap adımları (UMP mesajı, RC credentials, gizlilik sayfası, ads_app/house kaydı, ASO Atlas, tanıtım videosu).

## Sahibin kararı bekleyenler (hepsi için)
- İkon seçimi (`store/icon-candidates/c1..c3.png`) → seçileni `unity/Assets/Art/Icon`'a koy.
- Başlık sırası: şu an "anahtar kelime: Marka" (ASO); planlar marka-önde diyordu.
- CLAUDE.md §8 `ad_free` satırları (her oyunun `HANDOFF.md`'sinde ürün listesi) → onaydan sonra CLAUDE.md'ye ekle.
- `games/tools/ads/house_related.py` + `CoddyHouseRelated.gd`/`HouseRelatedTable.g.cs` gerçek satırları (şu an geçici satırlar).
- Sohbette paylaşılan Unity şifresi, Meshy ve ElevenLabs anahtarları değiştirilmeli (hiçbiri repoda değil).

## Araçlar
- Klonlayıcı: `tools/new_game.py`; şablon notu `GAME_TEMPLATE.md`; cila notu `POLISH_BRIEF.md`.
- Ses: `tools/audio/gen.py` (anahtar `~/.config/elevenlabs/api_key`, kalan ~26,7k kredi). Meshy: `<oyun>/tools/meshy/meshy.py` (ledger ~465 kalan).
- Çeviri: `unity/Tools/i18n/en.json` → diğer 9 dil → `Tools/i18n_merge.py` → `Resources/i18n/strings.csv`; `build_iap.py`.
