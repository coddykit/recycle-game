# Docket Day: Law Firm Rush — game plan (#184, S1, 2026-10-31)

> Docs only. English. Original design; no Codigames names, art, UI, text or unique mechanics are reused.
> Slug (proposal): `docket-day` / `com.coddykit.docketday`. Fleet rules: `games/CLAUDE.md`; IAP: `games/tools/IAP_STANDARD.md`.
> Sources inline as [S#]; list at the end. Unverified items are labelled.

## 1. Reference summary (Codigames — Play title "Law Empire Tycoon - Idle Game"; the brief's "Idle Law Firm")

- **Title / package:** Law Empire Tycoon - Idle Game, `com.codigames.idle.law.empire.tycoon`, Codigames. Released **Aug 31, 2021**; last updated **Nov 20, 2025** (corrected 2026-10-31 from C3 raw `/Volumes/BuildSSD/dev/i18n-work/tasks/next-wave-2026-10-31/raw/com.codigames.idle.law.empire.tycoon.json` → `release_date` / `updated_date`) [S1]. (An iOS app "Idle Law Firm: Justice Empire" exists, published by another company per search results — unverified, not the Codigames title [S6].)
- **Traction:** Play badge **1M+**, rating **4.40** from **51,739** ratings [S1]. ASO-Atlas scrape 2026-10-20: **3,494,966** installs [S2]. Free with ads + IAP ($0.99 – $139.99 per item); content rating Everyone (C3 raw JSON, same file).
- **Core loop** (store text [S1] + review evidence [S1b]):
  - Accept cases (traffic ticket, divorce, scam…), hire lawyers, interns, detectives, receptionists and delivery workers; staff carry files between offices; investigators collect evidence; a trial resolves the case.
  - Open new lawyer offices to unlock case types; research and construction run on multi-hour timers; vault capacity caps stored money; premium currency speeds timers [S1b].
  - One "season event"; later updates removed the investigation feature and added "leadership points" (reviewers confused) [S1b].
  - Progression is short: reviewers finish it in weeks to ~2–3 months; an area is "coming soon" for a long time [S1b].
- **Market position:** this keyword is almost unoccupied: `lawyer game` Low d41, `court game` Medium d42 [S4]. Codigames' law game is the only management title above 1M in the `lawyer game` shelf (3.49M); the rest are narrative (Ace Attorney series, Devil's Attorney 0.57M), edu (iCivics), or small 3D (Lawyer Life 3D 0.31M) [S2]. Judge 3D - Court Affairs 9.12M and Guilty! Choose The Justice 6.23M show demand for light court fantasy [S2].

## 2. What players love / hate (sample = first ~20 reviews on the public Play page, 2026-10-31 [S1b]; not representative)

**Love**
- "Loved the developer's idle games", "interesting little management game", addictive expansion and milestones, optional ads, "beatable in 2–3 months without spending" [S1b].
- Funny little workers; the one **court case / season event** was the most entertaining part [S1b].

**Hate**
1. **Too short and thin:** "fun for about 2 hours of gameplay", "nothing left to upgrade", "game is basically pointless after 2 months", abandoned/"coming soon" area never delivered [S1b].
2. **Time-gating:** upgrades/research take hours; "20 min of play then wait 4 h"; builders cost money [S1b].
3. **Content removed / confusing:** investigation features removed; confusing how to advance the court case; leadership points unexplained; no day/night cycle or clock that sister games have [S1b].
4. **Ads/monetization:** ad-free quoted at ~$12; reward ads not paid out; constant re-offer of "limited time" packs even with nothing to unlock [S1b].
5. **Caps and soft-locks:** vault cap lower than an upgrade price (item costs 3.8M, vault caps at 3.5M) → cannot finish without paying; income capped so idling doesn't work [S1b].
6. **Delivery logic:** couriers run files but the fraud department never receives any [S1b].

**Review pain → our answer (design contract)**
| Pain (source S1b) | Our answer |
|---|---|
| Too short / nothing left to upgrade | Chapters + Rebrand prestige + matter templates × judges × clients |
| Hours-long timers, paid builders | Minutes-long builds, scheduling is the challenge, not waiting |
| Features removed / confusing court case | Court flow shown on the matter card; hearing scene always explains odds |
| Ad reward not paid, $12 ad-free | Grant on SDK "earned", idempotent; `noads` $2.99; one offer per session |
| Vault cap below upgrade price | No vault / stored-cash cap at all (the money-vault cap is a Codigames mechanic; we do not use it) |
| Couriers/files logic broken | Matters are data on a rail, not couriers; no hidden delivery bugs |

## 3. OUR game

**Working title:** **Docket Day: Law Firm Rush** (25 chars).
**Options:** (b) *Brief & Bold: Law Firm Boss* (27) · (c) *Billable Hours: Law Tycoon* (26).
**Name check (WebSearch 2026-10-31):** no Play listing found for "Docket Day", "Billable Hours" or "Brief & Bold" — results were practice-management software articles and the Codigames/other law idle games [S6]. "Docket"/"Billable" appear in B2B legal-software names (DocketBuddy, Lawbillity) → ASO noise, not a store conflict. Avoid "Law Empire", "Justice Empire", "Idle Law Firm". **Play-UI search not possible here → P0/P7 manual check.**

**Pitch.** Your firm starts in a garage-front office in the city of **Calderport**, across the street from a courthouse that never moves its hearing dates. Every client matter is a string of **dated milestones** — intake, filing window, discovery, hearing. Your job is not to wait for timers; it is to **fit people-hours into a calendar that collides with itself**. Slot lawyers, paralegals and interns into the **Docket Rail**, decide when to settle and when to go to court, protect client money in the trust account, and walk through a short courtroom moment where your prep decides the verdict. Comedy-tinted, no real law advice, cartoon people and a tidy diorama of the office tower and the courthouse.

## 4. Core loop — chaos managed as a process

**Time:** 1 in-game day ≈ 3 real minutes at 1x; the **Docket Rail** shows the next 10 in-game days (a horizontal calendar strip). Fast-forward to the next "decision day".

1. **Intake (reception desk):** new clients walk in; each matter card shows area, fee model (hourly / retainer / contingency), value, deadlines, difficulty, **conflict check** (does this client conflict with an existing one?). Accept, decline, refer, or accept with an **ethical wall** (costs a room + paralegal time).
2. **Schedule:** drag people-cards (partner, associate, paralegal, intern, investigator, expert) onto the matter's tasks on the Docket Rail. Every task consumes **hours**; each person has daily hours (partners few and expensive, interns many and weak).
3. **Collisions:** hard deadlines (hearings set by the court) can't move; soft deadlines (filings) cost sanctions if missed. When two hearings fall on the same day and only one partner can attend, the player picks who covers, hires a visiting lawyer, or requests a continuance (once per matter, reputation cost).
4. **Prep table (20–40 s micro-moment, optional):** match 3–5 exhibit cards to the claims of the matter. Perfect = bonus odds. The paralegal automation does it for ~70% quality.
5. **Decide:** *Settle* (certain, lower fee, uses little partner time) vs *Litigate* (win odds from prep points, higher fee, partner time, risk of loss and reputation hit).
6. **Hearing (15–25 s animated scene):** the courthouse 2.5D room shows the result computed from prep + staff skill + judge profile (strict, chatty, tired on Fridays). No cross-examination mini-game; one "objection!" timing tap is optional flavour (±5% odds, can be turned off).
7. **Day-end:** billing run — hours become invoices; **client money** stays in the trust account until a matter closes; only fees move to the operating account.

What goes wrong, and the lever:
| Problem | Signal | Lever |
|---|---|---|
| Two hearings same day | red collision marker on rail | cover with associate, hire visiting counsel, request continuance, settle one |
| Filing window about to close | amber clock icon | reassign paralegal, pay overtime, intern runs file |
| Partner burnout | hours bar red, "tired" face | rotate, hire associate, decline a matter |
| Cash-flow dip (contingency matters pay late) | operating account line | take hourly work, call collections, bridge loan |
| Conflict of interest | purple "conflict" badge on intake | decline, refer, build ethical wall |

## 5. Systems

- **Matters:** area (Traffic, Tenancy, Small claims, Family, Contract, Employment, Insurance, IP, Corporate, Environmental later), tier (S/M/L/XL), fee model, milestones with dates and required skill, evidence deck, judge assignment.
- **People:** partner (rainmaker + trial skill), associate, paralegal (docs), intern (runner), receptionist (intake speed), investigator (evidence), clerk (calendar helper), expert witness (per-matter hire), plus a **coffee machine** morale object. Each: hours/day, skill tags per area, salary, fatigue, loyalty (poaching events).
- **Hours economy:** billable vs non-billable hours (admin, training, pro bono). Pro bono hours build **Standing** (reputation) which unlocks marquee clients. No hour is "free".
- **Accounts:** operating vs **trust** (client funds are locked); dipping into trust triggers an ethics sanction (a fun teaching constraint, not real legal advice).
- **Offices (Blender):** reception, partner offices, bullpen, document room, evidence room, library, conference room, break room, server closet, rooftop. Upgrading rooms increases hours, speed of intake, evidence quality.
- **Reputation by area:** each practice area has Standing (0–5 stars); gates matter tiers, hires and clients.
- **Judges:** 8 archetypes with simple modifiers (prefers settlements, strict on deadlines, sympathetic to small claimants) — shown on the matter card so players plan, not guess.
- **Time gating:** construction minutes, not hours; no builders-for-money; no vault or stored-cash cap (a Codigames mechanic we do not reuse), so no cap soft-locks.
- **Policies (automation, optional):** auto-intake rules (e.g., accept hourly small claims ≥ X), auto-assign paralegal prep, calendar warnings. Keep the player's decisions on settle/litigate and collisions.

## 6. Progression + content map (first ~10 hours)

Offline rule: the firm runs on **Standing Instructions** (auto-intake rules + assigned staff); no deadline can be missed offline — the engine either pre-completes soft tasks within capacity or parks them until you return ("Matter on hold, no penalty"); the Morning Brief shows fees earned and decisions waiting (offline cap 4 h; 8 h with `overnight_plus`).

| Chapter | Office / stage | ~Hours | New mechanic |
|---|---|---|---|
| 1 | The Garage Front (solo + intern) | 0–1.5 | intake, docket rail, hourly fees, first hearing |
| 2 | Main Street Suite | 1.5–3 | associate + paralegal, small claims/family, settle vs litigate |
| 3 | Courthouse Row | 3–5 | collisions, judges, retainers, prep table |
| 4 | Harbor Tower | 5–7 | trust account, conflicts/ethical walls, contingency fees |
| 5 | Skyline Floors | 7–9 | class actions: one evidence deck shared across 5 matters, expert witnesses |
| 6 | Calderport Partnership | 9–10+ | firm merger, partner politics (shares), recruiting from the law school |
| Meta | **Rebrand** (prestige) | 10+ | reopen a new firm name with permanent Standing perks and a harder client mix |

Chapter gates require Standing and caseload quality (win/settle ratios, zero ethics sanctions), not just money.

## 7. Crises / events

- **Daily:** surprise motion (adds a task), client no-show, power cut (no docs this hour), printer jam (cartoon), star associate poached.
- **Weekly "Hot Docket" (no pay-wall):** a themed set of matters with a deadline week; reward = cosmetic + small grant, no sold event currency.
- **Big events:** ethics audit (clean books = bonus), press leak (reputation drop unless a PR hour is spent), merger week (calendar overload), tax season (billing deadlines).
- **Judge events:** substitute judge changes the modifier; holiday closes the court (all dates shift: a rail-shifting puzzle).
- **Failure is soft:** missing a filing costs sanctions and Standing, never deletes a matter permanently; a lost trial pays a smaller "lesson" fee and unlocks an appeal option (once).

## 8. Map / diorama + art direction

- **Camera:** 2.5D tilted diorama with two linked scenes: the **office tower cutaway** (floors stack as you grow) and the **courthouse block** across the street; tap the courthouse to zoom into the hearing room.
- **Blender:** modular office floors (cubicles, partner suites, library, conference), courthouse facade + courtroom kit, street kit (steps, lamps, bench), evidence room props, signage kit (neutral fictional names), skyline backdrops.
- **Meshy:** client/lawyer archetypes (12 bases), judges (8), clerks, courthouse pigeon, mascot cat; wardrobe palettes for firm branding.
- **Tone:** comedy-tinted; no real laws, cases, people, firms or logos; legal "flavour only — not legal advice" line in About.
- **Low-profile plan (≤ 300k prims, ≤ 150 draws, 30 FPS):**
  - Low: only the active floor + courthouse animated; non-focused floors static silhouettes (one shared batch); people via MultiMesh (cap 30); no shadows; per-vertex lighting; hearing scene uses fixed pose crossfades (no skeleton updates beyond 3 characters); paperwork-flying particles disabled; update 15 Hz.
  - Medium: shadows simple, 60 people, paper particles ≤ 40.
  - High: shadows, MSAA 4x, window reflections as baked cubemap (no realtime).

## 9. Onboarding steps (show-and-do; no loss possible; Skip + replay in Settings)

1. "Welcome, Counsel." Tap the first client at the reception desk.
2. Tap **Accept** on the highlighted traffic-ticket matter.
3. Drag the intern card onto the glowing task on the **Docket Rail**.
4. Fast-forward one day (tap the glowing clock).
5. Tap the amber filing marker; drag a paralegal on it.
6. First hearing: open the **Prep table** and match two cards (hints on, can't fail in tutorial).
7. Watch the hearing; collect the fee; tap **Billing run**.
8. Build the next room: tap the glowing library icon, place it.
9. Meet the first **collision** (scripted: two hearings, one lawyer) and drag the associate to cover.
- Time frozen until each action; wrong taps re-show the step; `onboarding_step/complete/skip`. Context tips on first unlock of: conflicts, trust account, judges, class actions, expert witnesses.

## 10. Monetization (IAP_STANDARD; Play only; no trial; no loot boxes)

Id format `com.coddykit.docketday.<slug>`.

| Slug | Name | USD | Gives | ad_free? |
|---|---|---|---|---|
| `noads` | Remove Ads | 2.99 | no house interstitials; rewarded grants videoless | **YES** |
| `premium` | Managing Partner Edition | 8.99 | noads + gold nameplate/lobby theme + extra save slot + 1 extra standing instruction | **YES** |
| `partner_pass` | Partner's Pass (permanent) | 5.99 | noads + bonus docket preview (14 days) + saved intake rules | **YES** |
| `bundle` | Complete Docket Collection | 17.99 | premium + partner_pass + speed + overnight_plus + all cosmetics | **YES** |
| `speed` | Faster Days | 1.99 | 4x speed + skip billing animation | no |
| `automation` | Office Manager | 3.99 | auto-intake + auto prep assignment + calendar warnings (optional) | no |
| `overnight_plus` | Overnight+ | 2.99 | offline cap 4 → 8 h | no |
| `slots` | Extra Saves | 1.99 | +3 | no |
| `theme_office_*` (3) | Office styles (Classic, Loft, Art Deco) | 1.49 each | cosmetic | no |
| `outfits` | Suits & Gowns | 1.49 | cosmetic | no |
| `court_themes` | Courtroom themes | 1.49 | cosmetic | no |
| `cosmetics_all` | Cosmetic Collection | 4.99 | all cosmetic | no |
| `scenarios` | Practice Starters | 2.99 | start modes (family law boutique, startup counsel, public defender-style clinic) | no |
| `starter` | Starter Pack | 1.99 | operating cash + intern day pass, once on day 3–7, never first session | no |
| `retainer_relief` | Bridge Funds | 2.99 | consumable ≈ 2.5 days, offered once after a cash-flow dip (max 1/session, ≥3 days apart) | no |
| `comeback` | Welcome Back | 1.99 | after 3+ days away | no |
| `cash_s/m/l/xl` | Operating cash | 0.99/2.99/6.99/14.99 | 1/2.5/5/10 days of income (scaled) | no |

- Row for `games/CLAUDE.md` §8: `docket-day | noads, premium (Managing Partner Edition), partner_pass, bundle — speed/automation/overnight_plus/cosmetics/starter/cash packs do NOT remove ads`.
- Trust-account money can never be bought or "boosted" by IAP (keeps the teaching rule honest).
- **Rewarded (AdMob only; opt-in; fleet §4 caps: single money reward ≤ 1/5 of `cash_s`, per-UTC-day money rewards ≤ 1/2 `cash_s`, persistent cooldowns; ad_free owners get them videoless):**
  - R1 *Billing boost* (+15% of the day's invoices) — max 1/day, max 3/day total.
  - R2 *Case file preview* (reveals next 3 incoming matters' hidden difficulty; no money) — max 2/day.
  - R3 *Extra prep hint* in the prep table (natural pause) — max 2/day.
  - Nothing permanent (no pass, no cosmetics, no speed) is ever given by ad.
- **Ads:** house interstitial/banner only; interstitial after billing run close; never during hearing or tutorial; banner after onboarding; RC ad tracking per §5b; UMP before first ad; no repeated "limited time" pack spam (review theme 4) — one offer per session maximum.

## 11. ASO keyword set (Google Play)

- Title: **Docket Day: Law Firm Rush**. Short (80): "Run a law firm: juggle hearings, settle or litigate, and grow your practice." (draft)
- Core terms (bands [S4]): `lawyer game` (Low d41), `court game` (Medium d42), `law firm`, `law tycoon`, `lawyer simulator`, `attorney`, `judge`, `courtroom`, `legal`, `office manager`. Avoid "Ace Attorney", "Law Empire", "Justice Empire", "Idle Law Firm".
- 10 languages: tr (`avukat oyunu`, `hukuk bürosu`), de (`Anwalt Spiel`, `Kanzlei Manager`), es (`juego de abogados`, `bufete`), fr (`jeu d'avocat`, `cabinet d'avocats`), pt-BR (`jogo de advogado`, `escritório de advocacia`), ru (`игра адвокат`, `юридическая фирма`), ja (`弁護士 ゲーム`, `法律事務所 経営`), ko (`변호사 게임`, `법무법인 경영`), hi (`वकील गेम`, `लॉ फर्म मैनेजर`). Volumes unverified; native check P5/P7 via Haiku sub-agents.

## 12. Cannibalization with OUR games

| Our game | Overlap | How this differs |
|---|---|---|
| bureaucracy-office | queues, forms, processing | citizens at counters with stamps vs a **calendar/hours capacity puzzle** with risk decisions and money buckets |
| silent-evidence | evidence/justice | evidence deduction/detective vs light exhibit-matching inside a business sim |
| corrupt-tycoon | state/institutions | political favours vs professional ethics + client trust |
| moneytycoon | money/finance learning | personal finance vs firm accounting (trust/operating) |
| lastdigit | none | — |
| prison.md / police.md (this wave) | justice chain | law = **time capacity** (docket); prison = timetable of cohorts; police = spatial coverage. No shared meta-progression. |

## 13. Risks

- **Abstract core (calendar/hours) is less instantly legible than a visible queue** → the Docket Rail must be gorgeous, colour-coded and limited to 10 days; tutorial limited to 9 steps.
- **Legal accuracy / advice liability:** fictional jurisdiction, no real laws; disclaimer in About; no real cases/people.
- **Overlap perception with bureaucracy-office** → store assets show courthouse + calendar, not queues.
- **Narrow audience for law tycoon** (installs of the Codigames title 3.49M vs prison 38M [S2]) → treat as organic/ASO niche, low UA spend.
- **Thin content complaint** (top review theme) → matters generated from templates × judges × clients with arcs; reform ladder.
- **Courtroom scene cost** (animation) → fixed-pose crossfades; reuse per area.
- **Name collisions** (unverified in Play UI).

## 14. MVP scope

Chapters 1–3 (Garage Front, Main Street Suite, Courthouse Row), 5 matter areas, Docket Rail with hard/soft deadlines, 7 staff types, intake + conflict badge, settle vs litigate, prep table, hearing scene, billing run, trust account (basic), judges (4 archetypes), 4 crisis types, onboarding 9 steps, 10 languages, catalog without scenarios, rewarded R1–R2, house ads, save codec + fuzz, audio, Low profile validated. Chapters 4–6 and Rebrand after launch.

## 15. P0–P8 phase TODO

- **P0 Strategy:** this doc; Play-UI name search; ASO check; owner decision on title/tone; `house_related.py` tags.
- **P1 Core sim:** matter/milestone model, rail scheduler (hours, collisions), conflicts, trust/operating accounts, judge modifiers, odds model, billing; offline standing instructions; save codec + `save_fuzz` (mid-hearing, mid-collision, trust edge cases).
- **P2 Diorama/art:** Blender office floors + courthouse + courtroom kit, Meshy people, instancing, Low toggles, pose-crossfade hearing.
- **P3 UI/onboarding:** coddy_ui kit; Docket Rail widget (drag/drop, 48 dp targets, de/ja text fits), prep table, billing screen; 9-step onboarding + tests; TouchScroll gate.
- **P4 Monetization:** catalog + shop kit, ad_free + legacy, rewarded with caps, house ads, UMP, RC ad tracking, Firebase events, parity table vs Corrupt.
- **P5 i18n:** 10 languages + legal-term glossary per language (Haiku sub-agents), fonts, overflow tests.
- **P6 Perf/QA:** perf_gate (Low/Mid/High), 30 FPS p95 ≤ 50 ms, real-device PSS, Mini emulator smoke, in-app review gate at first won hearing (~3–5 min).
- **P7 Store assets:** full-bleed screenshots (courthouse + rail), trailer, listing ×10, Data safety, IARC.
- **P8 Internal → production:** INTERNAL; post-release steps (UMP, RC, privacy, ads backend registration, ASO Atlas); owner approval.

## Sources

- [S1] Play page: https://play.google.com/store/apps/details?id=com.codigames.idle.law.empire.tycoon (fetched 2026-10-31 by S1; rating/count/installs/description read from page)
- [S1b] Same page review block (20 reviews).
- [S2] ASO Atlas Play-US scrape 2026-10-20: `/Volumes/BuildSSD/dev/CoddyApps/marketing-data/asoatlas/chaos-2026-10-20/gp_US.json`
- [S4] ASO Atlas bands: `/Volumes/BuildSSD/dev/CoddyApps/marketing-data/asoatlas/chaos-2026-10-20/table.txt`
- [S6] Web searches 2026-10-31 (Pocket Gamer https://www.pocketgamer.com/law-empire-tycoon/ lists Codigames as publisher; App Store listing https://apps.apple.com/app/id1556452079 for "Idle Law Firm: Justice Empire"; name-collision searches). No Play-UI search possible.
