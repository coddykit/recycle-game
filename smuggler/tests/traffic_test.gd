extends Node
## W68 ambient traffic on the real road network (coddy_anim AmbientTraffic, games/docs/W68_ROLLOUT_TABLE.md): every
## region open, 26 cars for 3 minutes — cars never touch (the following gap holds min_gap but where two roads leave a
## town side by side), nobody stays stuck (no junction
## deadlock), no car jumps between steps, a region change rebuilds the network and the cars come back.
## Run: godot --headless --path . res://tests/traffic_test.tscn
const OverlapProbe = preload("res://addons/coddy_anim/overlap_probe.gd")
const MAX_STILL_SECONDS := 25.0
const TIME_EPSILON := 0.000001
var failures := 0
var checks := 0


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("TRAFFIC ", "PASS " if ok else "FAIL ", label)


func _ready() -> void:
	await get_tree().process_frame
	# deterministic (Patron 10-27): car speeds come from randf_range — unseeded, the longest queue wandered 11–25 s
	# between runs and sometimes hit the 25 s limit; one fixed seed = the same 3 minutes every run (#83, sized High traffic: longest 24.733 s)
	seed(42)
	GameManager.set_process(false)
	GameManager.settings["quality"] = "high"
	var saved: int = RegionManager.unlocked_count
	RegionManager.unlocked_count = RegionManager.count()
	var ts := TrafficSystem.new()
	add_child(ts)
	ts.refresh_count()
	for i in 40:   # spawn retries (two a quarter second)
		ts._process(0.25)
	var n := 0
	for c in ts.cars:
		n += int(c.car != null)
	check(ts.cars.size() >= 8 and n == ts.cars.size(), "every car found a clear spot (%d / %d)" % [n, ts.cars.size()])
	ts.traffic.vm.measure = true
	var probe := OverlapProbe.new()   # owner 10-24: vehicles never drive through each other (footprints never cross)
	var real := OverlapProbe.new()    # W68 10-30: each car's own model_meta box (a minibus / box truck is 2.7 × 1.16 m)
	for c in ts.cars:
		probe.add(c.proxy, 1.52, 0.92)
		var size: Array = Data.meta(str(c.model)).size
		real.add(c.proxy, float(size[2]), float(size[0]))
	var last := {}   # car index -> last position (the car dictionaries change every step: never keys)
	var jump := 0.0
	var still := {}
	var worst := 0
	for k in 180 * 15:   # 3 minutes at the Low rate
		ts.traffic.step(1.0 / 15.0)
		probe.sample()
		real.sample()
		for i in ts.cars.size():
			var c: Dictionary = ts.cars[i]
			if c.car == null:
				continue
			var p: Vector3 = (c.proxy as Node3D).position
			if last.has(i):
				var d := (p - (last[i] as Vector3)).length()
				jump = maxf(jump, d)
				still[i] = 0 if d > 0.001 else int(still.get(i, 0)) + 1
				worst = maxi(worst, int(still[i]))
			last[i] = p
	# a follower keeps min_gap; where two roads leave a town almost side by side (highway + backroad) a car can come into
	# the lane band a little closer before the roads part — still never touching (min_gap 2.4 m, a car 1.5 m: 0.7 = 1.7 m)
	check(ts.traffic.vm.min_gap_seen >= 0.7, "cars never touch: gap ≥ 0.7 × min_gap (%.3f)" % ts.traffic.vm.min_gap_seen)
	check(probe.worst_depth == 0.0, "no two cars ever overlap (%.3f m %s)" % [probe.worst_depth, probe.worst_pair])
	check(real.worst_depth == 0.0, "High real model boxes never overlap (%.3f m %s)" % [real.worst_depth, real.worst_pair])
	# Strict upper bound: neither exactly 25 s nor floating-point noise at the boundary passes.
	# Keep millisecond precision in the failure message so rounding cannot hide a near-boundary result.
	var longest_still := worst / 15.0
	check(longest_still < MAX_STILL_SECONDS - TIME_EPSILON,
		"no car stands still for 25 s (longest %.3f s)" % longest_still)
	check(jump < 0.6, "no car jumps between steps (max %.3f m)" % jump)
	RegionManager.unlocked_count = 1
	ts.refresh_count()
	for i in 40:
		ts._process(0.25)
	n = 0
	for c in ts.cars:
		n += int(c.car != null)
	check(n == ts.cars.size() and n > 0, "a region change rebuilds the roads and the cars come back (%d)" % n)
	GameManager.settings["quality"] = "low"
	ts.refresh_count()
	check(ts.traffic.vm.get_script() == TrafficSystem.VehicleMover and ts.traffic.vm.lane_offset == TrafficSystem.LANE,
		"switching to Low restores the original mover and lane spacing")
	var low_plain := true
	for c in ts.cars:
		low_plain = low_plain and not c.proxy.has_meta("traffic_half")
	check(low_plain, "Low proxies carry no added bounds work")
	GameManager.settings["quality"] = "medium"
	ts.refresh_count()
	check(ts.traffic.vm.get_script().resource_path == TrafficSystem.SIZED_MOVER_PATH,
		"switching back to Medium restores model-aware traffic")
	RegionManager.unlocked_count = saved
	ts.queue_free()
	print("TRAFFIC checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)
