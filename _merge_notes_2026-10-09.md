# Merge notes — 2026-10-09 (HEAD 451eb630dd vs 16:51 autostash)

Nothing was unmergeable. The stash variants below were not copied verbatim, because the
HEAD text already contains them or replaces them. They are recorded here so nothing is lost.
Full stash copies: `git show stash@{0}:games/smuggler/<path>` and branch `backup/autostash-1651-2026-10-09`.

- `data/monetization.json`: stash had the same RevenueCat keys and EMPTY `rewarded_android`/`rewarded_ios`.
  Kept HEAD's production AdMob rewarded ids:
  `ca-app-pub-5045495642689387/6320083416` (Android) and `ca-app-pub-5045495642689387/1682000256` (iOS).
- `export_presets.cfg`: the stash Firebase block equals HEAD's without `core_version="22.1.0"`/`analytics_version="23.2.0"`. Kept HEAD.
- `scripts/core/debug_tools.gd`: the stash `review_modal` block is identical to HEAD's. HEAD also has the `matrix_dir` QA capture block. Kept HEAD.
- `scripts/core/game_manager.gd` `set_setting`: the stash saved immediately for `["language", "theme"]`. HEAD's list
  `["language", "theme", "reduced_motion"]` is a superset. Kept HEAD.
- `PLAN.md` language count: the stash said "565 metin × 10 dil". Kept HEAD's 595 (tools/i18n/validate.py: 595 keys).
  The stash Firebase/GA4 details (property/stream ids, consent flags, emulator evidence) were kept in full under the HEAD bullets.
- `tools/check_release.py`: merged. Kept HEAD's tolerant JSON read (a missing or broken file means evidence is missing) and the stash's per-platform messages.
- `tools/native/setup_ios.sh`: the commands are identical. Took the stash's more explanatory comments.

Not a merge issue, but noted: `export_presets.cfg` has `package/unique_name` and
`application/bundle_identifier` = `com.borderlineempire.game` on BOTH sides (HEAD included). The product id is
`com.coddykit.borderlineempire.empire_pass`. Check this against the store bundle `com.coddykit.borderlineempire`.
  RESOLVED 2026-10-09: both export ids switched to `com.coddykit.borderlineempire`.
