# Analysis
Monetization extends the completion scope; the old offline/no-SDK privacy statement
must be replaced. The existing game has no backend, so use native RevenueCat receipt
validation and non-consumable entitlements; never trust a UI purchase button as proof.
A separate entitlement cache avoids reset/prestige erasing purchases. Ad cancellation,
load failures and duplicate completion callbacks must never mint cash. Missing
production IDs are an explicit release blocker, not a simulated success path.
