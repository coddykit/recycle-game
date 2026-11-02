# Charge Nurse: Hospital Manager (Unity 6)

Plan: kökteki `HOSPITAL_GAME_PLAN.md` (MVP = §14). Filo kuralları: kökteki `CLAUDE.md`. Şablon: `bottle-route/`
(`GAME_TEMPLATE.md`); CoddyUnityKit oradan aynen kopyalandı.

**Oyun:** Küçük bir kasaba kliniğinin sorumlu hemşiresisin; klinik zamanla kasaba hastanesine, sonra şehir hastanesine
büyüyor. Hastalar geliyor; her birine renk veriyorsun (yeşil hafif … kırmızı acil; renk + şekil, renk körü dostu),
boş yatağa gönderiyorsun, taburcu ediyorsun, kirli yatağı temizletiyorsun, ambulansı kabul ya da yönlendiriyorsun.
Ekipler (hemşire, doktor, hasta taşıyıcı, temizlik, kayıt, teknisyen) kurallarına göre çalışır; asıl iş **darboğazı
bulmak**: kırmızı koridora / mercek düğmesine dokununca oyun sebebi tek cümleyle söyler (çoğu zaman acil değil,
taburcu ya da kirli yatak). 18:30–19:00 arası devir teslim (yapmazsan notlar kaybolur, ekip yorgun kalır); gece ekibi
kendi başına, yarım kapasiteyle çalışır. Krizler: grip haftası, zincirleme kaza, elektrik kesintisi, tarayıcı arızası,
hastalık izni, mide virüsü (koğuş kapanır), malzeme gecikmesi, denetim. Kan, ölüm, gerçek hastane / ilaç / marka adı
yok; referans oyunun adı, görseli, metni kullanılmadı.

## Klasörler

| Yol | Ne |
|---|---|
| `unity/Assets/Scripts/Sim` | `ChargeNurse.Sim` — motordan bağımsız simülasyon (hasta akışı, ekipler, yatak/koğuş, darboğaz merceği, devir teslim + gece, krizler, ekonomi, kayıt, denge botu) |
| `unity/Assets/Scripts/Game` | oturum/kayıt, eylemler, 10 adımlı rehber, mağaza, ödüllü reklam, yorum anı, analitik, metinler |
| `unity/Assets/Scripts/{App,World,UI}` | açılış, kesitli kampüs dioraması (tek palet malzemesi, ısı haritalı koridorlar), UI Toolkit ekranları |
| `unity/Assets/Resources` | veri (`data/*.json`), metinler (`i18n/strings.csv`), Blender kiti, paletler (+3 tema), arayüz görselleri |
| `unity/Tools` | `check.sh` (tüm kapılar), `build_iap.py`, `i18n_merge.py`, `make_palette.py`, `i18n/*.json` |
| `tools/art`, `tools/meshy` | Blender kiti + arayüz render'ları, Meshy istemcisi + optimizer |
| `docs/` | `UNITY.md`, `BALANCE.md`, `MONETIZATION_PARITY.md`, `ART.md` |
| `store/` | Play metni (`listing/en-US.json`; 9 dil merkezi çeviride), ekran görüntüsü planı (`screenshots/en.json`), Data safety CSV, `validate.py` (check.sh çağırır), feature graphic + ikon adayları c1–c3 (`tools/art/render_store.py`) |

## Durum (2026-11-02)

- `sh unity/Tools/check.sh` → PASS (125 test yeşil, mağaza metni doğrulaması dahil); Unity'ye bağlı kod referans
  derlemede temiz.
- Cilalama (11-02): mağaza metni + görseller, kampüs kiti (giriş, ambulans sundurması, asansör, ağaçlar, otopark — statik
  parçalar kampüs ağına birleşik, Düşük'te 0 ek çizim), tempo plana göre (Kasaba Hastanesi ~13.–14. vardiya ≈ 1 sa,
  Şehir Hastanesi ~44. vardiya ≈ 3 sa). Ayrıntı `docs/ART.md`, `docs/BALANCE.md`.
- Doğrulanmadı: Unity Editor'da sahne, cihazda görünüm, perf_gate, emülatör smoke, AAB (`docs/UNITY.md` §2).
- Boş kimlikler (P8, Patron): AdMob ödüllü birim + uygulama, RevenueCat anahtarı, Firebase config.
- Sahip onayı bekleyen `ad_free` listesi: noads, premium, bundle (+ v1.1 chief_pass).
