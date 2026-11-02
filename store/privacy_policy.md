# Privacy — Borderline Empire

> **OUTDATED — 2026-10-22:** the privacy / data-collection statements in this file (for example "no ads", "no analytics", "no advertising ID", "non-personalised ads", "Data Not Collected") no longer match the current code. Source of truth: the live policy https://www.coddykit.com/pages/privacy-policy (sections 12.9 Cinnamon, 12.10 games table) and this game's `store/play-data-safety.csv`. Re-check against the code before reusing any answer below.

Updated 2026-10-09 for the rewarded-ad and in-app-purchase implementation.

Game progress, company name and settings are stored on the device. No game account
or cloud save is required. Reset Progress deletes the empire; purchase ownership
and the sponsor cooldown are retained separately. Reinstalling deletes local
progress; Restore Purchases can recover Empire Pass with the same platform account.

Optional rewarded ads use Google AdMob. Ad services may process device information,
ad interactions and network information to deliver ads, prevent fraud and measure
performance. The app requests non-personalized ads and uses Google's consent flow
where required. Privacy choices are accessible in the Shop. Rewards require an
earned-reward callback; closing an ad early grants nothing.

Purchases are processed by Apple or Google and validated through RevenueCat. These
services process purchase receipts, transaction data and an anonymous app user ID
to determine and restore ownership. The app does not receive payment card details.

Firebase Analytics processes app instance identifiers, sessions, screen views and gameplay
milestones to measure app usage. The game does not send company names, saved games,
account details or payment details to Analytics. Analytics storage is enabled; advertising
storage, advertising user data and ad personalization consent remain denied. Test/QA
runs are excluded from collection.

No purchase or advertisement is required for core gameplay. Network access is
needed for ads, new purchases and restoring ownership. Previously verified Empire
Pass benefits remain available offline. Vibration can be disabled in Settings.

Production release must provide the public policy URL, support contact and store
privacy declarations matching the final enabled SDKs and their data practices.
