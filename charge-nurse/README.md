# Charge Nurse: Recycling Tycoon (Unity 6)

Plan: kökteki `RECYCLING_GAME_PLAN.md`. Filo kuralları: kökteki `CLAUDE.md`. Örnek alınan oyunlar: `smuggler/`
(Borderline Empire, Godot) ve monorepodaki `games/volt-city` (Grid Keeper, filonun ilk Unity oyunu; CoddyUnityKit
oradan kopyalandı).

**Oyun:** Kıyıdaki bir bölge depozito sistemini yeni açtı. Vatandaşlar şişe/kutu/camı makinelere getiriyor, makineler
doluyor; sen kamyonlara rota çiziyorsun (her km yakıt), boşaltma kuralı koyuyorsun (%60/%80/%95, her gün, çağrılınca),
depoda sayım hattı ücret kazandırıyor, pres balya yapıyor, balyayı günün fiyatından satıyorsun. Az boşaltırsan makine
taşar, insanlar geri döner, Yeşil Puan düşer; çok boşaltırsan yarı boş kamyonlar yakıt yakar. Krizler: sıcak hava,
maç günü, sıkışma, sensör arızası, toplu iade, arıza, yakıt zammı, balya fiyatı çöküşü, pres arızası, cam kırılması.
Gerçek DOA / TÜÇA / bakanlık / market adı yok (taklit kuralı).

## Klasörler

| Yol | Ne |
|---|---|
| `unity/` | Unity 6 projesi (URP, Android, portre). `unity/Assets/CoddyKit` = filo Unity kiti (volt-city'den) |
| `unity/Assets/Scripts/Sim` | `ChargeNurse.Sim` — motordan bağımsız simülasyon (yol ağı, makineler, kamyonlar, depo, pazar, krizler, ekonomi, kayıt) |
| `unity/Assets/Scripts/Game` | `ChargeNurse.Game` — motordan bağımsız oyun katmanı (oturum/kayıt, eylemler, 14 adımlı rehber, mağaza, ödüllü reklam, yorum anı, analitik, 10 dil) |
| `unity/Assets/Scripts/{App,World,UI}` | Unity katmanı: açılış, diorama, UI Toolkit ekranları |
| `unity/Assets/Editor/ChargeNurse` | proje kurulumu (sahneler, malzemeler, Android politikası), Android derleme, font, ikon, içe aktarma |
| `unity/Assets/Resources` | veri (`data/*.json`), metinler (`i18n/strings.csv`), kit modelleri, palet, arayüz görselleri, fontlar |
| `unity/Assets/Tests` | NUnit testleri (EditMode) — aynı dosyalar .NET ile de koşar |
| `unity/Tools` | `check.sh` (tüm kapılar), `dotnet_check.sh`, `unity_ref_check.sh`, `build_iap.py`, `i18n_merge.py`, `make_palette.py`, `i18n/*.json` |
| `tools/meshy`, `tools/art` | Meshy istemcisi + optimizer (filo aracı), Blender kiti, arayüz render'ları |
| `docs/` | `UNITY.md` (editörde ilk açılış), `ART.md`, `BALANCE.md`, `MONETIZATION_PARITY.md` |

## Durum (2026-11-02)

- **Doğrulandı (editörsüz):** `sh unity/Tools/check.sh` → `BOTTLE_ROUTE_CHECK: PASS`
  - 125 NUnit testi (sim 43, oyun katmanı 27, CoddyKit 55): rehberin 14 adımı baştan sona, yanlış dokunuş, atla/tekrar,
    yarıda bırakıp yeniden açma, kayıt güvenliği (fuzz, `.unreadable` saklanır), mağaza kuralları (`ad_free` yalnız
    noads/premium/route_pass/bundle), ödül tavanları, 10 dil eksiksiz + yer tutucular, denge botu 7/20 gün.
  - Unity'ye bağlı kod (kit Runtime + App + World + UI) UnityEngine 2021.3 referanslarına karşı derleniyor (yalnız 2
    Unity-6 API'si farklı); 83 C# dosyası iki tanım kümesinde sözdizimi hatasız.
- **Doğrulanmadı:** Unity Editor'da derleme, sahne kurulumu, cihazda görünüm, performans ölçümü (perf_gate), emülatör
  smoke, AAB. → `docs/UNITY.md` §2: ilk açılış adımları (Mac Mini / sahibin Unity'si).
- **Boş bırakılan kimlikler (P8, Patron):** AdMob ödüllü birim, AdMob uygulama kimliği, RevenueCat anahtarı, Firebase
  config. Uydurma kimlik yok (§5).

## Komutlar

```bash
sh charge-nurse/unity/Tools/check.sh
```

```bash
python3 charge-nurse/unity/Tools/i18n_merge.py && python3 charge-nurse/unity/Tools/build_iap.py
```

```bash
blender -b --python charge-nurse/tools/art/kit.py -- charge-nurse/unity
```
