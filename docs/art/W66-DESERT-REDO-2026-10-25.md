# W66 Smuggler — çöl yeniden yapımı (C3, emir #7)

Baz: `3c32cbdb814f27642cb2ae4ceca59aeb8b24a442` (origin/main).
Kanıt: `/Volumes/BuildSSD/game-builds/w66/smuggler/redo-1025/`.

## Değişiklik

- Altı Blender kerpiç ev: açık düz çatı korkuluğu, girintili kapı, pencere/söve, farklı renk ve aşınma parçaları, üç tente, su deposu/çift depo, baca ve daha yüksek ev silueti. Düşük **194–198 üçgen/ev**, eski `house_adobe` **204**. Orta/Yüksek **390–394**: çatı erişim basamakları, kirişler, söveler ve daha fazla aşınma. Üretici `tools/blender/build_desert_redo.py`; 15 GLB toplam **280.928 bayt**, tek yüzey, gömülü doku/malzeme yok. Mevcut palet SHA-256: `f12cd7fea75b0dfe17d1bbe2ae8624612b9a4c77bfbc9ac0c4cfe9a262cd3e96` (değişmedi).
- Mevcut ev yerleşim sırası, footprint, RNG çağrıları ve gölge ayak izi korundu. Ev varyantları MultiMesh, Düşük'te bölgenin mevcut tek palet mesh'ine birleşiyor. Ağır detay yalnız Orta/Yüksek. Profil değişimi eski birleşik mesh'i bırakıp doğru modeli yeniden birleştiriyor.
- Turuncu nesne: `RegionDressing.build → _sign` içindeki, kasaba başına rastgele yönlendirilmiş yazısız turuncu panel. Mevcut kasaba etiketleri aynı bilgiyi verdiği için Dust Valley'nin üç boş paneli kaldırıldı; yerleşim/RNG akışı korundu. `before/medium/cactus.png` içindeki Cactus Junction yol kenarı paneli, `after/medium/cactus.png` içinde yok.
- Kilitli arazi: ayrı önizleme mesh'inde soluk kum, mevcut çöl dalga/gren kodu ve kesik sınır şeridi. Şerit aynı yüzeyde: ek draw/material/render pass yok. Kilit açıldığında özgün biyom ve şeritsiz mesh geri geliyor. İlk komşu kilitli bölgede üç Blender bitkisi: yayvan akasya, alçak adaçayı kümesi, ince servi; mevcut üç MultiMesh grubunu kullanıyor.
- `check.sh` başlangıçta ana daldaki beş eski coddy_ui kopyasını yakaladı. Yalnız oyunun kopyası, mevcut ortak kit ile `games/tools/ui/sync.sh` üzerinden eşitlendi; ortak kit değiştirilmedi. Yeni çöl regresyonu kapıya eklendi.

## Düşük önce/sonra

Aynı Godot 4.7.2 Compatibility renderer, 540×1170, seed 6601025, üretim World ve kamera. Sanat kıyasında araçlar gizli, dünya/kamera tam konumda dondurulmuş: hareketli araçların kadraja girmesi ve kamera yumuşatmasının süre farkı sanat bütçesini kirletmiyor. `tests/desert_perf_hook.gd`, normal `perf_gate.gd` sayaçlarını kullanır.

| Sahne | prim önce → sonra | değişim | draw önce → sonra | statik MB |
|---|---:|---:|---:|---:|
| Açılış | 18.576 → 18.790 | +%1,15 | 33 → 33 | 84 → 85 |
| Kilitli önizleme | 27.248 → 27.714 | +%1,71 | 37 → 37 | 84 → 85 |
| Uzak harita | 55.134 → 55.134 | %0 | 43 → 43 | 85 → 86 |

**Ek draw 0; prim ≤+%5; doku bütçesi aynı; yeni shader dalı/pası ve kare başına ek CPU işi yok.** Dondurulmuş sanat sahnesindeki TIME_PROCESS örnekleri başlangıç değerini taşıyabilir; telefon p95 kanıtı olarak kullanılmaz. Ham satırlar `before/perf-art.log`, `after/perf-art.log`; makinece kontrol edilen delta `low-budget.json`.

Canlı oynanış kapısı (`check.sh`, araçlar açık):

```text
PERF_GATE start prims=31126 draws=78 ui_draws=45 process_ms=5.88 (p95 5.88) frame_ms=2.83 static_mb=98 PASS
PERF_GATE midgame_near prims=33796 draws=39 ui_draws=74 process_ms=7.83 (p95 7.83) frame_ms=2.09 static_mb=101 PASS
PERF_GATE lategame_far prims=81856 draws=83 ui_draws=66 process_ms=7.94 (p95 7.94) frame_ms=3.36 static_mb=103 PASS
PERF_GATE: 3 scenes, 0 failures (limits: primitives 300000, draws 150, static 250 MB)
```

## Doğrulama ve kareler

- `sh games/smuggler/tools/check.sh`: exit 0; kit/TouchScroll/dil, tüm test sahneleri, layout scan ve normal perf kapısı geçti.
- `desert_redo_test.tscn`: **46 kontrol, 0 hata**; altı gerçek mesh, tek yüzey, Düşük bütçesi, altı varyantın sahnede olması, tier round-trip, üç bitki, kilit aç/kapat ve özgün biyomun korunması.
- Final modeller yeniden import edildikten sonra çöl testi tekrar **46/0**; sanat perf kapısı **3/0**.
- `before/{low,medium}/{start,cactus,locked}.png` ve `after/{low,medium}/{start,cactus,locked}.png`: **12 gerçek Godot viewport karesi**; `frames.json` SHA-256 kaydı. Tente üst yüzeyi, ev siluetleri ve sınır çizgileri görsel incelendi. Yakın ev/panel kıyası `cactus.png`; kilitli görünüm `locked.png`.
- Bunlar sessiz masaüstü sanat/perf kanıtıdır; Android release smoke veya gerçek cihaz FPS/PSS kanıtı değildir. Emülatör/simülatör, sürüm değişikliği, AAB, mağaza/DB yazması yapılmadı.

Son kontrol: sanat commit’i `665339d65f` rebase edilirken ortak UI kit `055be6f86d` ile ilerledi. Oyundaki `ui_shop`, `ui_theme`, kit testleri de bu kaynağa eşitlendi. Tam `check.sh` yeniden **exit 0**; kanıt `check-final.log`. Son üçgen/draw satırları yukarıda bu final koşudan alınmıştır. Sanat A/B ölçümü değişmedi.
