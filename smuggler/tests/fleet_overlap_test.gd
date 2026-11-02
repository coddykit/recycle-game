extends Node
## W68 (owner 10-24: "vehicles never drive through each other"), every moving actor of the map at once: the late-game
## empire (every region + depot, garage maxed, the whole fleet) with the player's fleet on contracts the whole time
## (re-dispatched as soon as a vehicle is home), the ambient cars on the same roads and the HQ crew walking about.
## Fixed step (1/60 s) driven by hand through the real systems: GameManager.tick (contracts, events), every
## VehicleActor, the TrafficSystem and the HQ workers. Every step each drawn footprint (model_meta box of the drawn
## model at the drawn node, coddy_anim OverlapProbe SAT maths, grid broad phase) is tested against every other:
##   ambient car × fleet vehicle                                             must stay 0
##   ambient × ambient (including long models on High)                       must stay 0
##   fleet × fleet                                                          KNOWN OPEN: measured and printed
##   person × vehicle (an HQ worker under a car)                           must stay 0
##   person × person                                                        measured and reported
## An ambient car fading out of the way (W68 fallback) is not "driving through" — its seconds are counted and must stay
## the rare exception. Also: nobody stands still for long (no deadlock), the fleet keeps delivering.
## Not covered: the 5 s delivery helpers (UnloadSequence, tween-timed) — they step off the road beside the vehicle.
## Run: godot --headless --path . res://tests/fleet_overlap_test.tscn [-- --seconds=N --profile=high|low]
const OverlapProbe = preload("res://addons/coddy_anim/overlap_probe.gd")
const DT := 1.0 / 60.0
const PERSON := 0.45
const CELL := 6.0
const MAX_STILL_S := 25.0
var failures := 0
var checks := 0
var _proxy := Node3D.new()
var _main: Node


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("FLEET_OVERLAP ", "PASS " if ok else "FAIL ", label)


func _ready() -> void:
	# deterministic (Patron 10-30): no wall-clock ticks of the game at all (GameManager is stepped by hand with DT
	# only), and every measurement starts from the same seed right before its loop (the game's RNG = the global one)
	GameManager.set_process(false)
	await get_tree().process_frame
	seed(42)
	var seconds := 90.0   # High 90 s + Low 45 s (≈ 2 min headless); `-- --seconds=240` for the long run
	var only := ""
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--seconds="):
			seconds = float(a.get_slice("=", 1))
		elif a.begins_with("--profile="):
			only = a.get_slice("=", 1)
	await _setup()
	if only != "low":
		await _measure("High", "high", seconds)
	if only != "high":
		await _measure("Low", "low", seconds if only == "low" else seconds * 0.5)
	_proxy.free()
	print("FLEET_OVERLAP checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)


func _settle(n: int) -> void:
	for i in n:
		await get_tree().process_frame


func _setup() -> void:
	SaveManager.delete_save()
	GameManager.settings["quality"] = "high"
	GameManager.settings["quality_policy_v"] = QualityPolicy.POLICY_VERSION
	GameManager.settings["quality_notice"] = ""
	_main = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(_main)
	await _settle(10)
	var hud: Node = _main.hud
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	ProgressionManager.flags["intro_done"] = true
	ProgressionManager.flags["tabs_all"] = true
	TutorialManager.skip()
	hud._restore_game_chrome()
	hud._modal_queue.clear()
	seed(42)   # (again: the engine frames above may have drawn from the RNG — region dressing reads it)
	for r in range(RegionManager.unlocked_count, RegionManager.count()):
		EconomyManager.earn(1e8, "overlap", false)
		EconomyManager.add_reputation(400)
		RegionManager.unlock(r)
		await get_tree().process_frame
	EconomyManager.earn(1e9, "overlap", false)
	for i in 6 - ProgressionManager.garage_level:
		ProgressionManager.upgrade_garage()
	for d in range(RegionManager.unlocked_count):
		RegionManager.build_depot(d)
	seed(43)   # a bought vehicle rolls its stats (speed, reliability): the same fleet every run
	for def in Data.vehicles:
		EconomyManager.earn(1e8, "overlap", false)
		VehicleManager.buy(str(def.id))
	await _settle(10)


## Every idle vehicle on the best contract it can take (repeated: the fleet stays on the road).
func _dispatch_all() -> int:
	var sent := 0
	ContractManager.refresh_offers()
	for c in ContractManager.offers.duplicate():
		var v: Dictionary = ContractManager.best_vehicle(c)
		if not v.is_empty() and ContractManager.dispatch_check(c, v) == "":
			if not ContractManager.dispatch(c, v, "normal").is_empty():
				sent += 1
	return sent


func _measure(label: String, quality: String, seconds: float) -> void:
	GameManager.set_process(false)
	seed(4100 + steps_seed(quality))
	GameManager.set_quality(quality, false)
	await _settle(20)
	var w: Node = _main.world
	var ts: TrafficSystem = w.traffic
	# the systems are stepped by hand with the fixed step (deterministic, fast): no engine-driven ticks meanwhile
	GameManager.set_process(false)
	ts.set_process(false)
	w.hq.set_process(false)
	for id in w.actors:
		(w.actors[id] as Node3D).set_process(false)
	# the same start every run: the engine frames above moved the ambient cars and the HQ crew on the wall clock (and
	# the crew's wandering draws from the global RNG the contracts use) — rebuild both from a fixed seed
	seed(4200 + steps_seed(quality))
	for c in ts.cars:
		ts._drop(c)
	ts.cars.clear()
	ts._region = -1
	ts.refresh_count()
	# Discard the engine-frame remainder and spawn timer carried over from the previous profile.
	# Otherwise the same Low simulation starts at a different substep after changing High traffic.
	ts._acc = 0.0
	ts._retry = 0.0
	ts.faded = 0
	w.hq._build_workers()
	for c in w.hq.workers:
		c.set_process(false)
	for i in 120:   # spawn retries of the ambient cars (as the game does over its first seconds)
		ts._process(0.25)
	var worst := {"veh": [0.0, ""], "ambient": [0.0, ""], "fleet": [0.0, ""], "person_veh": [0.0, ""], "people": [0.0, ""]}
	var hits := {"veh": 0, "ambient": 0, "fleet": 0, "person_veh": 0, "people": 0}
	var still := {}
	var last := {}
	var longest := [0.0, ""]
	var ghost_s := 0.0
	var car_s := 0.0
	var most := 0
	var pairs := {}   # "a × b" -> [deepest, steps, where] (the report lists the worst offenders)
	seed(4300 + steps_seed(quality))
	var deliveries0 := int(EconomyManager.stat("deliveries"))
	var failed0 := int(EconomyManager.stat("failed"))
	var events0 := int(EconomyManager.stat("events"))
	var sent_total := 0

	var steps := int(seconds / DT)
	var t := 0.0
	for k in steps:
		if k % 60 == 0:
			sent_total += _dispatch_all()
			for id in w.actors:   # a vehicle bought / rebuilt meanwhile
				(w.actors[id] as Node3D).set_process(false)
		GameManager.tick(DT)
		for id in w.actors:
			(w.actors[id] as VehicleActor)._process(DT)
		ts._process(DT)
		w.hq._process(DT)
		for c in w.hq.workers:
			c._process(DT)
		t += DT
		# (no engine frame inside the loop: a real frame runs tweens on the wall clock — their callbacks draw from the
		# global RNG, e.g. a delivery's sound pitch — and the contracts would then roll differently every run)
		var boxes := _boxes(w, ts)
		most = maxi(most, boxes.size())
		for c in ts.cars:
			if c.car != null or float(c.get("fade", 0.0)) > 0.0:
				car_s += DT
				if float(c.get("fade", 0.0)) > 0.0:
					ghost_s += DT
		var seen := {}
		var grid := {}
		for i in boxes.size():
			var b: Array = boxes[i]
			var cell := Vector2i(floori((b[0] as Vector2).x / CELL), floori((b[0] as Vector2).y / CELL))
			(grid.get_or_add(cell, []) as Array).append(i)
		for i in boxes.size():
			var a: Array = boxes[i]
			var ca := Vector2i(floori((a[0] as Vector2).x / CELL), floori((a[0] as Vector2).y / CELL))
			for dx in [-1, 0, 1]:
				for dy in [-1, 0, 1]:
					for j in grid.get(ca + Vector2i(dx, dy), []):
						if j <= i:
							continue
						var b: Array = boxes[j]
						var r: float = a[2] + b[2]
						if (a[0] as Vector2).distance_squared_to(b[0]) >= r * r:
							continue
						var d := OverlapProbe.depth(a[1], b[1])
						if d <= 0.0:
							continue
						var cat := "people" if a[4] == "person" and b[4] == "person" else ("person_veh" if a[4] == "person" or b[4] == "person" else "veh")
						if cat == "veh" and str(a[3]).begins_with("fleet") and str(b[3]).begins_with("fleet"):
							cat = "fleet"
						elif cat == "veh" and str(a[3]).begins_with("car") and str(b[3]).begins_with("car"):
							cat = "ambient"
						seen[cat] = true
						var pk := "%s %s × %s" % [cat, a[3], b[3]]
						pairs[pk] = [maxf(float(pairs.get(pk, [0.0, 0])[0]), d), int(pairs.get(pk, [0.0, 0])[1]) + 1, "(%.1f, %.1f)" % [(a[0] as Vector2).x, (a[0] as Vector2).y]]
						if d > float(worst[cat][0]):
							worst[cat] = [d, "%s × %s @ (%.1f, %.1f) t=%.1f" % [a[3], b[3], (a[0] as Vector2).x, (a[0] as Vector2).y, t]]
		for c in seen:
			hits[c] += 1
		# no deadlock: a moving thing (ambient car in the simulation, fleet vehicle on a job) never stands for long
		for b in boxes:
			if not b[5]:
				continue
			var key: String = b[3]
			var p: Vector2 = b[0]
			if last.has(key) and (last[key] as Vector2).distance_to(p) < 0.002:
				still[key] = float(still.get(key, 0.0)) + DT
				if float(still[key]) > float(longest[0]):
					longest = [float(still[key]), "%s @ (%.1f, %.1f) t=%.1f" % [key, p.x, p.y, t]]
			else:
				still[key] = 0.0
			last[key] = p
	var delivered := int(EconomyManager.stat("deliveries")) - deliveries0
	print("  %s: %d trips dispatched, %d delivered, %d failed, %d road events" % [label, sent_total, delivered,
		int(EconomyManager.stat("failed")) - failed0, int(EconomyManager.stat("events")) - events0])
	var fleet := 0
	for id in w.actors:
		fleet += 1
	print("  %s: %.0f game s, %d steps, up to %d actors (%d fleet, %d ambient, %d crew), %d deliveries, ambient fading %.1f s of %.1f s, longest stand %.1f s (%s)" % [
		label, t, steps, most, fleet, ts.cars.size(), w.hq.workers.size(), delivered, ghost_s, car_s, longest[0], longest[1]])
	for c in ["veh", "ambient", "fleet", "person_veh", "people"]:
		print("  %s %-10s worst %.2f m (%s), overlapping in %d of %d steps" % [label, c, worst[c][0], worst[c][1], hits[c], steps])
	var keys := pairs.keys()
	keys.sort_custom(func(x, y): return int(pairs[x][1]) > int(pairs[y][1]))
	var shown := {}
	for pk in keys:
		var pc: String = pk.get_slice(" ", 0)
		shown[pc] = int(shown.get(pc, 0)) + 1
		if int(shown[pc]) > 4:
			continue
		print("    pair %s: %.2f m, %d steps, %s" % [pk, pairs[pk][0], pairs[pk][1], pairs[pk][2]])
	print("  %s ambient cars made way %d times" % [label, ts.faded])
	check(delivered >= 3, "%s: the fleet keeps delivering (%d)" % [label, delivered])
	check(float(worst.veh[0]) <= 0.0, "%s: an ambient car never overlaps a fleet vehicle (%.2f m %s)" % [label, worst.veh[0], worst.veh[1]])
	check(float(worst.ambient[0]) <= 0.0, "%s: ambient real model boxes never overlap (%.2f m %s)" % [label, worst.ambient[0], worst.ambient[1]])
	# KNOWN OPEN (W68, Patron 10-30): the player's vehicles are drawn on their contract clocks with no spacing between
	# each other (shared lanes, the HQ exit, delivery points, the parking row) — measured, not yet a gate
	print("  %s KNOWN OPEN fleet × fleet: worst %.2f m (%s), %d of %d steps" % [label, worst.fleet[0], worst.fleet[1], hits.fleet, steps])
	check(float(worst.person_veh[0]) <= 0.0, "%s: no vehicle drives through a person (%.2f m %s)" % [label, worst.person_veh[0], worst.person_veh[1]])
	check(ghost_s <= 0.02 * car_s, "%s: fading out of the way stays the rare exception (%.1f s of %.1f s)" % [label, ghost_s, car_s])
	check(float(longest[0]) < MAX_STILL_S, "%s: nobody stands still for %d s (longest %.1f s %s)" % [label, int(MAX_STILL_S), longest[0], longest[1]])
	GameManager.set_process(true)
	ts.set_process(true)
	w.hq.set_process(true)
	for id in w.actors:
		(w.actors[id] as Node3D).set_process(true)
	for c in w.hq.workers:
		c.set_process(true)


static func steps_seed(quality: String) -> int:
	return ["low", "medium", "high"].find(quality)


## [centre, corners, radius, label, kind ("veh" / "person"), counts for the stand-still check] per drawn actor.
func _boxes(w: Node, ts: TrafficSystem) -> Array:
	var out := []
	for i in ts.cars.size():
		var c: Dictionary = ts.cars[i]
		var n: Node3D = c.node
		if not n.visible or n.scale.x < 0.999:   # (fading out of the way: not driving through anybody)
			continue
		# the holder carries the sim's +Z heading; its model inside is turned round (faces -Z): the box turns with it
		_add(out, n.global_transform * Transform3D(Basis(Vector3.UP, PI), Vector3.ZERO), Data.meta(str(c.get("model", "sedan"))),
			"car%d(%s)" % [i, c.get("model", "?")], "veh", c.car != null)
	for id in w.actors:
		var a: VehicleActor = w.actors[id]
		if not a.visible:
			continue
		var v := VehicleManager.get_vehicle(str(id))
		var on_job := not ContractManager.get_job_for_vehicle(str(id)).is_empty()
		var job: Dictionary = ContractManager.get_job_for_vehicle(str(id))
		var moving: bool = on_job and str(job.get("phase", "")) != "unload" and not bool(job.get("pending_event", false))
		_add(out, a.global_transform, Data.meta(str(v.get("def", "hatchback"))), "fleet:%s(%s)" % [id, v.get("def", "?")], "veh", moving)
	for i in w.hq.workers.size():
		var c: Node3D = w.hq.workers[i]
		var h := PERSON * 0.5
		_add(out, c.global_transform, {"min": [-h, 0, -h], "max": [h, 1, h]}, "worker%d" % i, "person", false)
	return out


## The model_meta box (min / max in the node's frame, × 0.9 like OverlapProbe.add) on the ground.
func _add(out: Array, x: Transform3D, meta: Dictionary, label: String, kind: String, moving: bool) -> void:
	var mn: Array = meta.get("min", [-0.5, 0, -0.5])
	var mx: Array = meta.get("max", [0.5, 1, 0.5])
	var half := Vector2(float(mx[0]) - float(mn[0]), float(mx[2]) - float(mn[2])) * 0.5 * 0.9
	var off := Vector3((float(mx[0]) + float(mn[0])) * 0.5, 0.0, (float(mx[2]) + float(mn[2])) * 0.5)
	var b := Basis(Vector3.UP, x.basis.get_euler().y)
	var o := x.origin + b * off
	_proxy.transform = Transform3D(b, o)
	out.append([Vector2(o.x, o.z), OverlapProbe._corners(_proxy, half), half.length(), label, kind, moving])
