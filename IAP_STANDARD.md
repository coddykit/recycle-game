# games/ — IAP standardı + oyun başına paket kataloğu (SAHİP 2026-10-18)

Sahip: "games'teki tüm oyunlarımızda standart olmalı: her oyun için ek IAP paketleri düşün, oyuncunun keyfini düşürmeyecek
şekilde; olabildiğince fazla olursa daha çok satabiliriz — reklamsız vs. gibi ek paketler ya da daha premium hissettirecek paketler."
Referans uygulama: **Corrupt State** (18 ürün, origin/main; `games/corrupt-tycoon/src/game/{Catalog,Store,StoreGate}.gd`,
`src/ui/{ScreensS,ShopDirector}.gd`, `src/sim/{offers,rescue,shop_grants}.gd`, mağaza tarafı `store/iap/`, T3 betikleri).
Yalnız Google Play (iOS şimdilik YOK — sahip 10-18). Hazırlayan: W1 çalışanı (calisan-28a11a07). Uygulamayı Patron dağıtır.

## 1. Değişmez kurallar (her oyun)
1. **Oyun baştan sona ücretsiz oynanır.** Duvar, gün sınırı, deneme/trial, enerji, zorunlu bekleme YOK. Oyun satış için zorlaştırılmaz.
2. **Zorunlu reklamı (interstitial) yalnız `ad_free` kapatır** = "Remove Ads" ürünü ya da onu içeren büyük/tam paket (premium, bundle, pass/VIP, starter'ın Remove Ads içeren hâli; oyun başına liste: `games/CLAUDE.md` §8) VEYA `legacy_ad_free` (kural gelmeden ÖNCE bir şey almış eski alıcı; tek seferlik göç). Ucuz paketler (para/kaynak, tek kozmetik, tek kolaylık) reklamı KAPATMAZ. Kodda `Ads.ad_free_check = func(): return Iap.ad_free()`; `purchased_any()` reklam kararında KULLANILMAZ (yalnız analitik / geri yükleme mesajı). Ayrıntı: `games/CLAUDE.md` §3-§4.
3. Abonelik yok, ganimet kutusu/şans yok, çocuk modunda (lego, cinnamon) consumable ve teklif açılır penceresi yok + ebeveyn kapısı.
4. Consumable (para/kaynak paketi) **sınırlı**: oyuncunun mevcut günlük gelirinin birkaç katı (Corrupt: 1/2.5/5/10 gün), ölçekli,
   oyunu bitirmez. Kalıcı ürünler geri yüklenir; consumable geri yüklenmez (mağaza metninde söylenir).
5. Teklif açılır penceresi: öğreticide ve ilk oturumda ASLA; oturum başına en fazla 1; kapatılınca aynı teklif ≥3 gün dönmez.
6. Ödüllü reklam yalnız isteğe bağlı bonus / "son koz"; hiç ödeme yapmamış oyuncuya; satın alan reklamsız bonusu doğrudan alır.
7. Fiyat: 4 PPP kademesi (T1 / ~%60 / ~%35 / ~%20), oyunda her zaman mağazanın `displayPrice`'ı. Ürün id `com.coddykit.<oyun>.<slug>`;
   mevcut id'ler ASLA değişmez (eski alıcılar korunur). 10 dil ürün adı/açıklaması.
8. Mağaza ekranı: Ayarlar + ana menüden erişilir, "Satın alımları geri yükle" hem mağazada hem Ayarlar'da, kilitli özelliğe
   dokununca mağaza o ürünle açılır. Sahip olunan ürün "Sahipsin" görünür.

## 2. Standart katalog iskeleti (her oyun bunu oyuna uyarlar)
| Katman | Ürün (slug) | Ref. USD | Ne verir |
|---|---|---|---|
| Temel | `noads` Reklamları Kaldır | 2.99–3.99 | interstitial'lar kapanır (`grants` `noads` içerir); ödüllü ödüller videosuz verilir (yalnız `ad_free`, aynı tavanlarla) |
| Premium his | `premium` Premium / "Edition" | 7.99–9.99 | noads + özel altın kozmetik (rozet/çerçeve/tema) + küçük kolaylık (ek kayıt yuvası / başlangıç bonusu) |
| Kolaylık | `speed` Hız Paketi | 1.99–2.99 | en yüksek hız kademesi (3×/4×) + gün sonu ekranını otomatik geç |
| Kolaylık | `automation` Yönetici / Kurul / Ekip | 3.99–4.99 | rutin işleri otomatik yapar (oyuncu isterse kapatır) |
| Kolaylık | `slots` Ek Kayıt Yuvaları | 1.99 | +3 kayıt |
| Kolaylık (idle) | `offline_plus` Çevrimdışı Kazanç+ | 2.99–4.99 | çevrimdışı oran/tavan yükselir |
| Kozmetik | `theme_*` / `skin_*` (3–6 adet) | 0.99–1.99 | yalnız görünüm, dengeye etkisiz |
| Kozmetik | `cosmetics_all` Koleksiyon | 3.99–4.99 | tüm kozmetikler (~%40 indirim) |
| İçerik | `scenarios` / `pack_*` | 2.99 | yeni başlangıç senaryoları / harita / kit paketi |
| Paket | `bundle` Hepsi Bir Arada | 14.99–19.99 | premium + kolaylıklar + kozmetikler (~%35–40 indirim) |
| Teklif (tek sefer) | `starter` Hızlı Başlangıç | 1.99 | gün 3–7; para + küçük kolaylık |
| Teklif (olay) | `crisis` / `comeback` / `milestone` | 2.99–4.99 | zor anda / 3+ gün dönüşte / terfi-prestij anında; consumable |
| Consumable | `cash_s/m/l/xl` | 0.99 / 2.99 / 6.99 / 14.99 | 1 / 2.5 / 5 / 10 günlük gelir (ölçekli) |

## 3. Ortak kod — ✅ HAZIR: `games/tools/shop/` (coddy_iap, 0622cbdc0d; kullanım `games/tools/shop/README.md`, Play betiği `store-automation/iap_catalog_gp.ts`)
Aşağıdaki ilk tasarım notu kayıt için duruyor:
Corrupt'ın mağaza katmanını oyundan bağımsız bir şablona çıkar: `games/tools/shop/` → `Catalog.gd` (veri: data/shop.json),
`Store.gd` + `StoreGate.gd` (entitlement + geri yükleme + ledger), `ShopScreen.gd` (gruplu ürün kartları, focus, restore),
`ShopDirector.gd` (kilit → mağaza, teklif kuyruğu), `iap_review` çekim kipi (`--iapcard product:<slug>`), testler
(`test_shop_template.gd`: katalog tutarlılığı, noads-ürünleri-reklamı-kapatır + legacy göç, restore tekrar vermez, eski id = premium). godot-iap (Corrupt)
ile RevenueCat (airport/train/zombi/lego/smuggler/aircraft/skiresort) arkaları için tek arayüz. Mağaza tarafı: T3 betiklerinin
(`store-automation/_corrupt_iap_catalog_gp.ts`, `_corrupt_rc_*.ts`) oyun parametreli hali (yalnız Play + RevenueCat).

## 4. Oyun başına katalog (MEVCUT korunur, ➕ = yeni)
### corrupt-tycoon — Corrupt State ✅ referans (18 ürün canlı mağazada; bu standarda zaten uyuyor)

### airport-baron — Airport Tycoon (mevcut 11: gw_pouch/crate/hangar/vault, starter_pack, hub_pass, skin_retro/aurora/midnight/candy, skin_collection)
➕ `noads` 2.99 · ➕ `premium` "Tycoon Edition" 9.99 (noads + altın "Gold Wings" uçak kaplaması + altın kule + 1 GW/gün) ·
➕ `speed` 2.99 (3× saat; Clock şu an 1×/2×) · ➕ `terminal_classic` / `terminal_neo` / `terminal_desert` terminal temaları 1.99 ·
➕ `vehicles_livery` yer araçları kaplaması 1.99 · ➕ `skin_airline_legends` 4 yeni uçak kaplaması 2.99 · ➕ `cosmetics_all` 6.99 ·
➕ `bundle` 19.99 (premium + hub_pass + speed + tüm kozmetik) · ➕ teklif `comeback_pack` (3+ gün dönüş, 150 GW, consumable 1.99) ·
➕ `prestige_pack` (ilk prestijde, 300 GW + 1 saat ×2, consumable 3.99). Not: GW harcama yolları zaten var, ek iş az.

### train-baron — Train Baron (mevcut 10: gs_pouch/crate/boxcar/vault, starter_pack, rail_pass, livery_royal_maroon/midnight_express/desert_sunset, livery_collection)
➕ `noads` 2.99 · ➕ `premium` "Baron Edition" 9.99 (noads + altın lokomotif kaplaması + altın istasyon) · ➕ `speed` 2.99 (3×) ·
➕ `skin_station_victorian` / `skin_station_artdeco` (şu an yalnız GS ile; nakit ürünü de olsun) 1.99 · ➕ `livery_diesel_era` /
`livery_electric_era` / `livery_highspeed_era` (çağ başına 3'lü set) 1.99 · ➕ `wagon_skins` 1.99 · ➕ `cosmetics_all` 6.99 ·
➕ `bundle` 19.99 · ➕ `comeback_pack` / `prestige_pack` (airport ile aynı mantık).

### zombi — Last Haven (mevcut 4: starter_pack, vip_pass, supplies_small, supplies_large — Play'de henüz oluşturulamadı: billing'li AAB lazım)
➕ `premium` "Haven Commander" 9.99 (vip_pass + altın sur/kule teması + 2. inşaat kuyruğu) · ➕ `speed` 2.99 (4×; şu an 1–3×) ·
➕ `builder_crew` 2. inşaat kuyruğu 3.99 · ➕ `offline_plus` 4 sa → 12 sa, %50 → %80, 2.99 · ➕ `skin_vehicles` (pickup/van/zırhlı
kamuflaj) 1.99 · ➕ `theme_walls` (sur/kule: ahşap barikat → beton kale → neon karantina) 1.99 · ➕ `outfits_survivors` 1.99 ·
➕ `cosmetics_all` 4.99 · ➕ `supplies_medium` 2.99 (small/large arası) · ➕ teklif `outbreak_kit` (salgın olayında, ilaç + karantina
hızlandırma, consumable 2.99) · ➕ `stage_pack` (yeni aşamaya geçince, consumable 3.99) · ➕ `bundle` 14.99. (Interstitial yok → `noads` yok;
vip_pass zaten reklamsız ödül.)

### lego — BlockForge (mevcut 1: builder_pass) — ÇOCUK MODU: ebeveyn kapısı, consumable/teklif penceresi YOK
➕ `pack_vehicles` (City & Rail) / `pack_robots` / `pack_space` — `data/progression/content_packs.json`'da "coming_soon" duruyor,
kitlerin yapılması gerekir, 2.99 her biri · ➕ `packs_all` 6.99 · ➕ `premium` "Master Builder" 7.99 (builder_pass + tüm renk paletleri +
tüm tezgâh temaları + altın showroom) · ➕ `palette_metallic` / `palette_neon` / `palette_pastel` 0.99 · ➕ `workbench_wood` /
`workbench_space` / `workbench_city` tezgâh+showroom temaları 1.99 · ➕ `stickers` çıkartma/desen paketi 0.99 · ➕ `bundle` 14.99.

### smuggler — Borderline Empire (mevcut 1: empire_pass — W5 çalışıyor; bu liste W5'e verilecek)
➕ `premium` "Kingpin Edition" 9.99 (empire_pass + altın filo boyası + karargâh teması + 1 ek otomasyon hattı) · ➕ `automation_plus`
ek otomasyon hattı 3.99 · ➕ `offline_plus` 2 sa → 8 sa 2.99 · ➕ `paint_street` / `paint_military` / `paint_luxury` araç boyası setleri
1.99 (`vehicles.json`'da `paint` alanı var) · ➕ `hq_theme` karargâh tabelası + şirket rengi 1.99 · ➕ `driver_portraits` 1.99 ·
➕ `cosmetics_all` 4.99 · ➕ `starter` 1.99 · ➕ `legacy_pack` (prestijde, consumable 3.99) · ➕ `cash_s/m/l` ⚠️ oyunun kendi planı
"ücretli nakit paketi yok" diyordu; sahip standardı bunu geçersiz kılıyor — Patron/sahip son kararı versin · ➕ `bundle` 14.99.

### aircraft — Parts Rush (mevcut 1: remove_ads)
➕ `premium` "Gold SLA Edition" 7.99 (remove_ads + altın havayolu kaplaması + hangar teması + 4. stand) · ➕ `speed` 2.99 (sim hızı
2×/3×) · ➕ `stand_plus` 4. eş zamanlı stand 3.99 · ➕ `auto_sourcing` parça tedarik müdürü (otomatik sipariş) 3.99 · ➕ `liveries_airlines`
(7 havayolu için özel kaplama) 1.99 · ➕ `theme_hangar` / `theme_night_apron` 1.99 · ➕ `vehicles_skin` 1.99 · ➕ `cosmetics_all` 4.99 ·
➕ `starter` 1.99 · ➕ `cash_s/m/l/xl` · ➕ `bundle` 14.99. Not: ödüllü "sponsor" reklamı premium/noads alana reklamsız.

### moneytycoon — Money District (IAP YOK; billing eklenmeli — RevenueCat iskeleti diğer oyunlardan)
➕ `noads` 2.99 · ➕ `premium` "Wealth Edition" 6.99 (noads + altın penthouse/araç görünümü + ek kayıt) · ➕ `speed` 1.99 (8×; şu an 4×) ·
➕ `slots` 1.99 · ➕ `scenarios` 2.99 (öğrenci kredisiyle başla / miras / iş kaybı sonrası yeniden başlangıç — eğitim değeri yüksek) ·
➕ `style_homes` / `style_cars` / `skyline_night` kozmetik 1.99 · ➕ `outfits` 1.99 · ➕ `cosmetics_all` 4.99 · ➕ `bundle` 12.99 ·
➕ `starter` 1.99. ⚠️ Finansal eğitim oyunu: nakit consumable önermiyorum (oyunun mesajıyla çelişir) — sahip isterse `bonus_paycheck_*` eklenir.

### skiresort — Snow Empire (mevcut 1: remove_ads; `ski_store/live_enabled=false` — canlı mağaza kapalı)
➕ `premium` "Alpine Edition" 7.99 (remove_ads + altın gondol + VIP dağ evi teması) · ➕ `speed` 2.99 (en yüksek hız kademesi) ·
➕ `auto_ops` otomatik ratrak/kar üretimi 3.99 · ➕ `skin_gondolas` / `skin_snowcats` / `theme_chalet` 1.99 · ➕ `cosmetics_all` 4.99 ·
➕ `return_plus` dönüş temettüsü tavanı 8 sa → 16 sa 2.99 · ➕ `starter` 1.99 · ➕ `cash_s/m/l/xl` · ➕ teklif `storm_kit` (fırtına/kaza
olayında, consumable 2.99) · ➕ `bundle` 14.99 · (sonra) `map_second_mountain` 4.99 içerik paketi.

### trading-sim — Trading Sim (IAP YOK; W4 çalışıyor) — ⚠️ belgelerdeki "Full Game unlock" duvar olur → YERİNE Premium
➕ `noads` 2.99 · ➕ `premium` "Fund Founder Edition" 7.99 (noads + altın terminal teması + takım elbise) · ➕ `pro_tools` gelişmiş grafik
göstergeleri + ek izleme listeleri 3.99 · ➕ `scenarios` 2.99 (2008 krizi / dot-com balonu / kripto çılgınlığı başlangıçları) ·
➕ `slots` ek kariyer 1.99 · ➕ `office_decor` / `outfits` / `chart_skins` 1.99 · ➕ `cosmetics_all` 4.99 · ➕ `speed` 1.99 (8×) ·
➕ `starter` 1.99 · ➕ teklif `bailout` (iflasa yakınken, sermaye consumable 2.99) · ➕ `capital_s/m/l` · ➕ `bundle` 14.99.

### travel-agency-game — Travel Tycoon: Cappadocia (mevcut 6: venue_kings/queens/kayaseda/tasevra/tufmar, town_pack)
➕ `premium` "Cappadocia Gold" 9.99 (altın balon + ajans ofisi teması + ek kayıt yuvası) · ➕ `balloons_designs` balon desenleri 1.99 ·
➕ `vehicles_livery` (jeep/ATV/minibüs) 1.99 · ➕ `office_skins` 1.99 · ➕ `staff_outfits` 1.99 · ➕ `photo_frames` 0.99 ·
➕ `cosmetics_all` 4.99 · ➕ `slots` 1.99 · ➕ `speed` 1.99 · ➕ `scenarios` 2.99 (sezon dışı başlangıç / rakip devralma) ·
➕ `bundle` 29.99 (town_pack + premium + kozmetik). Zümrüt consumable: hesap/bulut kayıt olmadan geri yüklenemez → şimdilik YOK.

### cinnamon-cappadocia — Cinnamon (mevcut 1: full "Tüm Macera") — REKLAMSIZ + çocuk/aile: ebeveyn kapısı, consumable/teklif YOK
➕ `paints_atv` Kızıl için 5 yeni boya 0.99 · ➕ `bandanas_plus` Tarçın için 5 bandana 0.99 · ➕ `outfits_efe` 0.99 ·
➕ `balloon_liveries` 0.99 · ➕ `cosmetics_all` 2.99 · ➕ `deluxe` "Deluxe Macera" 6.99 (full + tüm kozmetik) ·
(sonra) `chapter_kizilirmak` Avanos güncellemesi 2.99.

## 5. Uygulama sırası önerisi (Play geliri, mevcut kurulu taban)
1. Ortak mağaza şablonu (§3) — 1 çalışan.
2. Canlı Play'de olanlar: airport-baron, train-baron (yalnız ekleme; mevcut 10–11 ürün korunur), travel-agency-game, cinnamon.
3. Play'e yakın: aircraft, skiresort (live_enabled kapalı → aç), moneytycoon (billing ekle), zombi (billing'li AAB).
4. Geliştirmede: trading-sim (W4), smuggler (W5), lego (içerik paketleri kit üretimi ister — en büyük iş).
Her oyunda: ürünleri Play + RevenueCat'te oluştur (yalnız Android), 10 dil metin, testler, QaUi benzeri 10 dil mağaza taraması,
build-only AAB, Patron'a rapor. Fiyat/denge değişikliği bu standart dışında yapılmaz.
