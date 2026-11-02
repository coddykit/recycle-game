# Borderline Empire — Play phone + tablet images (coddy style, 10 languages)
Finals (not in git, ~100 MB RGB): /Volumes/BuildSSD/store-assets/smuggler-play-2026-10-23/{play,ipad}/<lang>/NN-*.png
(play = phone 1284x2568, ipad = tablet 2064x2752 for 7"/10"). `provenance.json` = hash per file; `sheets/` = contact sheets. Kit a817bed40c.
Pipeline: `WORK=<dir> GODOT=<godot> bash store/deck/capture_raw.sh iphone|ipad <langs>` (real game windowed under shot-lock, one at a time; --winsize makes the
window borderless so a device-size frame is not clamped to the screen) -> `WORK=<dir> SKILL=<base/.claude/skills/store-deck/scripts> bash store/deck/fleet_render.sh`.
The feature graphic (world shot, no UI) is unchanged.
