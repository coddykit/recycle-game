extends Node
## Contract offers, the accepted queue and active delivery jobs.
## Pure game logic: the 3D world only *reads* job progress to animate vehicles.

signal offers_changed
signal queue_changed
signal job_started(job: Dictionary)
signal job_delivered(job: Dictionary, result: Dictionary)
signal job_failed(job: Dictionary, reason: String)
signal job_returned(job: Dictionary)

const EVENT_TIMEOUT := 30.0
const UNLOAD_TIME := 2.6

var offers: Array = []
var queue: Array = []
var jobs: Array = []
var offline := false

var _spawn_timer := 2.0
var _auto_timer := 0.0
var _next_id := 1


func reset() -> void:
	offers = []
	queue = []
	jobs = []
	_spawn_timer = 2.0
	_next_id = 1


func _id(prefix: String) -> String:
	_next_id += 1
	return "%s%d" % [prefix, _next_id]


# --- offer generation -------------------------------------------------------
func add_tutorial_offer() -> void:
	var f: Dictionary = Data.config.get("first_contract", {})
	var c := _make_contract(str(f.get("category", "mystery_crates")), str(f.get("origin", "r0_hub")),
			str(f.get("dest", "r0_a")), int(f.get("units", 2)), "")
	c.reward = float(f.get("reward", 350))
	c.base_risk = float(f.get("risk", 0))
	c.tutorial = true
	c.expires = 99999.0
	c.prioritized = true
	offers.push_front(c)
	offers_changed.emit()


func _make_contract(cat_id: String, origin: String, dest: String, units: int, mod_id: String) -> Dictionary:
	var cat := Data.category(cat_id)
	var g := RegionManager.graph
	var dist := g.path_length(g.find_path(origin, dest, "normal", RegionManager.max_region()))
	var dest_r := RegionManager.region_of_node(dest)
	var org_r := RegionManager.region_of_node(origin)
	var mod := Data.modifier(mod_id)
	var reward := float(units) * float(cat.per_unit) * (1.0 + dist / 60.0)
	reward *= (float(Data.region_at(dest_r).econ) + float(Data.region_at(org_r).econ)) * 0.5
	reward *= 1.0 + HeatManager.reward_bonus(dest_r)
	reward *= ProgressionManager.reward_mult()
	reward *= float(mod.get("reward", 1.0))
	var region_risk := (float(Data.region_at(dest_r).risk) + float(Data.region_at(org_r).risk)) * 0.5
	var home := RegionManager.nearest_home(origin)
	var est_path := g.multi_path([home, origin, dest], "normal", RegionManager.max_region())
	var est := travel_time(g.path_length(est_path), 1.0, "normal", dest_r)
	var lt: Array = Data.config.get("offer_lifetime", [70, 120])
	return {
		"id": _id("c"), "category": cat_id, "origin": origin, "dest": dest, "units": units,
		"reward": float(Fmt.nice(reward)), "base_risk": float(cat.risk) + region_risk,
		"modifier": mod_id, "deadline": ceil(est * float(cat.deadline) * float(mod.get("deadline", 1.0))),
		"est": est, "dist": dist, "expires": randf_range(float(lt[0]), float(lt[1])) * (1.0 + 0.5 * ProgressionManager.conn("fixer")),
		"prioritized": false, "tutorial": false, "rep_required": int(cat.rep_required),
		"rep_gain": int(cat.rep) + int(mod.get("rep", 0)) + (1 if dest_r != org_r else 0),
		"heat_gain": float(cat.heat) * float(mod.get("heat", 1.0)),
		"cross": dest_r != org_r, "fresh": true,
	}


func generate_offer() -> Dictionary:
	var rep := EconomyManager.reputation
	var locs := RegionManager.locations()
	if locs.size() < 2:
		return {}
	# category: mostly ones the player qualifies for, sometimes a teaser just above
	var pool := []
	var weights := []
	var teaser := randf() < 0.12
	for c in Data.categories:
		var req := int(c.rep_required)
		if req <= rep or (teaser and req <= rep + 8):
			pool.append(c)
			weights.append(1.0)
	if pool.is_empty():
		pool = [Data.categories[0]]
		weights = [1.0]
	# origin & destination: favour recently unlocked regions & region preferences
	var origin: String = locs.pick_random()
	var dest: String = origin
	var top := RegionManager.max_region()
	for _i in 10:
		dest = locs.pick_random()
		if dest != origin:
			var dr := RegionManager.region_of_node(dest)
			if top > 0 and dr < top and randf() < 0.35:
				continue
			break
	if dest == origin:
		return {}
	var favored: Array = Data.region_at(RegionManager.region_of_node(dest)).get("favored", [])
	var total := 0.0
	for i in pool.size():
		if favored.has(pool[i].id):
			weights[i] = 2.5
		total += float(weights[i])
	var roll := randf() * total
	var cat: Dictionary = pool[0]
	for i in pool.size():
		roll -= float(weights[i])
		if roll <= 0:
			cat = pool[i]
			break
	# skip categories nobody in the fleet could ever carry (until a teaser)
	var kind: String = cat.kind
	var best := VehicleManager.best_capacity(kind, str(cat.get("requires", "")))
	if best == 0 and not teaser:
		cat = Data.category("mystery_crates")
		kind = "cargo"
		best = VehicleManager.best_capacity("cargo")
	var units := randi_range(int(cat.units[0]), int(cat.units[1]))
	if best > 0:
		if randf() < 0.85:
			units = clampi(units, int(cat.units[0]), maxi(int(cat.units[0]), best))
		else:
			units = clampi(units, int(cat.units[0]), best + randi_range(1, 3))
	var mod_id := ""
	if randf() < 0.3:
		mod_id = Data.modifiers.pick_random().id
		var m := Data.modifier(mod_id)
		if m.has("units"):
			units = maxi(1, int(round(units * float(m.units))))
	return _make_contract(cat.id, origin, dest, units, mod_id)


func refresh_offers() -> void:
	var keep := offers.filter(func(c): return c.prioritized)
	offers = keep
	while offers.size() < ProgressionManager.offer_slots():
		var c := generate_offer()
		if c.is_empty():
			break
		offers.append(c)
	offers_changed.emit()


func refresh_cost() -> float:
	return float(Fmt.nice(maxf(25.0, EconomyManager.income_per_minute() * 0.1)))


func paid_refresh() -> bool:
	if not EconomyManager.spend(refresh_cost(), "refresh"):
		return false
	refresh_offers()
	return true


func reject(c: Dictionary) -> void:
	if c.get("tutorial", false):
		return
	offers.erase(c)
	queue.erase(c)
	offers_changed.emit()
	queue_changed.emit()


func toggle_priority(c: Dictionary) -> void:
	c.prioritized = not bool(c.prioritized)
	offers_changed.emit()
	queue_changed.emit()


func accept(c: Dictionary) -> bool:
	if not offers.has(c):
		return queue.has(c)
	if not rep_ok(c):
		return false
	if queue.size() >= int(Data.config.get("max_queue", 4)):
		return false
	offers.erase(c)
	c.fresh = false
	queue.append(c)
	offers_changed.emit()
	queue_changed.emit()
	return true


func rep_ok(c: Dictionary) -> bool:
	return EconomyManager.reputation >= int(c.get("rep_required", 0))


func get_offer(id: String) -> Dictionary:
	for c in offers:
		if c.id == id:
			return c
	for c in queue:
		if c.id == id:
			return c
	return {}


# --- estimates ----------------------------------------------------------------
func travel_time(dist: float, speed: float, route_id: String, dest_region: int) -> float:
	var r := Data.route(route_id)
	var t := (10.0 + pow(maxf(dist, 0.0), 0.75) * 1.1) / maxf(0.2, speed)
	t *= float(r.get("time", 1.0)) * ProgressionManager.travel_mult()
	if HeatManager.is_critical(dest_region):
		t *= 1.15
	return round(t)


## A finite onboarding head start, never an offline/prestige/late-economy multiplier.
## Eligibility is captured at dispatch so delivery #5 keeps its short return after
## the delivery counter increments. Existing jobs without the marker are unchanged.
func is_intro_trip(c: Dictionary) -> bool:
	return not offline and int(ProgressionManager.flags.get("prestiges", 0)) == 0 \
			and EconomyManager.stat("deliveries") < 5 and EconomyManager.stat("play_time") < 900 \
			and RegionManager.region_of_node(c.origin) == 0 and RegionManager.region_of_node(c.dest) == 0


## Full numbers for sending vehicle `v` on contract `c` via `route_id`.
func estimate(c: Dictionary, v: Dictionary, route_id: String) -> Dictionary:
	var g := RegionManager.graph
	var r := Data.route(route_id)
	var home: String = v.home if not v.is_empty() else RegionManager.nearest_home(c.origin)
	var path := g.multi_path([home, c.origin, c.dest], route_id, RegionManager.max_region())
	var dist := g.path_length(path)
	var dest_r := RegionManager.region_of_node(c.dest)
	var speed := VehicleManager.speed_mult(v) if not v.is_empty() else 1.0
	var t := travel_time(dist, speed, route_id, dest_r)
	if is_intro_trip(c):
		t = minf(t, 12.0 if c.get("tutorial", false) else 20.0)
	var reward := float(c.reward) * (VehicleManager.reward_mult(v) if not v.is_empty() else 1.0)
	var crit := HeatManager.is_critical(dest_r)
	var route_cost := reward * float(r.get("cost", 0.1)) * (1.3 if crit else 1.0)
	var cost: float = round(route_cost + (VehicleManager.op_cost(v) if not v.is_empty() else 0.0))
	var risk := float(c.base_risk) + HeatManager.risk_bonus(dest_r) + float(r.get("risk", 0))
	if not v.is_empty():
		risk += VehicleManager.risk_mod(v)
	risk -= ProgressionManager.risk_reduction()
	risk = clampf(risk, 1.0, 95.0)
	if c.get("tutorial", false):
		risk = 0.0
	var late := t > float(c.deadline)
	var payout := reward * (0.7 if late else 1.0)
	return {"path": path, "dist": dist, "time": t, "reward": reward, "cost": cost, "risk": risk,
			"late": late, "profit": payout - cost, "payout": payout}


## Very risky trips can go wrong even without an event.
func base_fail_chance(risk: float) -> float:
	return clampf((risk - 30.0) / 250.0, 0.0, 0.25)


## Picks the best idle vehicle for a contract (highest profit per second).
func best_vehicle(c: Dictionary, route_id := "normal") -> Dictionary:
	var best := {}
	var best_score := -INF
	for v in VehicleManager.vehicles:
		if VehicleManager.available_reason(v) != "" or VehicleManager.fit_reason(v, c) != "":
			continue
		var e := estimate(c, v, route_id)
		# prefer vehicles that aren't massively oversized for the job
		var waste := float(VehicleManager.capacity(v) - int(c.units)) * 0.6
		var score := float(e.profit) / maxf(1.0, float(e.time)) - waste - float(e.risk) * 0.2
		if score > best_score:
			best_score = score
			best = v
	return best


func smart_route(c: Dictionary, v: Dictionary) -> String:
	var dest_r := RegionManager.region_of_node(c.dest)
	if not ProgressionManager.auto_enabled("regional_manager"):
		return "long" if HeatManager.get_heat(dest_r) > 70 else "normal"
	var best := "normal"
	var best_score := -INF
	for r in Data.routes:
		var e := estimate(c, v, r.id)
		var score := float(e.profit) * (1.0 - float(e.risk) / 140.0) / maxf(1.0, float(e.time))
		if score > best_score:
			best_score = score
			best = r.id
	return best


# --- dispatch & jobs ------------------------------------------------------------
func dispatch_check(c: Dictionary, v: Dictionary) -> String:
	if c.is_empty() or v.is_empty():
		return "Pick a vehicle"
	if not rep_ok(c):
		return tr("Need %d reputation") % int(c.rep_required)
	var a := VehicleManager.available_reason(v)
	if a != "":
		return a
	return VehicleManager.fit_reason(v, c)


func dispatch(c: Dictionary, v: Dictionary, route_id: String) -> Dictionary:
	if dispatch_check(c, v) != "":
		return {}
	var e := estimate(c, v, route_id)
	if (e.path as Array).size() < 2:
		return {}
	offers.erase(c)
	queue.erase(c)
	var job := {
		"id": _id("j"), "contract": c, "vehicle_id": v.id, "route": route_id,
		"path": e.path, "total": float(e.time), "elapsed": 0.0, "phase": "out",
		"reward": float(e.reward), "cost": float(e.cost), "risk": float(e.risk),
		"fail_chance": 0.0, "damage": 0.0, "reward_mult": 1.0, "pending_event": false,
		"intro_trip": is_intro_trip(c),
		"ret_path": [], "ret_total": 0.0, "ret_elapsed": 0.0, "home": v.home, "late": false,
	}
	# the third trip always teaches events
	var forced := ""
	if not offline and EconomyManager.stat("events") == 0 and EconomyManager.stat("deliveries") >= 2 \
			and not c.get("tutorial", false):
		forced = "traffic"
	EventManager.roll_for_job(job, forced)
	if offline:
		job.event_at = -1.0
	v.state = "busy"
	jobs.append(job)
	VehicleManager.vehicle_changed.emit(v)
	offers_changed.emit()
	queue_changed.emit()
	job_started.emit(job)
	return job


func get_job_for_vehicle(vid: String) -> Dictionary:
	for j in jobs:
		if j.vehicle_id == vid:
			return j
	return {}


func get_job(id: String) -> Dictionary:
	for j in jobs:
		if j.id == id:
			return j
	return {}


## Adds delay while keeping visual progress continuous.
func add_delay(job: Dictionary, delay: float) -> void:
	var p := float(job.elapsed) / maxf(0.01, float(job.total))
	var nt := maxf(float(job.elapsed) + 1.0, float(job.total) + delay)
	job.total = nt
	job.elapsed = p * nt


func progress(job: Dictionary) -> float:
	if job.phase == "out":
		return clampf(float(job.elapsed) / maxf(0.01, float(job.total)), 0.0, 1.0)
	if job.phase == "unload":
		return 1.0
	return clampf(float(job.ret_elapsed) / maxf(0.01, float(job.ret_total)), 0.0, 1.0)


func time_left(job: Dictionary) -> float:
	if job.phase == "out":
		return maxf(0.0, float(job.total) - float(job.elapsed))
	if job.phase == "unload":
		return maxf(0.0, float(job.get("unload_left", 0.0)))
	return maxf(0.0, float(job.ret_total) - float(job.ret_elapsed))


func tick(delta: float) -> void:
	if not offline:
		# offers
		for c in offers.duplicate():
			if c.prioritized or c.get("tutorial", false):
				continue
			c.expires = float(c.expires) - delta
			if c.expires <= 0:
				offers.erase(c)
				offers_changed.emit()
		if TutorialManager.allow_offer_spawning():
			_spawn_timer -= delta
			if _spawn_timer <= 0:
				_spawn_timer = ProgressionManager.offer_interval()
				if offers.size() < ProgressionManager.offer_slots():
					var c := generate_offer()
					if not c.is_empty():
						offers.append(c)
						offers_changed.emit()
	# jobs
	for job in jobs.duplicate():
		if job.get("pending_event", false):
			# idle-friendly: an ignored event gets handled by the driver
			job.pending_time = float(job.get("pending_time", 0.0)) + delta
			if job.pending_time > EVENT_TIMEOUT:
				job.pending_time = 0.0
				job["auto_handled"] = true
				EventManager.resolve(job, EventManager.auto_choice(job), true)
			continue
		if job.phase == "out":
			job.elapsed = float(job.elapsed) + delta
			if float(job.event_at) >= 0 and float(job.elapsed) >= float(job.event_at) and job.event_id != "":
				job.event_at = -1.0
				EventManager.trigger(job, offline)
				continue
			if float(job.elapsed) >= float(job.total):
				_deliver(job)
		elif job.phase == "unload":
			job.unload_left = float(job.get("unload_left", 0.0)) - delta
			if job.unload_left <= 0:
				_start_return(job)
		else:
			job.ret_elapsed = float(job.ret_elapsed) + delta
			if float(job.ret_elapsed) >= float(job.ret_total):
				_finish_return(job)
	if offline: return
	# automation
	_auto_timer -= delta
	if _auto_timer <= 0:
		_auto_timer = 0.75
		_run_automation()


func _run_automation() -> void:
	if ProgressionManager.auto_enabled("auto_accept"):
		var max_q := int(Data.config.get("max_queue", 4))
		if queue.size() < max_q:
			var best := {}
			var best_score := -INF
			for c in offers:
				if not rep_ok(c) or c.get("tutorial", false):
					continue
				var fits := false
				for v in VehicleManager.vehicles:
					if VehicleManager.fit_reason(v, c) == "":
						fits = true
						break
				if not fits:
					continue
				var e := estimate(c, {}, "normal")
				if float(e.risk) > 60:
					continue
				var score := float(e.profit) / maxf(1.0, float(e.time)) + (1000.0 if c.prioritized else 0.0)
				if score > best_score:
					best_score = score
					best = c
			if not best.is_empty():
				accept(best)
	if ProgressionManager.auto_enabled("auto_dispatch"):
		dispatch_all_ready()


func ready_pairs() -> Array:
	var out := []
	var used := {}
	var sorted := queue.duplicate()
	sorted.sort_custom(func(a, b): return a.prioritized and not b.prioritized)
	for c in sorted:
		if not rep_ok(c):
			continue
		var best := {}
		var best_score := -INF
		for v in VehicleManager.vehicles:
			if used.has(v.id) or VehicleManager.available_reason(v) != "" or VehicleManager.fit_reason(v, c) != "":
				continue
			var e := estimate(c, v, "normal")
			var score := float(e.profit) / maxf(1.0, float(e.time)) - float(VehicleManager.capacity(v) - int(c.units)) * 0.6
			if score > best_score:
				best_score = score
				best = v
		if not best.is_empty():
			used[best.id] = true
			out.append([c, best])
	return out


func dispatch_all_ready() -> int:
	var n := 0
	for pair in ready_pairs():
		var job := dispatch(pair[0], pair[1], smart_route(pair[0], pair[1]))
		if not job.is_empty():
			n += 1
	return n


func _deliver(job: Dictionary) -> void:
	var c: Dictionary = job.contract
	if randf() < float(job.fail_chance) + base_fail_chance(float(job.risk)):
		fail_job(job, "Things went sideways")
		return
	var v := VehicleManager.get_vehicle(job.vehicle_id)
	var late := float(job.total) > float(c.deadline) + 0.5
	var gross := float(job.reward) * float(job.reward_mult) * (0.7 if late else 1.0)
	var net: float = round(gross - float(job.cost))
	var dest_r := RegionManager.region_of_node(c.dest)
	var org_r := RegionManager.region_of_node(c.origin)
	if net >= 0:
		net *= Monetization.delivery_multiplier()
		EconomyManager.earn(net, "delivery")
	else:
		EconomyManager.charge(-net, "delivery")
	EconomyManager.add_reputation(int(c.rep_gain))
	EconomyManager.add_stat("deliveries")
	if bool(c.get("cross", false)):
		EconomyManager.add_stat("cross_border")
		if int(EconomyManager.stat("cross_border")) % 3 == 0:
			EconomyManager.add_influence(1)
	if net > EconomyManager.stat("best_payout"):
		EconomyManager.stats["best_payout"] = net
	var heat := float(c.heat_gain) + float(job.risk) * 0.15
	HeatManager.add_heat(dest_r, heat)
	if org_r != dest_r:
		HeatManager.add_heat(org_r, heat * 0.4)
	var lvl_up := false
	if not v.is_empty():
		v.trips = int(v.trips) + 1
		lvl_up = VehicleManager.give_driver_xp(v.driver, 1.0 + float(job.risk) / 30.0)
	job.late = late
	var result := {"net": net, "gross": gross, "late": late, "rep": int(c.rep_gain), "heat": heat,
			"driver_level_up": lvl_up, "dest": c.dest}
	# short unloading stop at the drop point before heading home
	job.phase = "unload"
	job.unload_left = UNLOAD_TIME
	job_delivered.emit(job, result)
	ProgressionManager.check_progress()


func fail_job(job: Dictionary, reason: String) -> void:
	if job.phase != "out":
		return
	var c: Dictionary = job.contract
	var soft := 1.0 - 0.45 * ProgressionManager.conn("lawyer")
	EconomyManager.charge(float(job.cost) * 0.5 * soft, "failed")
	EconomyManager.add_reputation(-int(round(3 * soft)))
	EconomyManager.add_stat("failed")
	HeatManager.add_heat(RegionManager.region_of_node(c.dest), 10.0 * soft)
	job.damage = float(job.get("damage", 0)) + 20.0
	job.pending_event = false
	_start_return(job)
	job_failed.emit(job, reason)


func _start_return(job: Dictionary) -> void:
	var v := VehicleManager.get_vehicle(job.vehicle_id)
	var g := RegionManager.graph
	# where the vehicle actually is right now along its route
	var here: String = job.contract.dest
	if job.phase == "out" and float(job.elapsed) < float(job.total):
		var p := progress(job)
		var path: Array = job.path
		var idx := clampi(int(round(p * (path.size() - 1))), 0, path.size() - 1)
		here = path[idx]
	job.phase = "back"
	job.fail_here = here
	var home := RegionManager.nearest_home(here)
	job.home = home
	job.ret_path = g.find_path(here, home, "normal", RegionManager.max_region())
	if (job.ret_path as Array).size() < 2:
		job.ret_path = [here, here]
	var speed := VehicleManager.speed_mult(v) if not v.is_empty() else 1.0
	job.ret_total = clampf(4.0 + pow(g.path_length(job.ret_path), 0.75) * 0.45 / speed, 4.0, 40.0)
	if job.get("intro_trip", false):
		job.ret_total = minf(float(job.ret_total), 6.0)
	job.ret_elapsed = 0.0


func _finish_return(job: Dictionary) -> void:
	jobs.erase(job)
	var v := VehicleManager.get_vehicle(job.vehicle_id)
	if not v.is_empty():
		v.home = job.home
		v.state = "idle"
		if float(job.get("damage", 0)) > 0:
			VehicleManager.start_repair(v, float(job.damage))
		VehicleManager.vehicle_changed.emit(v)
	job_returned.emit(job)


func to_dict() -> Dictionary:
	return {"offers": offers, "queue": queue, "jobs": jobs, "next_id": _next_id}


func from_dict(d: Dictionary) -> void:
	reset()
	_next_id = int(d.get("next_id", 1))
	var valid := func(c) -> bool:
		return typeof(c) == TYPE_DICTIONARY and not Data.category(str(c.get("category", ""))).is_empty() \
				and RegionManager.graph.nodes.has(str(c.get("origin", ""))) \
				and RegionManager.graph.nodes.has(str(c.get("dest", "")))
	for c in d.get("offers", []):
		if valid.call(c):
			offers.append(c)
	for c in d.get("queue", []):
		if valid.call(c):
			queue.append(c)
	for j in d.get("jobs", []):
		if typeof(j) != TYPE_DICTIONARY or not valid.call(j.get("contract", null)):
			continue
		var v := VehicleManager.get_vehicle(str(j.get("vehicle_id", "")))
		if v.is_empty():
			continue
		for k in ["total", "elapsed", "ret_total", "ret_elapsed", "event_at", "fail_chance", "damage", "reward_mult", "reward", "cost", "risk"]:
			j[k] = float(j.get(k, 0.0))
		if (j.get("path", []) as Array).size() < 2:
			continue
		v.state = "busy"
		jobs.append(j)
