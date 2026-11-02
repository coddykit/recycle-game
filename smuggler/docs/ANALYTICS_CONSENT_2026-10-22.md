# Analytics opt-in — fleet consent (#42)

Before: Firebase initialization granted analytics_storage and enabled collection for every non-QA player, without an analytics switch.

After: Settings → Privacy includes the shared UiRow “Share anonymous analytics” switch in all ten game languages. The new separate `analytics_consent.cfg` starts OFF for both new and existing players. It is read before Firebase startup; the choice is applied when the native SDK initializes and whenever the switch changes. No game-save schema, purchase, economy or performance code changed.

OFF denies analytics_storage and disables collection. No event is buffered without opt-in; revoking during initialization clears the pending queue. All three Firebase advertising signals remain denied even with analytics ON, so the switch never overrides missing UMP advertising consent. UMP code and export privacy-safe defaults are unchanged.

Validation:
- Full `tools/check.sh` PASS: shared kit/touch gates, translations, 17 test scenes (including 28 new analytics checks), EN/DE/RU layout scan, and Low performance gate.
- Real Privacy UiRow + fake native SDK verify OFF → ON → OFF, persistence/restart, malformed preference, initialization race, no event replay and always-denied ad signals.
- The existing IAP event-order fixture now explicitly represents an opted-in player; its standalone UI fixture initializes the bundled theme. Production IAP code is unchanged.
- 663 keys × 10 locales; the new label’s glyphs exist in the actual bundled font/fallback stack.
- Low perf gate: 3 scenes, 0 failures; primitives 32,785 / 30,561 / 69,092; world draws 85 / 47 / 80; static memory 96 / 99 / 101 MB. The gate checks geometry/draws/static memory; this is not an Android FPS certification.
- Pre-existing shared UI copy drift fixed via official sync.sh (pager/palette/icon-library additions; app icons unchanged).

Native SDK calls are verified with doubles, not device DebugView/network delivery. No AAB, emulator or store write. Evidence: `/Volumes/BuildSSD/game-builds/C2-fleet-consent/smuggler/`. After rebase, shared kit 346/0 and analytics 28/0 passed again.
