# Borderline Empire — App Review notes (App Store Connect "Notes" field)

Source of truth for the ASC review detail (`store-automation/_smuggler_asc.mjs base` uploads the text block below,
≤ 4000 chars). Google Play has no equivalent field; the same facts back the Play declarations in
`store/STORE_READINESS.md`.

```text
Borderline Empire — App Review Information

A screen recording (borderlineempire-review.mp4, attached) shows the app launching, the first guided steps, the Shop with the Empire Pass and its price, and the Restore Purchases button.

What the game is: a casual, single-player idle logistics tycoon. The player runs a cartoon delivery company in eight made-up countries: accept delivery contracts, pick a vehicle and a route (Fast / Normal / Safe), watch the low-poly truck drive, upgrade vehicles, hire drivers and automate dispatch. General audience.

About the theme (please read): the title "Borderline Empire" and the in-game "Heat" meter refer to light-hearted, fictional "shadow logistics" — discreet courier jobs and border crossings in invented countries. The game contains NO drugs, alcohol, tobacco, weapons, violence, gambling or real crime. Cargo is generic and harmless: "Mystery Crates", "Priority Delivery", "Restricted Electronics", "Secret Documents", "Luxury Goods", "Rare Artifacts", VIP / anonymous passengers. Road events are comic: breakdowns, traffic, weather, a rival crew tailing the van, or a routine paperwork inspection ("A routine stop. Paperwork, please!") that the player resolves by waiting, paying a processing fee, or talking. Risk is shown only as percentages and numbers. All countries, companies, people and goods are fictional; no real places, brands or organisations appear. The game does not encourage or teach illegal activity. Age rating answers: mature/suggestive themes = infrequent/mild (implied shady-courier flavour only); every other category = none.

Setup & access: no account, no sign-in, no demo account needed. The app opens to an animated introduction and a step-by-step tutorial; progress is saved on the device. Core gameplay works offline.

In-app purchase (this iOS version sells only the Empire Pass; the larger catalog of version 1.2.0 is Android-only for now; nothing is a subscription):
- Empire Pass (com.coddykit.borderlineempire.empire_pass) — $3.99 (USA): permanent ×1.25 delivery payouts and sponsor bonuses without watching videos.
How to reach it: tap the Business tab (bottom bar, right) → "Shop & sponsor bonus" (also Settings gear → "Shop & sponsor bonus"). The Shop shows the Empire Pass with the store's local price, a Buy button, "Restore Purchases" and the purchase terms. Any sandbox Apple ID works.

Restore Purchases: in the Shop panel, below the Empire Pass card.

Advertising: optional rewarded "sponsor" videos (Google AdMob), started by the player from the Shop, and — in version 1.2.0, for players who do not own Remove Ads, Kingpin Edition, the Ultimate Bundle or the Empire Pass — a cross-promotion for another of the developer's games between play sessions (on iOS only for games that are on the App Store); no banners, no AdMob interstitial on iOS. Google UMP consent is shown where required. The app does not request App Tracking Transparency and does not track users across apps; without ATT permission no SDK receives the advertising identifier (IDFA).

Other services: RevenueCat validates purchases and restores ownership via an anonymous app user id. Firebase Analytics measures sessions, screen views and gameplay milestones with an app instance id; analytics ad storage / personalisation signals are denied. No personal data (name, email, contacts, location) is collected; the company name the player types stays on the device.

Regional differences: none, except the UI language (10 languages) and the local price. Not offered in mainland China.

Third-party material: none. 3D models were made in Blender for this game, audio and voice lines are original, the font Baloo 2 is OFL-licensed.

Terms of Use: https://www.apple.com/legal/internet-services/itunes/dev/stdeula/
Privacy Policy: https://www.coddykit.com/pages/privacy-policy
Support: https://coddykit.com/contact
```
