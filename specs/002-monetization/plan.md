# Plan
Reuse the native Godot AdMob and RevenueCat plugin integrations from train-baron,
without sibling credentials. Add a monetization autoload with a separate persistent
entitlement cache and rewarded cooldown, a consent-aware rewarded adapter, and a
shop sheet linked from Business and Settings. Store prices and entitlement checks
come only from the native SDK. Keep unavailable states honest. Update privacy copy,
translate new UI via the same approved API, and add deterministic callback tests.
