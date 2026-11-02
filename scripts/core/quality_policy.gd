class_name QualityPolicy
extends RefCounted
## One-time graphics-quality migration (owner rule 2026-10-14).
##
## The first launch of the build that ships this policy moves EVERY player (even one who had
## picked High) to the device default and flags a one-time notice. After that the player's own
## choice is kept forever. Only the settings dictionary is touched, never save-game data.

## v2 (2026-10-23, a Galaxy S25 Ultra on Low): Android RAM was never read (Godot −1 → "unknown" → Low) and PerfGuard read
## the game's own 20 / 30 FPS window and idle caps as slowness. A v1 player on a strong phone (device default Medium)
## sitting on a Low he did not pick (`quality_user`: true = a Settings pick, false = an automatic downgrade; older
## files have no record) is put back on Medium once, silently (PerfDevice.fix_wrong_low). Everything else stands.
const POLICY_VERSION := 2
const KEY_USER := "quality_user"
const PerfDevice := preload("res://addons/coddy_perf/perf_device.gd")
const KEY_VERSION := "quality_policy_v"
## "" = nothing to show, otherwise the tier name ("low"/"medium") the popup should announce.
const KEY_NOTICE := "quality_notice"


## Mutates `settings`; returns true if anything changed (caller must persist).
## `as_index` stores the tier as 0/1/2 instead of its name.
static func migrate(settings: Dictionary, device_default: String, quality_key := "quality", as_index := false) -> bool:
	var old := int(settings.get(KEY_VERSION, 0))
	if old >= POLICY_VERSION:
		return false
	settings[KEY_VERSION] = POLICY_VERSION
	if old < 1:
		settings[quality_key] = DeviceTier.TIERS.find(device_default) if as_index else device_default
		settings[KEY_NOTICE] = device_default if device_default != DeviceTier.HIGH else ""
		return true
	var v: Variant = settings.get(quality_key, "")
	var cur := int(v) if as_index and (v is int or v is float) else DeviceTier.TIERS.find(str(v))
	if cur >= 0:
		# a "high" default is a desktop (phones never default to High) → not a handheld, never touched
		var to := PerfDevice.fix_wrong_low(cur, DeviceTier.TIERS.find(device_default), device_default != DeviceTier.HIGH,
			bool(settings.get(KEY_USER, false)))
		settings[quality_key] = to if as_index else DeviceTier.TIERS[to]
	return true


static func pending_notice(settings: Dictionary) -> String:
	return str(settings.get(KEY_NOTICE, ""))


## Marks the notice as shown. Returns true if it was pending (caller must persist).
static func consume_notice(settings: Dictionary) -> bool:
	if pending_notice(settings) == "":
		return false
	settings[KEY_NOTICE] = ""
	return true
