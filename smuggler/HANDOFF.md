# Smuggler implementation handoff — 2026-10-09

Work stopped at the owner's request. Source changes are saved in this application folder. No commit, store upload or publication was performed. This is an implemented quality/retention checkpoint, **not a fully release-verified build**. Read CLAUDE.md and this document before continuing; the older review describes the original defects, not current implementation status.

## Delivered source

- Save schema v2 with nested validation, v1 migration, future-version write protection, atomic writes, backup protection and visible save failures. Connections now serialize correctly.
- One offline elapsed-time budget; saved trips finish without spawning new offline dispatches. Manager income uses the remaining eligible interval. Settlement receipts and wallet changes commit together, roll back on write failure and survive reloads. The return dialog acknowledges already credited earnings.
- Dynamic message fragments translate before composition. NitroRouter `gpt-6-luna` translations cover 595 keys in ten languages; no translation credentials are embedded in the added source.
- Larger controls, wrapped HUD goals, bounded scrolling modals, Android Back handling and presentation suppression during onboarding/native activity. Reduced-motion and background audio/haptic safeguards.
- Dot's next-action advice, manual access and an optional progress-aware reminder. Show Me opens the relevant panel; a follow-up target spotlight is not implemented.
- Three persisted daily logistics jobs, UTC day handling, bounded rewards (maximum 600 per day), transactional claims, duplicate prevention and prestige preservation.
- Multi-seed pacing and procedural scene/model QA. Checkpoint flag/booth placements were separated to remove detected coplanar overlap candidates.
- Firebase Core/Analytics wiring restored after a shared-checkout reset. QA suppresses analytics. Earlier Android runtime evidence is in `docs/firebase-runtime-evidence.txt`; the new checkpoint APK was not installed or revalidated for Analytics.

RevenueCat and live advertising setup remain deferred by the owner. Existing commerce code has not been certified by this work.

## Evidence and its limits

| Check | Result | Qualification |
|---|---|---|
| Dedicated quality regressions | 60 checks, 0 failures | Latest recorded isolated quality run; not all subsequent visual/source edits were retested |
| Gameplay suite | 218 checks, 0 failures | Earlier implementation checkpoint; final recovered source needs a complete rerun (one Analytics assertion was added afterward) |
| Localization | 595 keys × 10 locales pass | Completeness/placeholders/source inventory, not human linguistic review |
| Desktop rendered UI | 19/19 cases, 135 panel captures | Ten dark locales, four narrow light locales, tablet and four modal cases; native density/safe areas untested |
| Modal QA caveat | FIXED 2026-10-20 (W18-3) | The reset case now presses the real Settings → Reset progress button (never Erase), reopens it after Back and prints `QA_RESET_DIALOG:true` / `QA_RESET_KEPT:true`; `tools/qa_quality_ui.py` fails the case without them |
| Pacing | Five seeds × daily on/off, 3,600 simulated seconds | Declared budgets pass; simulated rather than human first sessions |
| Scene QA | 82 models; eight regions × low/high; no reported issues | Fault injections pass; static face overlap and finite-transform checks do not prove all ground contact/occlusion |
| Android | Debug export succeeded | `build/BorderlineEmpire-quality-checkpoint-debug.apk`; final event-key/scene-spacing edits may postdate packaging; no fresh native smoke test |
| iOS | Archive failed | Xcode account login rejected; development provisioning profile missing for `com.borderlineempire.game` (bundle is now `com.coddykit.borderlineempire`, ASC app 6821004024) |

Machine-readable evidence: `docs/quality-implementation-status.json`, `docs/ui-quality.json`, `docs/pacing-quality.json`, `docs/scene-quality.json`, and `docs/translation-api-quality.json`. Screenshots and copied execution logs are under `docs/qa/quality/` (local, ignored). Old `docs/verification.json` describes an older build, not this source checkpoint.

## Next session: ordered work

1. Check the source SHA-256 manifest in `docs/quality-implementation-status.json` before changing files. Import the project, then rerun the full gameplay suite and dedicated quality suite on this recovered source. Investigate failures; do not just update expected results.
2. (Reset-modal QA target fixed 2026-10-20.) Visually inspect the final checkpoint spacing and representative narrow-screen captures. Continue real interaction testing (tutorial, advice navigation, daily claim, save failure, return dialog and Back).
3. Rebuild Android from final source and run native lifecycle/resume, touch/safe-area, reduced-motion and Analytics checks. Test rapid resumes, process death and failed storage on devices. The copied checkpoint APK is not final-source proof.
4. Restore the Xcode account session/provisioning, then produce and test an iOS build. The configured team ID alone does not establish signing readiness. The generated failed Xcode export remains in the private recovery directory listed below.
5. Reconcile Firebase registration/privacy documentation and store declarations with actual Core/Analytics behavior. Check the current policy text and release checker. iOS Analytics and both stores' declarations remain release gates.
6. Review broader acceptance gaps: full offline interruption/automation combinations, native modal/SDK interruptions, haptic/audio behavior and human pacing. Reduced motion covers primary UI/camera effects; ordinary world/vehicle motion remains. Semantic feedback has a shared facade but not every legacy sound call was migrated.
7. Update the plan with evidence. Keep deferred revenue work and optional P2 features separate; do not label the app 100% ready until required device/signing/privacy gates pass.

Useful commands (run from this app directory; all test scenes use isolated QA saves):

```sh
godot --headless --editor --path . --import
godot --headless --path . res://tests/test_runner.tscn
godot --headless --path . res://tests/quality_test.tscn
python3 tools/i18n/validate.py
godot --headless --path . res://tools/scene_quality.tscn
python3 tools/qa_quality_ui.py
python3 tools/qa_pacing.py
python3 tools/check_release.py
```

Read existing tool arguments and the base workflow instructions before rerunning native setup/builds. The release checker can still fail on explicitly deferred commerce as well as outstanding release evidence; report those separately.

## Recovery and process ownership

During implementation, tracked files in the shared checkout were externally reset. Correct source was reconstructed and tested in `/tmp/smuggler-quality-recovery/app`, then copied back into this application only. No repository-wide reset, stash or cleanup was used. Avoid commands that affect sibling apps or their concurrent sessions.

- Recovery implementation and generated iOS project: `/tmp/smuggler-quality-recovery/app`.
- Pre-copy versions of changed main-workspace source: `/tmp/smuggler-quality-recovery/pre-handoff-source`.
- Copy script: `/tmp/smuggler-quality-recovery/finish_handoff.py` (do not rerun blindly after new edits).
- These `/tmp` paths are secondary backups and may be cleared; the main application source and evidence are the durable handoff.
- All started UI/build jobs finished. The isolated Gradle daemon was stopped. No new feature work or long-running build was left queued.

## Release pipeline (2026-10-09)

Jenkins `Games/smuggler-mobile` (NAME `BorderlineEmpire`): `jenkins/game.env`, `jenkins/build.sh` (test | android | ios),
`tools/build_android.sh` (signed AAB, upload key `~/.android-keys/borderlineempire/` on the Mini, backup on the dev Mac),
`tools/build_ios.sh` (team 68563ZRFVT, automatic signing, App Store Connect export/upload). Non-git `ios/plugins`
(RevenueCat 2.4.0 + godotx_firebase 3.1.0) on the Mini: `/Volumes/BuildSSD/game-deps/BorderlineEmpire/ios/`.
Release exports use the real AdMob app ids (`project.godot [admob]`) and rewarded units (`data/monetization.json`);
debug builds switch to Google's test units in `scripts/monetization/rewarded_ads.gd`.
Mini verification (game-verify dir): tests 219/0 pass, Android AAB 96 MB signed with the upload key, iOS Godot export +
unsigned archive OK; the signed archive needs the login keychain unlocked (Jenkins does it), over plain ssh it fails
with errSecInternalComponent.
