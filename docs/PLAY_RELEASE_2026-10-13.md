# Google Play release — 13 October 2026

## Release candidate

- Package: `com.coddykit.borderlineempire`.
- New version: **1.1.0 (1791874686)**, built on the Linux Android build host and transferred through the Mac Mini.
- AAB SHA-256: `b7d44db8be78bad17d689bfda271caa2ee87ccedcac380ad31180a46f3681307`.
- Old internal draft **1.0 (1791611984)** must not be selected for production.
- **Uploaded to internal as a draft**, committed and read back successfully through the Play API. Google edit validation passed. Evidence: `docs/play-upload-1.1.0.log`. Production is unchanged.

## Verified on this source

- Godot 4.7.2 import and gameplay: **220 checks, 0 failures**.
- Quality: **60 checks, 0 failures**.
- Localization: **595 strings × 10 languages**.
- Release-gate unit tests: **8 passed**.
- Correct signing certificate, AAB ZIP integrity and official bundletool 1.18.3 validation verified. Embedded manifest confirms 1.1.0 / 1791874686 / target SDK 36. Universal APK generated from this exact AAB, and `zipalign -c -P 16 -v 4` passes. All arm64 native ELF load segments are 16KB aligned; report: `docs/android-bundle-validation-2026-10-13.json`. APK ZIP alignment/device runtime are separate checks. No store deck, Data Safety CSV or IAP catalogue files leaked into the AAB.
- Reset-dialog QA repaired and visually checked at 320×640 in German.
- CI now runs quality/localization/release-gate tests and fails immediately on project-import errors.
- Play API read-back: 10 localized listings; seven screenshots for each of phone, 7-inch and 10-inch formats per locale; icon and feature graphic present.
- Empire Pass product exists with active `buy` purchase option and 171 regions. This does not prove a successful device purchase.

## Play Console work

- Data Safety and listing were already complete in the dashboard.
- Ads = Yes saved and confirmed (dashboard advanced from 2/11 to 3/11).
- Government app = No saved and confirmed (dashboard 4/11). Financial features = None and Health features = None saved and confirmed (dashboard 6/11).
- Category/contact confirmed: Game → Simulation; coddy@coddykit.com; https://coddykit.com (dashboard 7/11).
- The other Console forms remain incomplete: privacy-policy URL, app access and target audience.
- IARC questionnaire completed and saved with owner-approved terms. Play reports IARC status **Completed**, PEGI 3 / ESRB Everyone / Brazil 14+ with In-Game Purchases. Screenshot: `docs/qa/play-release-2026-10-13/iarc-completed.png`. Dashboard: **8/11**.
- Owner confirmed intended target audience **13+**. Play blocks this questionnaire until Sign in details is complete; it is not yet saved.


## Production gates still open

1. Complete and read back all Console declarations and production country availability. The updated Sign in details form explicitly includes one-time paid products and requires full premium access for reviewers. No working reviewer entitlement/promo-code path has been verified; the form was left unsaved rather than falsely declaring unrestricted access.
2. The public CoddyKit privacy policy opens in a normal browser, but its per-app SDK section does not list Borderline Empire. Reconcile it with the game's Firebase Analytics, AdMob and RevenueCat use. Local game-specific policy is `store/privacy_policy.md`; a publication-ready addendum draft is `store/PRIVACY_POLICY_ADDENDUM.md`. The general public page was not entered as a fully compliant policy while its app-specific scope conflicts with the SDK use.
3. Native tests on the final candidate: launch, pause/resume, Back, consent, rewarded completion/cancellation, purchase/cancellation/restore and Analytics. Record actual evidence and the exact AAB hash in `docs/native-sandbox-verification.json`; do not fabricate test passes.
4. Record verified Android Analytics/Data Safety declarations in `docs/analytics-store-declarations.json`.
5. Run `python3 tools/check_release.py --platform android` against the final artifact. Configuration-only success is not production clearance.
6. Select the new uploaded version for production only after these gates pass. No production release has been made in this work.

## Limited native smoke

The universal APK derived from the uploaded AAB installed successfully on the dedicated Android emulator. Package Manager confirms 1.1.0 (1791874686). Godot main loop and Firebase plugin initialization were logged, with no captured GDScript parse/fatal crash errors. This is startup evidence only, not visual gameplay or purchase verification. Android release ignores external Godot QA command-line arguments, so this was a normal startup (a game_ready event was logged); the app was stopped. Do not use this route for automated QA that must exclude analytics.

## Final gate result

`python3 tools/check_release.py --platform android` correctly remains BLOCKED on verified Analytics declarations and native device/revenue evidence. Do not bypass it for production.

## Evidence locations

- Local AAB and manifest: `build/android/borderlineempire.aab` and `borderlineempire.release.json`.
- Local test logs: `docs/qa/play-release-2026-10-13/linux/`.
- Mini release directory: `/Volumes/BuildSSD/game-release/BorderlineEmpire-20261013/`.
- Linux isolated build: `/home/jenkins/smuggler-release-20261013/`.
- Initial API snapshot: `docs/play-snapshot-2026-10-13.json`.
