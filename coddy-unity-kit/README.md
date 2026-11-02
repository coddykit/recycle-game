# CoddyUnityKit + Unity game generator (U0, 2026-11-02)

| Path | What |
|---|---|
| `CoddyKit/` | **the one kit source** (taken from Volt City's `Assets/CoddyKit` 2026-11-02 = the newest of Volt / LastDigit / Silent Evidence: ConsentGatedAnalytics, consent mode v2 `CoddyConsentDefaults.androidlib`, UMP callbacks on the main thread, finger-hold list fix, wrapping list cards, toast width / ClearAll, SafeAreaPadding pad flags) + `GameSpec.Landscape` (portrait stays the default). Never edit a game's copy. |
| `gen_house_related.py [--check]` | writes `CoddyKit/Core/Ads/HouseRelatedTable.g.cs` from `games/tools/ads/house_related.py` (+ PENDING rows for new Unity games). |
| `gen_kit_icons.py <unity_project> [--check]` | the kit's white UI icons (`UI/Resources/coddykit/icons`). |
| `sync.sh [--check] <game>...` | copy the kit into `<game>/unity/Assets/CoddyKit` / drift = FAIL. Game-owned: `UI/CoddyTokens.uss`, `UI/Resources/CoddyPanelSettings.asset`. Existing `.meta` never overwritten. |
| `new_game.sh <slug> <package> <Namespace> <portrait\|landscape>` | new game skeleton from `template/` (see `games/NEW_GAME_PLAYBOOK.md` 7a). |

Volt City, LastDigit and Silent Evidence still carry their own (older or equal) copies; they move to `sync.sh` in their next
update (U0 did not touch them; `sync.sh --check` shows the drift).
