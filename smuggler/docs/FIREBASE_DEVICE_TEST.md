# Firebase device test — Borderline Empire (PENDING)

Not run yet with the current identity `com.coddykit.borderlineempire`. The earlier Android emulator run
(`docs/firebase-runtime-evidence.txt`) used the old `com.borderlineempire.game` app and does not count.

| | Android | iOS |
|---|---|---|
| Package / bundle | `com.coddykit.borderlineempire` | `com.coddykit.borderlineempire` |
| Firebase app | `1:998857716666:android:14dcaec9519b4b43d42383` | `1:998857716666:ios:df7af90fbbef8bf7d42383` |
| GA4 stream (property `557673345`) | `16096275075` | `16096334095` |

Source: `firebase apps:list` and Firebase Management API `projects/coddykit-game/analyticsDetails`, 2026-10-09
(`docs/firebase-registration.json`). `google-services.json` is the Firebase download trimmed to this one client.

## Steps

1. Checks first (on a `/tmp` copy, because Godot import can rewrite tracked files):
   `python3 tools/check_release.py`, `python3 tools/i18n/validate.py`,
   `godot --headless --import` then `godot --headless res://tests/test_runner.tscn`.
2. Android: `tools/build_android.sh` (debug APK) → `adb install -r <apk>` →
   `adb shell setprop debug.firebase.analytics.app com.coddykit.borderlineempire`.
   Logcat must show the Firebase app id `1:998857716666:android:14dcaec9519b4b43d42383` (not the old `e1cfc815…`).
3. iOS: `tools/native/setup_ios.sh` (Firebase frameworks), `tools/build_ios.sh`, signed device build, Xcode scheme
   argument `-FIRDebugEnabled`. GOOGLE_APP_ID must be `1:998857716666:ios:df7af90fbbef8bf7d42383`.
4. Firebase Console → coddykit-game → Analytics → DebugView, pick the device.
5. Expected events: `first_open`, `session_start`, `user_engagement`, `game_ready`, `screen_view`;
   finish a delivery → `delivery_completed`; finish the tutorial → `tutorial_complete`.
   Consent: `analytics_storage` granted; `ad_storage`, `ad_user_data`, `ad_personalization` denied.
   QA runs (`--qa`) must send nothing.
6. Afterwards `adb shell setprop debug.firebase.analytics.app .none.`. Record date, device and events in
   `docs/firebase-registration.json` (`device_verification.status` → `passed`) and tick the line in `PLAN.md`.
