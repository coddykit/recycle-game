# Smuggler: final review and next implementation plan

Review date: 2026-10-09. Player-facing name: Borderline Empire.
Status: **implementation checkpoint handed off at the owner's request.** See [HANDOFF.md](HANDOFF.md) for current evidence and remaining work. The original review below is historical.

## Implementation checkpoint

| Item | Current status |
|---|---|
| FIX-01–05 | Implemented; dedicated quality suite passes. Final full regression rerun remains. |
| FIX-06–08 | UI, Back, motion and feedback changes implemented; desktop matrix passes with the reset-harness caveat. Native interaction/lifecycle checks remain. |
| FIX-09 | Five-seed pacing report completed with daily jobs on/off. Human pacing review remains. |
| FIX-10 | Android debug export succeeded; iOS signing blocked. Final native and store privacy evidence remains. |
| ADD-01 | Advice and reminder implemented; Show Me opens a panel without a subsequent spotlight. |
| ADD-02 | Transactional daily jobs implemented and regression-tested. Native interaction checks remain. |
| ADD-03 | Model/region scanner and fault injections pass; final rendered contact/occlusion review remains. |

No item is claimed fully release-verified. RevenueCat/live ads remain deferred.

## Scope and conclusion

The core game is playable and its existing regression suite passes. It is not defect-free or fully release-verified. The next stage should fix offline time accounting, save validation and dynamic localization before adding retention features.

RevenueCat integration, catalog setup, purchase behavior and purchase testing are explicitly outside this review at the owner's request. Live advertising setup also remains deferred. Optional advertising ideas below are design candidates, not authorization to provision services or change the business model.

This document is the next implementation backlog. [PLAN.md](PLAN.md) remains the historical completion/release record; [CLAUDE.md](CLAUDE.md) remains the project-specific instruction source. Before implementation, create the relevant specify/clarify/plan/tasks/analyze documents and run the base pre-implementation gate. Do not mark this backlog complete on the strength of the previous 218 tests.

## Evidence collected in this review

| Check | Current result | Limits |
|---|---|---|
| Godot 4.7.2 headless gameplay suite | 218 checks, 0 failures | One deterministic seed, largely manager-level testing; expected parse errors belong to corruption fixtures |
| Localization validator | 565 keys × 10 complete locales; placeholders/source inventory pass | Does not prove that composed runtime strings are translated |
| Additional isolated offline probe | Confirmed repair timer advances twice for part of an absence | 60 seconds away reduced a repair from 1,000 to 908 seconds; expected 940 |
| Additional malformed-save probe | `_read()` accepts a vehicle with `upgrades: []` | Reader acceptance reproduced; a full malformed-save crash matrix has not been run |
| Turkish dynamic event probe | `Risk +5%, Vehicle damaged` | English fragment survives inside a formatted summary |
| Fresh rendered Russian settings screenshot | 360×720 desktop window; no current automated button/overflow errors | Visual inspection shows cramped/truncated HUD text; not a native-device density or safe-area test |
| Existing UI evidence | Previously recorded 80/80 panel checks | Not rerun as a complete matrix this review; tests suppress tutorial/modals and use dark theme |
| Release checker | Still blocked | Excluding deferred commerce: iOS signing and Analytics store-declaration evidence are missing |

Reproducible commands:

```sh
godot --headless res://tests/test_runner.tscn
python3 tools/i18n/validate.py
python3 tools/check_release.py
godot --path . --resolution 360x720 -- --qa --fresh --notitle --notut --nomodal --theme=dark --lang=ru --panel=settings --shot=/tmp/smuggler-review-ru-settings.png --delay=0.6 --wait=0.6 --audit
```

Local review artifacts are in `docs/qa/final-review-2026-10-09/`: gameplay log, isolated probe source/output, UI log and screenshot. This generated evidence directory is intentionally git-ignored. The probe uses `--qa`; never run it against player saves. Important results are recorded above so this plan does not depend on temporary files.

## A. Fix backlog

Priority meanings: **P0** before economy/retention additions; **P1** next quality pass; **P2** later polish. “Confirmed” means observed in a probe or directly established by the source path. “Risk” means a targeted regression/device reproduction is still needed.

### FIX-01 — Advance offline repair time exactly once

**P0 · Confirmed behavior defect.** In `scripts/core/game_manager.gd::_apply_offline`, the trip loop calls `VehicleManager.tick(1.0)` and then calls `VehicleManager.tick(seconds)` again. Repairs therefore receive the simulated trip duration plus the entire absence.

- Reproduction: start the tutorial trip, put a second vehicle into repair with 1,000 seconds remaining, apply 60 seconds offline. Actual remaining time: **908**; expected **940**.
- Fix: use one elapsed-time budget. Track the simulated portion and advance only the remainder, accounting correctly for repairs that start partway through that interval.
- Acceptance: pre-existing repairs, repairs started by returning vehicles, no active jobs, short absence and capped long absence each receive exactly their eligible elapsed time. Double resume must not award a second interval.
- Dependencies: none. Add a regression before changing the algorithm.

### FIX-02 — Define one offline economy policy and durable settlement

**P0 · Confirmed ambiguous implementation; exploit/overpayment magnitude unverified.** The comment promises to finish trips already underway, but `_apply_offline()` calls `ContractManager.tick()`, which also spawns offers and runs automation. A Regional Manager additionally receives `saved_ipm × elapsed × 0.5`. The exact trip simulation and estimated manager income can therefore cover overlapping time. No dedicated test currently defines the intended combination.

Offline earnings are already added before `hud.gd::_on_offline()` displays a **Collect** button with an empty callback. That is an acknowledgement presented as a claim. Settlement is not immediately committed by `_apply_offline`; ordinary save/debounce behavior determines persistence. On resume, the report is both emitted and stored as metadata, which also needs a consume-once policy.

- Proposed policy: finish the saved in-flight jobs once; prohibit new offline dispatches in that phase; award a bounded manager dividend only for explicitly defined eligible time. Preserve the current two-hour cap unless balance evidence supports a change.
- Use a persisted receipt containing ID, save generation, elapsed/capped time, job settlement, manager rate and applied state. Choose either immediate durable credit with an acknowledgement label or a genuine pending claim; do not mix both models.
- Do not add an ad multiplier in this fix. Specify how unavoidable in-flight losses are disclosed and ensure an optional return screen never introduces new hidden penalties.
- Acceptance: restart during settlement, repeated resume, clock rollback, cap boundary, no jobs, all automation levels, negative/net-zero results and storage failure. Receipt is consumed once; the same report does not reappear after a language/theme reload.
- Files: `scripts/core/game_manager.gd`, `scripts/contracts/contract_manager.gd`, `scripts/ui/hud.gd`, `scripts/core/save_manager.gd`.

### FIX-03 — Validate nested saves before choosing a primary over its backup

**P0 · Validation gap reproduced; crash consequences require fixtures.** `SaveManager._read()` checks section containers but not nested vehicle/job data. `_section()` only checks a few immediate fields. `VehicleManager.from_dict()` expects a dictionary of upgrades; `ContractManager.from_dict()` casts `path` to an array and later code expects IDs, phases and valid route nodes.

- Validate nested types, required IDs, unique IDs, references, known definitions, route nodes, enum values and finite/nonnegative numeric ranges before applying a save.
- A structurally valid JSON file with invalid gameplay data must not displace a valid backup. Do not rotate such a file over the last valid backup.
- Distinguish corrupt data from a future unsupported schema; preserve both files rather than overwriting an unsupported newer save with a fresh game.
- Introduce explicit version migrations when adding return receipts/daily state. Keep valid version-1 progress intact.
- Acceptance: malformed upgrades/path/jobs, missing vehicle ID, dangling driver/job references, duplicate IDs, extreme numbers, unknown schema and interrupted migration. Loading yields a valid playable state or a clear recoverable failure without GDScript errors.

### FIX-04 — Make saving failures visible and recoverable across all entry points

**P1 · Confirmed missing error propagation; disk-full behavior not exercised.** Manual Save reports failure, but autosave/quit callers generally ignore the return value. `save_settings()` returns no result and writes directly to its destination. A language/theme setting is persisted and applied before `save_now()` can fail, potentially leaving the current UI only partly refreshed.

- Use a shared save-health state with a non-spamming visible warning and retry policy. Persist settings through a validated temporary write; preserve the previous settings on failure.
- Check write/flush/rename outcomes, not just whether the file opened. Define rollback or consistent in-memory behavior for failed language/theme changes.
- Acceptance: failed open/write/rename, interrupted settings write, repeated autosave failures, retry after storage recovers and failed language change. Never report success for a failed operation.

### FIX-05 — Translate fragments before composing dynamic messages

**P1 · Confirmed localization defect.** `EventManager._describe()` appends the untranslated `Vehicle damaged`; event resolution also emits untranslated success/failure summaries. `event_modal.gd::_fail_text()` composes raw `contract lost`, `damage` and `nothing`; the heat label formats an English template before passing it to a widget. `hud.gd` assigns `EVENT!` directly. Godot's automatic label translation cannot reliably translate an already-composed sentence by matching individual fragments.

- Translate each fragment/template before formatting/joining. Audit dynamic assignments and runtime descriptions, not only literal inventory coverage.
- Add runtime assertions for event success/failure, heat, damage, pending-job labels, notifications and goals across all ten locales.
- Preserve EN/TR/DE/ES/FR/JA/KO/PT/RU/ZH. New or changed translations must use NitroRouter `gpt-6-luna` through the existing pipeline.
- Acceptance: no unintended English fallback in these localized runtime messages; placeholders and numeric values remain correct.

### FIX-06 — Coordinate modal presentation and define Android Back behavior

**P1 · Confirmed policy gaps; full interaction reproduction pending.** `hud.gd` already has a modal queue, but drains it whenever `_modal_open` is false without a common onboarding/title/native-flow suppression policy. Back returns immediately for any open modal. `ModalBase` has a fixed-width, non-scrolling body; long localized privacy/event content is not covered by the existing panel matrix.

- Extend the existing queue with priorities and suppression predicates; do not add a second popup manager.
- Define cancellable information/confirmation dialogs separately from journey decisions. Back should dismiss/cancel safe surfaces; an event must remain reachable if deferred and must not silently select an outcome.
- Make long modal content scrollable within safe bounds while keeping primary actions reachable. Avoid camera focus changes during manual camera input.
- Acceptance: onboarding plus queued stage/return/event messages, multiple events, language changes, small/notched devices, Android Back and long privacy text. One interactive interruption at a time; no hidden choice or trapped screen.

### FIX-07 — Test real mobile target size, text and interaction states

**P1 · Confirmed verification gap and visible HUD truncation.** The control audit only checks visible Button sizes in the 720-wide design canvas and horizontal bounds. At the reviewed 360-wide desktop render, an 88-unit design target spans approximately 44 image pixels; this is not proof of a 48-unit native touch target. Labels, vertical clipping, overlap, pointer interception and accessibility are not checked. The Russian HUD goal/stage text is cramped in the inspected screenshot.

- Establish the actual viewport-to-device logical-size conversion. Guarantee comfortable targets at the smallest supported device scale, including close buttons and map controls.
- Reflow the HUD goal/stage row; preserve readable localized text through wrapping, spacing or a details affordance instead of shrinking type.
- Expand QA beyond `--nomodal --notut`: intro/name keyboard, each tutorial gate, privacy, event choices, reset/prestige confirmations, offline report, empty/disabled states and late-game fleet.
- Matrix: small phone, tall/notched phone, tablet and desktop; both themes; all ten locales with focused DE/RU/CJK review; scrolling and Back/focus actions.
- Acceptance: target-size evidence in device units, reachable primary actions, no clipping/overlap, and explicit screenshots for states ordinary panel tests suppress.

### FIX-08 — Verify background feedback and add reduced-motion behavior

**P1 · Source-level gap; native behavior unverified.** AudioManager processes always and has no explicit app-lifecycle handler. `UIManager.haptic()` respects a preference but has no throttling or lifecycle gate. Motion is distributed across tutorial rings, modal loops, UI tweens and camera/world effects; settings have no reduced-motion control.

- Introduce a semantic feedback facade around existing audio/effect/haptic code. Pause/restore audio consistently and suppress feedback while backgrounded; throttle burst events.
- Add a persisted reduced-motion setting that disables shake/showcases and replaces pulsing/bouncing with static emphasis. Keep sound, voice and haptics independently controllable.
- Acceptance: background/resume on Android/iOS, disabled vibration, burst of 100 events, rapid setting changes and reduced-motion tutorial. Existing effects remain intact with defaults enabled.

### FIX-09 — Measure first-session pacing before changing balance

**P1 · Evidence gap, not a proven balance defect.** The bot uses `seed(12345)` and prints milestones without statistical pacing budgets. This review recorded Auto Accept at **23m 01s**, first depot at **24m 23s**, and Auto Dispatch at **44m 02s**. These are simulated times, not human-session results. Tutorial guidance ends and does not become a post-tutorial advisor.

- Extend the existing simulator, not a second economy implementation: multiple seeds, resource-starvation periods, meaningful waiting, failures, first upgrade, second vehicle, second region and automation times.
- Separate fixtures from the measured session and emit machine-readable configuration/seed reports with median and tail values.
- Establish genre-specific pass/fail budgets before tuning; pair the report with a human first-session playthrough. Do not copy Fuel Baron's timings as targets.

### FIX-10 — Complete release evidence outside the deferred revenue work

**P1 for release · Verification gate.** Android Analytics upload was verified previously; iOS runtime verification, signing and App Privacy/Play Data Safety evidence remain incomplete. No new native-device pass was performed in this review.

- Verify signed native builds, iPhone safe areas/background behavior, low-end Android frame time/memory/thermal behavior and long sessions.
- Complete and read back the actual store analytics declarations; do not fill evidence flags as a documentation shortcut.
- Keep these gates separate from local gameplay quality and the deliberately deferred RevenueCat/AdMob work. Public submission remains a separate user instruction.

## B. Fuel Baron feature transfer decisions

Reference: [Fuel Baron fleet playbook](../FUELBARON_FLEET_IMPROVEMENT_PLAYBOOK.md), G01–G12. Targeted original source inspection covered `HudGuide.cs`, `LiveOps.cs`, `Commerce.cs` and `FlickerTests.cs` under `/Users/mehmetcanker/Desktop/monorepo-rnapps-main/fuelbaron/Assets/FuelBaron/`. This was not a Unity run or a full audit of Fuel Baron. Train daily-goal and Last Haven return-flow source were also inspected as Godot adaptation references.

| Playbook item | Smuggler inventory | Decision and adaptation |
|---|---|---|
| G01: post-introduction recovery help | **Partial:** condition-based tutorial, registered UI targets and goal chip exist; no post-tutorial stuck advisor | **Adopt, P1.** Extend Dot with one reachable next action and blocker explanation: unassigned vehicle, affordable upgrade, garage/region gate or unresolved event. Detect lack of meaningful progress, not touch inactivity. |
| G02: immutable reward requests | **Partial:** sponsor token, frozen amount and duplicate-callback guard exist; durable cross-save grant reconciliation does not | **Adopt foundation, P0/P1.** Use save-generation and persisted receipt/claim IDs for new return/daily rewards. Leave RevenueCat out. Harden the optional sponsor path only in a separately scoped follow-up. |
| G03: return-session receipts | **Partial:** capped offline simulation and return modal already exist | **Adopt via FIX-01/02.** One clear, durable settlement with elapsed time, cap and breakdown; no second offline system and no initial ad multiplier. |
| G04: daily jobs/calendar | **Absent in reviewed runtime:** sequential lifetime/endless goals exist, but no real-calendar daily plan | **Adopt daily jobs, P1 after persistence.** Three capability-filtered logistics tasks; forgiving calendar reward is optional P2. Keep lifetime goals. |
| G05: contextual rewarded placements | **Partial:** one generic sponsor reward exists | **Defer.** After receipt safety and economy tests, evaluate at most one useful optional placement such as a return-dividend bonus. No new ad SDK/account work now. |
| G06: coordinated interruptions | **Partial:** HUD modal queue already exists | **Adopt by extending it, P1.** Prioritize pending decisions and return summary; suppress during introduction and critical interactions. No unsolicited commerce prompts. |
| G07: visible world progression | **Present:** five HQ models; vehicle roof rack/spoiler/antenna/bullbar/lightbar upgrade attachments | **Keep and validate, P1 QA / P2 art.** Before/after captures at normal zoom and low quality; improve only milestones whose existing changes are hard to read. Do not rebuild all 82 assets. |
| G08: semantic feedback | **Partial:** audio cooldowns, haptic preference, coin/float pools and effects exist | **Adopt incremental facade, P1.** Throttled contextual feedback and reduced motion through FIX-08; preserve existing assets. |
| G09: pacing measurement | **Partial:** 45-minute bot and milestone output exist | **Adopt, P1.** Multi-seed report, explicit budgets and human playthrough via FIX-09. |
| G10: scene/art validation | **Partial:** model-loading tests and screenshot tools exist; no equivalent scene-composition detector found | **Adopt, P1.** Godot geometry/bounds/pivot/material checks plus progression captures; adapt tolerances to our world scale. |
| G11: safe update/migration | **Partial:** temporary save, backup and version field exist; nested validation and explicit migration fixtures are incomplete | **Adopt, P0 via FIX-03/04.** Preserve version-1 saves and interrupted migration recovery. No speculative compensation rewards. |
| G12: regional/permanent progression | **Present:** eight regions, Connections, influence, Legacy stars and prestige exist | **Keep; optional P2 refinement.** Improve the retained/reset preview and evaluate distinct region decisions. Do not add a duplicate card currency or another prestige system. |

### ADD-01 — Dot's next-action advisor

Reuse `TutorialManager`, `UIManager._targets`, the HUD goal chip and current progression checks. Provide `{goal, reason, target, blocker, completion}` from current state. A player-requested hint is always available; an automatic reminder should be restrained and configurable. Reading a report, a moving delivery and intentional saving toward an upgrade are not failure states.

Acceptance: stall at three different progression gates after finishing/skipping the tutorial; the hint points to an unlocked/reachable action, can be dismissed, does not reopen closed sheets or steal a dragged camera, and survives language/reduced-motion changes without resetting the run.

### ADD-02 — Daily logistics jobs

Adapt the capability-filtered pattern in `../train-baron/scripts/sim/daily_goals.gd`; do not copy its wallet or economy constants. Suggested tasks: complete deliveries, earn a bounded net amount, finish eligible low-risk trips, or upgrade an available vehicle. Avoid requiring a locked region, unaffordable late tier, advertisement or purchase.

Proposed policy for clarification: UTC day ID, clearly displayed reset time, deterministic persisted task selection, progress baselines from assignment time, and no missed-day penalty. A level change must not reroll today's tasks. Keep durable claim IDs through prestige; explicitly define reset-progress behavior before implementation. Local-only clock defenses reduce accidental/repeated grants but are not a secure anti-cheat claim.

Acceptance: same tasks after restart; all tasks achievable from current capabilities; day rollover during claim, clock rollback/timezone changes, full storage, duplicate taps and prestige cannot grant twice. Establish capped rewards through multi-seed economy comparison before enabling.

### ADD-03 — Progression scene and art QA

Extend the existing Blender/Godot asset pipeline and `_test_models()` coverage. Inspect all eight regions, five HQ levels and representative vehicle attachments at low/high quality and near/far camera distances. Check missing materials/models, invalid/extreme bounds, floating/buried props and nearly coplanar overlapping surfaces. Keep documented exceptions for intentional decals/shadows.

Acceptance: a deliberately injected missing asset and z-fighting pair fail the test; known intentional overlays pass; representative early/mid/late captures are reviewed. Do not claim the geometry heuristic proves every visual scene defect absent. Regenerate Blender assets only when a verified defect calls for it.

### Optional P2 additions

- A forgiving seven-step login calendar, only if daily jobs provide enough value to justify another reward surface; no forced daily popup or punishment for missed days.
- More legible depot/HQ milestone celebrations using current models, semantic feedback and reduced-motion rules.
- A clearer prestige preview listing cash/fleet/regions reset and Connections/influence/Legacy/lifetime counters retained, validated against actual reset code.
- Additional region mechanics only after documenting a distinct route/heat/fleet decision and testing free-player progression. Do not add complexity solely to match the reference.

## C. Implementation order and completion gates

| Stage | Work | Required evidence before moving on |
|---|---|---|
| 1: state correctness | FIX-01, FIX-02 policy/settlement, FIX-03, FIX-04 | New failing-first regressions; version-1 migration fixtures; clock/resume/restart/storage-failure matrix; existing suite still passes |
| 2: trustworthy mobile UX | FIX-05, FIX-06, FIX-07, FIX-08 | Dynamic strings in 10 locales; actual modal/tutorial flows; device-scale touch measurements; background/reduced-motion checks |
| 3: evidence and guidance | FIX-09, ADD-01, ADD-03 | Multi-seed pacing report, three post-tutorial stalls, early/mid/late scene captures and detector fault injection |
| 4: retention pilot | ADD-02; optional calendar only after evaluation | Durable claim tests, explicit day/reset policy, with/without-reward balance report and return-session playthrough |
| 5: native readiness | FIX-10 | Signed build/device evidence, measured performance and verified store declarations; deferred revenue gates remain honestly separate |

For each completed item, record source revision, command/device, result and artifact path. Update this document and PLAN.md with what actually passed. A screenshot, source wrapper or headless pass alone cannot close a native behavior requirement.

## Explicit exclusions

- No RevenueCat work in this phase of review/planning.
- No live AdMob provisioning, automatic interstitials, forced paywalls, paid cash packs or fabricated scarcity.
- No Unity/Expo rewrite, copying Fuel Baron branding/assets/provider identities, or duplicate tutorial/save/goal/prestige systems.
- No unrequested push notifications, paid voice generation or public store submission.
- No claim that a promising feature improves retention/revenue without subsequent measurement.
