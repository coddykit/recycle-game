extends Node
## W60-B1 / W63 (games/tools/perf shared module): the fleet's device class on this game's classifier, the §10 rules on
## World.PROFILES, and World.perf_tick — 10 s under 25 FPS → one tier, a one-time notice, never raised, throttled frames
## ignored. Run: godot --headless --path . res://tests/perf_w60_test.tscn
const PerfChecks := preload("res://addons/coddy_perf/perf_checks.gd")
const PerfDevice := preload("res://addons/coddy_perf/perf_device.gd")
const PerfGuard := preload("res://addons/coddy_perf/perf_guard.gd")
const PerfProfiles := preload("res://addons/coddy_perf/perf_profiles.gd")
const World := preload("res://scripts/world/world.gd")
var failures := 0
var checks := 0


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("PERF ", "PASS " if ok else "FAIL ", label)


func none(list: Array, label: String) -> void:
	check(list.is_empty(), "%s %s" % [label, "; ".join(list)])


## Shared cases {handheld, os, ram_mb, cores, gpu} → this game's {platform, ram_bytes, cores, adapter, vendor}.
static func tier_of(info: Dictionary) -> int:
	var platform := "desktop" if not bool(info.get("handheld", false)) else String(info.get("os", "")).to_lower()
	var ram := int(info.get("ram_mb", -1))
	return PerfDevice.tier_of(DeviceTier.classify({"platform": platform, "ram_bytes": ram * 1048576 if ram > 0 else 0,
		"cores": int(info.get("cores", 0)), "adapter": String(info.get("gpu", "")), "vendor": ""}))


func _ready() -> void:
	await get_tree().process_frame
	none(PerfChecks.device_failures(tier_of), "device class")
	var table := []
	for q in ["low", "medium", "high"]:
		var p: Dictionary = World.PROFILES[q]
		table.append({"scale": p.scale, "fps": p.fps, "msaa": {Viewport.MSAA_2X: 2, Viewport.MSAA_4X: 4}.get(int(p.msaa), 0),
			"shadows": int(p.shadow_atlas) > 0, "crowd": p.traffic, "particles": GameManager.PARTICLE_SCALE[q], "lod": p.lod})
	none(PerfChecks.profile_failures(table), "profiles")
	var g := PerfGuard.new()
	none(PerfChecks.guard_failures(func(fps, dt, tier): return g.sample(fps, dt, tier)), "guard rule")
	# the real hook
	var keep: Dictionary = GameManager.settings.duplicate(true)
	GameManager.settings["quality"] = "medium"
	GameManager.settings.erase("perf_notice")
	var w: Node3D = World.new()
	Engine.max_fps = 0
	var to := -1
	var secs := 0.0
	while to < 0 and secs < 30.0:
		to = w.perf_tick(18.0, 0.1)
		secs += 0.1
	check(to == 0 and GameManager.quality() == "low", "Medium → Low after a slow stretch")
	check(secs > 14.5 and secs < 15.5, "after the 5 s warm-up + 10 s under 25 FPS (%.1f s)" % secs)
	check(bool(GameManager.settings.get("perf_notice", false)), "the one-time notice flag is saved")
	check(not bool(GameManager.settings.get("quality_user", true)), "an automatic Low is not the player's pick (policy v2 may correct it)")
	for i in 400:
		if w.perf_tick(5.0, 0.1) >= 0:
			check(false, "Low never goes lower")
			break
	GameManager.settings["quality"] = "high"
	Engine.max_fps = 20
	var thr := -1
	for i in 400:
		thr = maxi(thr, w.perf_tick(15.0, 0.1))
	check(thr == -1, "a throttled frame rate is never read as a slow device")
	Engine.max_fps = 0
	# heat: idle 5 s → 30 (Medium) / 20 (Low), a panel open → 20, a touch wakes it, nothing drawn in the background
	check(PerfProfiles.live_cap(60, false, 4.9) == 60, "not idle before 5 s")
	check(PerfProfiles.live_cap(60, false, 5.0) == 30, "Medium idle 5 s: 30")
	check(PerfProfiles.live_cap(30, false, 5.0) == 20, "Low idle 5 s: 20")
	check(PerfProfiles.live_cap(60, true, 0.0) == 60 and PerfProfiles.live_cap(30, true, 1.4) == 30, "a panel open, in use (a finger / a list scroll): the tier cap (Play review 10-23)")
	check(PerfProfiles.live_cap(60, true, 1.5) == 20 and PerfProfiles.live_cap(30, true, 1.5) == 20, "a panel open, left alone 1.5 s: 20")
	check(PerfProfiles.live_cap(60, false, 0.0) == 60 and PerfProfiles.live_cap(30, false, 0.0) == 30, "playing: the tier cap")
	w._idle_t = 9.0
	var touch := InputEventScreenTouch.new()
	touch.pressed = true
	w._input(touch)
	check(w._idle_t == 0.0, "a touch wakes it")
	w._notification(Node.NOTIFICATION_APPLICATION_PAUSED)
	check(not RenderingServer.render_loop_enabled, "background: render loop off")
	w._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	check(RenderingServer.render_loop_enabled, "resumed: render loop on")
	w.free()
	GameManager.settings = keep
	DirAccess.remove_absolute(SaveManager.SETTINGS_PATH)
	print("PERF checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)
