extends RefCounted
## Shared headless checks (W60-B1): every game's tests call these against its own wiring, so the fleet keeps one
## device-class answer and one downgrade rule. Each returns the failures ([] = ok).
##   PerfChecks.device_failures(func(info): return DeviceTier.default_quality(info))   # game's mapping, as tier ints
##   PerfChecks.profile_failures(MyGame.PROFILES)                                     # [low, medium, high] dicts
##   PerfChecks.guard_failures(func(fps, delta, tier): ...)                            # game's per-frame hook → new tier or −1
## Source of truth: games/tools/perf (copied into each game as addons/coddy_perf by sync.sh).

const PerfDevice = preload("res://addons/coddy_perf/perf_device.gd")
const PerfProfiles = preload("res://addons/coddy_perf/perf_profiles.gd")

static func _a(ram: int, cores: int, gpu: String, driver: String = "opengl3", method: String = "gl_compatibility") -> Dictionary:
	return {"handheld": true, "os": "Android", "ram_mb": ram, "cores": cores, "gpu": gpu, "driver": driver, "method": method}


## [device, expected tier, label]. Android rows use the fleet's real export path (OpenGL / gl_compatibility).
static func device_cases() -> Array:
	return [
		[_a(7600, 8, "Adreno (TM) 730"), 2, "8 GB / 8 cores / Adreno 730 on gl_compatibility → flagship High (owner 10-23)"],
		[_a(7600, 8, "Adreno (TM) 730", "vulkan", "mobile"), 2, "same phone on Vulkan → High"],
		[_a(11264, 8, "Adreno (TM) 830"), 2, "Galaxy S25 Ultra (12 GB, Adreno 830) → High"],
		[_a(7380, 8, "Adreno (TM) 740"), 2, "Galaxy S23 (8 GB reports ~7.2 GiB, Adreno 740) → High"],
		[_a(7450, 8, "Adreno (TM) 750"), 2, "8 GB / Adreno 750 → High"],
		[_a(11000, 8, "Mali-G715-Immortalis MC11"), 2, "Dimensity 9200 (Immortalis-G715) → High"],
		[_a(11000, 8, "Mali-G720-Immortalis MC12"), 2, "Dimensity 9300 (Immortalis-G720) → High"],
		[_a(11000, 8, "ANGLE (Samsung Xclipse 940) on Vulkan 1.3.279"), 2, "Exynos 2400 (Xclipse 940, ANGLE string) → High"],
		[_a(7000, 8, "Adreno (TM) 740"), 2, "exactly the 7000 MB flagship floor"],
		[_a(6999, 8, "Adreno (TM) 740"), 1, "just under the flagship RAM floor (6 GB-class) → Medium"],
		[_a(11000, 6, "Adreno (TM) 830"), 0, "6 cores, even with Adreno 830 → Low"],
		[_a(7600, 8, "Adreno (TM) 725"), 1, "Adreno 725 (7+ Gen 2) → strong only, Medium"],
		[_a(7600, 8, "Adreno (TM) 720"), 1, "Adreno 720 → Medium"],
		[_a(7600, 8, "Mali-G78 MP20"), 1, "Mali-G78 → Medium"],
		[_a(7600, 8, "Mali-G715 MC7"), 1, "Mali-G715 without Immortalis (Tensor G3) → Medium"],
		[_a(5632, 8, "Adreno (TM) 642L"), 1, "6 GB phone (reports ~5.5 GiB)"],
		[_a(5325, 8, "Mali-G710 MC10"), 1, "exactly the 5.2 GiB floor, Mali-G710"],
		[_a(5600, 8, "Adreno (TM) 830"), 1, "6 GB-class with Adreno 830 → Medium (RAM below the flagship floor)"],
		[_a(11000, 8, "Xclipse 920"), 2, "Xclipse 920 (12 GB) → High"],
		[_a(11000, 8, "Xclipse 530"), 1, "Xclipse 530 → Medium"],
		[_a(5324, 8, "Adreno (TM) 730"), 0, "just under the RAM floor"],
		[_a(3789, 8, "Adreno (TM) 730"), 0, "4 GB phone"],
		[_a(7600, 6, "Adreno (TM) 730"), 0, "6 cores"],
		[_a(7600, 8, ""), 0, "no GPU name = unknown"],
		[_a(-1, 8, "Adreno (TM) 730"), 0, "no RAM figure = unknown"],
		[_a(7545, 8, "Mali-G57 MC2"), 0, "Mali-G57"],
		[_a(7385, 8, "Adreno (TM) 619"), 0, "Adreno 619"],
		[_a(5548, 8, "Adreno (TM) 616"), 0, "Adreno 616"],
		[_a(8000, 8, "Mali-G68 MC4"), 0, "Mali-G68"],
		[_a(8000, 8, "PowerVR Rogue GE8320"), 0, "PowerVR"],
		[_a(8000, 8, "Adreno (TM) 506"), 0, "Adreno 506"],
		[{"handheld": true, "os": "iOS", "ram_mb": 3789}, 1, "4 GB iPhone"],
		[{"handheld": true, "os": "iOS", "ram_mb": 5600}, 1, "6 GB iPhone (14 Pro / 15) → Medium"],
		[{"handheld": true, "os": "iOS", "ram_mb": 7680, "gpu": "Apple A17 Pro GPU"}, 2, "8 GB iPhone (15 Pro / 16 Pro) → High"],
		[{"handheld": true, "os": "iOS", "ram_mb": 3481}, 0, "just under the iOS floor"],
		[{"handheld": true, "os": "iOS", "ram_mb": -1}, 0, "iOS without RAM"],
		[{"handheld": true, "os": "HarmonyOS", "ram_mb": 12000}, 0, "unknown OS"],
		[{"handheld": true}, 0, "a handheld with no info"],
		[{"handheld": false, "os": "Windows", "ram_mb": 2000}, 2, "desktop keeps High"],
	]


## `tier_of(info) -> int` is the game's own mapping (convert names with PerfDevice.tier_of). Also sweeps a grid to
## prove a handheld defaults to High only when it is a flagship, and every flagship does (owner 10-23).
static func device_failures(tier_of: Callable) -> Array[String]:
	var bad: Array[String] = []
	for c in device_cases():
		var got := int(tier_of.call(c[0]))
		if got != int(c[1]):
			bad.append("%s: got %s, want %s" % [c[2], PerfDevice.tier_name(got), PerfDevice.tier_name(int(c[1]))])
	for os in ["Android", "iOS", "Linux", ""]:
		for ram in [-1, 1024, 3000, 4096, 6000, 7400, 12000]:
			for cores in [4, 8, 12]:
				for drv in [["vulkan", "mobile"], ["opengl3", "gl_compatibility"], ["", ""]]:
					for gpu in ["", "Adreno (TM) 750", "Adreno (TM) 725", "Mali-G57", "Apple A17 Pro GPU"]:
						var info := {"handheld": true, "os": os, "ram_mb": ram, "cores": cores, "driver": drv[0], "method": drv[1], "gpu": gpu}
						var flagship := PerfDevice.classify(info) == "flagship"
						var high := int(tier_of.call(info)) >= 2
						if high != flagship:
							bad.append("handheld %s High: %s" % ["defaulted to" if high else "flagship not on", str(info)])
							return bad
	return bad


static func profile_failures(table: Array) -> Array[String]:
	return PerfProfiles.validate(table)


## `step(fps, delta, tier) -> int` is the game's per-frame hook (new tier, or −1 for no change). Checks: a short stutter
## does nothing, 10 s under 25 FPS lowers one tier after the warm-up, Low never goes lower, nothing ever raises.
static func guard_failures(step: Callable) -> Array[String]:
	var bad: Array[String] = []
	var keep_fps := Engine.max_fps
	Engine.max_fps = 0 # uncapped while the plain rules are checked (PerfGuard reads the live cap)
	var dt := 0.1
	for i in 60: # warm-up (start-up / tier change) at 10 FPS: ignored
		if int(step.call(10.0, dt, 1)) >= 0:
			bad.append("warm-up frames must not downgrade")
			break
	for i in 90: # 9 s slow, then one good frame: the clock starts again
		if int(step.call(20.0, dt, 2)) >= 0:
			bad.append("9 s of slowness must not downgrade")
			break
	step.call(60.0, dt, 2)
	var to := -1
	for i in 101:
		to = int(step.call(20.0, dt, 2))
		if to >= 0:
			break
	if to != 1:
		bad.append("10 s under 25 FPS on High must give Medium (got %d)" % to)
	# a capped frame rate is never a slow device (owner 2026-10-23, S25 Ultra on Low): a window left alone at the 20 FPS
	# pacing cap for 60 s, then the cap lifted — the first seconds still show the old 1 s FPS average
	Engine.max_fps = 20
	for i in 600:
		if int(step.call(20.0, dt, 2)) >= 0:
			bad.append("60 s at a 20 FPS cap (a window / idle) must not downgrade")
			break
	Engine.max_fps = 60
	for i in 20:
		if int(step.call(20.0, dt, 2)) >= 0:
			bad.append("the 2 s after a cap is lifted must not downgrade")
			break
	to = -1
	for i in 170: # really 18 FPS with a 60 FPS cap: slow
		to = int(step.call(18.0, dt, 2))
		if to >= 0:
			break
	if to != 1:
		bad.append("18 FPS under a 60 FPS cap for 10 s must downgrade (got %d)" % to)
	Engine.max_fps = 0
	for i in 300:
		if int(step.call(5.0, dt, 0)) >= 0:
			bad.append("Low never goes lower")
			break
	for i in 300:
		var r := int(step.call(120.0, dt, 0))
		if r > 0:
			bad.append("the guard must never raise the tier")
			break
	Engine.max_fps = keep_fps
	return bad
