# New game from the Bottle Route template (Unity 6) — what to keep, what to replace

`tools/new_game.py <slug> <Namespace> <Prefix> "<Product>"` cloned `bottle-route/` into `<slug>/` with every identifier
renamed. The clone compiles and passes Bottle Route's 125 tests as is. Your job: replace the **domain** with your game's
plan, keep the **infrastructure**. Reference implementation for every pattern: `bottle-route/` (read its files when you
need the detail; do not re-read the fleet docs end to end — the rules that matter are summarised below).

## Keep as is (infrastructure)

| Path (under `<slug>/unity/`) | Why |
|---|---|
| `Assets/CoddyKit/**` | fleet kit, verbatim. Only edit `Core/Ads/HouseRelatedTable.g.cs`: add a provisional row for your slug (copy the `bottle-route` row's idea: related fleet games first) and name it in the header comment |
| `Assets/Settings`, `Assets/GoogleMobileAds`, `Packages/`, `ProjectSettings/`, `Assets/Resources/ui/fonts`, `Assets/Resources/materials/palette.png` (+ `Tools/make_palette.py`, adjust colours if you like but keep swatch indices used by MeshKit) | project skeleton |
| `Assets/Scripts/Game/{Loc,Fmt,IapCatalog,GameSettings,GameSession,Commerce,Rewards,ReviewHook,GameEvents,GameActions}.cs` | generic plumbing — adapt names / placements / action ids, keep the rules (ad_free only from noads, consumables once per txn, offers, reward caps) |
| `Assets/Scripts/App/*`, `Assets/Editor/<Namespace>/*` | boot, kit config (ids stay EMPTY), project setup, Android build, fonts, icons, art import |
| `Assets/Scripts/UI/{<Prefix>Main,<Prefix>Audio,CoachView}.cs`, `Resources/ui/<Namespace>.uss` | wiring + guide view + audio; adapt to your screens |
| `Tools/*` | `check.sh` (the gate), `dotnet_check.sh`, `unity_ref_check.sh`, `build_iap.py` (edit its product TABLE), `i18n_merge.py` |
| `tools/meshy/*` (repo `<slug>/tools`) | Meshy client + optimizer (ledger + cap) |

## Replace (domain)

1. **Sim** `Assets/Scripts/Sim/*` (keep `Json.cs`, `Rng.cs`, the `SaveCodec` / `State` *pattern*, `BalanceRunner` *pattern*): your
   plan's core loop as an engine-free sim (noEngineReferences, C# 9, netstandard2.1, deterministic Rng, no per-frame
   allocation-heavy work). Day/shift cycle with a report, crises (short, never game over; none during the guide or the
   first ~3 days), forgiving start, upgrades/research ≤ 20 min, offline pay from player-set policies, sim side of the
   rewarded placements (caps per UTC day + cooldowns persisted in the save), save codec that accepts every state the
   sim can produce, balance bot.
2. **Data** `Assets/Resources/data/*.json` — numbers out of code.
3. **Coach** `Assets/Scripts/Game/Coach.cs` — the plan's onboarding steps (show-and-do, one thing per step, the step
   advances only on the real action, wrong tap = hint again, time paused while waiting for the player, fast-forward while
   waiting for the world, `Sync()` recovery after relaunch / leaving mid-step, Skip, replay from Settings).
4. **Strings** `Tools/i18n/en.json` only (English source; every UI text has a key; placeholders `{name}`). **Do not
   translate** — the other 9 languages are done centrally afterwards (fleet §9: Haiku agents). Run `python3 Tools/i18n_merge.py`.
   (`EveryLanguageIsComplete` will fail until then — that one failure is expected; everything else must be green.)
5. **IAP** `Tools/build_iap.py` TABLE + `iap.*` strings: the plan's §10 catalogue (no trial / subscription / loot box /
   premium currency; `noads` granted only by Remove Ads + edition + pass + bundle). `Commerce.PerkEntitlements`, rewards
   in `Rewards.cs` + `balance.json` (single reward ≤ 1/5, daily ≤ 1/2 of the cheapest pack).
6. **World** `Assets/Scripts/World/*` — your diorama (procedural, ONE palette material, props from a Blender kit in
   `Resources/kit`, instanced crowds; Low tier: ≤ 50 % crowd at 15 Hz, no shadows, target ≤ 120 draws) + picking.
7. **UI** `GameScreen.cs` / `Sheets.cs` — your HUD, tabs, sheets (kit components: `UiKit.Card`, `ActionPill`, `BottomSheet`,
   ≥ 48 dp targets, no visible scrollbar, settings privacy block + replay guide + language + music/effects + graphics).
8. **Tests** `Assets/Tests/EditMode/*` (sim) + `Assets/Tests/Game/*` (guide every step right/wrong/skip/relaunch, saves +
   fuzz, shop ad_free rules, reward caps = data, analytics names, every key exists, placeholders, IAP/strings sync, balance
   7 days). Keep the generic test classes, rewrite the domain ones.
9. **Art** (`<slug>/tools/art/`, Blender 4.0 is installed: `blender -b --python …`): a Blender kit on palette UVs →
   `Resources/kit/*.fbx` (copy `bottle-route/tools/art/kit.py` and its bevel/UV painting approach). Meshy only for LARGE
   characters / organic hero props (small crowd figures read worse — use `MeshKit.Figure`-style chunky figures). UI art
   via a `render_ui.py` like Bottle Route's: `Resources/ui/{coach_<role>,intro_1..3,curtain}.png`, `Art/Icon/icon_full.png`
   + `icon_adaptive_fg.png` (placeholder). Meshy: `cd <slug>/tools/meshy && TMPDIR=<scratch> python3 meshy.py preview|refine|wait|download …`
   then `optimize.py` → `bottle-route/tools/art/glb2fbx.py`. **Budget ≤ 70 credits per game**; never run two optimize.py in the
   same out dir at once (temp file race). Check every base-colour texture for text / logos.
10. **Docs** `<slug>/README.md` (Turkish, short), `docs/UNITY.md` (adapt), `docs/BALANCE.md`, `docs/MONETIZATION_PARITY.md`,
   `docs/ART.md`. Update `check.sh` art presence lists to your files.

## Fleet rules you must keep (CLAUDE.md, summarised)

Google Play only · no trial · ads: interstitial + banner = house only (kit), AdMob = rewarded only, opt-in, after earned
· `ad_free` = Remove Ads or big packs only · rewards never give what is sold · onboarding show-and-do + 10 languages ·
Low tier never lags (new heavy visuals Medium/High only) · saves never deleted (`SaveRepository`, `.unreadable`) · review
ask = OS sheet only, first session ~4 min, at a wow moment, never in the guide · no real brands / people / the reference
game's names, art or text (plan's originality rules) · release ids stay empty (never invent ids) · no secrets in git.

## Done = 

`TMPDIR=<your scratch> sh <slug>/unity/Tools/check.sh` → only `EveryLanguageIsComplete` failing (until the central
translation), everything else PASS incl. `UNITY_REF_CHECK`. **No git commands** (the lead commits). Report: what the game
is, file counts, test counts, balance table, Meshy credits, open questions.
