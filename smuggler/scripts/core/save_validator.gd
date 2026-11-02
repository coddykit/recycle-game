extends RefCounted
## Validate before mutating managers or replacing the last valid backup.
static func number(v: Variant, minimum := 0.0, maximum := 1e100) -> bool:
	return (v is float or v is int) and is_finite(float(v)) and float(v) >= minimum and float(v) <= maximum

static func numeric_fields(d: Dictionary, names: Array, minimum := 0.0) -> bool:
	for k in names:
		if d.has(k) and not number(d[k], minimum): return _bad(8)
	return true

static func numeric_map(v: Variant) -> bool:
	if not v is Dictionary: return _bad(12)
	for k in v:
		if not number(v[k]): return _bad(14)
	return true

static func path_valid(v: Variant, required := false) -> bool:
	if not v is Array or (required and v.size() < 2): return _bad(18)
	for n in v:
		if not n is String or not RegionManager.graph.nodes.has(n): return _bad(20)
	return true

static func contract(c: Variant) -> bool:
	if not c is Dictionary: return _bad(24)
	for k in ["id", "category", "origin", "dest"]:
		if not c.get(k) is String or c[k].is_empty(): return _bad(26)
	if Data.category(c.category).is_empty(): return _bad(27)
	if not RegionManager.graph.nodes.has(c.origin) or not RegionManager.graph.nodes.has(c.dest): return _bad(28)
	for k in ["reward", "units", "base_risk", "rep_required", "rep_gain", "expires"]:
		if not c.has(k) or not number(c[k], -1e6 if k == "expires" else 0.0): return _bad(30)
	return c.get("prioritized") is bool

static func valid(d: Dictionary) -> bool:
	if not number(d.get("version", 1), 0, 2): return _bad(34)
	for k in ["economy", "vehicles", "contracts", "progression", "regions", "heat", "tutorial"]:
		if not d.get(k) is Dictionary: return _bad(36)
	if not numeric_fields(d, ["time"]): return _bad(37)
	if not numeric_fields(d.economy, ["cash", "reputation", "influence", "ipm"]) or not numeric_map(d.economy.get("stats", {})): return _bad(38)
	var p: Dictionary = d.progression
	if not numeric_fields(p, ["garage", "stage", "goal"]): return _bad(40)
	for k in ["business", "connections"]:
		if not numeric_map(p.get(k, {})): return _bad(42)
	if not p.get("flags", {}) is Dictionary or not p.get("auto", {}) is Dictionary: return _bad(43)
	for k in p.get("auto", {}):
		if not p.auto[k] is bool: return _bad(45)
	for k in ["legacy", "prestiges", "earned_at_prestige"]:
		if p.get("flags", {}).has(k) and not number(p.flags[k]): return _bad(47)
	for k in ["company", "company_color"]:
		if p.get("flags", {}).has(k) and not p.flags[k] is String: return _bad(49)
	if not number(d.regions.get("unlocked", 1), 1, Data.regions.size()): return _bad(50)
	if not d.regions.get("depots", []) is Array or not d.heat.get("heat", []) is Array: return _bad(51)
	for v in d.regions.get("depots", []):
		if not number(v, 1, float(d.regions.unlocked) - 1): return _bad(53)
	for v in d.heat.get("heat", []):
		if not number(v, 0, 100): return _bad(55)
	if not number(d.tutorial.get("step", 0), 0, 10) or not d.tutorial.get("finished", false) is bool: return _bad(56)
	var fleet: Dictionary = d.vehicles
	for k in ["vehicles", "drivers", "market"]:
		if not fleet.get(k, []) is Array: return _bad(59)
	if not numeric_fields(fleet, ["next_id", "market_timer"], -1e6): return _bad(60)
	var drivers := {}
	for driver in fleet.get("drivers", []) + fleet.get("market", []):
		if not driver is Dictionary: return _bad(63)
		for k in ["id", "name", "rarity"]:
			if not driver.get(k) is String: return _bad(65)
		if not numeric_fields(driver, ["speed", "reliability", "stress", "xp", "level", "trips"]): return _bad(66)
	for driver in fleet.get("drivers", []):
		if drivers.has(driver.id): return _bad(68)
		drivers[driver.id] = true
	var ids := {}
	var seats := {}
	for v in fleet.get("vehicles", []):
		if not v is Dictionary: return _bad(73)
		for k in ["id", "def", "paint", "driver", "home", "state"]:
			if not v.get(k) is String: return _bad(75)
		if v.id.is_empty() or ids.has(v.id) or Data.vehicle_def(v.def).is_empty(): return _bad(76)
		if v.state not in ["idle", "busy", "repair"] or not RegionManager.graph.nodes.has(v.home): return _bad(77)
		if not numeric_map(v.get("upgrades", {})) or not numeric_fields(v, ["repair_left", "trips"]): return _bad(78)
		if not v.driver.is_empty():
			if not drivers.has(v.driver) or seats.has(v.driver): return _bad(80)
			seats[v.driver] = true
		ids[v.id] = true
	var contracts: Dictionary = d.contracts
	for k in ["offers", "queue", "jobs"]:
		if not contracts.get(k, []) is Array: return _bad(85)
	var cids := {}
	for c in contracts.get("offers", []) + contracts.get("queue", []):
		if not contract(c) or cids.has(c.id): return _bad(88)
		cids[c.id] = true
	var jobs := {}
	var busy := {}
	for j in contracts.get("jobs", []):
		if not j is Dictionary or not contract(j.get("contract")): return _bad(93)
		if not j.get("id") is String or jobs.has(j.id): return _bad(94)
		if not ids.has(j.get("vehicle_id")) or busy.has(j.vehicle_id): return _bad(95)
		if j.get("phase") not in ["out", "unload", "back"]: return _bad(96)
		if not path_valid(j.get("path"), true) or not path_valid(j.get("ret_path", []), j.phase == "back"): return _bad(97)
		if not numeric_fields(j, ["total", "elapsed", "ret_total", "ret_elapsed", "fail_chance", "damage", "reward_mult", "reward", "cost", "risk", "pending_time"]): return _bad(98)
		if not numeric_fields(j, ["event_at", "unload_left"], -1e6): return _bad(99)
		if not j.get("pending_event", false) is bool: return _bad(100)
		if j.get("pending_event", false):
			if not j.get("choices") is Array or Data.event_def(str(j.get("event_id", ""))).is_empty(): return _bad(102)
			for choice in j.choices:
				if not choice is Dictionary or not number(choice.get("cost")): return _bad(104)
		jobs[j.id] = true
		busy[j.vehicle_id] = true
	if not d.get("session", {}) is Dictionary or not d.get("daily", {}) is Dictionary: return _bad(107)
	var session: Dictionary = d.get("session", {})
	if not numeric_fields(session, ["settled_at"]): return _bad(109)
	if not session.get("generation", "") is String or not session.get("receipt", {}) is Dictionary: return _bad(110)
	var receipt: Dictionary = session.get("receipt", {})
	if not receipt.is_empty():
		if not receipt.get("id") is String or not receipt.get("acknowledged") is bool: return _bad(200)
		if not receipt.get("applied") is bool or not numeric_fields(receipt, ["seconds", "deliveries", "manager"]): return _bad(201)
		if not number(receipt.get("earned"), -1e100): return _bad(202)
	var daily: Dictionary = d.get("daily", {})
	if not daily.is_empty():
		if not number(daily.get("day"), 0, 1e7) or not daily.get("tasks") is Array: return _bad(203)
		var task_ids := {}
		for task in daily.tasks:
			if not task is Dictionary or not task.get("id") is String or task_ids.has(task.id): return _bad(204)
			if task.get("metric") not in ["earned", "deliveries", "vehicle_upgrades"] or not task.get("text") is String: return _bad(205)
			if not task.get("claimed") is bool or not number(task.get("base")) or not number(task.get("target"), 1) or not number(task.get("reward"), 0, 250): return _bad(206)
			task_ids[task.id] = true
	return true

static var last_reason := ""
static func _bad(line: int) -> bool:
	last_reason = "schema check %d" % line
	return false
