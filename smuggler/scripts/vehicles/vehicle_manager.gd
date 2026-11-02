extends Node
## Owned vehicles, their upgrades, drivers and the driver market.

signal vehicle_added(v: Dictionary)
signal vehicle_changed(v: Dictionary)
signal vehicle_upgraded(v: Dictionary, stat: String)
signal drivers_changed
signal market_changed

const MARKET_REFRESH := 150.0

var vehicles: Array = []  # {id, def, upgrades:{}, paint, driver, home, state, repair_left, trips}
var drivers: Array = []   # {id, name, rarity, speed, reliability, stress, xp, level, trips}
var market: Array = []
var market_timer := MARKET_REFRESH
var _next_id := 1


func reset() -> void:
	vehicles = []
	drivers = []
	market = []
	market_timer = MARKET_REFRESH
	_next_id = 1


func new_id(prefix: String) -> String:
	_next_id += 1
	return "%s%d" % [prefix, _next_id]


# --- vehicles ---------------------------------------------------------------
func get_vehicle(id: String) -> Dictionary:
	for v in vehicles:
		if v.id == id:
			return v
	return {}


func def_of(v: Dictionary) -> Dictionary:
	return Data.vehicle_def(v.def)


func add_vehicle(def_id: String, free := false) -> Dictionary:
	var d := Data.vehicle_def(def_id)
	if d.is_empty():
		return {}
	var v := {
		"id": new_id("v"), "def": def_id, "upgrades": {}, "paint": d.paint, "driver": "",
		"home": "hq", "state": "idle", "repair_left": 0.0, "trips": 0, "free": free,
	}
	vehicles.append(v)
	# every vehicle arrives with a rookie driver when there's room on the payroll
	var spare := unassigned_drivers()
	if spare.size() > 0:
		v.driver = spare[0].id
	elif drivers.size() < ProgressionManager.driver_slots():
		var dr := _make_driver("common")
		drivers.append(dr)
		v.driver = dr.id
		drivers_changed.emit()
	vehicle_added.emit(v)
	return v


func buy_check(def_id: String) -> Dictionary:
	var d := Data.vehicle_def(def_id)
	if d.is_empty():
		return {"ok": false, "reason": "?"}
	if ProgressionManager.garage_level < int(d.garage):
		return {"ok": false, "reason": tr("Needs %s") % tr(str(Data.garage_level(int(d.garage)).name)), "locked": true}
	if vehicles.size() >= ProgressionManager.vehicle_slots():
		return {"ok": false, "reason": "Garage full", "full": true}
	if not EconomyManager.can_afford(float(d.price)):
		return {"ok": false, "reason": tr("Need %s") % Fmt.money(float(d.price))}
	return {"ok": true, "reason": ""}


func buy(def_id: String) -> Dictionary:
	if not buy_check(def_id).ok:
		return {}
	EconomyManager.spend(float(Data.vehicle_def(def_id).price), "vehicle")
	return add_vehicle(def_id)


func upgrade_level(v: Dictionary, stat: String) -> int:
	return int(v.upgrades.get(stat, 0))


func upgrade_cost(v: Dictionary, stat: String) -> float:
	var d := def_of(v)
	var u := Data.vehicle_upgrade(stat)
	return Fmt.nice(float(d.upgrade_base) * float(u.get("mult", 1.0)) * pow(1.6, upgrade_level(v, stat)))


func upgrade_check(v: Dictionary, stat: String) -> Dictionary:
	var u := Data.vehicle_upgrade(stat)
	if upgrade_level(v, stat) >= int(u.get("max", 5)):
		return {"ok": false, "reason": "Max", "maxed": true}
	if not EconomyManager.can_afford(upgrade_cost(v, stat)):
		return {"ok": false, "reason": tr("Need %s") % Fmt.money(upgrade_cost(v, stat))}
	return {"ok": true, "reason": ""}


func upgrade(v: Dictionary, stat: String) -> bool:
	if not upgrade_check(v, stat).ok:
		return false
	EconomyManager.spend(upgrade_cost(v, stat), "vehicle_upgrade")
	v.upgrades[stat] = upgrade_level(v, stat) + 1
	EconomyManager.add_stat("vehicle_upgrades")
	vehicle_upgraded.emit(v, stat)
	vehicle_changed.emit(v)
	ProgressionManager.check_progress()
	return true


func total_upgrades(v: Dictionary) -> int:
	var n := 0
	for k in v.upgrades:
		n += int(v.upgrades[k])
	return n


func set_paint(v: Dictionary, hex: String) -> void:
	v.paint = hex
	vehicle_changed.emit(v)


# --- derived stats ------------------------------------------------------------
func capacity(v: Dictionary) -> int:
	var base := int(def_of(v).capacity)
	var step := maxi(1, int(round(base * 0.2)))
	return base + step * upgrade_level(v, "capacity")


func speed_mult(v: Dictionary) -> float:
	var s := float(def_of(v).speed) * (1.0 + 0.08 * upgrade_level(v, "engine"))
	var dr := get_driver(v.driver)
	if not dr.is_empty():
		s *= 1.0 + driver_stat(dr, "speed") * 0.003
	return s


func reliability(v: Dictionary) -> float:
	var r := float(def_of(v).reliability) + 6.0 * upgrade_level(v, "reliability") + 3.0 * upgrade_level(v, "suspension")
	var dr := get_driver(v.driver)
	if not dr.is_empty():
		r += driver_stat(dr, "reliability") * 0.12
	return clampf(r, 0.0, 99.0)


func risk_mod(v: Dictionary) -> float:
	var r := float(def_of(v).risk_mod) - 3.0 * upgrade_level(v, "navigation")
	var dr := get_driver(v.driver)
	if not dr.is_empty():
		r -= driver_stat(dr, "stress") * 0.06
	return r


func op_cost(v: Dictionary) -> float:
	var c := float(def_of(v).op_cost) * (1.0 - 0.08 * upgrade_level(v, "fuel")) * ProgressionManager.op_cost_mult()
	var dr := get_driver(v.driver)
	if not dr.is_empty():
		c += float(Data.rarity(dr.rarity).salary) * (1.0 - 0.15 * ProgressionManager.conn("union"))
	return round(c)


func reward_mult(v: Dictionary) -> float:
	return 1.0 + 0.04 * upgrade_level(v, "handling")


func repair_time(v: Dictionary, base: float) -> float:
	return base * pow(0.88, upgrade_level(v, "maintenance")) * ProgressionManager.repair_mult()


func has_tag(v: Dictionary, tag: String) -> bool:
	return tag == "" or (def_of(v).tags as Array).has(tag)


## Can this vehicle physically take the contract? Returns "" or a reason.
func fit_reason(v: Dictionary, c: Dictionary) -> String:
	var cat := Data.category(c.category)
	var kind: String = cat.get("kind", "cargo")
	if not has_tag(v, kind):
		return tr("Needs %s vehicle") % tr(kind)
	var req: String = cat.get("requires", "")
	if req != "" and not has_tag(v, req):
		return tr("Needs %s vehicle") % tr(req)
	if capacity(v) < int(c.units):
		return tr("Capacity %d/%d") % [capacity(v), int(c.units)]
	return ""


func available_reason(v: Dictionary) -> String:
	if v.state == "busy":
		return "On a job"
	if v.state == "repair":
		return "Repairing"
	if v.driver == "" or get_driver(v.driver).is_empty():
		return "No driver"
	return ""


func idle_vehicles() -> Array:
	return vehicles.filter(func(v): return available_reason(v) == "")


func best_capacity(kind: String, tag := "") -> int:
	var best := 0
	for v in vehicles:
		if has_tag(v, kind) and has_tag(v, tag):
			best = maxi(best, capacity(v))
	return best


# --- repairs ------------------------------------------------------------------
func start_repair(v: Dictionary, seconds: float) -> void:
	if seconds <= 0:
		return
	v.state = "repair"
	v.repair_left = repair_time(v, seconds)
	vehicle_changed.emit(v)


func instant_repair_cost(v: Dictionary) -> float:
	return Fmt.nice(maxf(20.0, float(def_of(v).price) * 0.01 * float(v.repair_left) / 10.0))


func instant_repair(v: Dictionary) -> bool:
	if v.state != "repair" or not EconomyManager.spend(instant_repair_cost(v), "repair"):
		return false
	v.repair_left = 0.0
	v.state = "idle"
	vehicle_changed.emit(v)
	return true


## A rewarded ad (or the ad-free player's no-video claim) finishes a repair for free. False = nothing was being repaired.
func free_repair(v: Dictionary) -> bool:
	if v.is_empty() or v.state != "repair":
		return false
	v.repair_left = 0.0
	v.state = "idle"
	vehicle_changed.emit(v)
	return true


func tick(delta: float) -> void:
	for v in vehicles:
		if v.state == "repair":
			v.repair_left = float(v.repair_left) - delta
			if v.repair_left <= 0:
				v.repair_left = 0.0
				v.state = "idle"
				vehicle_changed.emit(v)
	market_timer -= delta
	if market_timer <= 0 or market.is_empty():
		refresh_market()


# --- drivers ------------------------------------------------------------------
func get_driver(id: String) -> Dictionary:
	if id == "":
		return {}
	for d in drivers:
		if d.id == id:
			return d
	return {}


func driver_vehicle(driver_id: String) -> Dictionary:
	for v in vehicles:
		if v.driver == driver_id:
			return v
	return {}


func unassigned_drivers() -> Array:
	return drivers.filter(func(d): return driver_vehicle(d.id).is_empty())


func driver_stat(d: Dictionary, stat: String) -> float:
	var bonus := 2.0 * (int(d.get("level", 1)) - 1)
	if stat == "stress":
		bonus += 5.0 * ProgressionManager.business_level("driver_lounge")
	return clampf(float(d.get(stat, 0)) + bonus, 0.0, 120.0)


func _make_driver(rarity_id: String) -> Dictionary:
	var r := Data.rarity(rarity_id)
	var first: Array = Data.driver_data.get("first", ["Sam"])
	var nick: Array = Data.driver_data.get("nick", ["Wheels"])
	var lo := int(r.stats[0])
	var hi := int(r.stats[1])
	var used := {}
	for d in drivers:
		used[d.name] = true
	var nm := ""
	for _i in 12:
		nm = "%s %s" % [first.pick_random(), nick.pick_random()]
		if not used.has(nm):
			break
	return {
		"id": new_id("d"), "name": nm, "rarity": rarity_id,
		"speed": randi_range(lo, hi), "reliability": randi_range(lo, hi), "stress": randi_range(lo, hi),
		"xp": 0.0, "level": 1, "trips": 0,
	}


func add_start_driver() -> Dictionary:
	var s: Dictionary = Data.config.get("start_driver", {})
	var d := _make_driver("common")
	d.name = s.get("name", d.name)
	d.speed = int(s.get("speed", d.speed))
	d.reliability = int(s.get("reliability", d.reliability))
	d.stress = int(s.get("stress", d.stress))
	drivers.append(d)
	return d


## A driver given by a purchase (Quick Start): joins the payroll when there is room and takes an empty seat.
func grant_driver(rarity_id: String) -> bool:
	if drivers.size() >= ProgressionManager.driver_slots():
		return false
	var d := _make_driver(rarity_id)
	drivers.append(d)
	for v in vehicles:
		if v.driver == "" and v.state != "busy":
			v.driver = d.id
			vehicle_changed.emit(v)
			break
	drivers_changed.emit()
	return true


func refresh_market() -> void:
	market_timer = MARKET_REFRESH
	market = []
	var rep := EconomyManager.reputation
	var pool := []
	for r in Data.driver_data.get("rarities", []):
		if rep >= int(r.get("rep", 0)):
			pool.append(r)
	var scout := ProgressionManager.conn("scout")
	for _i in 3:
		var total := 0.0
		for r in pool:
			total += _scout_weight(r, scout)
		var roll := randf() * total
		var chosen: Dictionary = pool[0]
		for r in pool:
			roll -= _scout_weight(r, scout)
			if roll <= 0:
				chosen = r
				break
		market.append(_make_driver(chosen.id))
	market_changed.emit()


func _scout_weight(r: Dictionary, scout: int) -> float:
	return float(r.weight) * (pow(0.5, scout) if r.id == "common" else pow(1.6, scout))


func market_refresh_cost() -> float:
	return Fmt.nice(maxf(100.0, EconomyManager.income_per_minute() * 0.25))


func paid_market_refresh() -> bool:
	if not EconomyManager.spend(market_refresh_cost(), "market"):
		return false
	refresh_market()
	return true


func hire_cost(d: Dictionary) -> float:
	return float(Data.rarity(d.rarity).hire)


func hire_check(d: Dictionary) -> Dictionary:
	if drivers.size() >= ProgressionManager.driver_slots():
		return {"ok": false, "reason": "Payroll full", "full": true}
	if not EconomyManager.can_afford(hire_cost(d)):
		return {"ok": false, "reason": tr("Need %s") % Fmt.money(hire_cost(d))}
	return {"ok": true, "reason": ""}


func hire(d: Dictionary) -> bool:
	if not hire_check(d).ok:
		return false
	EconomyManager.spend(hire_cost(d), "hire")
	market.erase(d)
	drivers.append(d)
	# auto-seat the new hire in a vehicle without a driver, or upgrade the weakest seat
	for v in vehicles:
		if v.driver == "" and v.state != "busy":
			v.driver = d.id
			vehicle_changed.emit(v)
			break
	drivers_changed.emit()
	market_changed.emit()
	ProgressionManager.check_progress()
	return true


func assign_driver(v: Dictionary, driver_id: String) -> void:
	if v.state == "busy":
		return
	var other := driver_vehicle(driver_id)
	if not other.is_empty():
		if other.state == "busy":
			return
		other.driver = v.driver
		vehicle_changed.emit(other)
	v.driver = driver_id
	vehicle_changed.emit(v)
	drivers_changed.emit()


func dismiss_driver(driver_id: String) -> void:
	var v := driver_vehicle(driver_id)
	if not v.is_empty():
		if v.state == "busy":
			return
		v.driver = ""
		vehicle_changed.emit(v)
	for d in drivers:
		if d.id == driver_id:
			drivers.erase(d)
			break
	drivers_changed.emit()


func driver_power(d: Dictionary) -> float:
	return driver_stat(d, "speed") + driver_stat(d, "reliability") + driver_stat(d, "stress")


func give_driver_xp(driver_id: String, amount: float) -> bool:
	var d := get_driver(driver_id)
	if d.is_empty():
		return false
	d.trips = int(d.get("trips", 0)) + 1
	d.xp = float(d.get("xp", 0)) + amount * (1.0 + 0.25 * ProgressionManager.business_level("driver_lounge"))
	var need := 4.0 * int(d.level) + 2.0
	if d.xp >= need and int(d.level) < 10:
		d.xp -= need
		d.level = int(d.level) + 1
		drivers_changed.emit()
		return true
	return false


func to_dict() -> Dictionary:
	return {"vehicles": vehicles, "drivers": drivers, "market": market, "market_timer": market_timer, "next_id": _next_id}


func from_dict(d: Dictionary) -> void:
	reset()
	_next_id = int(d.get("next_id", 1))
	for x in d.get("drivers", []):
		if typeof(x) == TYPE_DICTIONARY and x.has("id"):
			for k in ["speed", "reliability", "stress", "level", "trips"]:
				x[k] = int(x.get(k, 1))
			x["xp"] = float(x.get("xp", 0))
			drivers.append(x)
	for x in d.get("vehicles", []):
		if typeof(x) != TYPE_DICTIONARY or Data.vehicle_def(str(x.get("def", ""))).is_empty():
			continue
		x["upgrades"] = x.get("upgrades", {})
		for k in x.upgrades:
			x.upgrades[k] = int(x.upgrades[k])
		x["trips"] = int(x.get("trips", 0))
		x["repair_left"] = float(x.get("repair_left", 0))
		x["home"] = str(x.get("home", "hq"))
		if not get_driver(str(x.get("driver", ""))).is_empty():
			x["driver"] = str(x.driver)
		else:
			x["driver"] = ""
		x["state"] = "repair" if x.get("state", "idle") == "repair" else "idle"
		vehicles.append(x)
	for x in d.get("market", []):
		if typeof(x) == TYPE_DICTIONARY and x.has("rarity"):
			market.append(x)
	market_timer = float(d.get("market_timer", MARKET_REFRESH))
